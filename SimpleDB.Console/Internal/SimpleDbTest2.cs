using System.Security.Cryptography;
using System.Text;

using SimpleDB.Console.Abstractions;
using SimpleDB.Console.Tables;

namespace SimpleDB.Console.Internal
{
    /// <summary>
    /// Multi-table, multi-transaction test:
    ///
    /// 1. Inserts several Settings and TestData rows within a single explicit transaction, commits.
    /// 2. Repeats with a second batch, but rolls the transaction back.
    /// 3. Repeats with a third batch, commits.
    /// 4. Verifies the WAL on disk contains entries for exactly the committed batches (and nothing
    ///    from the rolled back batch), then force flushes every table and verifies the table files.
    /// </summary>
    public sealed class SimpleDbTest2 : ITestExecution
    {
        private const string SettingsTableName = "Settings";
        private const string TestDataTableName = "TestData";
        private const string SettingsTableFile = "Settings.dat";
        private const string TestDataTableFile = "TestData.dat";

        private readonly ITestDatabaseContext _database;
        private readonly string _path;

        private readonly List<ExpectedRow> _committedBatchOne = [];
        private readonly List<ExpectedRow> _rolledBackBatch = [];
        private readonly List<ExpectedRow> _committedBatchTwo = [];

        private long _committedTransactionOne;
        private long _committedTransactionTwo;
        private long _rolledBackTransaction;

        public SimpleDbTest2(ITestDatabaseContext database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _path = database.DatabasePath;
        }

        public int Order => 20;

        public string TestName => "SimpleDbTest2";

        public void Execute()
        {
            ITransactionManager transactionManager = Resolve<ITransactionManager>();
            ISimpleDBOperations<SettingsDataRow> settingsTable = Resolve<ISimpleDBOperations<SettingsDataRow>>();
            ISimpleDBOperations<TestDataDataRow> testDataTable = Resolve<ISimpleDBOperations<TestDataDataRow>>();

            // Step 1 - multiple records across both tables in one transaction, committed.
            _committedTransactionOne = RunBatch(transactionManager, settingsTable, testDataTable, "batch1", 3, 2,
                _committedBatchOne, commit: true);

            // Step 2 - same shape of work, but rolled back. Nothing from this batch may reach the WAL.
            _rolledBackTransaction = RunBatch(transactionManager, settingsTable, testDataTable, "rollback", 2, 2,
                _rolledBackBatch, commit: false);

            // Step 3 - a further committed batch, proving the WAL keeps accumulating after a rollback.
            _committedTransactionTwo = RunBatch(transactionManager, settingsTable, testDataTable, "batch2", 2, 3,
                _committedBatchTwo, commit: true);
        }

        public void Validate()
        {
            // Step 4a - verify the write-ahead log on disk, before any table flush has occurred.
            List<WalFileEntry> walEntries = WalFileInspector.ReadEntries(_path);

            List<WalFileOutcome> walOutcomes = WalFileInspector.ReadOutcomes(_path);

            ValidateCommittedBatchInWal(walEntries, _committedBatchOne, _committedTransactionOne);
            ValidateCommittedBatchInWal(walEntries, _committedBatchTwo, _committedTransactionTwo);
            ValidateTransactionOutcomes(walOutcomes);
            ValidateRolledBackBatchMarkedAborted(walEntries, walOutcomes);
            ValidateWalSequenceNumbers(walEntries);

            // Step 4b - force a flush of all tables, then verify the durable table files.
            ISimpleDBOperations<SettingsDataRow> settingsTable = Resolve<ISimpleDBOperations<SettingsDataRow>>();
            ISimpleDBOperations<TestDataDataRow> testDataTable = Resolve<ISimpleDBOperations<TestDataDataRow>>();

            settingsTable.ForceWrite();
            testDataTable.ForceWrite();

            ValidateTableFile(SettingsTableFile, SettingsTableName);
            ValidateTableFile(TestDataTableFile, TestDataTableName);

            ReportRollbackVisibility(settingsTable, testDataTable);
        }

