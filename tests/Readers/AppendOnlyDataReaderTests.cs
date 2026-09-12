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
 *  File: AppendOnlyDataReaderTests.cs
 *
 *  Purpose:  Unit tests for AppendOnlyDataReader
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

namespace SimpleDB.Tests.Readers
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public sealed class AppendOnlyDataReaderTests
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

        private static void WriteHeader(string path, byte compressionType, int recordCount, int dataLength, long fileLength)
        {
            using FileStream fileStream = new(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            fileStream.SetLength(fileLength);
            fileStream.Seek(Consts.StartOfRecordCount, SeekOrigin.Begin);

            using BinaryWriter writer = new(fileStream, Encoding.UTF8, true);
            writer.Write(compressionType);
            writer.Write(recordCount);
            writer.Write(dataLength);
            writer.Write(dataLength);
        }

        private static void AppendRawBytes(string path, byte[] data)
        {
            using FileStream fileStream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            fileStream.Seek(0, SeekOrigin.End);
            fileStream.Write(data, 0, data.Length);
        }

        private FileStream OpenReadWrite()
        {
            return new FileStream(_tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }

        [TestMethod]
        public void Version_ReturnsAppendTableReaderWriterId()
        {
            AppendOnlyDataReader sut = new();

            Assert.AreEqual(Consts.AppendTableReaderWriterId, sut.Version);
        }

        [TestMethod]
        public void ClassImplements_IDataReader_ReturnsTrue()
        {
            AppendOnlyDataReader sut = new();

            Assert.IsInstanceOfType(sut, typeof(IDataReader));
        }

        [TestMethod]
        public void ReadRecords_DeclaredCountZero_ReturnsEmptyResult()
        {
            WriteHeader(_tempFile, (byte)CompressionType.None, 0, 0, Consts.TotalHeaderLength);
            AppendOnlyDataReader sut = new();
            int pageCount = 0;
            int recordCount = 99;
            int dataLength = 0;

            using FileStream fileStream = OpenReadWrite();
            List<MockRow> result = sut.ReadRecords<MockRow>(fileStream, ref pageCount, ref recordCount, ref dataLength);

            Assert.AreEqual(0, result.Count);
            Assert.AreEqual(0, recordCount);
            Assert.AreEqual(-1, pageCount);
        }

        [TestMethod]
        public void ReadRecords_DeclaredCountNegative_ReturnsEmptyResult()
        {
            WriteHeader(_tempFile, (byte)CompressionType.None, -3, 0, Consts.TotalHeaderLength);
            AppendOnlyDataReader sut = new();
            int pageCount = 0;
            int recordCount = 99;
            int dataLength = 0;

            using FileStream fileStream = OpenReadWrite();
            List<MockRow> result = sut.ReadRecords<MockRow>(fileStream, ref pageCount, ref recordCount, ref dataLength);

            Assert.AreEqual(0, result.Count);
            Assert.AreEqual(0, recordCount);
            Assert.AreEqual(-1, pageCount);
        }

        [TestMethod]
        public void ReadRecords_WellFormedFile_ReturnsAllRecords()
        {
            WriteHeader(_tempFile, (byte)CompressionType.None, 0, 0, Consts.TotalHeaderLength);
            AppendOnlyDataWriter writer = new();
            List<MockRow> records = [new MockRow(1), new MockRow(2), new MockRow(3)];
            byte compactPercent = 0;
            int pageCountWrite = 0;

            using (FileStream fileStream = OpenReadWrite())
            {
                writer.WriteData(fileStream, records, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCountWrite);
            }

            AppendOnlyDataReader sut = new();
            int pageCount = 0;
            int recordCount = 0;
            int dataLength = 0;

            using FileStream readStream = OpenReadWrite();
            List<MockRow> result = sut.ReadRecords<MockRow>(readStream, ref pageCount, ref recordCount, ref dataLength);

            Assert.AreEqual(records.Count, result.Count);
            Assert.AreEqual(records.Count, recordCount);
            Assert.AreEqual(-1, pageCount);
            Assert.AreEqual(1, result[0].Id);
            Assert.AreEqual(2, result[1].Id);
            Assert.AreEqual(3, result[2].Id);
        }

        [TestMethod]
        public void ReadRecords_TruncatedLengthPrefix_StopsAtLastCompleteRecord()
        {
            // Header declares 2 records but only 1 fully written record plus 2 stray bytes
            // (a partially written length prefix) follow - simulating a torn write.
            WriteHeader(_tempFile, (byte)CompressionType.None, 2, 0, Consts.TotalHeaderLength);

            byte[] recordData = JsonSerializer.SerializeToUtf8Bytes(new MockRow(1), Consts.JsonSerializerOptions);
            using (MemoryStream ms = new())
            {
                using BinaryWriter bw = new(ms, Encoding.UTF8, true);
                bw.Write(recordData.Length);
                bw.Write(recordData, 0, recordData.Length);
                bw.Write((short)0); // partial, incomplete length prefix for the second record
                AppendRawBytes(_tempFile, ms.ToArray());
            }

            AppendOnlyDataReader sut = new();
            int pageCount = 0;
            int recordCount = 99;
            int dataLength = 0;

            using FileStream fileStream = OpenReadWrite();
            List<MockRow> result = sut.ReadRecords<MockRow>(fileStream, ref pageCount, ref recordCount, ref dataLength);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, recordCount);
            Assert.AreEqual(1, result[0].Id);
        }

        [TestMethod]
        public void ReadRecords_ZeroLengthPrefix_StopsBeforeThatRecord()
        {
            WriteHeader(_tempFile, (byte)CompressionType.None, 2, 0, Consts.TotalHeaderLength);

            byte[] recordData = JsonSerializer.SerializeToUtf8Bytes(new MockRow(1), Consts.JsonSerializerOptions);
            using (MemoryStream ms = new())
            {
                using BinaryWriter bw = new(ms, Encoding.UTF8, true);
                bw.Write(recordData.Length);
                bw.Write(recordData, 0, recordData.Length);
                bw.Write(0); // zero length prefix - uninitialised / torn bytes
                AppendRawBytes(_tempFile, ms.ToArray());
            }

            AppendOnlyDataReader sut = new();
            int pageCount = 0;
            int recordCount = 99;
            int dataLength = 0;

            using FileStream fileStream = OpenReadWrite();
            List<MockRow> result = sut.ReadRecords<MockRow>(fileStream, ref pageCount, ref recordCount, ref dataLength);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, recordCount);
        }

        [TestMethod]
        public void ReadRecords_NegativeLengthPrefix_StopsBeforeThatRecord()
        {
            WriteHeader(_tempFile, (byte)CompressionType.None, 2, 0, Consts.TotalHeaderLength);

            byte[] recordData = JsonSerializer.SerializeToUtf8Bytes(new MockRow(1), Consts.JsonSerializerOptions);
            using (MemoryStream ms = new())
            {
                using BinaryWriter bw = new(ms, Encoding.UTF8, true);
                bw.Write(recordData.Length);
                bw.Write(recordData, 0, recordData.Length);
                bw.Write(-1); // negative length prefix - corrupt bytes
                AppendRawBytes(_tempFile, ms.ToArray());
            }

            AppendOnlyDataReader sut = new();
            int pageCount = 0;
            int recordCount = 99;
            int dataLength = 0;

            using FileStream fileStream = OpenReadWrite();
            List<MockRow> result = sut.ReadRecords<MockRow>(fileStream, ref pageCount, ref recordCount, ref dataLength);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, recordCount);
        }

        [TestMethod]
        public void ReadRecords_LengthPrefixExceedsRemainingFile_StopsBeforeThatRecord()
        {
            WriteHeader(_tempFile, (byte)CompressionType.None, 2, 0, Consts.TotalHeaderLength);

            byte[] recordData = JsonSerializer.SerializeToUtf8Bytes(new MockRow(1), Consts.JsonSerializerOptions);
            using (MemoryStream ms = new())
            {
                using BinaryWriter bw = new(ms, Encoding.UTF8, true);
                bw.Write(recordData.Length);
                bw.Write(recordData, 0, recordData.Length);
                // Declares far more bytes than actually follow in the file.
                bw.Write(10_000);
                bw.Write(new byte[] { 1, 2, 3 });
                AppendRawBytes(_tempFile, ms.ToArray());
            }

            AppendOnlyDataReader sut = new();
            int pageCount = 0;
            int recordCount = 99;
            int dataLength = 0;

            using FileStream fileStream = OpenReadWrite();
            List<MockRow> result = sut.ReadRecords<MockRow>(fileStream, ref pageCount, ref recordCount, ref dataLength);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, recordCount);
        }

        [TestMethod]
        public void ReadRecords_DeclaredCountExceedsRecordsPhysicallyPresent_ReturnsOnlyPhysicallyPresentRecords()
        {
            // Simulates a crash mid RewriteData: header says 5 records but the body only
            // physically contains 2 complete records and nothing more.
            WriteHeader(_tempFile, (byte)CompressionType.None, 5, 0, Consts.TotalHeaderLength);

            using (MemoryStream ms = new())
            {
                using BinaryWriter bw = new(ms, Encoding.UTF8, true);
                foreach (MockRow row in new[] { new MockRow(1), new MockRow(2) })
                {
                    byte[] recordData = JsonSerializer.SerializeToUtf8Bytes(row, Consts.JsonSerializerOptions);
                    bw.Write(recordData.Length);
                    bw.Write(recordData, 0, recordData.Length);
                }

                AppendRawBytes(_tempFile, ms.ToArray());
            }

            AppendOnlyDataReader sut = new();
            int pageCount = 0;
            int recordCount = 99;
            int dataLength = 0;

            using FileStream fileStream = OpenReadWrite();
            List<MockRow> result = sut.ReadRecords<MockRow>(fileStream, ref pageCount, ref recordCount, ref dataLength);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(2, recordCount);
        }

        [TestMethod]
        public void ReadRecords_EmptyDataAreaWithPositiveDeclaredCount_ReturnsEmptyResult()
        {
            WriteHeader(_tempFile, (byte)CompressionType.None, 3, 0, Consts.TotalHeaderLength);

            AppendOnlyDataReader sut = new();
            int pageCount = 0;
            int recordCount = 99;
            int dataLength = 0;

            using FileStream fileStream = OpenReadWrite();
            List<MockRow> result = sut.ReadRecords<MockRow>(fileStream, ref pageCount, ref recordCount, ref dataLength);

            Assert.AreEqual(0, result.Count);
            Assert.AreEqual(0, recordCount);
        }

        [TestMethod]
        public void ReadRecords_WellFormedFile_ReturnsCorrectDataLength()
        {
            WriteHeader(_tempFile, (byte)CompressionType.None, 0, 0, Consts.TotalHeaderLength);
            AppendOnlyDataWriter writer = new();
            List<MockRow> records = [new MockRow(1)];
            byte compactPercent = 0;
            int pageCountWrite = 0;

            using (FileStream fileStream = OpenReadWrite())
            {
                writer.WriteData(fileStream, records, CompressionType.None, PageSize.Size4096, ref compactPercent, ref pageCountWrite);
            }

            long expectedDataLength = new FileInfo(_tempFile).Length - Consts.TotalHeaderLength;

            AppendOnlyDataReader sut = new();
            int pageCount = 0;
            int recordCount = 0;
            int dataLength = 0;

            using FileStream readStream = OpenReadWrite();
            _ = sut.ReadRecords<MockRow>(readStream, ref pageCount, ref recordCount, ref dataLength);

            Assert.AreEqual(expectedDataLength, dataLength);
        }
    }
}
