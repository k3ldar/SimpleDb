/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 *  .Net Core Plugin Manager is distributed under the GNU General Public License version 3 and  
 *  is also available under alternative licenses negotiated directly with Simon Carter.  
 *  If you obtained Service Manager under the GPL, then the GPL applies to all loadable 
 *  Service Manager modules used on your system as well. The GPL (version 3) is 
 *  available at https://opensource.org/licenses/GPL-3.0
 *
 *  This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY,
 *  without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 *  See the GNU General Public License for more details.
 *
 *  The Original Code was created by Simon Carter (s1cart3r@gmail.com)
 *
 *  Copyright (c) 2018 - 2023 Simon Carter.  All Rights Reserved.
 *
 *  Product:  SimpleDB
 *  
 *  File: SimpleDBInitializer.cs
 *
 *  Purpose:  SimpleDB Initializer for SimpleDB
 *
 *  Date        Name                Reason
 *  23/05/2022  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using PluginManager.Abstractions;

using Shared.Classes;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable CA2208

namespace SimpleDB.Internal
{
    internal sealed class SimpleDBManager : ThreadManager, ISimpleDBManager, IDatabaseLock, ITransactionManagerAttachable
    {
        public const uint DefaultMinimumVersion = 1;
        private const int ThreadRuntime = 500;


        private uint _minimumVersion = DefaultMinimumVersion;
        private readonly Dictionary<string, ISimpleDBTable> _tables = [];
        private readonly Dictionary<ISimpleDBTable, DateTime> _tableLastAction = [];
        private readonly object _lock = new();

        private readonly object _databaseLock = new();

        #region Constructors

        private SimpleDBManager()
            : base(null, TimeSpan.FromMilliseconds(ThreadRuntime))
        {
            ContinueIfGlobalException = true;
        }

        public SimpleDBManager(ISettingsProvider settingsProvider)
            : this()
        {
            if (settingsProvider == null)
                throw new ArgumentNullException(nameof(settingsProvider));

            SimpleDBSettings settings = settingsProvider.GetSettings<SimpleDBSettings>(nameof(SimpleDBSettings));

            if (settings == null || String.IsNullOrEmpty(settings.Path))
                throw new InvalidOperationException();

            Initialize(settings);
        }

        public SimpleDBManager(string path, string encryptionKey = null, SimpleDBSettings settings = null)
            : this()
        {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            settings ??= new SimpleDBSettings { Path = path, EnycryptionKey = encryptionKey };

            // allow callers to pass settings with tuning values but let explicit path/key win,
            // matching current behaviour where these identify *this* instance rather than being tuning knobs
            settings.Path = path;
            settings.EnycryptionKey = encryptionKey ?? settings.EnycryptionKey;

            Initialize(settings);
        }

        #endregion Constructors

        public void Initialize(IPluginClassesService pluginClassesService)
        {
            foreach (KeyValuePair<string, ISimpleDBTable> table in _tables)
                table.Value.Initialize(pluginClassesService);
        }

        public void ClearMemory()
        {
            foreach (string table in _tables.Keys)
                _tables[table].ClearAllMemory();
        }

        internal string EncryptionKey { get; private set; }


        public string Path { get; private set; }

        public uint MinimumVersion
        {
            get
            {
                return _minimumVersion;
            }

            set
            {
                if (value < DefaultMinimumVersion)
                    value = DefaultMinimumVersion;

                _minimumVersion = value;
            }
        }

        #region IDatabaseLock

        public IDisposable AcquireForOperation()
        {
            // Read at the point of use rather than captured in a field, so the value cannot depend
            // on whether this instance happened to be built before or after configuration.
            TimeSpan timeout = SimpleDBConfiguration.OperationLockTimeout;

            try
            {
                // Monitor is reentrant, so a commit that re-enters the database to write the WAL and
                // watermark system tables simply reacquires the lock it already owns.
                return TimedLock.Lock(_databaseLock, timeout);
            }
            catch (LockTimeoutException e)
            {
                // TimedLock is an implementation detail of how the gate is built; callers of
                // BeginTransaction and the table operations should never have to know about it.
                // Translated here, at the only place the timeout can originate, with the original
                // exception preserved for diagnostics.
                throw new DatabaseLockTimeoutException(
                    $"Failed to acquire the database lock within {timeout.TotalSeconds} seconds.", e);
            }
        }