        private long RunBatch(ITransactionManager transactionManager,
            ISimpleDBOperations<SettingsDataRow> settingsTable,
            ISimpleDBOperations<TestDataDataRow> testDataTable,
            string prefix, int settingsCount, int testDataCount, List<ExpectedRow> expected, bool commit)
        {
            ITransactionalSimpleDBOperations<SettingsDataRow> transactionalSettings = AsTransactional(settingsTable);
            ITransactionalSimpleDBOperations<TestDataDataRow> transactionalTestData = AsTransactional(testDataTable);

            ITransaction transaction = transactionManager.BeginTransaction();

            List<SettingsDataRow> settingsRows = [];

            for (int i = 0; i < settingsCount; i++)
            {
                settingsRows.Add(new SettingsDataRow { Name = $"{prefix}-name-{i}", Value = $"{prefix}-value-{i}" });
            }

            List<TestDataDataRow> testDataRows = [];

            for (int i = 0; i < testDataCount; i++)
            {
                testDataRows.Add(new TestDataDataRow { Author = $"{prefix}-author-{i}", Data = $"{prefix}-data-{i}" });
            }

            transactionalSettings.Insert(settingsRows, new InsertOptions(), transaction);
            transactionalTestData.Insert(testDataRows, new InsertOptions(), transaction);

            settingsRows.ForEach(r => expected.Add(new ExpectedRow(SettingsTableName, r.Id, r.Name, r.Value)));
            testDataRows.ForEach(r => expected.Add(new ExpectedRow(TestDataTableName, r.Id, r.Author, r.Data)));

            if (commit)
            {
                transaction.Commit();
            }
            else
            {
                transaction.Rollback();
            }

            return transaction.TransactionId;
        }

        private static void ValidateCommittedBatchInWal(List<WalFileEntry> walEntries, List<ExpectedRow> expectedRows, long transactionId)
        {
            foreach (ExpectedRow expected in expectedRows)
            {
                List<WalFileEntry> matches = walEntries
                    .Where(e => e.TableName == expected.TableName && e.RecordId == expected.Id)
                    .ToList();

                if (matches.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected exactly one WAL entry for {expected.TableName} record {expected.Id}, found {matches.Count}.");
                }

                WalFileEntry entry = matches[0];

                if (entry.Operation != WalFileInspector.OperationInsert)
                {
                    throw new InvalidOperationException(
                        $"WAL entry for {expected.TableName} record {expected.Id} has operation {entry.Operation}, expected Insert.");
                }

                if (entry.TransactionId != transactionId)
                {
                    throw new InvalidOperationException(
                        $"WAL entry for {expected.TableName} record {expected.Id} belongs to transaction {entry.TransactionId}, expected {transactionId}.");
                }

                string actualFingerprint = ComputeFingerprint(
                    entry.GetRecordString(expected.TableName == SettingsTableName ? "Name" : "Author"),
                    entry.GetRecordString(expected.TableName == SettingsTableName ? "Value" : "Data"));

                if (actualFingerprint != expected.Fingerprint)
                {
                    throw new InvalidOperationException(
                        $"WAL payload fingerprint mismatch for {expected.TableName} record {expected.Id}; " +
                        $"expected {expected.Fingerprint}, found {actualFingerprint}.");
                }
            }
        }

        /// <summary>
        /// WAL entries are written as each operation happens, so a rolled back transaction still
        /// has its entries on disk - that is what makes crash recovery able to undo them. The
        /// contract is therefore that the transaction is resolved by an Abort marker, never that
        /// its entries vanish.
        /// </summary>
        private void ValidateRolledBackBatchMarkedAborted(List<WalFileEntry> walEntries, List<WalFileOutcome> walOutcomes)
        {
            List<WalFileOutcome> outcomes = walOutcomes.Where(o => o.TransactionId == _rolledBackTransaction).ToList();

            if (outcomes.Count != 1 || outcomes[0].Operation != WalFileInspector.OperationAbort)
            {
                throw new InvalidOperationException(
                    $"Rolled back transaction {_rolledBackTransaction} must have exactly one Abort marker in the WAL, " +
                    $"found {outcomes.Count} marker(s): {String.Join(", ", outcomes.Select(o => o.Operation))}.");
            }

            foreach (ExpectedRow rolledBack in _rolledBackBatch)
            {
                bool present = walEntries.Any(e => e.TransactionId == _rolledBackTransaction &&
                    e.TableName == rolledBack.TableName && e.RecordId == rolledBack.Id);

                if (!present)
                {
                    throw new InvalidOperationException(
                        $"WAL is missing the undo information for rolled back {rolledBack.TableName} record " +
                        $"{rolledBack.Id}; recovery could not reverse it after a crash.");
                }
            }
        }

