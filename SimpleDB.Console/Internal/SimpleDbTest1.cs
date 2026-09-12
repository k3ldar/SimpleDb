using System.Security.Cryptography;
using System.Text;

using SimpleDB.Console.Abstractions;
using SimpleDB.Console.Tables;

namespace SimpleDB.Console.Internal
{
    public sealed class SimpleDbTest1 : ITestExecution
    {
        // Relative to the test database root, matching SimpleDBOperations<T>.ValidateTableName's
        // path composition of {rootPath}\{domain}\{tableName}{.dat}.
        private const string SettingsTableFile = "Settings.dat";

        private readonly ITestDatabaseContext _database;
        private readonly string _path;

        private string _expectedName;
        private string _expectedValue;
        private string _expectedFingerprint;

        public SimpleDbTest1(ITestDatabaseContext database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _path = database.DatabasePath;
        }

        public int Order => 10;

        public string TestName => "SimpleDbTest1";

        public void Execute()
        {
            ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();

            if (settingsTable == null)
            {
                throw new InvalidOperationException("Failed to resolve ISimpleDBOperations<SettingsDataRow> from the service provider.");
            }

            _expectedName = "TestSetting";
            _expectedValue = "TestValue";

            SettingsDataRow row = new() { Name = _expectedName, Value = _expectedValue };
            settingsTable.Insert(row);

            // Fingerprint of only the deterministic fields (Name/Value) - audit fields such as
            // UpdatedTicks/Id vary per run so they are intentionally excluded from the hash.
            _expectedFingerprint = ComputeFingerprint(_expectedName, _expectedValue);

            // Force the in-memory table to flush to its on-disk file. Prior to this call the WAL
            // (Sys$WalEntries.dat) is the only durable copy of the change - the table file itself
            // is only updated on checkpoint/startup-recovery.
            settingsTable.ForceWrite();
        }

        public void Validate()
        {
            // 1) Round-trip check via the public API - proves the value was stored and can be
            //    read back correctly through normal table operations.
            ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();
            SettingsDataRow persisted = settingsTable.Select(r => r.Name == _expectedName).FirstOrDefault();

            if (persisted == null || persisted.Value != _expectedValue)
            {
                throw new InvalidOperationException("Inserted SettingsDataRow could not be found or its value did not match via Select().");
            }

            // 2) Raw on-disk verification of the WAL file - proves the DML was captured in the
            //    write-ahead log, independent of the in-memory table cache. The WAL stores each
            //    entry as a length-prefixed JSON document, with the source record itself held in
            //    SerializedRecord as a Base64 string, so it must be parsed rather than scanned.
            string walFingerprint = ReadWalFingerprint("Settings", persisted.Id);

            if (walFingerprint == null)
            {
                throw new InvalidOperationException($"Could not locate serialized record for '{_expectedName}'/'{_expectedValue}' in the WAL file '{WalFileInspector.WalTableFile}'.");
            }

            if (walFingerprint != _expectedFingerprint)
            {
                throw new InvalidOperationException("Fingerprint of the WAL-recorded data does not match the expected fingerprint computed at insert time.");
            }

            // 3) Raw on-disk verification of the table file itself, after ForceWrite() flushed the
            //    checkpoint - proves the data made it out of memory and into the durable table file.
            string tableFingerprint = FindFingerprintInFile(Path.Combine(_path, SettingsTableFile), _expectedName, _expectedValue);

            if (tableFingerprint == null)
            {
                throw new InvalidOperationException($"Could not locate serialized record for '{_expectedName}'/'{_expectedValue}' in the table file '{SettingsTableFile}'.");
            }

            if (tableFingerprint != _expectedFingerprint)
            {
                throw new InvalidOperationException("Fingerprint of the table file data does not match the expected fingerprint computed at insert time.");
            }
        }

        /// <summary>
        /// Computes a SHA-256 fingerprint over only the deterministic fields of the record so it
        /// can be compared regardless of where (WAL vs table file) or when the data was read.
        /// </summary>
        private static string ComputeFingerprint(string name, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes($"{name}|{value}");
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        /// <summary>
        /// Returns the fingerprint of the record logged in the WAL for the supplied table/record id,
        /// or null when no matching entry has been persisted.
        /// </summary>
        private string ReadWalFingerprint(string tableName, long recordId)
        {
            WalFileEntry entry = WalFileInspector.ReadEntries(_path)
                .FirstOrDefault(e => e.TableName == tableName && e.RecordId == recordId);

            if (entry == null)
            {
                return null;
            }

            return ComputeFingerprint(entry.GetRecordString("Name"), entry.GetRecordString("Value"));
        }

        /// <summary>
        /// Opens a SimpleDB data file for shared, read-only access (the table's own FileStream is
        /// opened with FileShare.Read, so a second read-only handle is permitted concurrently) and
        /// scans the raw bytes for the UTF8 JSON fragments produced by SerializeRecord(). Records
        /// are stored uncompressed by default (CompressionType.None), so the JSON text remains
        /// searchable as plain bytes on disk.
        /// </summary>
        private static string FindFingerprintInFile(string filePath, string name, string value)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            byte[] fileBytes = WalFileInspector.ReadAllBytesShared(filePath);

            byte[] nameNeedle = Encoding.UTF8.GetBytes($"\"Name\":\"{name}\"");
            byte[] valueNeedle = Encoding.UTF8.GetBytes($"\"Value\":\"{value}\"");

            if (IndexOf(fileBytes, nameNeedle) < 0 || IndexOf(fileBytes, valueNeedle) < 0)
            {
                return null;
            }

            return ComputeFingerprint(name, value);
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
    }
}