        public IDisposable TryAcquireForCheckpoint()
        {
            try
            {
                return TimedLock.Lock(_databaseLock, SimpleDBConfiguration.CheckpointLockTimeout);
            }
            catch (LockTimeoutException)
            {
                // Deliberately swallowed. The WAL stays over threshold, so the next commit will
                // attempt the checkpoint again - no work is lost by skipping this one.
                return null;
            }
        }

        #endregion IDatabaseLock

        public void RegisterTable(ISimpleDBTable simpleDBTable)
        {
            if (simpleDBTable == null)
                throw new ArgumentNullException(nameof(simpleDBTable));

            using (TimedLock timedLock = TimedLock.Lock(_lock))
            {
                if (String.IsNullOrEmpty(simpleDBTable.TableName))
                    throw new InvalidOperationException("Null table name");

                if (_tables.ContainsKey(simpleDBTable.TableName))
                    throw new InvalidOperationException($"Table {simpleDBTable.TableName} already exists");

                _tables.Add(simpleDBTable.TableName, simpleDBTable);

                if (simpleDBTable.CachingStrategy == CachingStrategy.SlidingMemory)
                {
                    _tableLastAction.Add(simpleDBTable, DateTime.UtcNow);
                    simpleDBTable.OnAction += SimpleDBTable_OnAction;

                    if (!ThreadManager.Exists(nameof(SimpleDBManager)))
                        ThreadManager.ThreadStart(this, nameof(SimpleDBManager), ThreadPriority.Lowest);
                }
            }
        }

        public void UnregisterTable(ISimpleDBTable simpleDBTable)
        {
            if (simpleDBTable == null)
                throw new ArgumentNullException(nameof(simpleDBTable));

            using (TimedLock timedLock = TimedLock.Lock(_lock))
            {
                if (string.IsNullOrEmpty(simpleDBTable.TableName))
                    return;

                if (!_tables.ContainsKey(simpleDBTable.TableName))
                    throw new ArgumentException($"Table {simpleDBTable.TableName} already exists");

                _tables.Remove(simpleDBTable.TableName);

                if (_tableLastAction.ContainsKey(simpleDBTable))
                {
                    simpleDBTable.OnAction -= SimpleDBTable_OnAction;
                    _tableLastAction.Remove(simpleDBTable);
                }
            }
        }

        public IReadOnlyDictionary<string, ISimpleDBTable> Tables
        {
            get
            {
                return new Dictionary<string, ISimpleDBTable>(_tables);
            }
        }

        /// <summary>
        /// Backing reference used to coordinate checkpoint/transaction close/sequence reset for
        /// backup and restore. Set by <see cref="TransactionManager"/>'s constructor to avoid a
        /// circular constructor dependency between the two singletons.
        /// </summary>
        internal ITransactionMaintenance TransactionManager { get; private set; }

        public void AttachTransactionManager(ITransactionMaintenance transactionManager)
        {
            TransactionManager = transactionManager;
        }

        protected override bool Run(object parameters)
        {
            using (TimedLock timedLock = TimedLock.Lock(_lock))
            {
                foreach (ISimpleDBTable simpleDBTable in _tableLastAction.Keys)
                {
                    DateTime lastRun = _tableLastAction[simpleDBTable];

                    TimeSpan timeFromLastRun = DateTime.UtcNow - lastRun;

                    if (timeFromLastRun > simpleDBTable.SlidingMemoryTimeout)
                    {
                        try
                        {
                            //simpleDBTable.ClearAllMemory();
                            _tableLastAction[simpleDBTable] = DateTime.UtcNow;
                        }
                        catch (LockTimeoutException)
                        {
                            //ignore specific exception
                        }

                        OnMemoryCleared?.Invoke(simpleDBTable);
                    }
                }
            }

            // Commits only trigger a checkpoint when the WAL crosses WalCheckpointThreshold at the
            // moment a commit happens (see TransactionManager.InternalCommitTransaction). An app
            // that goes idle right after crossing the threshold - or one that commits in small,
            // infrequent bursts that never individually push the count over the line - can leave
            // the WAL sitting above (or just under) threshold indefinitely, since nothing forces a
            // recheck once commit activity stops. Piggybacking a periodic, best-effort checkpoint
            // attempt onto this background thread (the same thread that used to drive
            // ClearAllMemory) closes that gap without requiring a dedicated thread of its own.
            try
            {
                TransactionManager?.CheckpointDatabaseIfNeeded();
            }
            catch (LockTimeoutException)
            {
                // Another checkpoint or an in-flight transaction currently holds what this needs;
                // the next periodic tick will simply try again.
            }

            return !HasCancelled();
        }

