namespace SimpleDB
{
    /// <summary>
    /// Identifies the current stage of a <see cref="ISimpleDBManager.BackupDatabase(BackupOptions)"/>
    /// or <see cref="ISimpleDBManager.RestoreDatabase(string, RestoreOptions)"/> operation, reported
    /// via <see cref="BackupOptions.ProgressCallback"/> / <see cref="RestoreOptions.ProgressCallback"/>
    /// so a UI or console can display meaningful feedback.
    /// </summary>
    public enum BackupProgressStage
    {
        /// <summary>
        /// The database is being made quiescent: active transactions are being closed (or
        /// checked) and the WAL is being checkpointed to the table files.
        /// </summary>
        Preparing,

        /// <summary>
        /// A safety backup of the live database is being taken before a restore overwrites it.
        /// </summary>
        SafetyBackup,

        /// <summary>
        /// An individual file is being read from disk and packed into the backup archive.
        /// </summary>
        PackingFile,

        /// <summary>
        /// The packed archive is being compressed and written to disk.
        /// </summary>
        WritingArchive,

        /// <summary>
        /// Table file handles are being released so the underlying file can be overwritten.
        /// </summary>
        ClosingTables,

        /// <summary>
        /// An individual file is being extracted from the backup archive and written to disk.
        /// </summary>
        RestoringFile,

        /// <summary>
        /// Table file handles are being re-acquired and the table state is being reloaded.
        /// </summary>
        ReopeningTables,

        /// <summary>
        /// The restored tables are being reconciled against the currently registered tables and
        /// the transaction sequence is being reset.
        /// </summary>
        Finalizing,

        /// <summary>
        /// The operation has completed.
        /// </summary>
        Completed,
    }
}
