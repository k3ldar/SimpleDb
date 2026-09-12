using System.Diagnostics.CodeAnalysis;

using SimpleDB.Internal;
using SimpleDB.Internal.Tables;
using SimpleDB.Tests.Mocks;

namespace SimpleDB.Tests
{
    [ExcludeFromCodeCoverage]
    public class BaseTest
    {
        internal SimpleDBOperations<T> CreateTable<T>(ISimpleDBManager simpleDbManager, IForeignKeyManager foreignKeymanager, ref IWalTableAccessor walTableAccessor, ref IWatermarkAccessor watermarkAccessor,
            ITransactionManager transactionManager = null) where T : TableRowDefinition
        {
            ArgumentNullException.ThrowIfNull(simpleDbManager, nameof(simpleDbManager));
            foreignKeymanager ??= new ForeignKeyManager();
            watermarkAccessor ??= new MockWatermarkAccessor();

            if (walTableAccessor == null)
            {
                ISimpleDBOperations<WalEntryDataRow> walTable = new SimpleDBOperations<WalEntryDataRow>(
                    simpleDbManager, new ForeignKeyManager(), new MockTransactionManager(),
                    new MockWalTableAccessor(), new MockWatermarkAccessor());

                walTableAccessor = new MockWalTableAccessor(walTable);
            }

            transactionManager ??= new MockTransactionManager();
            return new SimpleDBOperations<T>(simpleDbManager, foreignKeymanager, transactionManager, walTableAccessor, watermarkAccessor);
        }
    }
}