        public event SimpleDbEvent OnMemoryCleared;

        #region Backup / Restore

        private const string BackupMagic = "SDBBAK1";
        private const string DefaultBackupSubFolder = "Backups";
        private const string DefaultBackupExtension = ".sdbbak";

        public BackupResult BackupDatabase(BackupOptions options = null)
        {
            options ??= new BackupOptions();

            string backupFolder = String.IsNullOrEmpty(options.Path)
                ? System.IO.Path.Combine(Path, DefaultBackupSubFolder)
                : options.Path;

            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);

            string fileName = String.IsNullOrEmpty(options.FileName)
                ? $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}{DefaultBackupExtension}"
                : options.FileName;

            string backupFilePath = System.IO.Path.Combine(backupFolder, fileName);

            using (AcquireForOperation())
            {
                ReportProgress(options.ProgressCallback, BackupProgressStage.Preparing, null, 0, 0, 0,
                    "Preparing database for backup");

                PrepareForSnapshot(options.ForceCloseTransactions);

                List<string> files = [.. Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories)
                    .Where(f => !f.StartsWith(backupFolder, StringComparison.OrdinalIgnoreCase))];

                WriteBackupFile(backupFilePath, files, options.ProgressCallback, 0, 100);

                ReportProgress(options.ProgressCallback, BackupProgressStage.Completed, null, files.Count, files.Count, 100,
                    "Backup complete");

