using SimpleDB.Console.Abstractions;
using SimpleDB.Console.Tables;

using static System.Console;

namespace SimpleDB.Console.Internal
{
    /// <summary>
    /// Crash-during-checkpoint recovery test.
    ///
    /// SimpleDbTest4 covers the crash window BEFORE a checkpoint - committed rows live only in the
    /// WAL and the table files are behind. That leaves the checkpoint itself untested, and a
    /// checkpoint is not atomic. It walks the registered tables one at a time, flushing each and
    /// then advancing that table's watermark, and only once every table has been dealt with does it
    /// truncate and compact the WAL. Dying part way through leaves the database in a state no
    /// single-table view describes.
    ///
    /// Three distinct windows are exercised, each reproduced on a copy of the database files so the
    /// live session is never damaged:
    ///
    /// 1. Mid flush loop - one table is on disk, the next is not. Recovery must replay only what
    ///    the lagging table is missing. This is the case the MINIMUM watermark rule exists for.
    /// 2. Between flush and watermark advance - the data reached disk but the watermark still says
    ///    it did not, so recovery replays entries the table file already contains. Safe only if
    ///    replay is idempotent, which nothing currently proves.
    /// 3. Mid WAL compaction - AppendOnlyDataWriter.RewriteData truncates the file to the header
    ///    BEFORE rewriting the survivors and fixing up the header, so a crash inside it leaves a
    ///    WAL whose header disagrees with its contents. Everything discarded by compaction is
    ///    already durable in the table files, so recovery must tolerate the damaged log rather than
    ///    fail to open or mis-replay it.
    ///
    /// Runs last. It force writes tables and inspects crash copies, so it must not disturb the
    /// WAL-only premise SimpleDbTest4 depends on.
    /// </summary>
    public sealed class SimpleDbTest6 : ITestExecution
    {
        private const string SettingsPrefix = "checkpoint-crash-setting-";
        private const string TestDataPrefix = "checkpoint-crash-testdata-";
        private const string WatermarkTableFile = @"System\Sys$CheckpointWatermarks.dat";

        private const int SettingsRowCount = 6;
        private const int TestDataRowCount = 5;

        private readonly ITestDatabaseContext _database;

