using System.Buffers.Binary;
using System.Text.Json;

namespace SimpleDB.Console.Internal
{
    /// <summary>
    /// Reads SimpleDB data files directly from disk so tests can verify what was physically
    /// persisted, independently of any in-memory table cache.
    ///
    /// The append-only WAL layout written by AppendOnlyDataWriter is a fixed header
    /// (Consts.TotalHeaderLength bytes) followed by, per entry, a little-endian Int32 length
    /// prefix and that many bytes of UTF8 JSON for the WalEntryDataRow. WalEntryDataRow
    /// .SerializedRecord is a byte[], which System.Text.Json emits as a Base64 string, so the
    /// source record's JSON has to be decoded out of it rather than searched for as plain text.
    /// </summary>
    internal static class WalFileInspector
    {
        /// <summary>
        /// Path of the WAL table file relative to the database root, matching
        /// SimpleDBOperations&lt;T&gt;.ValidateTableName's composition of {root}\{domain}\{table}.dat.
        /// </summary>
        public const string WalTableFile = @"System\Sys$WalEntries.dat";

        /// <summary>
        /// Mirrors SimpleDB.Internal.Consts.TotalHeaderLength - the fixed header written ahead of
        /// any record data in every table file (2 + 2 + 8 + 8 + 16 + 1 + 4 + 4 + 4 + 4).
        /// </summary>
        public const int HeaderLength = 53;

        /// <summary>
        /// Mirrors SimpleDB.OperationType.Insert, which is internal to the SimpleDB assembly.
        /// </summary>
        public const int OperationInsert = 0;

        /// <summary>
        /// Mirrors SimpleDB.OperationType.Commit and .Abort. These are transaction outcome
        /// markers rather than DML: they carry no SerializedRecord and exist only to record
        /// whether the entries sharing their TransactionId were committed or rolled back.
        /// </summary>
        public const int OperationCommit = 3;
        public const int OperationAbort = 4;

        public static List<WalFileEntry> ReadEntries(string databasePath)
        {
            ReadAll(databasePath, out List<WalFileEntry> entries, out _);

            return entries;
        }

        /// <summary>
        /// Returns the transaction outcome markers in the log. A rolled back transaction still
        /// has its DML entries on disk - they are written as each operation happens, not at
        /// commit - so the Abort marker is what records that they must never be replayed.
        /// </summary>
        public static List<WalFileOutcome> ReadOutcomes(string databasePath)
        {
            ReadAll(databasePath, out _, out List<WalFileOutcome> outcomes);

            return outcomes;
        }

        private static void ReadAll(string databasePath, out List<WalFileEntry> entries, out List<WalFileOutcome> outcomes)
        {
            entries = [];
            outcomes = [];

            string filePath = Path.Combine(databasePath, WalTableFile);

            if (!File.Exists(filePath))
            {
                return;
            }

            byte[] fileBytes = ReadAllBytesShared(filePath);
            int offset = HeaderLength;

            while (offset + sizeof(int) <= fileBytes.Length)
            {
                int length = BinaryPrimitives.ReadInt32LittleEndian(fileBytes.AsSpan(offset, sizeof(int)));
                offset += sizeof(int);

                if (length <= 0 || offset + length > fileBytes.Length)
                {
                    break;
                }

                using JsonDocument document = JsonDocument.Parse(fileBytes.AsMemory(offset, length));
                offset += length;

                JsonElement root = document.RootElement;

                int operation = root.GetProperty("Operation").GetInt32();
                long transactionId = root.GetProperty("TransactionId").GetInt64();
                long sequenceNumber = root.GetProperty("SequenceNumber").GetInt64();

                // Outcome markers are not DML - they carry a null SerializedRecord, which
                // GetBytesFromBase64 would reject, and no table or record of their own.
                if (operation == OperationCommit || operation == OperationAbort)
                {
                    outcomes.Add(new WalFileOutcome(transactionId, operation, sequenceNumber));
                    continue;
                }

                entries.Add(new WalFileEntry(
                    transactionId,
                    root.GetProperty("TableName").GetString(),
                    operation,
                    root.GetProperty("RecordId").GetInt64(),
                    sequenceNumber,
                    root.GetProperty("SerializedRecord").GetBytesFromBase64()));
            }
        }

        /// <summary>
        /// Opens a data file for shared, read-only access. The owning table holds the file open
        /// with FileAccess.ReadWrite/FileShare.Read, so a concurrent read-only handle is permitted.
        /// </summary>
        public static byte[] ReadAllBytesShared(string filePath)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using MemoryStream memory = new();
            stream.CopyTo(memory);

            return memory.ToArray();
        }
    }

    /// <summary>
    /// A single write-ahead log entry as physically stored on disk.
    /// </summary>
    internal sealed record WalFileEntry(long TransactionId, string TableName, int Operation,
        long RecordId, long SequenceNumber, byte[] SerializedRecord)
    {
        /// <summary>
        /// Reads a string property from the Base64-decoded source record JSON.
        /// </summary>
        public string GetRecordString(string propertyName)
        {
            using JsonDocument record = JsonDocument.Parse(SerializedRecord);

            return record.RootElement.TryGetProperty(propertyName, out JsonElement value) ? value.GetString() : null;
        }
    }

    /// <summary>
    /// A transaction outcome marker (commit or abort) as physically stored in the log.
    /// </summary>
    internal sealed record WalFileOutcome(long TransactionId, int Operation, long SequenceNumber);
}
