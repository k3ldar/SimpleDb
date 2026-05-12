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
 *  File: SimpleDBOperationsExtendedTests.cs
 *
 *  Purpose:  Extended coverage tests for SimpleDBOperations
 *
 *  Date        Name                Reason
 *  23/05/2022  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Shared.Classes;
using SharedPluginFeatures;
using SimpleDb.Tests;
using SimpleDB.Internal;
using SimpleDB.Tests.Mocks;

using io = System.IO;

#pragma warning disable CA1806, CA1859

namespace SimpleDB.Tests
{
    #region Mock trigger helpers

    [ExcludeFromCodeCoverage]
    internal sealed class MockTriggers<T> : ITableTriggers<T>
        where T : TableRowDefinition
    {
        public int BeforeInsertCallCount { get; private set; }
        public int AfterInsertCallCount { get; private set; }
        public int BeforeDeleteCallCount { get; private set; }
        public int AfterDeleteCallCount { get; private set; }
        public int BeforeUpdateCallCount { get; private set; }
        public int BeforeUpdateCompareCallCount { get; private set; }
        public int AfterUpdateCallCount { get; private set; }

        public int Position => 0;

        public TriggerType TriggerTypes =>
            TriggerType.BeforeInsert | TriggerType.AfterInsert |
            TriggerType.BeforeDelete | TriggerType.AfterDelete |
            TriggerType.BeforeUpdate | TriggerType.BeforeUpdateCompare | TriggerType.AfterUpdate;

        public void BeforeInsert(List<T> records) => BeforeInsertCallCount++;
        public void AfterInsert(List<T> records) => AfterInsertCallCount++;
        public void BeforeDelete(List<T> records) => BeforeDeleteCallCount++;
        public void AfterDelete(List<T> records) => AfterDeleteCallCount++;
        public void BeforeUpdate(List<T> records) => BeforeUpdateCallCount++;
        public void BeforeUpdate(T newRecord, T oldRecord) => BeforeUpdateCompareCallCount++;
        public void AfterUpdate(List<T> records) => AfterUpdateCallCount++;
    }

    [ExcludeFromCodeCoverage]
    internal sealed class MockTableDefaults<T> : ITableDefaults<T>
        where T : TableRowDefinition
    {
        private readonly Func<ushort, List<T>> _initialData;

        public MockTableDefaults(long primarySequence = 0, long secondarySequence = -1,
            ushort version = 0, Func<ushort, List<T>> initialData = null)
        {
            PrimarySequence = primarySequence;
            SecondarySequence = secondarySequence;
            Version = version;
            _initialData = initialData;
        }

        public long PrimarySequence { get; }
        public long SecondarySequence { get; }
        public ushort Version { get; }
        public List<T> InitialData(ushort version) => _initialData?.Invoke(version);
    }

    #endregion

