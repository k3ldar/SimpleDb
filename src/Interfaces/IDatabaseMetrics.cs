namespace SimpleDB
{
    /// <summary>
    /// Provides internal metrics for the database, such as timings and performance statistics.
    /// </summary>
    public interface IDatabaseMetrics
    {
        /// <summary>
        /// Provides internal metrics for transactions, such as timings and performance statistics.
        /// </summary>
        ITransactionMetrics TransactionMetrics { get; }

        /// <summary>
        /// Dictionary of metrics for each table in the database, where the key is the table name and the value is the corresponding table metrics.
        /// </summary>
        Dictionary<string, ITableMetrics> TableMetrics { get; }
    }
}
