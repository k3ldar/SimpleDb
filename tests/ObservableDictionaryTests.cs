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
 *  File: ObservableDictionaryTests.cs
 *
 *  Purpose:  Unit tests for ObservableDictionary
 *
 *  Date        Name                Reason
 *  10/08/2022  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SimpleDB.Tests
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class ObservableDictionaryTests
    {
        [TestMethod]
        public void Add_NewItem_RaisesChangedEvent()
        {
            ObservableDictionary<string, int> sut = new();
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.Add("key", 1);

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void Add_NewItem_ItemExistsInDictionary()
        {
            ObservableDictionary<string, int> sut = new();

            sut.Add("key", 42);

            Assert.AreEqual(42, sut["key"]);
        }

        [TestMethod]
        public void Indexer_SetValue_RaisesChangedEvent()
        {
            ObservableDictionary<string, int> sut = new();
            sut.Add("key", 1);
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut["key"] = 99;

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void Indexer_SetValue_UpdatesValue()
        {
            ObservableDictionary<string, int> sut = new();
            sut.Add("key", 1);

            sut["key"] = 99;

            Assert.AreEqual(99, sut["key"]);
        }

        [TestMethod]
        public void Indexer_GetValue_ReturnsCorrectValue()
        {
            ObservableDictionary<string, int> sut = new();
            sut.Add("key", 55);

            int result = sut["key"];

            Assert.AreEqual(55, result);
        }

        [TestMethod]
        public void Clear_WithItems_RaisesChangedEvent()
        {
            ObservableDictionary<string, int> sut = new();
            sut.Add("key", 1);
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.Clear();

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void Clear_WithItems_DictionaryIsEmpty()
        {
            ObservableDictionary<string, int> sut = new();
            sut.Add("key1", 1);
            sut.Add("key2", 2);

            sut.Clear();

            Assert.AreEqual(0, sut.Count);
        }

        [TestMethod]
        public void Clear_WhenEmpty_RaisesChangedEvent()
        {
            ObservableDictionary<string, int> sut = new();
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.Clear();

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void Remove_ExistingKey_RaisesChangedEvent()
        {
            ObservableDictionary<string, int> sut = new();
            sut.Add("key", 1);
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.Remove("key");

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void Remove_ExistingKey_ReturnsTrue()
        {
            ObservableDictionary<string, int> sut = new();
            sut.Add("key", 1);

            bool result = sut.Remove("key");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Remove_ExistingKey_ItemRemovedFromDictionary()
        {
            ObservableDictionary<string, int> sut = new();
            sut.Add("key", 1);

            sut.Remove("key");

            Assert.IsFalse(sut.ContainsKey("key"));
        }

        [TestMethod]
        public void Remove_NonExistingKey_ReturnsFalse()
        {
            ObservableDictionary<string, int> sut = new();
            bool result = sut.Remove("missing");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Remove_NonExistingKey_DoesNotRaiseChangedEvent()
        {
            ObservableDictionary<string, int> sut = new();
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.Remove("missing");

            Assert.IsFalse(eventRaised);
        }

        [TestMethod]
        public void TryAdd_NewKey_RaisesChangedEvent()
        {
            ObservableDictionary<string, int> sut = new();
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.TryAdd("key", 1);

            Assert.IsTrue(eventRaised);
        }

        [TestMethod]
        public void TryAdd_NewKey_ReturnsTrue()
        {
            ObservableDictionary<string, int> sut = new();

            bool result = sut.TryAdd("key", 1);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TryAdd_NewKey_ItemExistsInDictionary()
        {
            ObservableDictionary<string, int> sut = new();

            sut.TryAdd("key", 77);

            Assert.AreEqual(77, sut["key"]);
        }

        [TestMethod]
        public void TryAdd_DuplicateKey_ReturnsFalse()
        {
            ObservableDictionary<string, int> sut = new();
            sut.Add("key", 1);

            bool result = sut.TryAdd("key", 2);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryAdd_DuplicateKey_DoesNotRaiseChangedEvent()
        {
            ObservableDictionary<string, int> sut = new();
            sut.Add("key", 1);
            bool eventRaised = false;
            sut.Changed += (s, e) => eventRaised = true;

            sut.TryAdd("key", 2);

            Assert.IsFalse(eventRaised);
        }

        [TestMethod]
        public void Changed_EventArgs_SenderIsTheDictionary()
        {
            ObservableDictionary<string, int> sut = new();
            object capturedSender = null;
            sut.Changed += (s, e) => capturedSender = s;

            sut.Add("key", 1);

            Assert.AreSame(sut, capturedSender);
        }

        [TestMethod]
        public void Changed_MultipleSubscribers_AllAreNotified()
        {
            ObservableDictionary<string, int> sut = new();
            int callCount = 0;
            sut.Changed += (s, e) => callCount++;
            sut.Changed += (s, e) => callCount++;

            sut.Add("key", 1);

            Assert.AreEqual(2, callCount);
        }
    }
}
