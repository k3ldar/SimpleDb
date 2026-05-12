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
 *  File: ObservableListTests.cs
 *
 *  Purpose:  Unit tests for ObservableList
 *
 *  Date        Name                Reason
 *  26/06/2022  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SimpleDB.Tests
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class ObservableListTests
    {
        [TestMethod]
        public void Add_NewItem_RaisesChangedEvent()
        {
            ObservableList<int> sut = new();
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.Add(1);

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void Add_NewItem_ItemExistsInList()
        {
            ObservableList<int> sut = new();

            sut.Add(42);

            Assert.AreEqual(42, sut[0]);
        }

        [TestMethod]
        public void AddRange_Collection_RaisesChangedEvent()
        {
            ObservableList<int> sut = new();
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.AddRange(new[] { 1, 2, 3 });

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void AddRange_Collection_AllItemsAdded()
        {
            ObservableList<int> sut = new();

            sut.AddRange(new[] { 1, 2, 3 });

            Assert.AreEqual(3, sut.Count);
        }

        [TestMethod]
        public void Clear_WithItems_RaisesChangedEvent()
        {
            ObservableList<int> sut = new();
            sut.Add(1);
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.Clear();

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void Clear_WithItems_ListIsEmpty()
        {
            ObservableList<int> sut = new();
            sut.Add(1);
            sut.Add(2);

            sut.Clear();

            Assert.AreEqual(0, sut.Count);
        }

        [TestMethod]
        public void Clear_WhenEmpty_RaisesChangedEvent()
        {
            ObservableList<int> sut = new();
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.Clear();

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void Insert_AtIndex_RaisesChangedEvent()
        {
            ObservableList<int> sut = new();
            sut.Add(1);
            sut.Add(3);
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.Insert(1, 2);

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void Insert_AtIndex_ItemInsertedAtCorrectPosition()
        {
            ObservableList<int> sut = new();
            sut.Add(1);
            sut.Add(3);

            sut.Insert(1, 2);

            Assert.AreEqual(2, sut[1]);
            Assert.AreEqual(3, sut.Count);
        }

        [TestMethod]
        public void InsertRange_AtIndex_RaisesChangedEvent()
        {
            ObservableList<int> sut = new();
            sut.Add(1);
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.InsertRange(0, new[] { 10, 20 });

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void InsertRange_AtIndex_ItemsInsertedAtCorrectPosition()
        {
            ObservableList<int> sut = new();
            sut.Add(99);

            sut.InsertRange(0, new[] { 10, 20 });

            Assert.AreEqual(10, sut[0]);
            Assert.AreEqual(20, sut[1]);
            Assert.AreEqual(99, sut[2]);
        }

        [TestMethod]
        public void Remove_ExistingItem_RaisesChangedEvent()
        {
            ObservableList<int> sut = new();
            sut.Add(1);
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.Remove(1);

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void Remove_ExistingItem_ReturnsTrue()
        {
            ObservableList<int> sut = new();
            sut.Add(1);

            bool result = sut.Remove(1);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Remove_ExistingItem_ItemRemovedFromList()
        {
            ObservableList<int> sut = new();
            sut.Add(1);

            sut.Remove(1);

            Assert.AreEqual(0, sut.Count);
        }

        [TestMethod]
        public void Remove_NonExistingItem_ReturnsFalse()
        {
            ObservableList<int> sut = new();

            bool result = sut.Remove(99);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Remove_NonExistingItem_DoesNotRaiseChangedEvent()
        {
            ObservableList<int> sut = new();
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.Remove(99);

            Assert.IsFalse(eventRaised);
        }

        [TestMethod]
        public void RemoveAll_MatchingItems_RaisesChangedEvent()
        {
            ObservableList<int> sut = new();
            sut.AddRange(new[] { 1, 2, 3, 4 });
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.RemoveAll(x => x % 2 == 0);

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void RemoveAll_MatchingItems_ReturnsCorrectCount()
        {
            ObservableList<int> sut = new();
            sut.AddRange(new[] { 1, 2, 3, 4 });

            int removed = sut.RemoveAll(x => x % 2 == 0);

            Assert.AreEqual(2, removed);
        }

        [TestMethod]
        public void RemoveAll_MatchingItems_ItemsRemovedFromList()
        {
            ObservableList<int> sut = new();
            sut.AddRange(new[] { 1, 2, 3, 4 });

            sut.RemoveAll(x => x % 2 == 0);

            Assert.AreEqual(2, sut.Count);
            Assert.IsTrue(sut.Contains(1));
            Assert.IsTrue(sut.Contains(3));
        }

        [TestMethod]
        public void RemoveAll_NoMatchingItems_DoesNotRaiseChangedEvent()
        {
            ObservableList<int> sut = new();
            sut.AddRange(new[] { 1, 3, 5 });
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.RemoveAll(x => x % 2 == 0);

            Assert.IsFalse(eventRaised);
        }

        [TestMethod]
        public void RemoveAt_ValidIndex_RaisesChangedEvent()
        {
            ObservableList<int> sut = new();
            sut.Add(1);
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.RemoveAt(0);

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void RemoveAt_ValidIndex_ItemRemovedFromList()
        {
            ObservableList<int> sut = new();
            sut.AddRange(new[] { 10, 20, 30 });

            sut.RemoveAt(1);

            Assert.AreEqual(2, sut.Count);
            Assert.AreEqual(10, sut[0]);
            Assert.AreEqual(30, sut[1]);
        }

        [TestMethod]
        public void RemoveRange_ValidRange_RaisesChangedEvent()
        {
            ObservableList<int> sut = new();
            sut.AddRange(new[] { 1, 2, 3, 4 });
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.RemoveRange(1, 2);

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void RemoveRange_ValidRange_ItemsRemovedFromList()
        {
            ObservableList<int> sut = new();
            sut.AddRange(new[] { 1, 2, 3, 4 });

            sut.RemoveRange(1, 2);

            Assert.AreEqual(2, sut.Count);
            Assert.AreEqual(1, sut[0]);
            Assert.AreEqual(4, sut[1]);
        }

        [TestMethod]
        public void Changed_EventArgs_SenderIsTheList()
        {
            ObservableList<int> sut = new();
            object capturedSender = null;
            sut.Changed += (s, e) => capturedSender = s;

            sut.Add(1);

            Assert.AreSame(sut, capturedSender);
        }

        [TestMethod]
        public void Changed_MultipleSubscribers_AllAreNotified()
        {
            ObservableList<int> sut = new();
            int callCount = 0;
            sut.Changed += (s, e) => callCount++;
            sut.Changed += (s, e) => callCount++;

            sut.Add(1);

            Assert.AreEqual(2, callCount);
        }
    }
}
