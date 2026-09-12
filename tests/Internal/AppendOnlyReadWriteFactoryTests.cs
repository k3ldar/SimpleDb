using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimpleDB.Internal;
using SimpleDB.Readers;
using SimpleDB.Writers;

namespace SimpleDB.Tests.Internal
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class AppendOnlyReadWriteFactoryTests
    {
        [TestMethod]
        public void ClassImplements_IVersionedReadWriteFactory_ReturnsTrue()
        {
            // Arrange
            AppendOnlyReadWriteFactory sut = new();

            // Act & Assert
            Assert.IsInstanceOfType(sut, typeof(IVersionedReadWriteFactory));
        }

        [TestMethod]
        public void GetReader_VersionZero_ReturnsAppendOnlyDataReaderInstance()
        {
            // Arrange
            AppendOnlyReadWriteFactory sut = new();

            // Act
            IDataReader result = sut.GetReader(0);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(AppendOnlyDataReader));
        }

        [TestMethod]
        public void GetReader_MaxUshortVersion_ReturnsAppendOnlyDataReaderInstance()
        {
            // Arrange
            AppendOnlyReadWriteFactory sut = new();

            // Act
            IDataReader result = sut.GetReader(ushort.MaxValue);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(AppendOnlyDataReader));
        }

        [TestMethod]
        public void GetReader_ArbitraryVersion_ReturnsAppendOnlyDataReaderInstance()
        {
            // Arrange
            AppendOnlyReadWriteFactory sut = new();

            // Act
            IDataReader result = sut.GetReader(42);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(AppendOnlyDataReader));
        }

        [TestMethod]
        public void GetReader_CalledMultipleTimes_ReturnsNewInstanceEachTime()
        {
            // Arrange
            AppendOnlyReadWriteFactory sut = new();

            // Act
            IDataReader first = sut.GetReader(0);
            IDataReader second = sut.GetReader(0);

            // Assert
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
        }

        [TestMethod]
        public void GetReader_DifferentVersions_ReturnsSameFormatInstanceType()
        {
            // Arrange
            AppendOnlyReadWriteFactory sut = new();

            // Act
            IDataReader lowVersion = sut.GetReader(1);
            IDataReader highVersion = sut.GetReader(9999);

            // Assert
            Assert.AreEqual(lowVersion.GetType(), highVersion.GetType());
        }

        [TestMethod]
        public void GetWriter_WhenCalled_ReturnsAppendOnlyDataWriterInstance()
        {
            // Arrange
            AppendOnlyReadWriteFactory sut = new();

            // Act
            IDataWriter result = sut.GetWriter();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(AppendOnlyDataWriter));
        }

        [TestMethod]
        public void GetWriter_CalledMultipleTimes_ReturnsNewInstanceEachTime()
        {
            // Arrange
            AppendOnlyReadWriteFactory sut = new();

            // Act
            IDataWriter first = sut.GetWriter();
            IDataWriter second = sut.GetWriter();

            // Assert
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
        }
    }
}
