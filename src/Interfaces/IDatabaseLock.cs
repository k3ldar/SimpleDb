namespace SimpleDB
{
    /// <summary>
    /// Database wide mutual exclusion, implemented by the object that owns the table registry.
    /// </summary>
    /// <remarks>
    /// This is deliberately a single reentrant lock rather than a reader/writer or counter based
    /// gate. Two earlier attempts using an "active operations" counter deadlocked, because
    /// committing a transaction re-enters the database to write the WAL and watermark system
    /// tables. A counter cannot distinguish that nested re-entry from a second thread, so an
    /// exclusive waiter blocked waiting for a count that could never drop to zero.
    ///
    /// Monitor is reentrant per thread, so nested re-entry is free and correct. The tables are
    /// fully in memory, so transactions are short and serialising them is inexpensive.
    /// </remarks>
    internal interface IDatabaseLock
    {
        /// <summary>
        /// Acquires the database lock for ordinary work, waiting until it is available.
        /// </summary>
        /// <remarks>
        /// Uses a generous timeout. A checkpoint is the only long held section and it is bounded
        /// by flushing already in memory tables to disk, so a timeout here indicates a genuine
        /// defect rather than ordinary contention and is allowed to throw.
        /// </remarks>
        /// <exception cref="DatabaseLockTimeoutException">Thrown if the lock could not be acquired
        /// within the permitted time.</exception>
        IDisposable AcquireForOperation();

        /// <summary>
        /// Attempts to acquire the database lock for checkpointing, returning null if it is not
        /// available promptly.
        /// </summary>
        /// <remarks>
        /// A checkpoint is never urgent - it is a background optimisation that keeps the WAL from
        /// growing without bound. If the lock is busy the checkpoint is abandoned and reattempted
        /// on the next commit that still finds the WAL over threshold, so it must never throw or
        /// stall a caller doing real work.
        /// </remarks>
        IDisposable TryAcquireForCheckpoint();
    }
}
