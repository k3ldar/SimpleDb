namespace SimpleDB
{
    /// <summary>
    /// Options controlling how <see cref="ISimpleDBManager.BackupDatabase(BackupOptions)"/> behaves.
    /// </summary>
    public sealed class BackupOptions
    {
        /// <summary>
        /// Folder that the backup file will be written to. When null, defaults to a "Backups"
        /// sub folder of <see cref="ISimpleDBManager.Path"/>.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Name of the backup file. When null, a timestamped name is generated automatically.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// When true, any active transactions are rolled back before the backup is taken. When
        /// false and a transaction is active, <see cref="ISimpleDBManager.BackupDatabase(BackupOptions)"/>
        /// throws an <see cref="System.InvalidOperationException"/>.
        /// </summary>
        public bool ForceCloseTransactions { get; set; }

        /// <summary>
        /// Optional callback invoked as the backup progresses, so a UI or console can display
        /// which stage/file is currently being processed and an approximate completion
        /// percentage. May be called from the calling thread; implementations should be quick and
        /// must not throw.
        /// </summary>
        public System.Action<BackupProgressEventArgs> ProgressCallback { get; set; }
    }
}
