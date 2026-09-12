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
 *  File: VersionedReadWriteFactoryTests.cs
 *
 *  Purpose:  Unit tests for VersionedReadWriteFactory
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimpleDB.Internal;
using SimpleDB.Readers;
using SimpleDB.Writers;

namespace SimpleDB.Tests.Internal
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class VersionedReadWriteFactoryTests
    {
        [TestMethod]
        public void GetReader_VersionZero_ReturnsTableReadVersionOne()
        {
            VersionedReadWriteFactory sut = new();

            IDataReader reader = sut.GetReader(0);

            Assert.IsInstanceOfType<TableReadVersionOne>(reader);
        }

        [TestMethod]
        public void GetReader_VersionOne_ReturnsTableReadVersionOne()
        {
            VersionedReadWriteFactory sut = new();

            IDataReader reader = sut.GetReader(1);

            Assert.IsInstanceOfType<TableReadVersionOne>(reader);
        }

        [TestMethod]
        public void GetReader_VersionTwo_ReturnsTableReadVersionTwo()
        {
            VersionedReadWriteFactory sut = new();

            IDataReader reader = sut.GetReader(2);

            Assert.IsInstanceOfType<TableReadVersionTwo>(reader);
        }

        [TestMethod]
        public void GetReader_VersionThree_ReturnsTableReadVersionThree()
        {
            VersionedReadWriteFactory sut = new();

            IDataReader reader = sut.GetReader(3);

            Assert.IsInstanceOfType<TableReadVersionThree>(reader);
        }

        [TestMethod]
        public void GetReader_UnknownVersion_ThrowsArgumentException()
        {
            VersionedReadWriteFactory sut = new();

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => sut.GetReader(4));

            Assert.AreEqual("version", exception.ParamName);
        }

        [TestMethod]
        public void GetReader_MaxUShortValue_ThrowsArgumentException()
        {
            VersionedReadWriteFactory sut = new();

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => sut.GetReader(ushort.MaxValue));

            Assert.AreEqual("version", exception.ParamName);
        }

        [TestMethod]
        public void GetWriter_WhenCalled_ReturnsLatestTableWriteVersionThree()
        {
            VersionedReadWriteFactory sut = new();

            IDataWriter writer = sut.GetWriter();

            Assert.IsInstanceOfType<TableWriteVersionThree>(writer);
        }

        [TestMethod]
        public void GetWriter_CalledMultipleTimes_ReturnsNewInstanceEachTime()
        {
            VersionedReadWriteFactory sut = new();

            IDataWriter writer1 = sut.GetWriter();
            IDataWriter writer2 = sut.GetWriter();

            Assert.IsNotNull(writer1);
            Assert.IsNotNull(writer2);
            Assert.AreNotSame(writer1, writer2);
        }
    }
}
