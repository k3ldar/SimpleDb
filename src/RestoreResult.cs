using System.Collections.Generic;

namespace SimpleDB
{
    /// <summary>
    /// Outcome of a call to <see cref="ISimpleDBManager.RestoreDatabase(string, RestoreOptions)"/>.
    /// </summary>
    public sealed class RestoreResult
    {
        /// <summary>
        /// Number of files restored from the backup.
        /// </summary>
        public int FileCount { get; internal set; }

        /// <summary>
        /// Full path to the safety backup taken before the restore, or null if
        /// <see cref="RestoreOptions.CreateSafetyBackup"/> was false.
        /// </summary>
        public string SafetyBackupFilePath { get; internal set; }

        /// <summary>
        /// Non fatal issues encountered while restoring, e.g. a table present in the backup that
        /// is no longer registered, or a currently registered table that was absent from the
        /// backup.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; internal set; } = [];
    }
}
