namespace SimpleDB
{
    /// <summary>
    /// Outcome of a call to <see cref="ISimpleDBManager.BackupDatabase(BackupOptions)"/>.
    /// </summary>
    public sealed class BackupResult
    {
        /// <summary>
        /// Full path to the backup file that was created.
        /// </summary>
        public string BackupFilePath { get; internal set; }

        /// <summary>
        /// Number of files packed into the backup.
        /// </summary>
        public int FileCount { get; internal set; }

        /// <summary>
        /// Size, in bytes, of the resulting backup file on disk.
        /// </summary>
        public long BackupSizeBytes { get; internal set; }
    }
}
