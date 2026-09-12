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
 *  File: TextTableInitializerTests.cs
 *
 *  Purpose:  TextTableInitializerTests tests for SimpleDB
 *
 *  Date        Name                Reason
 *  30/05/2022  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using PluginManager.Abstractions;

using Shared.Classes;

using SharedPluginFeatures;

using SimpleDb.Tests;

using SimpleDB.Internal;
using SimpleDB.Internal.Tables;
using SimpleDB.Tests.Mocks;

#pragma warning disable CA1806

namespace SimpleDB.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class SimpleDbManagerTests : BaseTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullPath_Throws_ArgumentNullException()
        {
            try
            {
                new SimpleDBManager(path: null);
            }
            catch (ArgumentNullException e)
            {
                Assert.AreEqual("path", e.ParamName);
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_EmptyStringPath_Throws_ArgumentNullException()
        {
            try
            {
                new SimpleDBManager("");
            }
            catch (ArgumentNullException)
            {
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(DirectoryNotFoundException))]
        public void Construct_DirectoryDoesNotExists_Throws_DirectoryNotFoundException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                new SimpleDBManager(directory);
            }
            catch (DirectoryNotFoundException e)
            {
                Assert.AreEqual($"Path does not exist: {directory}", e.Message);
                throw;
            }
        }

        [TestMethod]
        public void Construct_ValidInstance_Success()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);
                Assert.AreEqual(1u, sut.MinimumVersion);
                Assert.AreEqual(directory, sut.Path);
            }
            finally
            {
                Directory.Delete(directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RegisterTable_InvalidParam_Null_Throws_ArgumentNullException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);
                Assert.AreEqual(1u, sut.MinimumVersion);
                Assert.AreEqual(directory, sut.Path);

                sut.RegisterTable(null);
            }
            finally
            {
                Directory.Delete(directory);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RegisterTable_TableAlreadyRegistered_Throws_InvalidOperationException()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);
                Assert.AreEqual(1u, sut.MinimumVersion);
                Assert.AreEqual(directory, sut.Path);

                IWatermarkAccessor watermarkAccessor = null;
                using (SimpleDBOperations<MockRow> mockTable = base.CreateTable<MockRow>(sut, new ForeignKeyManager(), ref walTableAccessor, ref watermarkAccessor))
                    sut.RegisterTable(mockTable);
            }
            finally
            {
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void RegisterTable_TableRegistered_Success()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);
                Assert.AreEqual(1u, sut.MinimumVersion);
                Assert.AreEqual(directory, sut.Path);

                IWatermarkAccessor watermarkAccessor = null;
                using (ISimpleDBOperations<MockRow> mockTable = base.CreateTable<MockRow>(sut, new ForeignKeyManager(), ref walTableAccessor, ref watermarkAccessor))
                {
                    IReadOnlyDictionary<string, ISimpleDBTable> tables = sut.Tables;
                    Assert.AreEqual(2, tables.Count);
                    Assert.IsTrue(tables.ContainsKey("MockTable"));
                }
            }
            finally
            {
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void RegisterAndUnregisterTable_WithForeignKeyManager_Success()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);
                Assert.AreEqual(1u, sut.MinimumVersion);
                Assert.AreEqual(directory, sut.Path);
                MockForeignKeyManager foreignKeyManager = new();
                Assert.AreEqual(0, foreignKeyManager.RegisteredTables.Count);

                IWatermarkAccessor watermarkAccessor = null;
                using (ISimpleDBOperations<MockRow> mockTable = base.CreateTable<MockRow>(sut, foreignKeyManager, ref walTableAccessor, ref watermarkAccessor))
                {
                    sut.Initialize(new MockPluginClassesService());
                    Assert.AreEqual(1, foreignKeyManager.RegisteredTables.Count);
                    Assert.IsTrue(foreignKeyManager.RegisteredTables.Contains("MockTable"));

                    IReadOnlyDictionary<string, ISimpleDBTable> tables = sut.Tables;
                    Assert.AreEqual(2, tables.Count);
                    Assert.IsTrue(tables.ContainsKey("MockTable"));
                }

                Assert.AreEqual(0, foreignKeyManager.RegisteredTables.Count);
            }
            finally
            {

                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                Directory.Delete(directory, true);
            }
        }

        #region Construct via ISettingsProvider

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullSettingsProvider_Throws_ArgumentNullException()
        {
            try
            {
                _ = new SimpleDBManager((ISettingsProvider)null);
            }
            catch (ArgumentNullException e)
            {
                Assert.AreEqual("settingsProvider", e.ParamName);
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(AppSettings.SettingException))]
        public void Construct_SettingsProviderReturnsSettingsWithNoPath_Throws_SettingException()
        {
            MockSettingsProvider settingsProvider = new("{}");

            _ = new SimpleDBManager(settingsProvider);
        }

        [TestMethod]
        public void Construct_SettingsProviderReturnsValidSettings_Success()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                string json = $$"""{"SimpleDBSettings": {"Path": "{{directory.Replace("\\", "\\\\")}}"} }""";
                MockSettingsProvider settingsProvider = new(json);

                SimpleDBManager sut = new(settingsProvider);

                Assert.AreEqual(directory, sut.Path);
                Assert.AreEqual(1u, sut.MinimumVersion);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        #endregion Construct via ISettingsProvider

        #region Construct via path/encryptionKey/settings

        [TestMethod]
        public void Construct_PathWithExplicitEncryptionKeyAndSettings_PathAndKeyOverrideSettings()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBSettings settings = new() { Path = "ignored", EnycryptionKey = "ignored-key" };

                SimpleDBManager sut = new(directory, "explicit-key", settings);

                Assert.AreEqual(directory, sut.Path);
                Assert.AreEqual("explicit-key", sut.EncryptionKey);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Construct_PathWithNullEncryptionKeyAndSettings_UsesSettingsEncryptionKey()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBSettings settings = new() { Path = "ignored", EnycryptionKey = "settings-key" };

                SimpleDBManager sut = new(directory, null, settings);

                Assert.AreEqual(directory, sut.Path);
                Assert.AreEqual("settings-key", sut.EncryptionKey);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        #endregion Construct via path/encryptionKey/settings

        #region MinimumVersion

        [TestMethod]
        public void MinimumVersion_SetBelowDefault_ClampsToDefault()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory)
                {
                    MinimumVersion = 0
                };

                Assert.AreEqual(SimpleDBManager.DefaultMinimumVersion, sut.MinimumVersion);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void MinimumVersion_SetAboveDefault_SetsValue()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory)
                {
                    MinimumVersion = 5
                };

                Assert.AreEqual(5u, sut.MinimumVersion);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        #endregion MinimumVersion

        #region ClearMemory

        [TestMethod]
        public void ClearMemory_RegisteredTable_CallsClearAllMemoryOnTable()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);

                IWatermarkAccessor watermarkAccessor = null;
                using SimpleDBOperations<MockRow> mockTable = base.CreateTable<MockRow>(sut, new ForeignKeyManager(), ref walTableAccessor, ref watermarkAccessor);
                mockTable.Insert(new MockRow(1));

                sut.ClearMemory();

                // Data must still be readable, reloaded transparently from disk after the cache was cleared.
                IReadOnlyList<MockRow> rows = mockTable.Select();
                Assert.AreEqual(1, rows.Count);
            }
            finally
            {
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                Directory.Delete(directory, true);
            }
        }

        #endregion ClearMemory

        #region UnregisterTable

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void UnregisterTable_NullParam_Throws_ArgumentNullException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);

                sut.UnregisterTable(null);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void UnregisterTable_EmptyTableName_ReturnsWithoutThrowing()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);
                StubSimpleDBTable stubTable = new(String.Empty);

                sut.UnregisterTable(stubTable);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void UnregisterTable_TableNotRegistered_Throws_ArgumentException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);
                StubSimpleDBTable stubTable = new("NeverRegistered");

                sut.UnregisterTable(stubTable);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void UnregisterTable_SlidingMemoryTable_RemovesTableAndUnsubscribesFromOnAction()
        {
            bool initialised = false;

            if (!ThreadManager.IsInitialized)
            {
                ThreadManager.Initialise();
                initialised = true;
            }

            try
            {
                IWalTableAccessor walTableAccessor = null;
                string directory = TestHelper.GetTestPath();
                try
                {
                    Directory.CreateDirectory(directory);
                    SimpleDBManager sut = new(directory);

                    IWatermarkAccessor watermarkAccessor = null;

                    using (SimpleDBOperations<MockRowSlidingMemory> mockTable = base.CreateTable<MockRowSlidingMemory>(sut, new ForeignKeyManager(), ref walTableAccessor, ref watermarkAccessor))
                    {
                        Assert.IsTrue(sut.Tables.ContainsKey("MockTableSlidingMemory"));
                    }

                    Assert.IsFalse(sut.Tables.ContainsKey("MockTableSlidingMemory"));
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(1000); // allow background thread to release file handle
                }
                finally
                {
                    SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                    baseWal?.CloseForMaintenance();
                    DeleteDirectoryWithRetries(directory);
                }
            }
            finally
            {
                if (initialised)
                    ThreadManager.Finalise();
            }
        }

        #endregion UnregisterTable

        #region Tables

        [TestMethod]
        public void Tables_Get_ReturnsSnapshotNotLiveDictionary()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);

                IWatermarkAccessor watermarkAccessor = null;
                SimpleDBOperations<MockRow> mockTable = base.CreateTable<MockRow>(sut, new ForeignKeyManager(), ref walTableAccessor, ref watermarkAccessor);

                IReadOnlyDictionary<string, ISimpleDBTable> firstSnapshot = sut.Tables;

                sut.UnregisterTable(mockTable);

                Assert.IsTrue(firstSnapshot.ContainsKey("MockTable"));
                Assert.IsFalse(sut.Tables.ContainsKey("MockTable"));

                mockTable.CloseForMaintenance();
            }
            finally
            {
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                Directory.Delete(directory, true);
            }
        }

        #endregion Tables

        #region Backup / Restore

        [TestMethod]
        public void BackupDatabase_ThenRestoreDatabase_RoundTripsDataAndResetsTransactionSequence()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            SimpleDBOperations<MockRow> table = null;
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager manager = new(directory);
                IWatermarkAccessor watermarkAccessor = null;
                table = base.CreateTable<MockRow>(manager, new ForeignKeyManager(), ref walTableAccessor, ref watermarkAccessor);
                TransactionManager transactionManager = new(walTableAccessor, watermarkAccessor, new StubTransactionSequenceAccessor(), manager);

                table.Insert(new MockRow(1));
                table.Insert(new MockRow(2));

                List<BackupProgressEventArgs> backupProgress = [];
                BackupResult backupResult = manager.BackupDatabase(new BackupOptions
                {
                    ProgressCallback = backupProgress.Add
                });

                Assert.IsTrue(File.Exists(backupResult.BackupFilePath));
                Assert.IsTrue(backupResult.FileCount > 0);
                Assert.IsTrue(backupResult.BackupSizeBytes > 0);
                Assert.IsTrue(backupProgress.Count > 0);

                table.Insert(new MockRow(3));
                Assert.AreEqual(3, table.Select().Count);

                List<BackupProgressEventArgs> restoreProgress = [];
                RestoreResult restoreResult = manager.RestoreDatabase(backupResult.BackupFilePath, new RestoreOptions
                {
                    CreateSafetyBackup = true,
                    ProgressCallback = restoreProgress.Add
                });

                Assert.AreEqual(backupResult.FileCount, restoreResult.FileCount);
                Assert.IsFalse(String.IsNullOrEmpty(restoreResult.SafetyBackupFilePath));
                Assert.IsTrue(File.Exists(restoreResult.SafetyBackupFilePath));
                Assert.IsTrue(restoreProgress.Count > 0);

                IReadOnlyList<MockRow> rows = table.Select();
                Assert.AreEqual(2, rows.Count);
                Assert.AreEqual(0, transactionManager.NextTransactionId);
            }
            finally
            {
                table?.CloseForMaintenance();
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void BackupDatabase_CustomPathAndFileName_UsesProvidedValues()
        {
            string directory = TestHelper.GetTestPath();
            string customBackupFolder = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);

                BackupResult result = sut.BackupDatabase(new BackupOptions
                {
                    Path = customBackupFolder,
                    FileName = "custom-backup.sdbbak"
                });

                Assert.AreEqual(System.IO.Path.Combine(customBackupFolder, "custom-backup.sdbbak"), result.BackupFilePath);
                Assert.IsTrue(File.Exists(result.BackupFilePath));
            }
            finally
            {
                Directory.Delete(directory, true);
                if (Directory.Exists(customBackupFolder))
                    Directory.Delete(customBackupFolder, true);
            }
        }

        [TestMethod]
        public void BackupDatabase_ProgressCallbackThrows_ExceptionIsSwallowed()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);

                BackupResult result = sut.BackupDatabase(new BackupOptions
                {
                    ProgressCallback = _ => throw new InvalidOperationException("boom")
                });

                Assert.IsTrue(File.Exists(result.BackupFilePath));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RestoreDatabase_NullBackupFilePath_Throws_ArgumentNullException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);

                sut.RestoreDatabase(null);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void RestoreDatabase_BackupFileDoesNotExist_Throws_FileNotFoundException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);

                sut.RestoreDatabase(System.IO.Path.Combine(directory, "does-not-exist.sdbbak"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidDataException))]
        public void RestoreDatabase_FileIsNotRecognisedBackup_Throws_InvalidDataException()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);

                string badBackupPath = System.IO.Path.Combine(directory, "bad.sdbbak");

                using (FileStream fileStream = File.Create(badBackupPath))
                using (System.IO.Compression.BrotliStream brotli = new(fileStream, System.IO.Compression.CompressionLevel.Optimal))
                using (BinaryWriter writer = new(brotli, System.Text.Encoding.UTF8))
                {
                    writer.Write("NOT-A-BACKUP");
                    writer.Write(DateTime.UtcNow.Ticks);
                    writer.Write(0L);
                    writer.Write(0);
                }

                sut.RestoreDatabase(badBackupPath);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void BackupDatabase_ActiveTransactionWithoutForceClose_Throws_InvalidOperationException()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            SimpleDBOperations<MockRow> table = null;
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager manager = new(directory);
                IWatermarkAccessor watermarkAccessor = null;
                table = base.CreateTable<MockRow>(manager, new ForeignKeyManager(), ref walTableAccessor, ref watermarkAccessor);
                TransactionManager transactionManager = new(walTableAccessor, watermarkAccessor, new StubTransactionSequenceAccessor(), manager);

                ITransaction transaction = transactionManager.BeginTransaction();
                try
                {
                    manager.BackupDatabase();
                }
                finally
                {
                    transaction.Rollback();
                }
            }
            finally
            {
                table?.CloseForMaintenance();
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void BackupDatabase_ActiveTransactionWithForceClose_ClosesTransactionAndSucceeds()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            SimpleDBOperations<MockRow> table = null;
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager manager = new(directory);
                IWatermarkAccessor watermarkAccessor = null;
                table = base.CreateTable<MockRow>(manager, new ForeignKeyManager(), ref walTableAccessor, ref watermarkAccessor);
                TransactionManager transactionManager = new(walTableAccessor, watermarkAccessor, new StubTransactionSequenceAccessor(), manager);

                transactionManager.BeginTransaction();
                Assert.AreEqual(1, transactionManager.ActiveTransactionCount);

                BackupResult result = manager.BackupDatabase(new BackupOptions { ForceCloseTransactions = true });

                Assert.IsTrue(File.Exists(result.BackupFilePath));
                Assert.AreEqual(0, transactionManager.ActiveTransactionCount);
            }
            finally
            {
                table?.CloseForMaintenance();
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                Directory.Delete(directory, true);
            }
        }

        #endregion Backup / Restore

        #region AttachTransactionManager

        [TestMethod]
        public void AttachTransactionManager_SetsTransactionManagerProperty()
        {
            IWalTableAccessor walTableAccessor = null;
            string directory = TestHelper.GetTestPath();
            SimpleDBOperations<MockRow> table = null;
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager manager = new(directory);
                IWatermarkAccessor watermarkAccessor = null;
                table = base.CreateTable<MockRow>(manager, new ForeignKeyManager(), ref walTableAccessor, ref watermarkAccessor);
                TransactionManager transactionManager = new(walTableAccessor, watermarkAccessor, new StubTransactionSequenceAccessor(), manager);

                Assert.AreSame(transactionManager, manager.TransactionManager);
            }
            finally
            {
                table?.CloseForMaintenance();
                SimpleDBOperations<WalEntryDataRow> baseWal = walTableAccessor?.WalTable as SimpleDBOperations<WalEntryDataRow>;
                baseWal?.CloseForMaintenance();
                Directory.Delete(directory, true);
            }
        }

        #endregion AttachTransactionManager

        #region IDatabaseLock

        [TestMethod]
        public void AcquireForOperation_NoContention_ReturnsDisposableScope()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);

                using (IDisposable scope = ((IDatabaseLock)sut).AcquireForOperation())
                {
                    Assert.IsNotNull(scope);
                }
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void TryAcquireForCheckpoint_NoContention_ReturnsDisposableScope()
        {
            string directory = TestHelper.GetTestPath();
            try
            {
                Directory.CreateDirectory(directory);
                SimpleDBManager sut = new(directory);

                using (IDisposable scope = ((IDatabaseLock)sut).TryAcquireForCheckpoint())
                {
                    Assert.IsNotNull(scope);
                }
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        #endregion IDatabaseLock

        private static void DeleteDirectoryWithRetries(string directory)
        {
            const int maxAttempts = 10;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    Directory.Delete(directory, true);
                    return;
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    // A background thread (e.g. sliding memory cache clearing) may still be
                    // releasing a file handle; briefly retry rather than failing the test.
                    Thread.Sleep(200);
                }
            }
        }

        [ExcludeFromCodeCoverage]
        private sealed class StubSimpleDBTable : ISimpleDBTable
        {
            public StubSimpleDBTable(string tableName)
            {
                TableName = tableName;
            }

            public string TableName { get; }

            public CachingStrategy CachingStrategy => CachingStrategy.Memory;

            public WriteStrategy WriteStrategy => WriteStrategy.Forced;

            public TimeSpan SlidingMemoryTimeout => TimeSpan.Zero;

            public Dictionary<string, Timings> GetAllTimings => [];

            public long LogicalDataSizeBytes => 0;

            public long PhysicalDataSizeBytes => 0;

            public long InMemoryCacheSizeBytes => 0;

            public int RecordCount => 0;

            public event SimpleDbEvent OnAction { add { } remove { } }

            public bool IdExists(long id) => false;

            public bool IdIsInUse(string propertyName, long value) => false;

            public void Initialize(IPluginClassesService pluginClassesService)
            {
            }

            public void ClearAllMemory()
            {
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

#pragma warning restore CA1806