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
 *  File: AppendOnlyDataWriterTests.cs
 *
 *  Purpose:  Unit tests for AppendOnlyDataWriter
 *
 *  Date        Name                Reason
 *  12/12/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimpleDB.Internal;
using SimpleDB.Readers;
using SimpleDB.Tests.Mocks;
using SimpleDB.Writers;

namespace SimpleDB.Tests.Writers
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public sealed class AppendOnlyDataWriterTests
    {
        private string _tempFile;

        [TestInitialize]
        public void Initialize()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dat");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }

        private static void CreateEmptyHeaderFile(string path, int recordCount)
        {
            using FileStream fileStream = new(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            fileStream.SetLength(Consts.TotalHeaderLength);
            fileStream.Seek(Consts.StartOfRecordCount, SeekOrigin.Begin);

            using BinaryWriter writer = new(fileStream, Encoding.UTF8, true);
            writer.Write((byte)CompressionType.None);
            writer.Write(recordCount);
            writer.Write(0);
            writer.Write(0);
        }

        private FileStream OpenReadWrite()
        {
            return new FileStream(_tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }

        [TestMethod]
        public void Version_ReturnsAppendTableReaderWriterId()
        {
            AppendOnlyDataWriter sut = new();

            Assert.AreEqual(Consts.AppendTableReaderWriterId, sut.Version);
        }

        [TestMethod]
        public void ClassImplements_IDataWriter_ReturnsTrue()
        {
            AppendOnlyDataWriter sut = new();

            Assert.IsInstanceOfType(sut, typeof(IDataWriter));
        }

        [TestMethod]
        public void ClassImplements_ICompactingDataWriter_ReturnsTrue()
        {
            AppendOnlyDataWriter sut = new();

            Assert.IsInstanceOfType(sut, typeof(ICompactingDataWriter));
        }

        [TestMethod]
        public void WriteData_EmptyFile_AppendsAllRecords()
        {
            CreateEmptyHeaderFile(_tempFile, 0);
            AppendOnlyDataWriter sut = new();
            List<MockRow> records = [new MockRow(1), new MockRow(2)];
            byte compactPercent = 99;
            int pageCount = 99;

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, records, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            Assert.AreEqual(0, compactPercent);
            Assert.AreEqual(-1, pageCount);
            Assert.IsTrue(new FileInfo(_tempFile).Length > Consts.TotalHeaderLength);

            AssertRecordsReadBack(records.Count);
        }

        [TestMethod]
        public void WriteData_ExistingRecordCountEqualsNewCount_DoesNotAppendAdditionalData()
        {
            CreateEmptyHeaderFile(_tempFile, 0);
            AppendOnlyDataWriter sut = new();
            List<MockRow> initialRecords = [new MockRow(1), new MockRow(2)];
            byte compactPercent = 0;
            int pageCount = 0;

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, initialRecords, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            long lengthAfterFirstWrite = new FileInfo(_tempFile).Length;

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, initialRecords, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            Assert.AreEqual(lengthAfterFirstWrite, new FileInfo(_tempFile).Length);
            AssertRecordsReadBack(initialRecords.Count);
        }

        [TestMethod]
        public void WriteData_MoreRecordsThanExisting_AppendsOnlyNewRecords()
        {
            CreateEmptyHeaderFile(_tempFile, 0);
            AppendOnlyDataWriter sut = new();
            List<MockRow> initialRecords = [new MockRow(1)];
            byte compactPercent = 0;
            int pageCount = 0;

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, initialRecords, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            List<MockRow> allRecords = [new MockRow(1), new MockRow(2), new MockRow(3)];

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, allRecords, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            AssertRecordsReadBack(allRecords.Count);
        }

        [TestMethod]
        public void WriteData_NegativeExistingRecordCount_TreatedAsZero()
        {
            CreateEmptyHeaderFile(_tempFile, -5);
            AppendOnlyDataWriter sut = new();
            List<MockRow> records = [new MockRow(1), new MockRow(2)];
            byte compactPercent = 0;
            int pageCount = 0;

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, records, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            AssertRecordsReadBack(records.Count);
        }

        [TestMethod]
        public void WriteData_NoRecordsToSave_HeaderUpdatedWithZeroCount()
        {
            CreateEmptyHeaderFile(_tempFile, 0);
            AppendOnlyDataWriter sut = new();
            List<MockRow> records = [];
            byte compactPercent = 0;
            int pageCount = 0;

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, records, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            AssertRecordsReadBack(0);
        }

        [TestMethod]
        public void WriteData_FewerRecordsThanExisting_DoesNotAppendAndHeaderReflectsSmallerCount()
        {
            CreateEmptyHeaderFile(_tempFile, 0);
            AppendOnlyDataWriter sut = new();
            List<MockRow> initialRecords = [new MockRow(1), new MockRow(2), new MockRow(3)];
            byte compactPercent = 0;
            int pageCount = 0;

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, initialRecords, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            long lengthAfterFullWrite = new FileInfo(_tempFile).Length;

            List<MockRow> fewerRecords = [new MockRow(1)];

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, fewerRecords, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            // WriteData only ever appends; it does not truncate, so the on-disk length is unchanged
            // even though the header now advertises a smaller record count.
            Assert.AreEqual(lengthAfterFullWrite, new FileInfo(_tempFile).Length);
            Assert.AreEqual(0, compactPercent);
            Assert.AreEqual(-1, pageCount);
        }

        [TestMethod]
        public void RewriteData_WhenCalled_TruncatesAndWritesRecordsFromStart()
        {
            CreateEmptyHeaderFile(_tempFile, 0);
            AppendOnlyDataWriter sut = new();
            List<MockRow> initialRecords = [new MockRow(1), new MockRow(2), new MockRow(3)];
            byte compactPercent = 0;
            int pageCount = 0;

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, initialRecords, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            List<MockRow> survivors = [new MockRow(2)];

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.RewriteData(fileStream, survivors, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            Assert.AreEqual(0, compactPercent);
            Assert.AreEqual(-1, pageCount);
            AssertRecordsReadBack(survivors.Count);
        }

        [TestMethod]
        public void RewriteData_WithNoRecords_ResultsInEmptyDataArea()
        {
            CreateEmptyHeaderFile(_tempFile, 0);
            AppendOnlyDataWriter sut = new();
            List<MockRow> initialRecords = [new MockRow(1), new MockRow(2)];
            byte compactPercent = 0;
            int pageCount = 0;

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, initialRecords, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            List<MockRow> emptyRecords = [];

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.RewriteData(fileStream, emptyRecords, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            Assert.AreEqual(Consts.TotalHeaderLength, new FileInfo(_tempFile).Length);
            AssertRecordsReadBack(0);
        }

        [TestMethod]
        public void RewriteData_AfterShrinkingRecordSet_DoesNotLeaveObsoleteBytes()
        {
            CreateEmptyHeaderFile(_tempFile, 0);
            AppendOnlyDataWriter sut = new();
            List<MockRow> manyLargeRecords = [];
            for (int i = 0; i < 10; i++)
                manyLargeRecords.Add(new MockRow(i));

            byte compactPercent = 0;
            int pageCount = 0;

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.WriteData(fileStream, manyLargeRecords, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            long lengthAfterFullWrite = new FileInfo(_tempFile).Length;

            List<MockRow> survivor = [new MockRow(0)];

            using (FileStream fileStream = OpenReadWrite())
            {
                sut.RewriteData(fileStream, survivor, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCount);
            }

            Assert.IsTrue(new FileInfo(_tempFile).Length < lengthAfterFullWrite);
            AssertRecordsReadBack(survivor.Count);
        }

        private void AssertRecordsReadBack(int expectedCount)
        {
            AppendOnlyDataReader reader = new();
            int pageCount = 0;
            int recordCount = 0;
            int dataLength = 0;

            using FileStream fileStream = OpenReadWrite();
            List<MockRow> result = reader.ReadRecords<MockRow>(fileStream, ref pageCount, ref recordCount, ref dataLength);

            Assert.AreEqual(expectedCount, result.Count);
        }
    }
}
