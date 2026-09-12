using SimpleDB.Console.Abstractions;
using SimpleDB.Console.Tables;

using static System.Console;

namespace SimpleDB.Console.Internal
{
    /// <summary>
    /// Clean restart / persistence test:
    ///
    /// 1. Commits a batch of rows across both tables without ever forcing a table flush, so at
    ///    commit time the only durable record of the batch is the WAL.
    /// 2. Closes the database cleanly and reopens it over the same files.
    /// 3. Verifies every committed row is readable again in the new session.
    /// 4. Commits and rolls back a further batch afterwards, verifying the undo path still behaves
    ///    correctly in the reopened session.
    ///
    /// IMPORTANT - what this test does NOT cover:
    /// A clean shutdown checkpoints every table (SimpleDBOperations.Dispose calls
    /// InternalCheckpoint), so by the time the database is reopened the table files are already up
    /// to date and PerformStartupRecovery has nothing to replay. This test therefore verifies
    /// commit + checkpoint-on-shutdown + reopen; it does NOT exercise WAL crash recovery. Proving
    /// that requires abandoning a session without disposing it so the table files are left behind
    /// the WAL, which is tracked separately.
    /// </summary>
    public sealed class SimpleDbTest3 : ITestExecution
    {
        private const string SettingsPrefix = "restart-setting-";
        private const string TestDataPrefix = "restart-testdata-";
        private const string SettingsTableFile = "Settings.dat";
        private const int SettingsRowCount = 4;
        private const int TestDataRowCount = 3;

        private readonly ITestDatabaseContext _database;

        private bool _settingsFileContainedRowsBeforeShutdown;
        private bool _settingsFileContainedRowsAfterShutdown;

