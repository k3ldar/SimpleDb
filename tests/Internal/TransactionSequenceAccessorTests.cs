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
 *  Copyright (c) 2018 - 2023 Simon Carter.  All Rights Reserved.
 *
 *  Product:  SimpleDB.Tests
 *  
 *  File: TransactionSequenceAccessorTests.cs
 *
 *  Purpose:  TransactionSequenceAccessor tests for SimpleDB
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
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
    public class TransactionSequenceAccessorTests : BaseTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullServiceProvider_Throws_ArgumentNullException()
        {
            try
            {
                new TransactionSequenceAccessor(null);
            }
            catch (ArgumentNullException e)
            {
                Assert.AreEqual("serviceProvider", e.ParamName);
                throw;
            }
        }

        [TestMethod]
        public void SequenceTable_FirstAccess_ResolvesFromServiceProviderLazily()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager initializer = CreateTestInitializer(directory);
                IWatermarkAccessor watermarkAccessor = null;

                using ISimpleDBOperations<TransactionSequenceDataRow> table =
                    CreateTable<TransactionSequenceDataRow>(initializer, null, ref walTableAccessor, ref watermarkAccessor);

                int resolveCount = 0;
                ServiceCollection services = new();
                services.AddTransient<ISimpleDBOperations<TransactionSequenceDataRow>>(_ =>
                {
                    resolveCount++;
                    return table;
                });

                using ServiceProvider serviceProvider = services.BuildServiceProvider();

                TransactionSequenceAccessor sut = new(serviceProvider);

                Assert.AreEqual(0, resolveCount, "Constructor must not eagerly resolve the sequence table.");

                ISimpleDBOperations<TransactionSequenceDataRow> firstAccess = sut.SequenceTable;
                ISimpleDBOperations<TransactionSequenceDataRow> secondAccess = sut.SequenceTable;

                Assert.AreEqual(1, resolveCount, "SequenceTable must only be resolved once, and cached thereafter.");
                Assert.AreSame(table, firstAccess);
                Assert.AreSame(firstAccess, secondAccess);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void CurrentValue_NewSequence_ReturnsZeroWithoutIncrementing()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<TransactionSequenceDataRow> table = CreateSut(directory, out walTableAccessor, out TransactionSequenceAccessor sut);

                Assert.AreEqual(-1L, sut.CurrentValue());
                Assert.AreEqual(-1L, sut.CurrentValue());
                Assert.AreEqual(-1L, sut.SequenceTable.PrimarySequence);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void NextValue_WhenCalledRepeatedly_IncrementsSequenceEachTime()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<TransactionSequenceDataRow> table = CreateSut(directory, out walTableAccessor, out TransactionSequenceAccessor sut);

                Assert.AreEqual(0L, sut.NextValue());
                Assert.AreEqual(1L, sut.NextValue());
                Assert.AreEqual(2L, sut.NextValue());
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void CurrentValue_AfterNextValue_ReflectsIncrementedValueWithoutFurtherIncrementing()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<TransactionSequenceDataRow> table = CreateSut(directory, out walTableAccessor, out TransactionSequenceAccessor sut);

                Assert.AreEqual(0L, sut.NextValue());

                Assert.AreEqual(0L, sut.CurrentValue());
                Assert.AreEqual(0L, sut.CurrentValue());
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void Reset_AfterAdvancingSequence_ResetsBackToZero()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                using ISimpleDBOperations<TransactionSequenceDataRow> table = CreateSut(directory, out walTableAccessor, out TransactionSequenceAccessor sut);

                sut.NextValue();
                sut.NextValue();
                sut.NextValue();

                sut.Reset();

                Assert.AreEqual(0L, sut.CurrentValue());
                Assert.AreEqual(1L, sut.NextValue());
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        private ISimpleDBOperations<TransactionSequenceDataRow> CreateSut(string directory, out IWalTableAccessor walTableAccessor,
            out TransactionSequenceAccessor sut)
        {
            Directory.CreateDirectory(directory);
            SimpleDBManager initializer = CreateTestInitializer(directory);
            IWatermarkAccessor watermarkAccessor = null;
            walTableAccessor = null;

            ISimpleDBOperations<TransactionSequenceDataRow> table =
                CreateTable<TransactionSequenceDataRow>(initializer, null, ref walTableAccessor, ref watermarkAccessor);

            ServiceCollection services = new();
            services.AddSingleton(table);
            ServiceProvider serviceProvider = services.BuildServiceProvider();

            sut = new TransactionSequenceAccessor(serviceProvider);
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
