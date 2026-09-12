using System.Diagnostics.CodeAnalysis;

using SimpleDB.Internal.Tables;

namespace SimpleDB.Tests.Mocks
{
    [ExcludeFromCodeCoverage]
    internal class MockWalTableAccessor : IWalTableAccessor
    {
        private readonly ISimpleDBOperations<WalEntryDataRow> _walTable;

        public MockWalTableAccessor(ISimpleDBOperations<WalEntryDataRow> walTable = null)
        {
            _walTable = walTable;
        }

        public ISimpleDBOperations<WalEntryDataRow> WalTable => _walTable;
    }
}