    [TestClass]
    [ExcludeFromCodeCoverage]
    public class SimpleDBOperationsExtendedTests
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Initialize ThreadManager for SlidingMemory tests
            Shared.Classes.ThreadManager.Initialise();
        }

        #region Helpers

        private static SimpleDBManager CreateTestInitializer(string path) => new(path);

        private static void DeleteDirectoryWithRetry(string directory, int retries = 10, int delayMs = 100)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    io.Directory.Delete(directory, true);
                    return;
                }
                catch (io.IOException)
                {
                    if (i < retries - 1)
                    {
                        Thread.Sleep(delayMs);
                    }
                    else
                    {
                        // On final retry, try to delete individual files first
                        try
                        {
                            foreach (var file in io.Directory.GetFiles(directory, "*", io.SearchOption.AllDirectories))
                            {
                                try
                                {
                                    io.File.SetAttributes(file, io.FileAttributes.Normal);
                                    io.File.Delete(file);
                                }
                                catch { }
                            }
                            io.Directory.Delete(directory, true);
                            return;
                        }
                        catch
                        {
                            // If we still can't delete after all retries, just swallow the exception
                            // The test has completed and the next test run will clean up
                        }
                    }
                }
            }
        }

        #endregion

        #region Properties

        [TestMethod]
        public void TableName_ReturnsCorrectValue()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Assert.AreEqual("MockTable", sut.TableName);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void CachingStrategy_ReturnsCorrectValue()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Assert.AreEqual(CachingStrategy.Memory, sut.CachingStrategy);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void WriteStrategy_ForcedWrite_ReturnsCorrectValue()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Assert.AreEqual(WriteStrategy.Forced, sut.WriteStrategy);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void WriteStrategy_LazyWrite_ReturnsCorrectValue()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockLazyWriteRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Assert.AreEqual(WriteStrategy.Lazy, sut.WriteStrategy);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void SlidingMemoryTimeout_ReturnsCorrectValue()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                SimpleDBOperations<MockRowSlidingMemory> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                TimeSpan timeout = sut.SlidingMemoryTimeout;
                sut.Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(1000); // allow background thread to release file handle
                Assert.AreEqual(TimeSpan.FromMilliseconds(2), timeout);
            }
            finally
            {
                DeleteDirectoryWithRetry(directory, 30, 200);
            }
        }

        [TestMethod]
        public void TableLock_ReturnsNonNullObject()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Assert.IsNotNull(sut.TableLock);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void CompactPercent_ReturnsZeroOnNewTable()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Assert.AreEqual((byte)0, sut.CompactPercent);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void FileVersion_ReturnsZeroOnNewTable()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Assert.AreEqual((ushort)0, sut.FileVersion);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void GetAllTimings_ReturnsNonEmptyDictionary()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Dictionary<string, Timings> timings = sut.GetAllTimings;
                Assert.IsNotNull(timings);
                Assert.IsTrue(timings.Count > 0);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void GetAllTimings_ReturnsClonedCopies_NotSameReferences()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Dictionary<string, Timings> timings1 = sut.GetAllTimings;
                Dictionary<string, Timings> timings2 = sut.GetAllTimings;
                // Each call returns a new dictionary
                Assert.AreNotSame(timings1, timings2);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region Select with predicate

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        [SuppressMessage("Major Code Smell", "S3966:Objects should not be disposed more than once", Justification = "Testing disposed behaviour")]
        public void Select_Predicate_ObjectDisposed_Throws_ObjectDisposedException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.Dispose();
                _ = sut.Select(r => r.Id > 0);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Select_Predicate_NullPredicate_Throws_ArgumentNullException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                _ = sut.Select(predicate: null);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Select_Predicate_ReturnsMatchingRecords()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                using (SimpleDBOperations<MockRow> sut = new(manager, keyManager))
                {
                    for (int i = 0; i < 10; i++)
                        sut.Insert(new MockRow());
                }

                using SimpleDBOperations<MockRow> readSut = new(manager, keyManager);
                IReadOnlyList<MockRow> result = readSut.Select(r => r.Id >= 5);
                Assert.AreEqual(5, result.Count);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Select_Predicate_NoMatchingRecords_ReturnsEmptyList()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                using (SimpleDBOperations<MockRow> sut = new(manager, keyManager))
                {
                    for (int i = 0; i < 5; i++)
                        sut.Insert(new MockRow());
                }

                using SimpleDBOperations<MockRow> readSut = new(manager, keyManager);
                IReadOnlyList<MockRow> result = readSut.Select(r => r.Id > 1000);
                Assert.AreEqual(0, result.Count);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region Truncate

        [TestMethod]
        public void Truncate_RemovesAllRecords()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                using (SimpleDBOperations<MockRow> sut = new(manager, keyManager))
                {
                    for (int i = 0; i < 10; i++)
                        sut.Insert(new MockRow());

                    Assert.AreEqual(10, sut.RecordCount);

                    sut.Truncate();

                    Assert.AreEqual(0, sut.RecordCount);
                }

                using SimpleDBOperations<MockRow> readSut = new(manager, keyManager);
                Assert.AreEqual(0, readSut.RecordCount);
                Assert.AreEqual(0, readSut.Select().Count);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Truncate_OnEmptyTable_Succeeds()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.Truncate();
                Assert.AreEqual(0, sut.RecordCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region ForceWrite

        [TestMethod]
        public void ForceWrite_ForcedWriteStrategy_DoesNotThrow()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                // MockRow uses WriteStrategy.Forced — ForceWrite short-circuits in that case
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.Insert(new MockRow());
                sut.ForceWrite(); // should not throw
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ForceWrite_LazyWriteStrategy_FlushesDataToDisk()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                long sizeBeforeForce;
                long sizeAfterForce;

                using (SimpleDBOperations<MockLazyWriteRow> sut = new(manager, keyManager))
                {
                    sut.Insert(new MockLazyWriteRow("hello"));
                    sizeBeforeForce = new io.FileInfo(io.Path.Combine(directory, "MockLazyWriteTable.dat")).Length;
                    sut.ForceWrite();
                    sizeAfterForce = new io.FileInfo(io.Path.Combine(directory, "MockLazyWriteTable.dat")).Length;
                }

                Assert.IsTrue(sizeAfterForce > sizeBeforeForce);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region ClearAllMemory

        [TestMethod]
        public void ClearAllMemory_MemoryCachingStrategy_FlushesCacheAndWritesToDisk()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                using (SimpleDBOperations<MockRow> sut = new(manager, keyManager))
                {
                    sut.Insert(new MockRow());
                    // Populate in-memory cache via Select
                    _ = sut.Select();
                    sut.ClearAllMemory();
                }

                // Data should still be readable after ClearAllMemory
                using SimpleDBOperations<MockRow> readSut = new(manager, keyManager);
                Assert.AreEqual(1, readSut.RecordCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ClearAllMemory_CalledMultipleTimes_DoesNotThrow()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.ClearAllMemory();
                sut.ClearAllMemory();
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region IdExists

        [TestMethod]
        public void IdExists_ExistingId_ReturnsTrue()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.Insert(new MockRow());
                Assert.IsTrue(sut.IdExists(0));
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void IdExists_NonExistingId_ReturnsFalse()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Assert.IsFalse(sut.IdExists(999));
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region IndexExists

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void IndexExists_NullName_Throws_ArgumentNullException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRowMultipleIndex> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.IndexExists(null, "value");
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void IndexExists_NonExistentIndexName_Throws_ArgumentOutOfRangeException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRowMultipleIndex> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.IndexExists("DoesNotExist", "value");
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void IndexExists_ExistingIndexValue_ReturnsTrue()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRowMultipleIndex> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.Insert(new MockRowMultipleIndex() { Name = "Alpha", Index = 1 });
                // Multi-property index value is concatenated
                Assert.IsTrue(sut.IndexExists("TestIndex", "Alpha1"));
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void IndexExists_NonExistingIndexValue_ReturnsFalse()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRowMultipleIndex> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Assert.IsFalse(sut.IndexExists("TestIndex", "NeverInserted99"));
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region IdIsInUse

        [TestMethod]
        public void IdIsInUse_ValueExists_ReturnsTrue()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                using SimpleDBOperations<MockRow> sut = new(manager, keyManager);
                sut.Insert(new MockRow());
                Assert.IsTrue(sut.IdIsInUse(nameof(TableRowDefinition.Id), 0));
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void IdIsInUse_ValueNotFound_ReturnsFalse()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                Assert.IsFalse(sut.IdIsInUse(nameof(TableRowDefinition.Id), 999));
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region Insert with InsertOptions

        [TestMethod]
        public void Insert_Single_WithInsertOptions_NullOptions_UsesDefaults()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                using SimpleDBOperations<MockRow> sut = new(manager, keyManager);
                sut.Insert(new MockRow(), null);
                Assert.AreEqual(1, sut.RecordCount);
                Assert.AreEqual(0, sut.Select(0).Id);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Insert_List_WithInsertOptions_AssignPrimaryKeyFalse_DoesNotChangePrimarySequence()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                using SimpleDBOperations<MockRow> sut = new(manager, keyManager);
                long sequenceBefore = sut.PrimarySequence;

                sut.Insert([new MockRow()], new InsertOptions(false));

                Assert.AreEqual(sequenceBefore, sut.PrimarySequence);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Insert_List_EmptyList_Throws_ArgumentException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.Insert([]);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Insert_List_WithInsertOptions_NullOptions_UsesDefaults()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                using SimpleDBOperations<MockRow> sut = new(manager, keyManager);
                sut.Insert([new MockRow()], null);
                Assert.AreEqual(1, sut.RecordCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region InsertOrUpdate - disposed / null checks

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        [SuppressMessage("Major Code Smell", "S3966:Objects should not be disposed more than once", Justification = "Testing disposed behaviour")]
        public void InsertOrUpdate_ObjectDisposed_Throws_ObjectDisposedException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.Dispose();
                sut.InsertOrUpdate(new MockRow());
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void InsertOrUpdate_NullRecord_Throws_ArgumentNullException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.InsertOrUpdate(null!);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region NextSequence overloads

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        [SuppressMessage("Major Code Smell", "S3966:Objects should not be disposed more than once", Justification = "Testing disposed behaviour")]
        public void NextSequence_WithIncrement_ObjectDisposed_Throws_ObjectDisposedException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.Dispose();
                _ = sut.NextSequence(5L);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void NextSequence_WithIncrement_IncreasesByGivenAmount()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                long seq = sut.NextSequence(10L);
                Assert.AreEqual(9L, seq); // starts at -1, -1 + 10 = 9
                Assert.AreEqual(9L, sut.PrimarySequence);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        [SuppressMessage("Major Code Smell", "S3966:Objects should not be disposed more than once", Justification = "Testing disposed behaviour")]
        public void NextSecondarySequence_ObjectDisposed_Throws_ObjectDisposedException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                sut.Dispose();
                _ = sut.NextSecondarySequence(1L);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void NextSecondarySequence_WithIncrement_IncreasesByGivenAmount()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                using SimpleDBOperations<MockRow> sut = new(CreateTestInitializer(directory), new ForeignKeyManager());
                long seq = sut.NextSecondarySequence(5L);
                Assert.AreEqual(-1L + 5L, seq);
                Assert.AreEqual(4L, sut.SecondarySequence);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region Initialize

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Initialize_NullPluginClassesService_Throws_ArgumentNullException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                SimpleDBManager manager = CreateTestInitializer(directory);
                using SimpleDBOperations<MockRow> sut = new(manager, new ForeignKeyManager());
                sut.Initialize(null);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Initialize_CalledTwice_OnlyProcessesOnce()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                SimpleDBManager manager = CreateTestInitializer(directory);
                MockTriggers<MockRow> triggers = new();
                MockPluginClassesService pluginService = new([triggers]);

                using SimpleDBOperations<MockRow> sut = new(manager, new ForeignKeyManager());
                sut.Initialize(pluginService);
                sut.Insert(new MockRow());
                int countAfterFirst = triggers.BeforeInsertCallCount;

                sut.Initialize(pluginService); // second call should be a no-op
                sut.Insert(new MockRow());
                // BeforeInsert should have fired only once more (not twice)
                Assert.AreEqual(countAfterFirst + 1, triggers.BeforeInsertCallCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Initialize_WithTableDefaults_InsertsInitialData()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                MockTableDefaults<MockRow> defaults = new(
                    primarySequence: 0,
                    secondarySequence: -1,
                    version: 0,
                    initialData: version => version == 1 ? [new MockRow(), new MockRow()] : null
                );

                MockPluginClassesService pluginService = new([defaults]);

                using SimpleDBOperations<MockRow> sut = new(manager, keyManager);
                sut.Initialize(pluginService);

                Assert.AreEqual(2, sut.RecordCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region Triggers

        [TestMethod]
        public void Insert_Single_FiresBeforeAndAfterInsertTriggers()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                MockTriggers<MockRow> triggers = new();
                MockPluginClassesService pluginService = new([triggers]);

                using SimpleDBOperations<MockRow> sut = new(manager, new ForeignKeyManager());
                sut.Initialize(pluginService);
                sut.Insert(new MockRow());

                Assert.AreEqual(1, triggers.BeforeInsertCallCount);
                Assert.AreEqual(1, triggers.AfterInsertCallCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Delete_Single_FiresBeforeAndAfterDeleteTriggers()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();
                MockTriggers<MockRow> triggers = new();
                MockPluginClassesService pluginService = new([triggers]);

                using SimpleDBOperations<MockRow> sut = new(manager, keyManager);
                sut.Initialize(pluginService);
                sut.Insert(new MockRow());
                sut.Delete(sut.Select(0));

                Assert.AreEqual(1, triggers.BeforeDeleteCallCount);
                Assert.AreEqual(1, triggers.AfterDeleteCallCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Update_Single_FiresBeforeAndAfterUpdateTriggers()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();
                MockTriggers<MockUpdateRow> triggers = new();
                MockPluginClassesService pluginService = new([triggers]);

                using SimpleDBOperations<MockUpdateRow> sut = new(manager, keyManager);
                sut.Initialize(pluginService);
                sut.Insert(new MockUpdateRow());

                MockUpdateRow row = sut.Select(0);
                row.HasChanged = true;
                sut.Update(row);

                Assert.AreEqual(1, triggers.BeforeUpdateCallCount);
                Assert.AreEqual(1, triggers.BeforeUpdateCompareCallCount);
                Assert.AreEqual(1, triggers.AfterUpdateCallCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Insert_List_FiresBeforeAndAfterInsertTriggers()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                MockTriggers<MockRow> triggers = new();
                MockPluginClassesService pluginService = new([triggers]);

                using SimpleDBOperations<MockRow> sut = new(manager, new ForeignKeyManager());
                sut.Initialize(pluginService);
                sut.Insert([new MockRow(), new MockRow(), new MockRow()]);

                Assert.AreEqual(1, triggers.BeforeInsertCallCount);
                Assert.AreEqual(1, triggers.AfterInsertCallCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Delete_List_FiresBeforeAndAfterDeleteTriggers()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();
                MockTriggers<MockRow> triggers = new();
                MockPluginClassesService pluginService = new([triggers]);

                using SimpleDBOperations<MockRow> sut = new(manager, keyManager);
                sut.Initialize(pluginService);
                sut.Insert([new MockRow(), new MockRow()]);

                IReadOnlyList<MockRow> all = sut.Select();
                sut.Delete([all[0], all[1]]);

                Assert.AreEqual(1, triggers.BeforeDeleteCallCount);
                Assert.AreEqual(1, triggers.AfterDeleteCallCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Update_List_FiresBeforeAndAfterUpdateTriggers()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();
                MockTriggers<MockUpdateRow> triggers = new();
                MockPluginClassesService pluginService = new([triggers]);

                using SimpleDBOperations<MockUpdateRow> sut = new(manager, keyManager);
                sut.Initialize(pluginService);
                sut.Insert([new MockUpdateRow(), new MockUpdateRow()]);

                IReadOnlyList<MockUpdateRow> all = sut.Select();
                all[0].HasChanged = true;
                all[1].HasChanged = true;
                sut.Update([all[0], all[1]]);

                Assert.AreEqual(1, triggers.BeforeUpdateCallCount);
                Assert.AreEqual(2, triggers.BeforeUpdateCompareCallCount);
                Assert.AreEqual(1, triggers.AfterUpdateCallCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region Foreign key violations

        [TestMethod]
        [ExpectedException(typeof(ForeignKeyException))]
        public void Insert_ForeignKeyViolation_Throws_ForeignKeyException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                // Create user table (foreign key target)
                using SimpleDBOperations<MockTableUserRow> users = new(manager, keyManager);

                // Insert address with a UserId that does not exist
                using SimpleDBOperations<MockTableAddressRow> addresses = new(manager, keyManager);
                addresses.Insert(new MockTableAddressRow() { UserId = 999, Description = "orphan" });
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ForeignKeyException))]
        public void Delete_ForeignKeyReferenced_Throws_ForeignKeyException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                using SimpleDBOperations<MockTableUserRow> users = new(manager, keyManager);
                using SimpleDBOperations<MockTableAddressRow> addresses = new(manager, keyManager);

                users.Insert(new MockTableUserRow(0) { Id = 0 });
                addresses.Insert(new MockTableAddressRow() { UserId = 0, Description = "test" });

                // Deleting the user while referenced by address should throw
                users.Delete(users.Select(0));
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Insert_ForeignKey_ValidReference_Succeeds()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                using SimpleDBOperations<MockTableUserRow> users = new(manager, keyManager);
                using SimpleDBOperations<MockTableAddressRow> addresses = new(manager, keyManager);

                users.Insert(new MockTableUserRow(0) { Id = 0 });
                addresses.Insert(new MockTableAddressRow() { UserId = 0, Description = "valid" });

                Assert.AreEqual(1, addresses.RecordCount);
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ForeignKeyException))]
        public void Insert_ForeignKey_NoMatchingReference_Throws_ForeignKeyException()
        {
            // MockTableAddressRow.UserId uses [ForeignKey("MockTableUser")] with ForeignKeyAttributes.None,
            // so any value with no matching row in the user table throws ForeignKeyException.
            string directory = TestHelper.GetTestPath();
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                using SimpleDBOperations<MockTableUserRow> users = new(manager, keyManager);
                using SimpleDBOperations<MockTableAddressRow> addresses = new(manager, keyManager);

                // No users inserted; UserId = 0 does not exist and attribute is None, not DefaultValue
                addresses.Insert(new MockTableAddressRow() { UserId = 0, Description = "no match" });
            }
            finally
            {
                io.Directory.Delete(directory, true);
            }
        }

        #endregion

        #region SlidingMemory caching

        [TestMethod]
        public void SlidingMemory_DataReadableAfterTimeout()
        {
            string directory = TestHelper.GetTestPath();
            int count;
            int rowId;
            try
            {
                io.Directory.CreateDirectory(directory);
                ISimpleDBManager manager = CreateTestInitializer(directory);
                IForeignKeyManager keyManager = new ForeignKeyManager();

                SimpleDBOperations<MockRowSlidingMemory> sut = new(manager, keyManager);
                sut.Insert(new MockRowSlidingMemory() { RowId = 42 });

                // Wait for sliding memory timeout (2ms)
                Thread.Sleep(20);

                // Records should still be readable (re-loaded from disk)
                IReadOnlyList<MockRowSlidingMemory> records = sut.Select();
                count = records.Count;
                rowId = records[0].RowId;
                sut.Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(1000); // allow background thread to release file handle
            }
            finally
            {
                DeleteDirectoryWithRetry(directory, 30, 200);
            }

            Assert.AreEqual(1, count);
            Assert.AreEqual(42, rowId);
        }

        #endregion
    }
}
