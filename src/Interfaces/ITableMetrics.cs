using SharedPluginFeatures;

namespace SimpleDB
{
    /// <summary>
    /// Provides internal metrics for a table, such as caching strategy, write strategy, and sliding memory timeout.
    /// </summary>
    public interface ITableMetrics
    {
        /// <summary>
        /// Name of the table
        /// </summary>
        string TableName { get; }

        /// <summary>
        /// Retrieves the caching strategy for the table
        /// </summary>
        CachingStrategy CachingStrategy { get; }

        /// <summary>
        /// Retrieves the write strategy for the table
        /// </summary>
        WriteStrategy WriteStrategy { get; }

        /// <summary>
        /// Determines the sliding memory timeout for the table
        /// </summary>
        TimeSpan SlidingMemoryTimeout { get; }

        /// <summary>
        /// Retrieves timings for operations within the table
        /// </summary>
        Dictionary<string, Timings> GetAllTimings { get; }

        /// <summary>
        /// Total logical size (uncompressed) of the data held by the table, in bytes
        /// </summary>
        long LogicalDataSizeBytes { get; }

        /// <summary>
        /// Total physical size (on-disk / stored) of the data held by the table, in bytes
        /// </summary>
        long PhysicalDataSizeBytes { get; }

        /// <summary>
        /// Size in bytes of the records currently held in the in-memory cache (if any)
        /// </summary>
        long InMemoryCacheSizeBytes { get; }

        /// <summary>
        /// Total number of records held by the table
        /// </summary>
        int RecordCount { get; }
    }
}
