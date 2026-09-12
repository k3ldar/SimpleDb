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
 *  File: SimpleDBOperationsWalAndMaintenanceTests.cs
 *
 *  Purpose:  Coverage for SimpleDBOperations WAL undo, checkpoint watermark and
 *            maintenance/compaction related members.
 *
 *  Date        Name                Reason
 *  12/12/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimpleDb.Tests;

using SimpleDB.Internal;
using SimpleDB.Internal.Tables;
using SimpleDB.Tests.Mocks;

using io = System.IO;

#pragma warning disable CA1806, CA1859

namespace SimpleDB.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public sealed class SimpleDBOperationsWalAndMaintenanceTests : BaseTest
    {
        private static SimpleDBManager CreateTestInitializer(string path) => new(path);

        private static void FinaliseTest(IWalTableAccessor walTableAccessor, string directory)
        {
            SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
            baseWal?.CloseForMaintenance();
            io.Directory.Delete(directory, true);
        }

        [TestMethod]
        public void ParticipatesInWal_OrdinaryTable_ReturnsTrue()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                Assert.IsTrue(sut.ParticipatesInWal);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void AdvanceCheckpointWatermark_WhenCalled_UpdatesWatermarkAccessor()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = new MockWatermarkAccessor();
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                sut.AdvanceCheckpointWatermark(42);

                Assert.AreEqual(42, watermarkAccessor.GetLastFlushedSequence(sut.TableName));
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void UndoInsert_RecordExists_RemovesRecord()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                sut.Insert(new MockRow());
                sut.Insert(new MockRow());
                Assert.AreEqual(2, sut.RecordCount);

                sut.UndoInsert(1);

                Assert.AreEqual(1, sut.RecordCount);
                Assert.IsFalse(sut.IdExists(1));
                Assert.IsTrue(sut.IdExists(0));
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void UndoInsert_RecordDoesNotExist_DoesNotThrow()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                sut.Insert(new MockRow());

                sut.UndoInsert(999);

                Assert.AreEqual(1, sut.RecordCount);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void UndoUpdate_RecordExists_RestoresOriginalRecord()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                MockRow original = new MockRow(0)
                {
                    CreatedTicks = 12345L
                };
                sut.Insert(original);

                byte[] undoImage = JsonSerializer.SerializeToUtf8Bytes(original, Consts.JsonSerializerOptions);

                MockRow updated = new MockRow(0)
                {
                    CreatedTicks = 98765L
                };
                sut.Update(updated);

                sut.UndoUpdate(0, undoImage);

                MockRow restored = sut.Select(0);
                Assert.IsNotNull(restored);
                Assert.AreEqual(12345L, restored.CreatedTicks);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void UndoUpdate_NullUndoRecord_Throws_ArgumentNullException()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                sut.Insert(new MockRow());

                sut.UndoUpdate(0, null);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void UndoDelete_RecordDoesNotExist_RestoresDeletedRecord()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                MockRow record = new MockRow(0);
                sut.Insert(record);

                byte[] undoImage = JsonSerializer.SerializeToUtf8Bytes(record, Consts.JsonSerializerOptions);

                sut.Delete(record);
                Assert.AreEqual(0, sut.RecordCount);

                sut.UndoDelete(undoImage);

                Assert.AreEqual(1, sut.RecordCount);
                Assert.IsTrue(sut.IdExists(0));
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void UndoDelete_RecordAlreadyExists_DoesNotDuplicate()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                MockRow record = new MockRow(0);
                sut.Insert(record);

                byte[] undoImage = JsonSerializer.SerializeToUtf8Bytes(record, Consts.JsonSerializerOptions);

                sut.UndoDelete(undoImage);

                Assert.AreEqual(1, sut.RecordCount);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void CloseForMaintenance_WhenCalledTwice_SecondCallIsNoOp()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                sut.Insert(new MockRow());

                sut.CloseForMaintenance();
                sut.CloseForMaintenance();

                Assert.AreEqual(1, sut.RecordCount);

                // Reopen so the file handle is valid again for the using block's Dispose call.
                sut.ReopenAfterMaintenance();
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void ReopenAfterMaintenance_WhenNotClosed_IsNoOp()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                sut.Insert(new MockRow());

                sut.ReopenAfterMaintenance();

                Assert.AreEqual(1, sut.RecordCount);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void CloseForMaintenance_ThenReopen_RestoresRecordsAndIndexes()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                sut.Insert(new MockRow());
                sut.Insert(new MockRow());

                sut.CloseForMaintenance();
                sut.ReopenAfterMaintenance();

                Assert.AreEqual(2, sut.RecordCount);
                Assert.IsTrue(sut.IdExists(0));
                Assert.IsTrue(sut.IdExists(1));
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Compact_NullPredicate_Throws_ArgumentNullException()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                sut.Compact(null);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void Compact_DiscardsRecordsNotMatchingPredicate_KeepsSurvivors()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                sut.Insert(new MockRow(0));
                sut.Insert(new MockRow(1));
                sut.Insert(new MockRow(2));

                sut.Compact(record => record.Id != 1);

                Assert.AreEqual(2, sut.RecordCount);
                Assert.IsTrue(sut.IdExists(0));
                Assert.IsFalse(sut.IdExists(1));
                Assert.IsTrue(sut.IdExists(2));
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void Compact_NoRecordsDiscarded_IsNoOp()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> sut = CreateTable<MockRow>(CreateTestInitializer(directory), null, ref walTableAccessor, ref watermarkAccessor);

                sut.Insert(new MockRow(0));
                sut.Insert(new MockRow(1));

                sut.Compact(_ => true);

                Assert.AreEqual(2, sut.RecordCount);
            }
            finally
            {
                FinaliseTest(walTableAccessor, directory);
            }
        }
    }
}
