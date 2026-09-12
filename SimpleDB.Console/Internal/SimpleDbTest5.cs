using System.Collections.Concurrent;

using SimpleDB.Console.Abstractions;
using SimpleDB.Console.Tables;

using static System.Console;

namespace SimpleDB.Console.Internal
{
    /// <summary>
    /// Concurrent checkpoint test.
    ///
    /// Exercises the race the database wide lock exists to prevent: the WAL crosses
    /// WalCheckpointThreshold and a checkpoint begins rolling every table forward to disk, while
    /// other threads are simultaneously beginning, committing and rolling back transactions.
    ///
    /// Without the lock a checkpoint could flush a table midway through another thread's
    /// transaction, persisting uncommitted rows, or a rollback could undo in-memory state that had
    /// already been written to disk. This is a realistic production shape - checkpoints are
    /// triggered by commit volume, which is highest exactly when concurrency is highest.
    ///
    /// The test asserts three things:
    /// 1. No thread observes an exception (a deadlock shows up as the harness hanging).
    /// 2. A checkpoint genuinely fired, otherwise the test proves nothing.
    /// 3. Every committed row is present and every rolled back row is absent.
    ///
    /// Runs last because it deliberately pushes the WAL past the checkpoint threshold, which
    /// flushes the table files and would invalidate SimpleDbTest4's crash state premise.
    /// </summary>
    public sealed class SimpleDbTest5 : ITestExecution
    {
        private const string CommittedPrefix = "concurrent-committed-";
        private const string RolledBackPrefix = "concurrent-rolledback-";
        private const string SettingsTableFile = "Settings.dat";

        private const int WorkerCount = 8;

        // WalCheckpointThreshold is 1000. Each worker commits RowsPerTransaction rows per
        // transaction, so the totals below are chosen to cross the threshold comfortably part way
        // through the run rather than at the very end, ensuring checkpointing overlaps live work.
        private const int TransactionsPerWorker = 25;
        private const int RowsPerTransaction = 8;

        private readonly ITestDatabaseContext _database;
        private readonly ConcurrentBag<Exception> _failures = [];

