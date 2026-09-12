namespace SimpleDB
{
    /// <summary>
    /// Provides internal metrics for transactions, such as timings and performance statistics.
    /// </summary>
    public interface ITransactionMetrics : IOperationalTimings
    {
        /// <summary>
        /// Number of user transactions that have been started but not yet committed or rolled
        /// back. Transactions begun internally by a checkpoint are excluded.
        /// </summary>
        int ActiveTransactionCount { get; }

        /// <summary>
        /// Number of checkpoints abandoned because a transaction was in flight when the WAL
        /// crossed the checkpoint threshold.
        /// </summary>
        long CheckpointsSkippedForActiveTransactions { get; }

        /// <summary>
        /// Number of checkpoints that have successfully run to completion.
        /// </summary>
        long CheckpointsCompleted { get; }

        /// <summary>
        /// Gets the next transaction ID that will be assigned to a new transaction. This can be used for monitoring and debugging purposes.
        /// </summary>
        long NextTransactionId { get; }

        /// <summary>
        /// Gets a read-only list of all active transactions, allowing for inspection of their state and properties.
        /// </summary>
        IReadOnlyList<ITransaction> ActiveTransactions { get; }
    }
}