        public SimpleDbTest6(ITestDatabaseContext database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public int Order => 60;

        public string TestName => "SimpleDbTest6";

        public void Execute()
        {
            ITransactionManager transactionManager = _database.Resolve<ITransactionManager>();
            ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();
            ISimpleDBOperations<TestDataDataRow> testDataTable = _database.Resolve<ISimpleDBOperations<TestDataDataRow>>();

            ITransaction transaction = transactionManager.BeginTransaction();

            List<SettingsDataRow> settingsRows = [];
            List<TestDataDataRow> testDataRows = [];

            for (int i = 0; i < SettingsRowCount; i++)
            {
                settingsRows.Add(new SettingsDataRow
                {
                    Name = $"{SettingsPrefix}{i}",
                    Value = $"checkpoint-crash-value-{i}",
                });
            }

            for (int i = 0; i < TestDataRowCount; i++)
            {
                testDataRows.Add(new TestDataDataRow
                {
                    Author = $"{TestDataPrefix}{i}",
                    Data = $"checkpoint-crash-data-{i}",
                });
            }

            AsTransactional(settingsTable).Insert(settingsRows, new InsertOptions(), transaction);
            AsTransactional(testDataTable).Insert(testDataRows, new InsertOptions(), transaction);

            transaction.Commit();
        }

        public void Validate()
        {
            ValidatePartialFlush();
            ValidateFlushedButWatermarkNotAdvanced();
            ValidateInterruptedWalCompaction();
        }

        /// <summary>
        /// Window 1: the checkpoint flushed one table and died before reaching the next.
        ///
        /// Reproduced faithfully rather than by editing files - only the settings table is force
        /// written, so at the moment the copy is taken the settings file is current while the test
        /// data file is still behind, which is exactly the shape of a checkpoint interrupted part
        /// way through its loop. Recovery has to replay the test data rows without losing or
        /// duplicating the settings rows that are already durable.
        /// </summary>
        private void ValidatePartialFlush()
        {
            ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();

            // Deliberately NOT flushing the test data table - that is the interruption.
            settingsTable.ForceWrite();

            using ICrashRecoverySession recovered = _database.OpenCrashCopy();

            AssertAllRowsRecovered(recovered, "checkpoint interrupted between two table flushes");

            WriteLine("  Recovered from a checkpoint that flushed one table and died before the next.");
        }

        /// <summary>
        /// Window 2: the flush completed but the process died before the watermark was advanced.
        ///
        /// Reproduced by deleting the watermark file from the copy, which makes every table report
        /// "never flushed" and forces startup recovery to replay the entire surviving WAL over
        /// table files that already contain those rows. That is strictly harsher than the real
        /// window, where only one table's watermark would be stale.
        ///
        /// This is the idempotency check. If replay inserts rather than reconciles, the duplicate
        /// assertion below is what will catch it.
        /// </summary>
        private void ValidateFlushedButWatermarkNotAdvanced()
        {
            using ICrashRecoverySession recovered = _database.OpenCrashCopy(crashPath =>
            {
                string watermarkFile = Path.Combine(crashPath, WatermarkTableFile);

                if (File.Exists(watermarkFile))
                    File.Delete(watermarkFile);
            });

            AssertAllRowsRecovered(recovered, "checkpoint flushed a table but died before advancing its watermark");

            WriteLine("  Recovered from a stale watermark: replaying already flushed entries did not duplicate rows.");
        }

        /// <summary>
        /// Window 3: the process died inside AppendOnlyDataWriter.RewriteData.
        ///
        /// That method calls SetLength(TotalHeaderLength) first, then rewrites the surviving
        /// records, then flushes, then finally corrects the header. The copy is mutated to sit in
        /// the middle of that sequence - the body is truncated while the header still advertises
        /// the original record count and data length.
        ///
        /// Compaction only ever discards entries at or below the minimum watermark, which by
        /// definition every table already has on disk, so nothing durable depends on the bytes
        /// destroyed here. The database must therefore still open and still return every row.
        /// </summary>
        private void ValidateInterruptedWalCompaction()
        {
            // Everything is flushed first, so the rows under test are genuinely durable in the
            // table files and the WAL is legitimately discardable. Without this the test would be
            // asserting that data survives the destruction of its only durable copy.
            _database.Resolve<ISimpleDBOperations<SettingsDataRow>>().ForceWrite();
            _database.Resolve<ISimpleDBOperations<TestDataDataRow>>().ForceWrite();

            bool walWasTruncated = false;

            using ICrashRecoverySession recovered = _database.OpenCrashCopy(crashPath =>
            {
                walWasTruncated = TruncateWalMidRewrite(crashPath);
            });

            if (!walWasTruncated)
            {
                WriteLine("  WAL had no body to truncate; interrupted compaction window not exercised.");
                return;
            }

            AssertAllRowsRecovered(recovered, "process died midway through compacting the WAL");

            WriteLine("  Recovered from a WAL left half rewritten by an interrupted compaction.");
        }

        /// <summary>
        /// Leaves the WAL file in the state RewriteData produces between its SetLength call and its
        /// header fix up: a body that has been cut short while the header still describes the full,
        /// original contents.
        /// </summary>
        /// <returns>True if the file was large enough for the truncation to be meaningful.</returns>
        private static bool TruncateWalMidRewrite(string crashPath)
        {
            string walFile = Path.Combine(crashPath, WalFileInspector.WalTableFile);

            if (!File.Exists(walFile))
                return false;

            using FileStream stream = new(walFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            long bodyLength = stream.Length - WalFileInspector.HeaderLength;

            if (bodyLength <= 0)
                return false;

            // Half way through the body: past the header, but stopping mid entry so the final
            // length prefix describes bytes that are not there - precisely what a crash during the
            // rewrite loop leaves behind. The header is deliberately left untouched.
            stream.SetLength(WalFileInspector.HeaderLength + (bodyLength / 2));

            return true;
        }

        /// <summary>
        /// Asserts that every row committed by this test is present exactly once in the recovered
        /// database. Both directions matter: a missing row means recovery discarded durable work,
        /// a duplicated row means replay was not idempotent.
        /// </summary>
        private static void AssertAllRowsRecovered(ICrashRecoverySession recovered, string scenario)
        {
            ISimpleDBOperations<SettingsDataRow> settingsTable = recovered.Resolve<ISimpleDBOperations<SettingsDataRow>>();
            ISimpleDBOperations<TestDataDataRow> testDataTable = recovered.Resolve<ISimpleDBOperations<TestDataDataRow>>();

            List<SettingsDataRow> settings = [.. settingsTable.Select()];
            List<TestDataDataRow> testData = [.. testDataTable.Select()];

            for (int i = 0; i < SettingsRowCount; i++)
            {
                string expectedName = $"{SettingsPrefix}{i}";
                List<SettingsDataRow> matches = [.. settings.Where(r => r.Name == expectedName)];

                if (matches.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Settings row '{expectedName}' is missing after recovery ({scenario}).");
                }

                if (matches.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Settings row '{expectedName}' appears {matches.Count} times after recovery ({scenario}); " +
                        "replaying entries a table had already flushed must not duplicate them.");
                }

                string expectedValue = $"checkpoint-crash-value-{i}";

                if (matches[0].Value != expectedValue)
                {
                    throw new InvalidOperationException(
                        $"Settings row '{expectedName}' recovered with value '{matches[0].Value}', " +
                        $"expected '{expectedValue}' ({scenario}).");
                }
            }

            for (int i = 0; i < TestDataRowCount; i++)
            {
                string expectedAuthor = $"{TestDataPrefix}{i}";
                List<TestDataDataRow> matches = [.. testData.Where(r => r.Author == expectedAuthor)];

                if (matches.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"TestData row '{expectedAuthor}' is missing after recovery ({scenario}).");
                }

                if (matches.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"TestData row '{expectedAuthor}' appears {matches.Count} times after recovery ({scenario}); " +
                        "replaying entries a table had already flushed must not duplicate them.");
                }
            }
        }

        private static ITransactionalSimpleDBOperations<T> AsTransactional<T>(ISimpleDBOperations<T> table)
            where T : TableRowDefinition
        {
            return table as ITransactionalSimpleDBOperations<T>
                ?? throw new InvalidOperationException(
                    $"Table {typeof(T).Name} does not support transactional operations.");
        }
    }
}