        public SimpleDbTest5(ITestDatabaseContext database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public int Order => 50;

        public string TestName => "SimpleDbTest5";

        public void Execute()
        {
            ITransactionManager transactionManager = _database.Resolve<ITransactionManager>();
            ITransactionalSimpleDBOperations<SettingsDataRow> settingsTable =
                (ITransactionalSimpleDBOperations<SettingsDataRow>)_database.Resolve<ISimpleDBOperations<SettingsDataRow>>();

            using Barrier startLine = new(WorkerCount);
            List<Thread> workers = [];

            for (int worker = 0; worker < WorkerCount; worker++)
            {
                int workerId = worker;

                Thread thread = new(() => RunWorker(workerId, startLine, transactionManager, settingsTable))
                {
                    IsBackground = true,
                    Name = $"SimpleDbTest5-Worker-{workerId}",
                };

                workers.Add(thread);
                thread.Start();
            }

            foreach (Thread thread in workers)
            {
                // A deadlock in the database lock would hang here rather than fail, so the join is
                // bounded. The timeout is generous - it is a deadlock detector, not a perf budget.
                if (!thread.Join(TimeSpan.FromMinutes(2)))
                {
                    throw new TimeoutException(
                        $"{thread.Name} did not complete within the timeout, which indicates a deadlock " +
                        "between the checkpoint and an active transaction.");
                }
            }

            if (!_failures.IsEmpty)
            {
                throw new AggregateException(
                    "One or more workers failed during concurrent checkpointing.", [.. _failures]);
            }
        }

        /// <summary>
        /// Each worker interleaves commits and rollbacks so that a checkpoint firing at an
        /// arbitrary moment has a good chance of landing while another thread holds an open
        /// transaction with uncommitted rows already applied in memory.
        /// </summary>
        private void RunWorker(int workerId, Barrier startLine,
            ITransactionManager transactionManager,
            ITransactionalSimpleDBOperations<SettingsDataRow> settingsTable)
        {
            try
            {
                // Maximise contention by releasing every worker at the same instant.
                startLine.SignalAndWait();

                for (int iteration = 0; iteration < TransactionsPerWorker; iteration++)
                {
                    bool shouldRollback = iteration % 5 == 4;
                    string prefix = shouldRollback ? RolledBackPrefix : CommittedPrefix;

                    ITransaction transaction = transactionManager.BeginTransaction();

                    try
                    {
                        List<SettingsDataRow> rows = [];

                        for (int row = 0; row < RowsPerTransaction; row++)
                        {
                            rows.Add(new SettingsDataRow
                            {
                                Name = $"{prefix}{workerId}-{iteration}-{row}",
                                Value = $"worker-{workerId}",
                            });
                        }

                        settingsTable.Insert(rows, new InsertOptions(), transaction);

                        if (shouldRollback)
                            transaction.Rollback();
                        else
                            transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                _failures.Add(new InvalidOperationException($"Worker {workerId} failed: {ex.Message}", ex));
            }
        }

        public void Validate()
        {
            ValidateCheckpointOccurred();
            ValidateCommittedRowsPresent();
            ValidateRolledBackRowsAbsent();
        }

        /// <summary>
        /// Guards the premise of the test. If the WAL never crossed the threshold then no
        /// checkpoint ran concurrently with the workers and a pass would be meaningless.
        /// </summary>
        private void ValidateCheckpointOccurred()
        {
            string settingsFile = Path.Combine(_database.DatabasePath, SettingsTableFile);

            if (!File.Exists(settingsFile))
                throw new InvalidOperationException($"Expected table file {settingsFile} to exist.");

            string contents = ReadSharedText(settingsFile);

            if (!contents.Contains(CommittedPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "No committed row reached the table file, so the WAL never crossed the checkpoint " +
                    "threshold during the run. This test cannot prove concurrent checkpoint safety.");
            }

            WriteLine("  A checkpoint rolled committed rows forward while workers were active.");
        }

        /// <summary>
        /// The live session holds the table file open with FileShare.Read, so it must be read with
        /// matching share flags - File.ReadAllText requests a mode the owning handle denies.
        /// </summary>
        private static string ReadSharedText(string path)
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new(stream);

            return reader.ReadToEnd();
        }

        private void ValidateCommittedRowsPresent()
        {
            ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();

            HashSet<string> actual = [.. settingsTable.Select()
                .Where(r => r.Name.StartsWith(CommittedPrefix, StringComparison.Ordinal))
                .Select(r => r.Name)];

            int expectedCount = 0;

            for (int workerId = 0; workerId < WorkerCount; workerId++)
            {
                for (int iteration = 0; iteration < TransactionsPerWorker; iteration++)
                {
                    if (iteration % 5 == 4)
                        continue;

                    for (int row = 0; row < RowsPerTransaction; row++)
                    {
                        string expectedName = $"{CommittedPrefix}{workerId}-{iteration}-{row}";
                        expectedCount++;

                        if (!actual.Contains(expectedName))
                            throw new InvalidOperationException($"Committed row {expectedName} is missing.");
                    }
                }
            }

            // A surplus would mean a rolled back row leaked in, or a row was written twice by the
            // checkpoint racing an in flight insert.
            if (actual.Count != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Expected exactly {expectedCount} committed rows but found {actual.Count}.");
            }

            WriteLine($"  All {expectedCount} rows committed across {WorkerCount} threads are intact.");
        }

        private void ValidateRolledBackRowsAbsent()
        {
            ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();

            int leaked = settingsTable.Select()
                .Count(r => r.Name.StartsWith(RolledBackPrefix, StringComparison.Ordinal));

            if (leaked > 0)
            {
                throw new InvalidOperationException(
                    $"{leaked} rolled back rows were persisted, so a checkpoint flushed uncommitted data.");
            }

            WriteLine("  No rolled back rows were persisted by a concurrent checkpoint.");
        }
    }
}
