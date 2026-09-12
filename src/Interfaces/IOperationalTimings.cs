using SharedPluginFeatures;

namespace SimpleDB
{
    /// <summary>
    /// Provides internal timings for various operations, allowing for performance monitoring and analysis.
    /// </summary>
    public interface IOperationalTimings
    {
        /// <summary>
        /// Gets a dictionary of all timings for various operations, where the key is the operation name and the value is the corresponding timing information.
        /// </summary>
        Dictionary<string, Timings> GetAllTimings { get; }
    }
}
