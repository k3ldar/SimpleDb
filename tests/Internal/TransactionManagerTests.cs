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
 *  File: TransactionManagerTests.cs
 *
 *  Purpose:  Unit tests for TransactionManager
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimpleDb.Tests;

using SimpleDB.Internal;
using SimpleDB.Internal.Tables;
using SimpleDB.Tests.Mocks;

#pragma warning disable CA1806, CA1859

namespace SimpleDB.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public sealed class TransactionManagerTests : BaseTest
    {
        private static SimpleDBManager CreateTestInitializer(string path) => new(path);

        private (TransactionManager Sut, SimpleDBOperations<MockRow> Table, IWalTableAccessor WalTableAccessor, string Directory) CreateSut()
        {
            string directory = TestHelper.GetTestPath();
            Directory.CreateDirectory(directory);
            SimpleDBManager manager = CreateTestInitializer(directory);
            IWalTableAccessor walTableAccessor = null;
            IWatermarkAccessor watermarkAccessor = null;
            SimpleDBOperations<MockRow> table = CreateTable<MockRow>(manager, null, ref walTableAccessor, ref watermarkAccessor);

            TransactionManager sut = new(walTableAccessor, watermarkAccessor, new StubTransactionSequenceAccessor(), manager);

            return (sut, table, walTableAccessor, directory);
        }

        private static void FinaliseTest(SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory)
        {
            table?.CloseForMaintenance();
            (walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>)?.CloseForMaintenance();
            Directory.Delete(directory, true);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullWalTableAccessor_ThrowsArgumentNullException()
        {
            (_, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                _ = new TransactionManager(null, new MockWatermarkAccessor(), new StubTransactionSequenceAccessor(), CreateTestInitializer(directory));
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullWatermarkAccessor_ThrowsArgumentNullException()
        {
            (_, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                _ = new TransactionManager(walTableAccessor, null, new StubTransactionSequenceAccessor(), CreateTestInitializer(directory));
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullSequenceAccessor_ThrowsArgumentNullException()
        {
            (_, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                _ = new TransactionManager(walTableAccessor, new MockWatermarkAccessor(), null, CreateTestInitializer(directory));
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullSimpleDBManager_ThrowsArgumentNullException()
        {
            (_, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                _ = new TransactionManager(walTableAccessor, new MockWatermarkAccessor(), new StubTransactionSequenceAccessor(), null);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void BeginTransaction_Default_ReturnsTransactionWithDefaultsAndTracksIt()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                ITransaction transaction = sut.BeginTransaction();

                Assert.AreEqual(TransactionAccessMode.ReadWrite, transaction.AccessMode);
                Assert.AreEqual(TransactionIsolationLevel.DirtyRead, transaction.IsolationLevel);
                Assert.AreEqual(1, sut.ActiveTransactionCount);
                Assert.AreEqual(1, sut.ActiveTransactions.Count);
                Assert.AreSame(transaction, sut.ActiveTransactions[0]);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void BeginTransaction_ExplicitParams_ReturnsTransactionWithGivenParams()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                ITransaction transaction = sut.BeginTransaction(TransactionAccessMode.ReadOnly, TransactionIsolationLevel.DirtyRead);

                Assert.AreEqual(TransactionAccessMode.ReadOnly, transaction.AccessMode);
                Assert.AreEqual(TransactionIsolationLevel.DirtyRead, transaction.IsolationLevel);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void NextTransactionId_ReflectsSequenceCurrentValue()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                sut.BeginTransaction();

                Assert.AreEqual(1L, sut.NextTransactionId);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CommitTransaction_Null_ThrowsArgumentNullException()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                sut.CommitTransaction(null);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void CommitTransaction_NoEntries_DecrementsActiveCountAndRemovesFromActiveList()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                ITransaction transaction = sut.BeginTransaction();

                sut.CommitTransaction(transaction);

                Assert.AreEqual(0, sut.ActiveTransactionCount);
                Assert.AreEqual(0, sut.ActiveTransactions.Count);
                Assert.AreEqual(0, walTableAccessor.WalTable.RecordCount);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void CommitTransaction_WithEntries_WritesCommitMarkerAndDecrementsActiveCount()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                ITransaction transaction = sut.BeginTransaction();

                table.Insert(new MockRow(1), null, transaction);

                sut.CommitTransaction(transaction);

                Assert.AreEqual(0, sut.ActiveTransactionCount);
                Assert.IsTrue(walTableAccessor.WalTable.RecordCount >= 2);
                Assert.IsTrue(walTableAccessor.WalTable.Select().Any(w => w.Operation == OperationType.Commit && w.TransactionId == transaction.TransactionId));
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RollbackTransaction_Null_ThrowsArgumentNullException()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                sut.RollbackTransaction(null);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void RollbackTransaction_WithInsertedRecord_UndoesInsertAndWritesAbortMarker()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                ITransaction transaction = sut.BeginTransaction();

                table.Insert(new MockRow(1), null, transaction);

                sut.RollbackTransaction(transaction);

                Assert.AreEqual(0, sut.ActiveTransactionCount);
                Assert.AreEqual(0, table.Select().Count);
                Assert.IsTrue(walTableAccessor.WalTable.Select().Any(w => w.Operation == OperationType.Abort && w.TransactionId == transaction.TransactionId));
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void ForceCloseActiveTransactions_NoActiveTransactions_ReturnsZero()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                int result = sut.ForceCloseActiveTransactions();

                Assert.AreEqual(0, result);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void ForceCloseActiveTransactions_WithActiveTransactions_RollsBackAllAndReturnsCount()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                sut.BeginTransaction();
                sut.BeginTransaction();

                int result = sut.ForceCloseActiveTransactions();

                Assert.AreEqual(2, result);
                Assert.AreEqual(0, sut.ActiveTransactionCount);
                Assert.AreEqual(0, sut.ActiveTransactions.Count);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void ResetTransactionSequence_WhenCalled_ResetsSequenceAccessor()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                sut.BeginTransaction();
                sut.BeginTransaction();

                sut.ResetTransactionSequence();

                Assert.AreEqual(0L, sut.NextTransactionId);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void ActiveTransactions_Empty_ReturnsEmptyList()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                Assert.AreEqual(0, sut.ActiveTransactions.Count);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void GetAllTimings_WhenCalled_ReturnsAllExpectedKeys()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                var timings = sut.GetAllTimings;

                Assert.AreEqual(5, timings.Count);
                Assert.IsTrue(timings.ContainsKey("TimingsBegin"));
                Assert.IsTrue(timings.ContainsKey("TimingsCommit"));
                Assert.IsTrue(timings.ContainsKey("TimingsRollback"));
                Assert.IsTrue(timings.ContainsKey("TimingsRollbackUndo"));
                Assert.IsTrue(timings.ContainsKey("TimingsCheckpoint"));
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void CheckpointDatabase_Forced_NoParticipatingActivity_CompletesSuccessfully()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                sut.CheckpointDatabase();

                Assert.AreEqual(1L, sut.CheckpointsCompleted);
                Assert.AreEqual(0L, sut.CheckpointsSkippedForActiveTransactions);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void CheckpointDatabase_Forced_WithCommittedWalEntries_TruncatesWalAndCompletes()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                ITransaction transaction = sut.BeginTransaction();
                table.Insert(new MockRow(1), null, transaction);
                sut.CommitTransaction(transaction);

                long recordCountBeforeCheckpoint = walTableAccessor.WalTable.RecordCount;

                sut.CheckpointDatabase();

                Assert.AreEqual(1L, sut.CheckpointsCompleted);
                Assert.IsTrue(walTableAccessor.WalTable.RecordCount < recordCountBeforeCheckpoint);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [TestMethod]
        public void CheckpointDatabase_WhileTransactionActive_SkipsAndIncrementsSkippedCounter()
        {
            (TransactionManager sut, SimpleDBOperations<MockRow> table, IWalTableAccessor walTableAccessor, string directory) = CreateSut();

            try
            {
                sut.BeginTransaction();

                sut.CheckpointDatabase();

                Assert.AreEqual(0L, sut.CheckpointsCompleted);
                Assert.AreEqual(1L, sut.CheckpointsSkippedForActiveTransactions);
            }
            finally
            {
                FinaliseTest(table, walTableAccessor, directory);
            }
        }

        [ExcludeFromCodeCoverage]
        private sealed class StubTransactionSequenceAccessor : ITransactionSequenceAccessor
        {
            private long _value;

            public ISimpleDBOperations<TransactionSequenceDataRow> SequenceTable => throw new NotImplementedException();

            public long CurrentValue() => _value;

            public long NextValue() => ++_value;

            public void Reset() => _value = 0;
        }
    }
}