        public SimpleDbTest3(ITestDatabaseContext database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        // Restarts the shared session, so it must run after tests that assume a continuously
        // open database.
        public int Order => 30;

        public string TestName => "SimpleDbTest3";

        public void Execute()
        {
            ITransactionManager transactionManager = _database.Resolve<ITransactionManager>();
            ITransactionalSimpleDBOperations<SettingsDataRow> settingsTable = AsTransactional(_database.Resolve<ISimpleDBOperations<SettingsDataRow>>());
            ITransactionalSimpleDBOperations<TestDataDataRow> testDataTable = AsTransactional(_database.Resolve<ISimpleDBOperations<TestDataDataRow>>());

            ITransaction transaction = transactionManager.BeginTransaction();

            List<SettingsDataRow> settingsRows = [];
            List<TestDataDataRow> testDataRows = [];

            for (int i = 0; i < SettingsRowCount; i++)
            {
                settingsRows.Add(new SettingsDataRow
                {
                    Name = $"{SettingsPrefix}{i}",
                    Value = $"value-{i}",
                });
            }

            for (int i = 0; i < TestDataRowCount; i++)
            {
                testDataRows.Add(new TestDataDataRow
                {
                    Author = $"{TestDataPrefix}{i}",
                    Data = $"data-{i}",
                });
            }

            settingsTable.Insert(settingsRows, new InsertOptions(), transaction);
            testDataTable.Insert(testDataRows, new InsertOptions(), transaction);

            transaction.Commit();

            // Capture what is actually on disk before shutting down. Dispose performs a
            // checkpoint, so if the rows are already in the table file at this point then the
            // post-restart read proves nothing about WAL recovery - it would simply be reading a
            // fully up to date table file.
            _settingsFileContainedRowsBeforeShutdown = TableFileContains(
                Path.Combine(_database.DatabasePath, SettingsTableFile), $"{SettingsPrefix}0");

            // Deliberately no ForceWrite here. The tables are lazily written, so at this point the
            // only durable record of this batch is the WAL.
            _database.Restart();

            _settingsFileContainedRowsAfterShutdown = TableFileContains(
                Path.Combine(_database.DatabasePath, SettingsTableFile), $"{SettingsPrefix}0");
        }

        private static bool TableFileContains(string filePath, string value)
        {
            if (!File.Exists(filePath))
                return false;

            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new(stream);

            return reader.ReadToEnd().Contains(value, StringComparison.Ordinal);
        }

        public void Validate()
        {
            ValidateCheckpointOnShutdown();
            ValidateReadableAfterRestart();
            ValidateRollbackStillWorksAfterRestart();
        }

        /// <summary>
        /// Asserts the documented durability contract: a lazily written table holds committed rows
        /// only in the WAL until something flushes it, and a clean shutdown is one such flush.
        /// </summary>
        private void ValidateCheckpointOnShutdown()
        {
            if (_settingsFileContainedRowsBeforeShutdown)
            {
                throw new InvalidOperationException(
                    "Committed rows were already present in the table file before shutdown, but the " +
                    "Settings table is lazily written and was never force flushed. Deferred writing is not working.");
            }

            if (!_settingsFileContainedRowsAfterShutdown)
            {
                throw new InvalidOperationException(
                    "Committed rows were absent from the table file after a clean shutdown; " +
                    "Dispose should have checkpointed the table.");
            }

            WriteLine("  Rows were WAL-only before shutdown and checkpointed to the table file during Dispose.");
            WriteLine("  NOTE: because shutdown checkpoints, this test does not exercise WAL crash recovery.");
        }

        private void ValidateReadableAfterRestart()
        {
            ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();
            ISimpleDBOperations<TestDataDataRow> testDataTable = _database.Resolve<ISimpleDBOperations<TestDataDataRow>>();

            for (int i = 0; i < SettingsRowCount; i++)
            {
                string expectedName = $"{SettingsPrefix}{i}";
                SettingsDataRow row = settingsTable.Select().FirstOrDefault(r => r.Name == expectedName)
                    ?? throw new InvalidOperationException(
                        $"Settings row '{expectedName}' was committed before shutdown but is missing after restart; committed data did not survive the restart.");

                if (row.Value != $"value-{i}")
                {
                    throw new InvalidOperationException(
                        $"Settings row '{expectedName}' recovered with value '{row.Value}', expected 'value-{i}'.");
                }
            }

            for (int i = 0; i < TestDataRowCount; i++)
            {
                string expectedName = $"{TestDataPrefix}{i}";

                _ = testDataTable.Select().FirstOrDefault(r => r.Author == expectedName)
                    ?? throw new InvalidOperationException(
                        $"TestData row '{expectedName}' was committed before shutdown but is missing after restart; committed data did not survive the restart.");
            }

            WriteLine($"  {SettingsRowCount} settings and {TestDataRowCount} test data rows survived the clean restart.");
        }

        private void ValidateRollbackStillWorksAfterRestart()
        {
            ITransactionManager transactionManager = _database.Resolve<ITransactionManager>();
            ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();
            ITransactionalSimpleDBOperations<SettingsDataRow> transactionalSettings = AsTransactional(settingsTable);

            const string rolledBackName = "restart-rolled-back";

            ITransaction transaction = transactionManager.BeginTransaction();
            transactionalSettings.Insert(
                [new SettingsDataRow { Name = rolledBackName, Value = "should-not-persist" }],
                new InsertOptions(),
                transaction);
            transaction.Rollback();

            if (settingsTable.Select().Any(r => r.Name == rolledBackName))
            {
                throw new InvalidOperationException(
                    $"Settings row '{rolledBackName}' is still readable after rollback in a reopened session; the undo path did not run.");
            }

            WriteLine("  Rollback correctly undone in the reopened session.");
        }

        private static ITransactionalSimpleDBOperations<T> AsTransactional<T>(ISimpleDBOperations<T> table)
            where T : TableRowDefinition
        {
            if (table is not ITransactionalSimpleDBOperations<T> transactional)
            {
                throw new InvalidOperationException(
                    $"ISimpleDBOperations<{typeof(T).Name}> does not support transactional operations.");
            }

            return transactional;
        }
    }
}
