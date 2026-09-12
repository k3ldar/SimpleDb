using SimpleDB.Console.Abstractions;
using SimpleDB.Console.Tables;

using static System.Console;

namespace SimpleDB.Console.Internal
{
    /// <summary>
    /// End to end smoke test for <see cref="ISimpleDBManager.BackupDatabase(BackupOptions)"/> and
    /// <see cref="ISimpleDBManager.RestoreDatabase(string, RestoreOptions)"/>.
    /// </summary>
    /// <remarks>
    /// Seeds a known set of rows, takes a backup, then deliberately diverges the live data (both
    /// changing an existing row and adding brand new ones) so that a successful restore is
    /// observable: everything present at backup time must come back exactly as it was, and
    /// everything added afterwards must disappear.
    ///
    /// Restoring rewrites table files in place, which cannot be done while the current session
    /// still holds them open, so this runs through
    /// <see cref="ITestDatabaseContext.ExecuteWithDatabaseClosed(Action{ISimpleDBManager})"/>
    /// rather than calling RestoreDatabase directly against a resolved manager.
    ///
    /// Runs after SimpleDbTest7, as a final, independent check that does not depend on any prior
    /// test's data - it only asserts on the rows it creates itself.
    /// </remarks>
    public sealed class SimpleDbTest8 : ITestExecution
    {
        private const string BaselineKeyPrefix = "backup-restore-baseline-";
        private const string DivergedKeyPrefix = "backup-restore-diverged-";
        private const int BaselineRowCount = 5;
        private const int DivergedRowCount = 3;

        private readonly ITestDatabaseContext _database;
        private readonly Dictionary<string, string> _expectedAfterRestore = [];
        private BackupResult _backupResult;

        public SimpleDbTest8(ITestDatabaseContext database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public int Order => 80;

        public string TestName => "SimpleDbTest8";

        public void Execute()
        {
            // Forces ITransactionManager to be constructed against the live session, so it
            // attaches itself to ISimpleDBManager and the backup below genuinely checkpoints
            // the WAL rather than silently skipping it.
            _database.Resolve<ITransactionManager>();

            ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();

            for (int i = 0; i < BaselineRowCount; i++)
            {
                string key = $"{BaselineKeyPrefix}{i}";
                string value = $"baseline-value-{i}";

                settingsTable.Insert(new SettingsDataRow { Name = key, Value = value });
                _expectedAfterRestore[key] = value;
            }

            WriteLine($"  Inserted {BaselineRowCount} baseline rows.");

            ISimpleDBManager manager = _database.Resolve<ISimpleDBManager>();
            _backupResult = manager.BackupDatabase(new BackupOptions
            {
                ProgressCallback = ReportProgress,
            });

            if (!File.Exists(_backupResult.BackupFilePath))
                throw new InvalidOperationException($"Backup file '{_backupResult.BackupFilePath}' was not created.");

            WriteLine($"  Backed up {_backupResult.FileCount} files ({_backupResult.BackupSizeBytes:N0} bytes) to '{_backupResult.BackupFilePath}'.");

            // Diverge from the backup: change a baseline row's value, and add rows the backup has
            // never seen. Both must be undone once the restore runs.
            string mutatedKey = $"{BaselineKeyPrefix}0";
            SettingsDataRow mutatedRow = settingsTable.Select(r => r.Name == mutatedKey).First();
            mutatedRow.Value = "value-after-backup-must-not-survive-restore";
            settingsTable.Update(mutatedRow);

            for (int i = 0; i < DivergedRowCount; i++)
            {
                settingsTable.Insert(new SettingsDataRow
                {
                    Name = $"{DivergedKeyPrefix}{i}",
                    Value = $"diverged-value-{i}",
                });
            }

            WriteLine($"  Diverged from the backup: mutated 1 row and inserted {DivergedRowCount} new rows.");

            RestoreResult restoreResult = null;

            _database.ExecuteWithDatabaseClosed(standaloneManager =>
            {
                restoreResult = standaloneManager.RestoreDatabase(_backupResult.BackupFilePath, new RestoreOptions
                {
                    ForceCloseTransactions = true,
                    CreateSafetyBackup = true,
                    ProgressCallback = ReportProgress,
                });
            });

            if (restoreResult == null)
                throw new InvalidOperationException("RestoreDatabase did not run.");

            if (restoreResult.FileCount != _backupResult.FileCount)
            {
                throw new InvalidOperationException(
                    $"Restore reported {restoreResult.FileCount} files, expected {_backupResult.FileCount}.");
            }

            if (String.IsNullOrEmpty(restoreResult.SafetyBackupFilePath) || !File.Exists(restoreResult.SafetyBackupFilePath))
                throw new InvalidOperationException("Restore did not create the expected safety backup.");

            WriteLine($"  Restored {restoreResult.FileCount} files; safety backup at '{restoreResult.SafetyBackupFilePath}'.");

            if (restoreResult.Warnings.Count > 0)
            {
                foreach (string warning in restoreResult.Warnings)
                    WriteLine($"    Warning: {warning}");
            }
        }

        public void Validate()
        {
            ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();

            foreach (KeyValuePair<string, string> expected in _expectedAfterRestore)
            {
                List<SettingsDataRow> matches = [.. settingsTable.Select(r => r.Name == expected.Key)];

                if (matches.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Baseline row '{expected.Key}' should exist exactly once after restore, found {matches.Count}.");
                }

                if (matches[0].Value != expected.Value)
                {
                    throw new InvalidOperationException(
                        $"Baseline row '{expected.Key}' has value '{matches[0].Value}' after restore, expected '{expected.Value}'.");
                }
            }

            for (int i = 0; i < DivergedRowCount; i++)
            {
                string key = $"{DivergedKeyPrefix}{i}";
                List<SettingsDataRow> matches = [.. settingsTable.Select(r => r.Name == key)];

                if (matches.Count != 0)
                {
                    throw new InvalidOperationException(
                        $"Row '{key}' was inserted after the backup and must not survive a restore.");
                }
            }

            ITransactionManager transactionManager = _database.Resolve<ITransactionManager>();

            if (transactionManager.NextTransactionId != 0)
            {
                throw new InvalidOperationException(
                    $"Transaction id sequence should have reset to zero after restore, found {transactionManager.NextTransactionId}.");
            }

            WriteLine("  Backup/restore round trip verified: baseline rows intact, post-backup rows gone, transaction sequence reset.");
        }

        /// <summary>
        /// Prints backup/restore progress to the console, e.g.:
        /// "  [Restoring    35%] PackingFile: Settings.dat"
        /// so the current stage, file/table and approximate overall percentage are all visible.
        /// </summary>
        private static void ReportProgress(BackupProgressEventArgs progress)
        {
            string percent = progress.PercentComplete.HasValue
                ? $"{progress.PercentComplete.Value,3}%"
                : "  ? ";

            string item = String.IsNullOrEmpty(progress.CurrentItem)
                ? String.Empty
                : $" - {progress.CurrentItem}";

            string count = progress.TotalItems > 0
                ? $" ({progress.ItemsProcessed}/{progress.TotalItems})"
                : String.Empty;

            WriteLine($"    [{percent}] {progress.Stage,-16}{count}{item}");
        }
    }
}
