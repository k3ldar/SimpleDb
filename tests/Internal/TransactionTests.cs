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
 *  File: TransactionTests.cs
 *
 *  Purpose:  Unit tests for Transaction
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SharedPluginFeatures;

using SimpleDB.Internal;

namespace SimpleDB.Tests.Internal
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class TransactionTests
    {
        [TestMethod]
        public void Construct_Default_PropertiesSetCorrectly()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();

            Transaction sut = new(1, manager, lockScope);

            Assert.AreEqual(1, sut.TransactionId);
            Assert.AreEqual(TransactionAccessMode.ReadWrite, sut.AccessMode);
            Assert.AreEqual(TransactionIsolationLevel.DirtyRead, sut.IsolationLevel);
            Assert.IsFalse(sut.IsInternalTransaction);
            Assert.AreEqual(0, sut.Entries.Count);
        }

        [TestMethod]
        public void Construct_WithExplicitValues_PropertiesSetCorrectly()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();

            Transaction sut = new(
                42, manager, lockScope,
                TransactionAccessMode.ReadOnly,
                TransactionIsolationLevel.DirtyRead,
                internalTransaction: true);

            Assert.AreEqual(42, sut.TransactionId);
            Assert.AreEqual(TransactionAccessMode.ReadOnly, sut.AccessMode);
            Assert.AreEqual(TransactionIsolationLevel.DirtyRead, sut.IsolationLevel);
            Assert.IsTrue(sut.IsInternalTransaction);
        }

        [TestMethod]
        public void Construct_Default_StartedIsCloseToUtcNow()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();

            DateTime before = DateTime.UtcNow;
            Transaction sut = new(1, manager, lockScope);
            DateTime after = DateTime.UtcNow;

            Assert.IsTrue(sut.Started >= before && sut.Started <= after);
        }

        [TestMethod]
        public void AddEntry_ValidEntry_AddsToEntries()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);
            WalEntry entry = new() { TransactionId = 1, TableName = "Orders" };

            ((IWalEntryCollector)sut).AddEntry(entry);

            Assert.AreEqual(1, sut.Entries.Count);
            Assert.AreSame(entry, sut.Entries[0]);
        }

        [TestMethod]
        public void AddEntry_MultipleEntries_AddsAllInOrder()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);
            WalEntry entry1 = new() { TransactionId = 1, TableName = "Orders" };
            WalEntry entry2 = new() { TransactionId = 1, TableName = "Customers" };

            ((IWalEntryCollector)sut).AddEntry(entry1);
            ((IWalEntryCollector)sut).AddEntry(entry2);

            Assert.AreEqual(2, sut.Entries.Count);
            Assert.AreSame(entry1, sut.Entries[0]);
            Assert.AreSame(entry2, sut.Entries[1]);
        }

        [TestMethod]
        public void AddEntry_AfterCommit_ThrowsTransactionException()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);
            sut.Commit();

            Assert.ThrowsException<TransactionException>(() => ((IWalEntryCollector)sut).AddEntry(new WalEntry()));
        }

        [TestMethod]
        public void AddEntry_AfterRollback_ThrowsTransactionException()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);
            sut.Rollback();

            Assert.ThrowsException<TransactionException>(() => ((IWalEntryCollector)sut).AddEntry(new WalEntry()));
        }

        [TestMethod]
        public void Commit_ValidTransaction_CallsTransactionManagerCommit()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);

            sut.Commit();

            Assert.AreEqual(1, manager.CommitCallCount);
            Assert.AreSame(sut, manager.LastCommitted);
        }

        [TestMethod]
        public void Commit_CalledTwice_ThrowsTransactionException()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);
            sut.Commit();

            TransactionException exception = Assert.ThrowsException<TransactionException>(() => sut.Commit());

            Assert.AreEqual("Transaction 1 has already been committed or rolled back and cannot be reused.", exception.Message);
        }

        [TestMethod]
        public void Commit_AfterRollback_ThrowsTransactionException()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);
            sut.Rollback();

            Assert.ThrowsException<TransactionException>(() => sut.Commit());
            Assert.AreEqual(0, manager.CommitCallCount);
        }

        [TestMethod]
        public void Rollback_ValidTransaction_CallsTransactionManagerRollback()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);

            sut.Rollback();

            Assert.AreEqual(1, manager.RollbackCallCount);
            Assert.AreSame(sut, manager.LastRolledBack);
        }

        [TestMethod]
        public void Rollback_CalledTwice_ThrowsTransactionException()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);
            sut.Rollback();

            TransactionException exception = Assert.ThrowsException<TransactionException>(() => sut.Rollback());

            Assert.AreEqual("Transaction 1 has already been committed or rolled back and cannot be reused.", exception.Message);
        }

        [TestMethod]
        public void Rollback_AfterCommit_ThrowsTransactionException()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);
            sut.Commit();

            Assert.ThrowsException<TransactionException>(() => sut.Rollback());
            Assert.AreEqual(0, manager.RollbackCallCount);
        }

        [TestMethod]
        public void ReleaseDatabaseLock_ValidScope_DisposesScope()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);

            ((IDatabaseLockScope)sut).ReleaseDatabaseLock();

            Assert.AreEqual(1, lockScope.DisposeCallCount);
        }

        [TestMethod]
        public void ReleaseDatabaseLock_CalledTwice_IsIdempotent()
        {
            SpyTransactionManager manager = new();
            SpyDatabaseLockScope lockScope = new();
            Transaction sut = new(1, manager, lockScope);

            ((IDatabaseLockScope)sut).ReleaseDatabaseLock();
            ((IDatabaseLockScope)sut).ReleaseDatabaseLock();

            Assert.AreEqual(1, lockScope.DisposeCallCount);
        }

        [TestMethod]
        public void ReleaseDatabaseLock_NullScope_DoesNotThrow()
        {
            SpyTransactionManager manager = new();
            Transaction sut = new(1, manager, null);

            ((IDatabaseLockScope)sut).ReleaseDatabaseLock();
        }

        [ExcludeFromCodeCoverage]
        private sealed class SpyDatabaseLockScope : IDisposable
        {
            public int DisposeCallCount { get; private set; }

            public void Dispose()
            {
                DisposeCallCount++;
            }
        }

        [ExcludeFromCodeCoverage]
        private sealed class SpyTransactionManager : ITransactionManager
        {
            public int CommitCallCount { get; private set; }

            public int RollbackCallCount { get; private set; }

            public ITransaction LastCommitted { get; private set; }

            public ITransaction LastRolledBack { get; private set; }

            public ITransaction BeginTransaction(
                TransactionAccessMode accessMode = TransactionAccessMode.ReadWrite,
                TransactionIsolationLevel isolationLevel = TransactionIsolationLevel.DirtyRead)
            {
                throw new NotImplementedException();
            }

            public void CommitTransaction(ITransaction transaction)
            {
                CommitCallCount++;
                LastCommitted = transaction;
            }

            public void RollbackTransaction(ITransaction transaction)
            {
                RollbackCallCount++;
                LastRolledBack = transaction;
            }

            public void CheckpointDatabase()
            {
                // no-op for tests
            }

            public Dictionary<string, Timings> GetAllTimings => [];

            public int ActiveTransactionCount => 0;

            public long CheckpointsSkippedForActiveTransactions => 0;

            public long CheckpointsCompleted => 0;

            public long NextTransactionId => throw new NotImplementedException();

            public IReadOnlyList<ITransaction> ActiveTransactions => throw new NotImplementedException();

            public int ForceCloseActiveTransactions() => 0;

            public void ResetTransactionSequence()
            {
                // no-op for tests
            }
        }
    }
}
