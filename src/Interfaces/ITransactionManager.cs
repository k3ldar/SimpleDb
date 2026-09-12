namespace SimpleDB
{
    /// <summary>
    /// Interface describing a transaction manager
    /// </summary>
    public interface ITransactionManager : ITransactionMetrics
    {
        /// <summary>
        /// Begins a new transaction
        /// </summary>
        /// <param name="accessMode">The access mode of the transaction</param>
        /// <param name="isolationLevel">The isolation level of the transaction</param>
        /// <returns>The started transaction</returns>
        ITransaction BeginTransaction(TransactionAccessMode accessMode = TransactionAccessMode.ReadWrite,
            TransactionIsolationLevel isolationLevel = TransactionIsolationLevel.DirtyRead);

        /// <summary>
        /// Commits a transaction
        /// </summary>
        /// <param name="transaction">The transaction to commit</param>
        void CommitTransaction(ITransaction transaction);

        /// <summary>
        /// Rolls back a transaction
        /// </summary>
        /// <param name="transaction">The transaction to roll back</param>
        void RollbackTransaction(ITransaction transaction);

        /// <summary>
        /// Forces an immediate database wide checkpoint, flushing every registered table's in
        /// memory record set to disk and truncating the WAL of entries that are now durable
        /// elsewhere. Unlike the automatic checkpoint triggered on commit, this is not gated by
        /// the WAL threshold, but it still refuses to run while a transaction is active.
        /// </summary>
        void CheckpointDatabase();
    }
}