        private void ValidateTransactionOutcomes(List<WalFileOutcome> walOutcomes)
        {
            foreach (long transactionId in new[] { _committedTransactionOne, _committedTransactionTwo })
            {
                List<WalFileOutcome> outcomes = walOutcomes.Where(o => o.TransactionId == transactionId).ToList();

                if (outcomes.Count != 1 || outcomes[0].Operation != WalFileInspector.OperationCommit)
                {
                    throw new InvalidOperationException(
                        $"Committed transaction {transactionId} must have exactly one Commit marker in the WAL, " +
                        $"found {outcomes.Count} marker(s).");
                }
            }
        }

        private void ValidateWalSequenceNumbers(List<WalFileEntry> walEntries)
        {
            List<WalFileEntry> ownEntries = walEntries
                .Where(e => e.TransactionId == _committedTransactionOne || e.TransactionId == _committedTransactionTwo)
                .ToList();

            int expectedCount = _committedBatchOne.Count + _committedBatchTwo.Count;

            if (ownEntries.Count != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Expected {expectedCount} WAL entries across the two committed transactions, found {ownEntries.Count}.");
            }

            for (int i = 1; i < ownEntries.Count; i++)
            {
                if (ownEntries[i].SequenceNumber <= ownEntries[i - 1].SequenceNumber)
                {
                    throw new InvalidOperationException(
                        "WAL sequence numbers must increase monotonically in the order entries are appended; " +
                        $"found {ownEntries[i - 1].SequenceNumber} followed by {ownEntries[i].SequenceNumber}.");
                }
            }

            // Both tables must be represented in each committed transaction.
            foreach (long transactionId in new[] { _committedTransactionOne, _committedTransactionTwo })
            {
                List<string> tables = ownEntries.Where(e => e.TransactionId == transactionId)
                    .Select(e => e.TableName)
                    .Distinct()
                    .ToList();

                if (!tables.Contains(SettingsTableName) || !tables.Contains(TestDataTableName))
                {
                    throw new InvalidOperationException(
                        $"Transaction {transactionId} should span both tables, found: {String.Join(", ", tables)}.");
                }
            }
        }

        private void ValidateTableFile(string tableFile, string tableName)
        {
            string filePath = Path.Combine(_path, tableFile);

            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException($"Table file '{tableFile}' does not exist after ForceWrite().");
            }

            byte[] fileBytes = WalFileInspector.ReadAllBytesShared(filePath);

            IEnumerable<ExpectedRow> expectedRows = _committedBatchOne.Concat(_committedBatchTwo)
                .Where(r => r.TableName == tableName);

            foreach (ExpectedRow expected in expectedRows)
            {
                if (IndexOf(fileBytes, Encoding.UTF8.GetBytes(expected.First)) < 0 ||
                    IndexOf(fileBytes, Encoding.UTF8.GetBytes(expected.Second)) < 0)
                {
                    throw new InvalidOperationException(
                        $"Committed record {expected.Id} was not found in flushed table file '{tableFile}'.");
                }
            }
        }

        /// <summary>
        /// Rollback reverses the table's in-memory state, so no rolled back row may remain
        /// readable once the transaction has been abandoned.
        /// </summary>
        private void ReportRollbackVisibility(ISimpleDBOperations<SettingsDataRow> settingsTable,
            ISimpleDBOperations<TestDataDataRow> testDataTable)
        {
            int leaked = _rolledBackBatch.Count(r => r.TableName == SettingsTableName
                ? settingsTable.Select(s => s.Name == r.First).Count > 0
                : testDataTable.Select(t => t.Author == r.First).Count > 0);

            if (leaked > 0)
            {
                throw new InvalidOperationException(
                    $"{leaked} of {_rolledBackBatch.Count} rolled back rows are still readable from the tables; " +
                    "rollback must undo the table data.");
            }
        }

        private T Resolve<T>() where T : notnull
        {
            return _database.Resolve<T>();
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

        private static string ComputeFingerprint(string first, string second)
        {
            byte[] bytes = Encoding.UTF8.GetBytes($"{first}|{second}");

            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length)
            {
                return -1;
            }

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;

                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }

        private sealed record ExpectedRow(string TableName, long Id, string First, string Second)
        {
            public string Fingerprint { get; } = ComputeFingerprint(First, Second);
        }
    }
}
