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
 *  File: DatabaseMetricsTests.cs
 *
 *  Purpose:  DatabaseMetrics tests for SimpleDB
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimpleDb.Tests;

using SimpleDB.Internal;
using SimpleDB.Tests.Mocks;

namespace SimpleDB.Tests.Internal
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class DatabaseMetricsTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullSimpleDBManager_Throws_ArgumentNullException()
        {
            try
            {
                new DatabaseMetrics(null, new MockTransactionManager());
            }
            catch (ArgumentNullException e)
            {
                Assert.AreEqual("simpleDBManager", e.ParamName);
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullTransactionManager_Throws_ArgumentNullException()
        {
            string directory = TestHelper.GetTestPath();

            try
            {
                Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);

                try
                {
                    new DatabaseMetrics(simpleDBManager, null);
                }
                catch (ArgumentNullException e)
                {
                    Assert.AreEqual("transactionManager", e.ParamName);
                    throw;
                }
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void TransactionMetrics_WhenCalled_ReturnsTransactionManagerInstanceAsMetrics()
        {
            string directory = TestHelper.GetTestPath();

            try
            {
                Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);
                MockTransactionManager transactionManager = new();
                DatabaseMetrics sut = new(simpleDBManager, transactionManager);

                ITransactionMetrics result = sut.TransactionMetrics;

                Assert.IsNotNull(result);
                Assert.AreSame(transactionManager, result);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void TableMetrics_NoTablesRegistered_ReturnsEmptyDictionary()
        {
            string directory = TestHelper.GetTestPath();

            try
            {
                Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);
                DatabaseMetrics sut = new(simpleDBManager, new MockTransactionManager());

                Dictionary<string, ITableMetrics> result = sut.TableMetrics;

                Assert.IsNotNull(result);
                Assert.AreEqual(0, result.Count);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void TableMetrics_SingleTableRegistered_ReturnsDictionaryContainingTable()
        {
            string directory = TestHelper.GetTestPath();

            try
            {
                Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);
                MockTextTable table = new("TestTable", true);
                simpleDBManager.RegisterTable(table);

                DatabaseMetrics sut = new(simpleDBManager, new MockTransactionManager());

                Dictionary<string, ITableMetrics> result = sut.TableMetrics;

                Assert.IsNotNull(result);
                Assert.AreEqual(1, result.Count);
                Assert.IsTrue(result.ContainsKey("TestTable"));
                Assert.AreSame(table, result["TestTable"]);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void TableMetrics_MultipleTablesRegistered_ReturnsDictionaryContainingAllTables()
        {
            string directory = TestHelper.GetTestPath();

            try
            {
                Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);
                MockTextTable tableA = new("TableA", true);
                MockTextTable tableB = new("TableB", false);
                simpleDBManager.RegisterTable(tableA);
                simpleDBManager.RegisterTable(tableB);

                DatabaseMetrics sut = new(simpleDBManager, new MockTransactionManager());

                Dictionary<string, ITableMetrics> result = sut.TableMetrics;

                Assert.AreEqual(2, result.Count);
                Assert.AreSame(tableA, result["TableA"]);
                Assert.AreSame(tableB, result["TableB"]);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void TableMetrics_CalledMultipleTimes_ReturnsNewDictionaryInstanceEachTime()
        {
            string directory = TestHelper.GetTestPath();

            try
            {
                Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);
                MockTextTable table = new("TestTable", true);
                simpleDBManager.RegisterTable(table);

                DatabaseMetrics sut = new(simpleDBManager, new MockTransactionManager());

                Dictionary<string, ITableMetrics> first = sut.TableMetrics;
                Dictionary<string, ITableMetrics> second = sut.TableMetrics;

                Assert.AreNotSame(first, second);
                Assert.AreEqual(first.Count, second.Count);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void TableMetrics_TableUnregisteredAfterRetrieval_ReflectsUpdatedTables()
        {
            string directory = TestHelper.GetTestPath();

            try
            {
                Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);
                MockTextTable table = new("TestTable", true);
                simpleDBManager.RegisterTable(table);

                DatabaseMetrics sut = new(simpleDBManager, new MockTransactionManager());

                Assert.AreEqual(1, sut.TableMetrics.Count);

                simpleDBManager.UnregisterTable(table);

                Assert.AreEqual(0, sut.TableMetrics.Count);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
