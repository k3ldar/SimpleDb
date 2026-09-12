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
 *  File: WalTableAccessorTests.cs
 *
 *  Purpose:  Unit tests for WalTableAccessor
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
    public sealed class WalTableAccessorTests : BaseTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullServiceProvider_ThrowsArgumentNullException()
        {
            try
            {
                _ = new WalTableAccessor(null);
            }
            catch (ArgumentNullException e)
            {
                Assert.AreEqual("serviceProvider", e.ParamName);
                throw;
            }
        }

        [TestMethod]
        public void WalTable_FirstAccess_ResolvesFromServiceProviderLazily()
        {
            MockWalTableAccessor walTableAccessor = new();
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager initializer = new(directory);

                ISimpleDBOperations<WalEntryDataRow> table = new SimpleDBOperations<WalEntryDataRow>(
                    initializer, new ForeignKeyManager(), new MockTransactionManager(), walTableAccessor, new MockWatermarkAccessor());

                int resolveCount = 0;
                ServiceCollection services = new();
                services.AddTransient<ISimpleDBOperations<WalEntryDataRow>>(_ =>
                {
                    resolveCount++;
                    return table;
                });

                using ServiceProvider serviceProvider = services.BuildServiceProvider();

                WalTableAccessor sut = new(serviceProvider);

                Assert.AreEqual(0, resolveCount, "Constructor must not eagerly resolve the wal table.");

                ISimpleDBOperations<WalEntryDataRow> firstAccess = sut.WalTable;
                ISimpleDBOperations<WalEntryDataRow> secondAccess = sut.WalTable;

                Assert.AreEqual(1, resolveCount, "WalTable must only be resolved once, and cached thereafter.");
                Assert.AreSame(table, firstAccess);
                Assert.AreSame(firstAccess, secondAccess);
            }
            finally
            {
                // The transient registration's factory-created instance is tracked and disposed
                // automatically by the ServiceProvider (it implements IDisposable), so the file is
                // already closed by the time this runs - no explicit CloseForMaintenance needed.
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ClassImplements_IWalTableAccessor_ReturnsTrue()
        {
            ServiceCollection services = new();
            using ServiceProvider serviceProvider = services.BuildServiceProvider();

            WalTableAccessor sut = new(serviceProvider);

            Assert.IsInstanceOfType(sut, typeof(IWalTableAccessor));
        }
    }
}
