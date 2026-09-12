using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SimpleDB.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class IndexManagerTests
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // IndexManager reads lock timeouts from SimpleDBConfiguration, which asserts (and, in
            // a test host, throws) if read before Configure has been called. When these tests run
            // in isolation no other test class has configured it yet, so configure it here with
            // the built in defaults to make this test class self contained regardless of run order.
            Assembly asm = typeof(IIndexManager).Assembly;
            Type configurationType = asm.GetType("SimpleDB.Internal.SimpleDBConfiguration");
            Assert.IsNotNull(configurationType, "Could not find internal SimpleDBConfiguration type");

            bool isConfigured = (bool)configurationType.GetProperty("IsConfigured", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);

            if (!isConfigured)
            {
                object settings = Activator.CreateInstance(typeof(SimpleDBSettings));
                configurationType.GetMethod("Configure", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { settings });
            }
        }

        private static IIndexManager CreateIndexManager(Type genericArg, IndexType indexType, params string[] propertyNames)
        {
            Assembly asm = typeof(IIndexManager).Assembly;
            Type generic = asm.GetType("SimpleDB.Internal.IndexManager`1");
            Assert.IsNotNull(generic, "Could not find internal IndexManager`1 type");
            Type constructed = generic.MakeGenericType(genericArg);
            object instance = Activator.CreateInstance(constructed, indexType, propertyNames);
            return (IIndexManager)instance;
        }

        [TestMethod]
        public void Constructor_Throws_WhenNoPropertyNames()
        {
            // Activator will wrap the real exception in a TargetInvocationException
            Assembly asm = typeof(IIndexManager).Assembly;
            Type generic = asm.GetType("SimpleDB.Internal.IndexManager`1");
            Type constructed = generic.MakeGenericType(typeof(int));

            try
            {
                Activator.CreateInstance(constructed, IndexType.Ascending, new string[] { });
                Assert.Fail("Expected an exception for missing property names");
            }
            catch (TargetInvocationException tie) when (tie.InnerException is ArgumentOutOfRangeException)
            {
                // expected
            }
        }

        [TestMethod]
        public void AddAndContains_Int_Ascending_AddsAndRemoves()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Ascending, "Id");

            idx.Add(1);
            idx.Add(3);
            idx.Add(2);

            Assert.IsTrue(idx.Contains(1));
            Assert.IsTrue(idx.Contains(2));
            Assert.IsTrue(idx.Contains(3));

            // Duplicate add should be ignored
            idx.Add(3);
            Assert.IsTrue(idx.Contains(3));

            idx.Remove(3);
            Assert.IsFalse(idx.Contains(3));
        }

        [TestMethod]
        public void Add_List_MixedTypes_OnlyAddsCorrectType()
        {
            var idx = CreateIndexManager(typeof(string), IndexType.Ascending, "Name");

            var items = new List<object> { "one", 2, "three", null };
            idx.Add(items);

            Assert.IsTrue(idx.Contains("one"));
            Assert.IsTrue(idx.Contains("three"));
            // Contains called with the wrong type will raise InvalidCastException
            Assert.ThrowsException<InvalidCastException>(() => idx.Contains(2));
        }

        [TestMethod]
        public void Contains_WithWrongType_Throws_InvalidCast()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Ascending, "Id");
            idx.Add(1);

            // Contains will attempt to cast the incoming object to T and should throw
            Assert.ThrowsException<InvalidCastException>(() => idx.Contains("not-an-int"));
        }

        [TestMethod]
        public void Add_WithWrongType_InList_DoesNotThrow_OnlyAddsValid()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Ascending, "Id");
            var mixed = new List<object> { 1, "two", 3 };

            // Add(list) should ignore non-matching types rather than throw
            idx.Add(mixed);

            Assert.IsTrue(idx.Contains(1));
            Assert.IsTrue(idx.Contains(3));
            // Contains with wrong type will throw
            Assert.ThrowsException<InvalidCastException>(() => idx.Contains("two"));
        }

        [TestMethod]
        public void PropertyNames_And_IndexType_AreExposed()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Descending, "Id", "Name");

            Assert.AreEqual(IndexType.Descending, idx.IndexType);
            CollectionAssert.AreEqual(new List<string> { "Id", "Name" }, idx.PropertyNames);
            Assert.IsFalse(idx.IsUpdating);
        }

        [TestMethod]
        public void AddAndContains_Int_Descending_SortsInReverseOrder()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Descending, "Id");

            idx.Add(1);
            idx.Add(3);
            idx.Add(2);

            Assert.IsTrue(idx.Contains(1));
            Assert.IsTrue(idx.Contains(2));
            Assert.IsTrue(idx.Contains(3));

            idx.Remove(2);
            Assert.IsFalse(idx.Contains(2));
        }

        [TestMethod]
        public void AddAndContains_Long_Ascending_And_Descending()
        {
            var ascending = CreateIndexManager(typeof(long), IndexType.Ascending, "Id");
            ascending.Add(1L);
            ascending.Add(3L);
            ascending.Add(2L);
            Assert.IsTrue(ascending.Contains(1L));
            Assert.IsTrue(ascending.Contains(2L));
            Assert.IsTrue(ascending.Contains(3L));

            var descending = CreateIndexManager(typeof(long), IndexType.Descending, "Id");
            descending.Add(1L);
            descending.Add(3L);
            descending.Add(2L);
            Assert.IsTrue(descending.Contains(1L));
            Assert.IsTrue(descending.Contains(2L));
            Assert.IsTrue(descending.Contains(3L));
        }

        [TestMethod]
        public void Add_List_Empty_DoesNotThrow()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Ascending, "Id");
            idx.Add(new List<object>());
            Assert.IsFalse(idx.Contains(1));
        }

        [TestMethod]
        public void Add_List_Null_DoesNotThrow()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Ascending, "Id");
            idx.Add((List<object>)null);
            Assert.IsFalse(idx.Contains(1));
        }

        [TestMethod]
        public void Remove_NonExistentValue_DoesNotThrow()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Ascending, "Id");
            idx.Add(1);

            idx.Remove(99);

            Assert.IsTrue(idx.Contains(1));
        }

        [TestMethod]
        public void BeginUpdate_EndUpdate_BatchesAddsWithoutImmediateSort()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Ascending, "Id");

            idx.BeginUpdate();
            Assert.IsTrue(idx.IsUpdating);

            idx.Add(3);
            idx.Add(1);
            idx.Add(2);

            // While updating, Contains falls back to linear scan and should still find items
            Assert.IsTrue(idx.Contains(1));
            Assert.IsTrue(idx.Contains(2));
            Assert.IsTrue(idx.Contains(3));

            idx.EndUpdate();

            Assert.IsFalse(idx.IsUpdating);
            Assert.IsTrue(idx.Contains(1));
            Assert.IsTrue(idx.Contains(2));
            Assert.IsTrue(idx.Contains(3));
        }

        [TestMethod]
        public void BeginUpdate_CalledTwice_Throws()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Ascending, "Id");
            idx.BeginUpdate();

            Assert.ThrowsException<InvalidOperationException>(() => idx.BeginUpdate());
        }

        [TestMethod]
        public void EndUpdate_WithoutBeginUpdate_Throws()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Ascending, "Id");

            Assert.ThrowsException<InvalidOperationException>(() => idx.EndUpdate());
        }

        [TestMethod]
        public void Add_List_DuringBatchUpdate_DoesNotSortUntilEndUpdate()
        {
            var idx = CreateIndexManager(typeof(int), IndexType.Ascending, "Id");

            idx.BeginUpdate();
            idx.Add(new List<object> { 5, 1, 3 });
            Assert.IsTrue(idx.Contains(1));
            Assert.IsTrue(idx.Contains(3));
            Assert.IsTrue(idx.Contains(5));

            idx.EndUpdate();
            Assert.IsTrue(idx.Contains(1));
            Assert.IsTrue(idx.Contains(3));
            Assert.IsTrue(idx.Contains(5));
        }

        [TestMethod]
        public void Add_StringType_UsesDefaultSortRequiredPath()
        {
            var idx = CreateIndexManager(typeof(string), IndexType.Descending, "Name");

            idx.Add("banana");
            idx.Add("apple");
            idx.Add("cherry");

            Assert.IsTrue(idx.Contains("banana"));
            Assert.IsTrue(idx.Contains("apple"));
            Assert.IsTrue(idx.Contains("cherry"));

            idx.Remove("apple");
            Assert.IsFalse(idx.Contains("apple"));
        }
    }
}
