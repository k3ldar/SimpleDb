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
 *  File: WalRecorderTests.cs
 *
 *  Purpose:  Unit tests for WalRecorder
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using SharedPluginFeatures;

using SimpleDB.Internal;
using SimpleDB.Internal.Tables;
using SimpleDB.Tests.Mocks;

namespace SimpleDB.Tests.Internal
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class WalRecorderTests
    {
        private sealed class FakeTransactionOnly : ITransaction
        {
            public long TransactionId => 1L;

            public TransactionAccessMode AccessMode => TransactionAccessMode.ReadWrite;

            public TransactionIsolationLevel IsolationLevel => TransactionIsolationLevel.DirtyRead;

            public DateTime Started => DateTime.UtcNow;

            public void Commit() { }

            public void Rollback() { }
        }

        private sealed class FakeTransactionWithCollector : ITransaction, IWalEntryCollector
        {
            private readonly List<WalEntry> _entries = [];

            public FakeTransactionWithCollector(long transactionId = 42L)
            {
                TransactionId = transactionId;
            }

            public long TransactionId { get; }

            public TransactionAccessMode AccessMode => TransactionAccessMode.ReadWrite;

            public TransactionIsolationLevel IsolationLevel => TransactionIsolationLevel.DirtyRead;

            public DateTime Started => DateTime.UtcNow;

            public IReadOnlyList<WalEntry> Entries => _entries;

            public void AddEntry(WalEntry entry) => _entries.Add(entry);

            public void Commit() { }

            public void Rollback() { }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullWalTableAccessor_ThrowsArgumentNullException()
        {
            _ = new WalRecorder(null);
        }

        [TestMethod]
        public void GetAllTimings_WhenCalled_ReturnsExpectedKey()
        {
            WalRecorder sut = new(new MockWalTableAccessor());

            Dictionary<string, Timings> result = sut.GetAllTimings;

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey("TimingsRecord"));
        }

        [TestMethod]
        public void GetAllTimings_CalledMultipleTimes_ReturnsNewInstanceEachTime()
        {
            WalRecorder sut = new(new MockWalTableAccessor());

            Dictionary<string, Timings> first = sut.GetAllTimings;
            Dictionary<string, Timings> second = sut.GetAllTimings;

            Assert.AreNotSame(first, second);
        }

        [TestMethod]
        public void Construct_ValidInstance_ImplementsIWalRecorder()
        {
            WalRecorder sut = new(new MockWalTableAccessor());

            Assert.IsInstanceOfType<IWalRecorder>(sut);
        }
    }
}