                return new BackupResult
                {
                    BackupFilePath = backupFilePath,
                    FileCount = files.Count,
                    BackupSizeBytes = new FileInfo(backupFilePath).Length,
                };
            }
        }

        public RestoreResult RestoreDatabase(string backupFilePath, RestoreOptions options = null)
        {
            if (String.IsNullOrEmpty(backupFilePath))
                throw new ArgumentNullException(nameof(backupFilePath));

            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Backup file not found", backupFilePath);

            options ??= new RestoreOptions();

            using (AcquireForOperation())
            {
                ReportProgress(options.ProgressCallback, BackupProgressStage.Preparing, null, 0, 0, 0,
                    "Preparing database for restore");

                PrepareForSnapshot(options.ForceCloseTransactions);

                string safetyBackupPath = null;

                if (options.CreateSafetyBackup)
                {
                    ReportProgress(options.ProgressCallback, BackupProgressStage.SafetyBackup, null, 0, 0, 5,
                        "Taking safety backup of live database");

                    BackupResult safetyBackup = BackupDatabase(new BackupOptions
                    {
                        Path = System.IO.Path.Combine(Path, DefaultBackupSubFolder),
                        FileName = $"pre-restore_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}{DefaultBackupExtension}",
                    });

                    safetyBackupPath = safetyBackup.BackupFilePath;
                }

                List<string> warnings = [];
                HashSet<string> restoredRelativePaths = new(StringComparer.OrdinalIgnoreCase);

                List<IMaintainableTable> maintainableTables = [.. _tables.Values.OfType<IMaintainableTable>()];

                ReportProgress(options.ProgressCallback, BackupProgressStage.ClosingTables, null, 0, maintainableTables.Count, 15,
                    "Closing table files for maintenance");

                for (int i = 0; i < maintainableTables.Count; i++)
                {
                    maintainableTables[i].CloseForMaintenance();

                    ReportProgress(options.ProgressCallback, BackupProgressStage.ClosingTables,
                        (maintainableTables[i] as ISimpleDBTable)?.TableName, i + 1, maintainableTables.Count, 15,
                        $"Closed table {(maintainableTables[i] as ISimpleDBTable)?.TableName}");
                }

                try
                {
                    using (FileStream fileStream = File.OpenRead(backupFilePath))
                    using (BrotliStream brotli = new(fileStream, CompressionMode.Decompress))
                    using (BinaryReader reader = new(brotli, Encoding.UTF8))
                    {
                        string magic = reader.ReadString();

                        if (!String.Equals(magic, BackupMagic, StringComparison.Ordinal))
                            throw new InvalidDataException("File is not a recognised SimpleDB backup.");

                        _ = reader.ReadInt64(); // timestamp, informational only
                        _ = reader.ReadInt64(); // transaction sequence at time of backup, informational only
                        int entryCount = reader.ReadInt32();

                        for (int i = 0; i < entryCount; i++)
                        {
                            string relativePath = reader.ReadString();
                            long originalLength = reader.ReadInt64();
                            byte[] checksum = reader.ReadBytes(32);
                            byte[] data = reader.ReadBytes((int)originalLength);

                            if (!checksum.AsSpan().SequenceEqual(SHA256.HashData(data)))
                            {
                                warnings.Add($"Checksum mismatch for '{relativePath}', file was still restored.");
                            }

                            string targetPath = System.IO.Path.Combine(Path, relativePath);
                            string targetDirectory = System.IO.Path.GetDirectoryName(targetPath);

                            if (!String.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                                Directory.CreateDirectory(targetDirectory);

                            File.WriteAllBytes(targetPath, data);
                            restoredRelativePaths.Add(relativePath);

                            int percent = 20 + (entryCount == 0 ? 0 : (i + 1) * 70 / entryCount);
                            ReportProgress(options.ProgressCallback, BackupProgressStage.RestoringFile, relativePath,
                                i + 1, entryCount, percent, $"Restored {relativePath}");
                        }
                    }
                }
                finally
                {
                    ReportProgress(options.ProgressCallback, BackupProgressStage.ReopeningTables, null, 0, maintainableTables.Count, 90,
                        "Reopening table files");

                    for (int i = 0; i < maintainableTables.Count; i++)
                    {
                        maintainableTables[i].ReopenAfterMaintenance();

                        ReportProgress(options.ProgressCallback, BackupProgressStage.ReopeningTables,
                            (maintainableTables[i] as ISimpleDBTable)?.TableName, i + 1, maintainableTables.Count, 90,
                            $"Reopened table {(maintainableTables[i] as ISimpleDBTable)?.TableName}");
                    }
                }

                ReportProgress(options.ProgressCallback, BackupProgressStage.Finalizing, null, 0, _tables.Count, 95,
                    "Reconciling tables and resetting transaction sequence");

                foreach (string tableName in _tables.Keys)
                {
                    bool found = restoredRelativePaths.Any(p =>
                        p.EndsWith(tableName, StringComparison.OrdinalIgnoreCase) ||
                        p.EndsWith(tableName + Consts.DefaultExtension, StringComparison.OrdinalIgnoreCase));

                    if (!found)
                        warnings.Add($"Table '{tableName}' is currently registered but was not present in the backup.");
                }

                TransactionManager?.ResetTransactionSequence();

                //ClearMemory();

                ReportProgress(options.ProgressCallback, BackupProgressStage.Completed, null,
                    restoredRelativePaths.Count, restoredRelativePaths.Count, 100, "Restore complete");

                return new RestoreResult
                {
                    FileCount = restoredRelativePaths.Count,
                    SafetyBackupFilePath = safetyBackupPath,
                    Warnings = warnings,
                };
            }
        }

        /// <summary>
        /// Ensures the database is quiescent before a backup or restore snapshot is taken: no
        /// transaction is in flight and the WAL is empty, its contents already reflected in every
        /// table file.
        /// </summary>
        private void PrepareForSnapshot(bool forceCloseTransactions)
        {
            if (TransactionManager == null)
                return;

            if (forceCloseTransactions)
            {
                TransactionManager.ForceCloseActiveTransactions();
            }
            else if (TransactionManager.ActiveTransactionCount > 0)
            {
                throw new InvalidOperationException(
                    "Cannot backup or restore while transactions are active. Commit or roll back " +
                    "open transactions first, or set ForceCloseTransactions to true.");
            }

            TransactionManager.CheckpointDatabase();
        }

        private void WriteBackupFile(string backupFilePath, List<string> files,
            System.Action<BackupProgressEventArgs> progressCallback, int percentFrom, int percentTo)
        {
            using FileStream fileStream = File.Create(backupFilePath);
            using BrotliStream brotli = new(fileStream, CompressionLevel.Optimal);
            using BinaryWriter writer = new(brotli, Encoding.UTF8);

            writer.Write(BackupMagic);
            writer.Write(DateTime.UtcNow.Ticks);
            writer.Write(TransactionManager?.NextTransactionId ?? 0L);
            writer.Write(files.Count);

            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                string relativePath = System.IO.Path.GetRelativePath(Path, file);
                byte[] data = ReadFileAllowingConcurrentWriter(file);

                writer.Write(relativePath);
                writer.Write((long)data.Length);
                writer.Write(SHA256.HashData(data));
                writer.Write(data);

                int percent = files.Count == 0
                    ? percentTo
                    : percentFrom + (i + 1) * (percentTo - percentFrom) / files.Count;

                ReportProgress(progressCallback, BackupProgressStage.PackingFile, relativePath, i + 1, files.Count,
                    percent, $"Packed {relativePath}");
            }
        }

        /// <summary>
        /// Invokes a backup/restore progress callback, swallowing any exception the caller's
        /// handler throws so a misbehaving UI callback can never abort the operation itself.
        /// </summary>
        private static void ReportProgress(System.Action<BackupProgressEventArgs> progressCallback,
            BackupProgressStage stage, string currentItem, int itemsProcessed, int totalItems,
            int? percentComplete, string message)
        {
            if (progressCallback == null)
                return;

            try
            {
                progressCallback(new BackupProgressEventArgs
                {
                    Stage = stage,
                    CurrentItem = currentItem,
                    ItemsProcessed = itemsProcessed,
                    TotalItems = totalItems,
                    PercentComplete = percentComplete,
                    Message = message,
                });
            }
            catch
            {
                // Progress reporting must never break the backup/restore operation itself.
            }
        }

        /// <summary>
        /// Reads a table file's full contents while the live session may still hold it open.
        /// </summary>
        /// <remarks>
        /// Table files are opened with <c>FileAccess.ReadWrite, FileShare.Read</c>. Windows file
        /// sharing checks are symmetric: it is not enough for this read to request only
        /// <see cref="FileShare.Read"/> - since the existing handle already holds Write access,
        /// this handle must also request <see cref="FileShare.Write"/> (i.e. ReadWrite share), or
        /// the open fails with a sharing violation even though this handle itself only reads.
        /// <see cref="File.ReadAllBytes(string)"/> defaults to <see cref="FileShare.Read"/> and
        /// so cannot be used here.
        /// </remarks>
        private static byte[] ReadFileAllowingConcurrentWriter(string path)
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using MemoryStream buffer = new();

            stream.CopyTo(buffer);

            return buffer.ToArray();
        }

        #endregion Backup / Restore

        #region Private Methods

        private void Initialize(SimpleDBSettings settings)
        {
            if (!Directory.Exists(settings.Path))
                throw new DirectoryNotFoundException($"Path does not exist: {settings.Path}");

            SimpleDBConfiguration.Configure(settings);

            Path = settings.Path;
            EncryptionKey = settings.EnycryptionKey;
        }

        private void SimpleDBTable_OnAction(ISimpleDBTable sender)
        {
            using (TimedLock timedLock = TimedLock.Lock(_lock))
            {
                if (!_tableLastAction.ContainsKey(sender))
                    throw new InvalidOperationException($"Table {sender.TableName} failed to register sliding memory");

                _tableLastAction[sender] = DateTime.UtcNow;
            }
        }

        #endregion Private Methods
    }
}

#pragma warning restore CA2208