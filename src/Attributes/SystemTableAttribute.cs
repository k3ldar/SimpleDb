namespace SimpleDB.Attributes
{
    /// <summary>
    /// Marks a table row class as an internal system table (e.g. the WAL <c>_walentries</c> table),
    /// excluding it from user-facing table enumeration/registration and optionally selecting an
    /// alternate <see cref="StorageEngine"/> for its underlying reader/writer.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class SystemTableAttribute : Attribute
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="storageEngine">Storage engine to use for this system table, defaults to <see cref="StorageEngine.Paged"/>.</param>
        /// <param name="participatesInWal">
        /// Indicates whether DML performed against this system table should generate WAL entries.
        /// Defaults to <c>false</c>, as most system tables (e.g. the WAL table itself) must not log
        /// against themselves to avoid recursion. Set to <c>true</c> for future system tables (e.g.
        /// an internal audit/config table) that require crash-recovery coverage via the WAL.
        /// </param>
        public SystemTableAttribute(StorageEngine storageEngine = StorageEngine.Paged, bool participatesInWal = false)
        {
            StorageEngine = storageEngine;
            ParticipatesInWal = participatesInWal;
        }

        /// <summary>
        /// Gets the storage engine used to persist this system table's records.
        /// </summary>
        public StorageEngine StorageEngine { get; }

        /// <summary>
        /// Gets whether this system table's DML operations should be recorded in the WAL.
        /// </summary>
        public bool ParticipatesInWal { get; }
    }
}
