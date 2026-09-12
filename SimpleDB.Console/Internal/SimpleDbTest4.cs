using SimpleDB.Console.Abstractions;
using SimpleDB.Console.Tables;

using static System.Console;

namespace SimpleDB.Console.Internal
{
    /// <summary>
    /// WAL crash recovery test.
    ///
    /// SimpleDbTest3 covers a clean restart, which always checkpoints on shutdown and therefore
    /// never replays the WAL. This test covers the case that checkpointing is designed to protect
    /// against: the process dies after a commit but before the lazily written table files catch up.
    ///
    /// 1. Commits a batch of rows without forcing a table flush, so the batch is durable only in
    ///    the WAL.
    /// 2. Copies the database files while the session is still open - i.e. before any shutdown
    ///    checkpoint - capturing the genuine crash state.
    /// 3. Opens a database over that copy and verifies every committed row is present, which can
    ///    only happen if PerformStartupRecovery replayed the WAL entries beyond the watermark.
    /// </summary>
    public sealed class SimpleDbTest4 : ITestExecution
    {
        private const string SettingsPrefix = "crash-setting-";
        private const string TestDataPrefix = "crash-testdata-";
        private const string SettingsTableFile = "Settings.dat";
        private const int SettingsRowCount = 5;
        private const int TestDataRowCount = 4;

        private readonly ITestDatabaseContext _database;

        public SimpleDbTest4(ITestDatabaseContext database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        // Runs last: it depends on its own committed rows still being WAL-only, so it must not be
        // followed by anything, and must come after the restart test which checkpoints on shutdown.
        public int Order => 40;

        public string TestName => "SimpleDbTest4";

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
                settingsRows.Add(new SettingsDataRow { Name = $"{SettingsPrefix}{i}", Value = $"crash-value-{i}" });
            }

            for (int i = 0; i < TestDataRowCount; i++)
            {
                testDataRows.Add(new TestDataDataRow { Author = $"{TestDataPrefix}{i}", Data = $"crash-data-{i}" });
            }

            settingsTable.Insert(settingsRows, new InsertOptions(), transaction);
            testDataTable.Insert(testDataRows, new InsertOptions(), transaction);

            transaction.Commit();
        }

        public void Validate()
        {
            // Taken while the live session is still open, so no shutdown checkpoint has run.
            using ICrashRecoverySession recovered = _database.OpenCrashCopy();

            ValidateCrashStateWasCaptured(recovered.DatabasePath);
            ValidateRecoveredFromWal(recovered);
        }

        /// <summary>
        /// Guards the premise of the test. If the copied table file already contained the committed
        /// rows then nothing was left for the WAL to recover and a pass would be meaningless.
        /// </summary>
        private static void ValidateCrashStateWasCaptured(string crashPath)
        {
            string settingsFile = Path.Combine(crashPath, SettingsTableFile);

            if (File.Exists(settingsFile))
            {
                string contents = File.ReadAllText(settingsFile);

                if (contents.Contains($"{SettingsPrefix}0", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The copied table file already contained the committed rows, so this test cannot " +
                        "prove WAL recovery - the crash state was not captured before a checkpoint.");
                }
            }

            WriteLine("  Crash copy captured with the table file behind the WAL.");
        }

        private static void ValidateRecoveredFromWal(ICrashRecoverySession recovered)
        {
            ISimpleDBOperations<SettingsDataRow> settingsTable = recovered.Resolve<ISimpleDBOperations<SettingsDataRow>>();
            ISimpleDBOperations<TestDataDataRow> testDataTable = recovered.Resolve<ISimpleDBOperations<TestDataDataRow>>();

            for (int i = 0; i < SettingsRowCount; i++)
            {
                string expectedName = $"{SettingsPrefix}{i}";
                SettingsDataRow row = settingsTable.Select().FirstOrDefault(r => r.Name == expectedName)
                    ?? throw new InvalidOperationException(
                        $"Settings row '{expectedName}' was committed to the WAL but is missing after crash recovery.");

                if (row.Value != $"crash-value-{i}")
                {
                    throw new InvalidOperationException(
                        $"Settings row '{expectedName}' recovered with value '{row.Value}', expected 'crash-value-{i}'.");
                }
            }

            for (int i = 0; i < TestDataRowCount; i++)
            {
                string expectedAuthor = $"{TestDataPrefix}{i}";

                _ = testDataTable.Select().FirstOrDefault(r => r.Author == expectedAuthor)
                    ?? throw new InvalidOperationException(
                        $"TestData row '{expectedAuthor}' was committed to the WAL but is missing after crash recovery.");
            }

            WriteLine($"  Replayed {SettingsRowCount} settings and {TestDataRowCount} test data rows from the WAL after a simulated crash.");
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
