namespace SimpleDB
{
    /// <summary>
    /// Options controlling how <see cref="ISimpleDBManager.RestoreDatabase(string, RestoreOptions)"/> behaves.
    /// </summary>
    public sealed class RestoreOptions
    {
        /// <summary>
        /// When true, any active transactions are rolled back before the restore begins. When
        /// false and a transaction is active, <see cref="ISimpleDBManager.RestoreDatabase(string, RestoreOptions)"/>
        /// throws an <see cref="System.InvalidOperationException"/>.
        /// </summary>
        public bool ForceCloseTransactions { get; set; }

        /// <summary>
        /// When true (the default), a safety backup of the current, live database is taken before
        /// any file is overwritten, so a failed or unwanted restore can be undone.
        /// </summary>
        public bool CreateSafetyBackup { get; set; } = true;

        /// <summary>
        /// Optional callback invoked as the restore progresses, so a UI or console can display
        /// which stage/file is currently being processed and an approximate completion
        /// percentage. May be called from the calling thread; implementations should be quick and
        /// must not throw.
        /// </summary>
        public System.Action<BackupProgressEventArgs> ProgressCallback { get; set; }
    }
}
