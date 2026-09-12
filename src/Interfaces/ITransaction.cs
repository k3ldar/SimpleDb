namespace SimpleDB
{
    /// <summary>
    /// Interface describing a transaction
    /// </summary>
    public interface ITransaction
    {
        /// <summary>
        /// Gets the transaction id
        /// </summary>
        long TransactionId { get; }

        /// <summary>
        /// Commits the transaction
        /// </summary>
        void Commit();

        /// <summary>
        /// Rolls back the transaction
        /// </summary>
        void Rollback();

        /// <summary>
        /// Gets the access mode of the transaction
        /// </summary>
        TransactionAccessMode AccessMode { get; }

        /// <summary>
        /// Gets the isolation level of the transaction
        /// </summary>
        TransactionIsolationLevel IsolationLevel { get; }

        /// <summary>
        /// Gets the time when the transaction was started
        /// </summary>
        DateTime Started { get; }
    }
}
