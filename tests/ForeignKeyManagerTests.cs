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
 *  File: ForeignKeyManagerTests.cs
 *
 *  Purpose:  ForeignKeyManagerTests tests for SimpleDB
 *
 *  Date        Name                Reason
 *  02/06/2022  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimpleDb.Tests;

using SimpleDB.Internal;
using SimpleDB.Internal.Tables;
using SimpleDB.Tests.Mocks;

using io = System.IO;

#pragma warning disable CA1859

namespace SimpleDB.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class ForeignKeyManagerTests : BaseTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RegisterTable_InvalidParamTable_Null_Throws_ArgumentNullException()
        {
            ForeignKeyManager sut = new();
            sut.RegisterTable(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void UnregisterTable_InvalidParamTable_Null_Throws_ArgumentNullException()
        {
            ForeignKeyManager sut = new();
            sut.UnregisterTable(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddRelationShip_InvalidParamSourceTable_Null_Throws_ArgumentNullException()
        {
            ForeignKeyManager sut = new();
            sut.AddRelationShip(null, "targetTable", "Id", "Id", ForeignKeyAttributes.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddRelationShip_InvalidParamTargetTable_Null_Throws_ArgumentNullException()
        {
            ForeignKeyManager sut = new();
            sut.AddRelationShip("sourceTable", null, "Id", "Id", ForeignKeyAttributes.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddRelationShip_InvalidParamPropertyName_Null_Throws_ArgumentNullException()
        {
            ForeignKeyManager sut = new();
            sut.AddRelationShip("sourceTable", "targetTable", null, "Id", ForeignKeyAttributes.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddRelationShip_InvalidParamTargetPropertyName_Null_Throws_ArgumentNullException()
        {
            ForeignKeyManager sut = new();
            sut.AddRelationShip("sourceTable", "targetTable", "Id", "", ForeignKeyAttributes.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ForeignKeyException))]
        public void ForeignKey_InsertRecordWhenKeyDoesNotExists_Throws_ForeignKeyException()
        {
            IWalTableAccessor walTableAccessor = null;
            ForeignKeyManager sut = new();
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);

                IWatermarkAccessor watermarkAccessor = null;
                using ISimpleDBOperations<MockTableUserRow> mockUsers = base.CreateTable<MockTableUserRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                simpleDBManager.Initialize(new MockPluginClassesService());
                List<MockTableUserRow> testData = [];

                for (int i = 0; i < 5; i++)
                    testData.Add(new MockTableUserRow(i));

                mockUsers.Insert(testData);

                using ISimpleDBOperations<MockTableAddressRow> mockAddresses = base.CreateTable<MockTableAddressRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                simpleDBManager.Initialize(new MockPluginClassesService());
                mockAddresses.Insert(new MockTableAddressRow(10));
            }
            finally
            {
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ForeignKey_InsertRecordWhenForeignKeyDoesNotExists_DefaultValueAllowed_DoesNotThrowException()
        {
            IWalTableAccessor walTableAccessor = null;
            ForeignKeyManager sut = new();
            Assert.IsNotNull(sut);
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);

                IWatermarkAccessor watermarkAccessor = null;
                using ISimpleDBOperations<MockTableUserRow> mockUsers = base.CreateTable<MockTableUserRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                mockUsers.ResetSequence(10, 10);
                List<MockTableUserRow> testData = [];

                for (int i = 1; i < 6; i++)
                    testData.Add(new MockTableUserRow(i));

                mockUsers.Insert(testData);

                using ISimpleDBOperations<MockTableForeignKeyDefaultAllowed> mockAddresses = base.CreateTable<MockTableForeignKeyDefaultAllowed>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                mockAddresses.Insert(new MockTableForeignKeyDefaultAllowed(0));
            }
            finally
            {
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ForeignKeyException))]
        public void ForeignKey_UpdateRecordWhenKeyDoesNotExists_Throws_ForeignKeyException()
        {
            IWalTableAccessor walTableAccessor = null;
            ForeignKeyManager sut = new();
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);

                IWatermarkAccessor watermarkAccessor = null;
                using ISimpleDBOperations<MockTableUserRow> mockUsers = base.CreateTable<MockTableUserRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                List<MockTableUserRow> testData = [];

                for (int i = 0; i < 5; i++)
                    testData.Add(new MockTableUserRow(i));

                mockUsers.Insert(testData);

                using ISimpleDBOperations<MockTableAddressRow> mockAddresses = base.CreateTable<MockTableAddressRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                mockAddresses.Insert(new MockTableAddressRow(3));

                MockTableAddressRow addressRow = mockAddresses.Select(0);
                Assert.IsNotNull(addressRow);

                addressRow.UserId = 10;
                mockAddresses.Update(addressRow);
            }
            finally
            {
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ForeignKeyException))]
        public void ForeignKey_DeleteForeignKeyWhenForeignKeyIsInUse_Throws_ForeignKeyException()
        {
            IWalTableAccessor walTableAccessor = null;
            ForeignKeyManager sut = new();
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);

                IWatermarkAccessor watermarkAccessor = null;
                using ISimpleDBOperations<MockTableUserRow> mockUsers = base.CreateTable<MockTableUserRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                List<MockTableUserRow> testData = [];

                for (int i = 0; i < 5; i++)
                    testData.Add(new MockTableUserRow(i));

                mockUsers.Insert(testData);

                using ISimpleDBOperations<MockTableAddressRow> mockAddresses = base.CreateTable<MockTableAddressRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                mockAddresses.Insert(new MockTableAddressRow(3));

                MockTableAddressRow addressRow = mockAddresses.Select(0);
                Assert.IsNotNull(addressRow);

                mockUsers.Truncate();
            }
            finally
            {
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ForeignKey_InsertRecordWhenKeyExists_Success()
        {
            IWalTableAccessor walTableAccessor = null;
            ForeignKeyManager sut = new();
            Assert.IsNotNull(sut);
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);

                IWatermarkAccessor watermarkAccessor = null;
                using ISimpleDBOperations<MockTableUserRow> mockUsers = base.CreateTable<MockTableUserRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                List<MockTableUserRow> testData = [];

                for (int i = 0; i < 5; i++)
                    testData.Add(new MockTableUserRow(i));

                mockUsers.Insert(testData);

                using ISimpleDBOperations<MockTableAddressRow> mockAddresses = base.CreateTable<MockTableAddressRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                mockAddresses.Insert(new MockTableAddressRow(3));
            }
            finally
            {
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ForeignKey_UpdateRecordWhenKeyExists_Success()
        {
            IWalTableAccessor walTableAccessor = null;
            ForeignKeyManager sut = new();
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);

                IWatermarkAccessor watermarkAccessor = null;
                using ISimpleDBOperations<MockTableUserRow> mockUsers = base.CreateTable<MockTableUserRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                List<MockTableUserRow> testData = [];

                for (int i = 0; i < 5; i++)
                    testData.Add(new MockTableUserRow(i));

                mockUsers.Insert(testData);

                using ISimpleDBOperations<MockTableAddressRow> mockAddresses = base.CreateTable<MockTableAddressRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                mockAddresses.Insert(new MockTableAddressRow(3));

                MockTableAddressRow addressRow = mockAddresses.Select(0);
                Assert.IsNotNull(addressRow);

                addressRow.UserId = 2;
                mockAddresses.Update(addressRow);
            }
            finally
            {
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ForeignKey_DeleteRecordWhenKeyExists_Success()
        {
            IWalTableAccessor walTableAccessor = null;
            ForeignKeyManager sut = new();
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager simpleDBManager = new SimpleDBManager(directory);

                IWatermarkAccessor watermarkAccessor = null;
                using ISimpleDBOperations<MockTableUserRow> mockUsers = base.CreateTable<MockTableUserRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                List<MockTableUserRow> testData = [];

                for (int i = 0; i < 5; i++)
                    testData.Add(new MockTableUserRow(i));

                mockUsers.Insert(testData);

                using ISimpleDBOperations<MockTableAddressRow> mockAddresses = base.CreateTable<MockTableAddressRow>(simpleDBManager, sut, ref walTableAccessor, ref watermarkAccessor);
                mockAddresses.Insert(new MockTableAddressRow(4));
                mockAddresses.Truncate();

                Assert.AreEqual(0, mockAddresses.RecordCount);
            }
            finally
            {
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                io.Directory.Delete(directory, true);
            }
        }
    }
}

#pragma warning restore CA1859