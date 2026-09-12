namespace SimpleDB
{
    /// <summary>
    /// Implemented by a transaction that holds the database lock for its lifetime.
    /// </summary>
    /// <remarks>
    /// The lock is acquired in BeginTransaction and released once the transaction has been
    /// committed or rolled back, so it must travel with the transaction object rather than being
    /// tracked separately - a transaction may legitimately be completed on a different code path
    /// from the one that began it.
    /// </remarks>
    internal interface IDatabaseLockScope
    {
        /// <summary>
        /// Releases the database lock held by this transaction. Safe to call more than once.
        /// </summary>
        void ReleaseDatabaseLock();
    }
}
