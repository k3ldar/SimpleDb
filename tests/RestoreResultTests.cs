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
 *  File: RestoreResultTests.cs
 *
 *  Purpose:  Unit tests for RestoreResult
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SimpleDB.Tests
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class RestoreResultTests
    {
        [TestMethod]
        public void Construct_Default_FileCountIsZero()
        {
            RestoreResult sut = new();

            Assert.AreEqual(0, sut.FileCount);
        }

        [TestMethod]
        public void Construct_Default_SafetyBackupFilePathIsNull()
        {
            RestoreResult sut = new();

            Assert.IsNull(sut.SafetyBackupFilePath);
        }

        [TestMethod]
        public void Construct_Default_WarningsIsEmpty()
        {
            RestoreResult sut = new();

            Assert.IsNotNull(sut.Warnings);
            Assert.AreEqual(0, sut.Warnings.Count);
        }

        [TestMethod]
        public void FileCount_SetPositiveValue_ReturnsValue()
        {
            RestoreResult sut = new()
            {
                FileCount = 42
            };

            Assert.AreEqual(42, sut.FileCount);
        }

        [TestMethod]
        public void FileCount_SetZero_ReturnsZero()
        {
            RestoreResult sut = new()
            {
                FileCount = 0
            };

            Assert.AreEqual(0, sut.FileCount);
        }

        [TestMethod]
        public void FileCount_SetNegativeValue_ReturnsNegativeValue()
        {
            RestoreResult sut = new()
            {
                FileCount = -1
            };

            Assert.AreEqual(-1, sut.FileCount);
        }

        [TestMethod]
        public void SafetyBackupFilePath_SetValue_ReturnsValue()
        {
            RestoreResult sut = new()
            {
                SafetyBackupFilePath = @"C:\backup\file.bak"
            };

            Assert.AreEqual(@"C:\backup\file.bak", sut.SafetyBackupFilePath);
        }

        [TestMethod]
        public void SafetyBackupFilePath_SetNull_ReturnsNull()
        {
            RestoreResult sut = new()
            {
                SafetyBackupFilePath = "somepath"
            };

            sut.SafetyBackupFilePath = null;

            Assert.IsNull(sut.SafetyBackupFilePath);
        }

        [TestMethod]
        public void SafetyBackupFilePath_SetEmptyString_ReturnsEmptyString()
        {
            RestoreResult sut = new()
            {
                SafetyBackupFilePath = string.Empty
            };

            Assert.AreEqual(string.Empty, sut.SafetyBackupFilePath);
        }

        [TestMethod]
        public void Warnings_SetEmptyList_ReturnsEmptyList()
        {
            List<string> warnings = [];
            RestoreResult sut = new()
            {
                Warnings = warnings
            };

            Assert.AreEqual(0, sut.Warnings.Count);
        }

        [TestMethod]
        public void Warnings_SetPopulatedList_ReturnsSameItems()
        {
            List<string> warnings = ["Table 'Orders' missing from backup", "Table 'Legacy' not registered"];
            RestoreResult sut = new()
            {
                Warnings = warnings
            };

            Assert.AreEqual(2, sut.Warnings.Count);
            Assert.AreEqual("Table 'Orders' missing from backup", sut.Warnings[0]);
            Assert.AreEqual("Table 'Legacy' not registered", sut.Warnings[1]);
        }

        [TestMethod]
        public void Warnings_SetNull_ReturnsNull()
        {
            RestoreResult sut = new()
            {
                Warnings = null
            };

            Assert.IsNull(sut.Warnings);
        }
    }
}
