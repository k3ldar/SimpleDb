/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 *  .Net Core Plugin Manager is distributed under the GNU General Public License version 3 and  
 *  is also available under alternative licenses negotiated directly with Simon Carter.  
 *  If you obtained Service Manager under the GPL, then the GPL applies to all loadable 
 *  Service Manager modules used on your system as well. The GPL (version 3) is 
 *  available at https://opensource.org/licenses/GPL-3.0
 *
 *  This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY,
 *  without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 *  See the GNU General Public License for more details.
 *
 *  The Original Code was created by Simon Carter (s1cart3r@gmail.com)
 *
 *  Copyright (c) 2018 - 2024 Simon Carter.  All Rights Reserved.
 *
 *  Product:  SimpleDB.Tests
 *  
 *  File: WatermarkAccessorTests.cs
 *
 *  Purpose:  Unit tests for WatermarkAccessor
 *
 *  Date        Name                Reason
 *  12/12/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimpleDb.Tests;

using SimpleDB.Internal;
using SimpleDB.Internal.Tables;
using SimpleDB.Tests.Mocks;

namespace SimpleDB.Tests.Internal
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public sealed class WatermarkAccessorTests : BaseTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullServiceProvider_ThrowsArgumentNullException()
        {
            try
            {
                _ = new WatermarkAccessor(null);
            }
            catch (ArgumentNullException e)
            {
                Assert.AreEqual("serviceProvider", e.ParamName);
                throw;
            }
        }

        [TestMethod]
        public void WatermarkTable_FirstAccess_ResolvesFromServiceProviderLazily()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager initializer = CreateTestInitializer(directory);
                IWatermarkAccessor watermarkAccessor = null;

                using ISimpleDBOperations<CheckpointWatermarkDataRow> table =
                    CreateTable<CheckpointWatermarkDataRow>(initializer, null, ref walTableAccessor, ref watermarkAccessor);

                int resolveCount = 0;
                ServiceCollection services = new();
                services.AddTransient<ISimpleDBOperations<CheckpointWatermarkDataRow>>(_ =>
                {
                    resolveCount++;
                    return table;
                });

                using ServiceProvider serviceProvider = services.BuildServiceProvider();

                WatermarkAccessor sut = new(serviceProvider);

                Assert.AreEqual(0, resolveCount, "Constructor must not eagerly resolve the watermark table.");

                ISimpleDBOperations<CheckpointWatermarkDataRow> firstAccess = sut.WatermarkTable;
                ISimpleDBOperations<CheckpointWatermarkDataRow> secondAccess = sut.WatermarkTable;

                Assert.AreEqual(1, resolveCount, "WatermarkTable must only be resolved once, and cached thereafter.");
                Assert.AreSame(table, firstAccess);
                Assert.AreSame(firstAccess, secondAccess);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetLastFlushedSequence_NullTableName_ThrowsArgumentNullException()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<CheckpointWatermarkDataRow> table = CreateSut(directory, out walTableAccessor, out WatermarkAccessor sut);

                sut.GetLastFlushedSequence(null);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetLastFlushedSequence_EmptyTableName_ThrowsArgumentNullException()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<CheckpointWatermarkDataRow> table = CreateSut(directory, out walTableAccessor, out WatermarkAccessor sut);

                sut.GetLastFlushedSequence(String.Empty);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetLastFlushedSequence_NullTableName_ThrowsArgumentNullException()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<CheckpointWatermarkDataRow> table = CreateSut(directory, out walTableAccessor, out WatermarkAccessor sut);

                sut.SetLastFlushedSequence(null, 1L);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetLastFlushedSequence_EmptyTableName_ThrowsArgumentNullException()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<CheckpointWatermarkDataRow> table = CreateSut(directory, out walTableAccessor, out WatermarkAccessor sut);

                sut.SetLastFlushedSequence(String.Empty, 1L);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void GetLastFlushedSequence_TableNotFound_ReturnsMinusOne()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<CheckpointWatermarkDataRow> table = CreateSut(directory, out walTableAccessor, out WatermarkAccessor sut);

                Assert.AreEqual(-1L, sut.GetLastFlushedSequence("DoesNotExist"));
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void SetLastFlushedSequence_NewTable_InsertsRowAndCanBeReadBack()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<CheckpointWatermarkDataRow> table = CreateSut(directory, out walTableAccessor, out WatermarkAccessor sut);

                sut.SetLastFlushedSequence("MyTable", 5L);

                Assert.AreEqual(5L, sut.GetLastFlushedSequence("MyTable"));
                Assert.AreEqual(1, table.Select().Count);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void SetLastFlushedSequence_ExistingTable_UpdatesRowInPlace()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<CheckpointWatermarkDataRow> table = CreateSut(directory, out walTableAccessor, out WatermarkAccessor sut);

                sut.SetLastFlushedSequence("MyTable", 5L);
                sut.SetLastFlushedSequence("MyTable", 10L);

                Assert.AreEqual(10L, sut.GetLastFlushedSequence("MyTable"));
                Assert.AreEqual(1, table.Select().Count);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void SetLastFlushedSequence_MultipleTables_TracksEachIndependently()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<CheckpointWatermarkDataRow> table = CreateSut(directory, out walTableAccessor, out WatermarkAccessor sut);

                sut.SetLastFlushedSequence("TableA", 1L);
                sut.SetLastFlushedSequence("TableB", 2L);

                Assert.AreEqual(1L, sut.GetLastFlushedSequence("TableA"));
                Assert.AreEqual(2L, sut.GetLastFlushedSequence("TableB"));
                Assert.AreEqual(2, table.Select().Count);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        private ISimpleDBOperations<CheckpointWatermarkDataRow> CreateSut(string directory, out IWalTableAccessor walTableAccessor, out WatermarkAccessor sut)
        {
            Directory.CreateDirectory(directory);
            SimpleDBManager initializer = CreateTestInitializer(directory);
            walTableAccessor = null;
            IWatermarkAccessor watermarkAccessor = null;

            ISimpleDBOperations<CheckpointWatermarkDataRow> table =
                CreateTable<CheckpointWatermarkDataRow>(initializer, null, ref walTableAccessor, ref watermarkAccessor);

            ServiceCollection services = new();
            services.AddTransient(_ => table);

            ServiceProvider serviceProvider = services.BuildServiceProvider();

            sut = new WatermarkAccessor(serviceProvider);

            return table;
        }

        private static SimpleDBManager CreateTestInitializer(string path)
        {
            return new SimpleDBManager(path);
        }

        private static void FinaliseTest(IWalTableAccessor walTableAccessor, string directory)
        {
            SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
            baseWal?.CloseForMaintenance();
            Directory.Delete(directory, true);
        }
    }
}
