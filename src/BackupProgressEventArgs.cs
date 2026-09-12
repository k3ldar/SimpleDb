namespace SimpleDB
{
    /// <summary>
    /// Progress information reported during a backup or restore operation via
    /// <see cref="BackupOptions.ProgressCallback"/> / <see cref="RestoreOptions.ProgressCallback"/>.
    /// </summary>
    public sealed class BackupProgressEventArgs
    {
        /// <summary>
        /// The stage the operation is currently in.
        /// </summary>
        public BackupProgressStage Stage { get; init; }

        /// <summary>
        /// Name of the table or file currently being processed, when applicable to
        /// <see cref="Stage"/>; otherwise null.
        /// </summary>
        public string CurrentItem { get; init; }

        /// <summary>
        /// Number of items (files/tables) processed so far within <see cref="Stage"/>.
        /// </summary>
        public int ItemsProcessed { get; init; }

        /// <summary>
        /// Total number of items (files/tables) expected within <see cref="Stage"/>, when known;
        /// otherwise 0.
        /// </summary>
        public int TotalItems { get; init; }

        /// <summary>
        /// Approximate overall completion percentage (0-100) for the whole backup/restore
        /// operation, when it can be estimated; otherwise null.
        /// </summary>
        public int? PercentComplete { get; init; }

        /// <summary>
        /// Short human readable description of what is currently happening, suitable for direct
        /// display to a user.
        /// </summary>
        public string Message { get; init; }
    }
}
