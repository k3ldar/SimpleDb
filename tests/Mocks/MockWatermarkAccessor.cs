using System.Diagnostics.CodeAnalysis;

using SimpleDB.Internal.Tables;

namespace SimpleDB.Tests.Mocks
{
    [ExcludeFromCodeCoverage]
    internal class MockWatermarkAccessor : IWatermarkAccessor
    {
        private readonly Dictionary<string, long> _watermarks = new();

        public ISimpleDBOperations<CheckpointWatermarkDataRow> WatermarkTable => null;

        public long GetLastFlushedSequence(string tableName)
        {
            return _watermarks.TryGetValue(tableName, out long value) ? value : -1L;
        }

        public void SetLastFlushedSequence(string tableName, long lastFlushedSequence)
        {
            _watermarks[tableName] = lastFlushedSequence;
        }
    }
}
