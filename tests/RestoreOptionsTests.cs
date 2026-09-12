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
 *  File: RestoreOptionsTests.cs
 *
 *  Purpose:  Unit tests for RestoreOptions
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SimpleDB.Tests
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class RestoreOptionsTests
    {
        [TestMethod]
        public void Construct_Default_ForceCloseTransactionsIsFalse()
        {
            RestoreOptions sut = new();
            Assert.IsFalse(sut.ForceCloseTransactions);
        }

        [TestMethod]
        public void Construct_Default_CreateSafetyBackupIsTrue()
        {
            RestoreOptions sut = new();
            Assert.IsTrue(sut.CreateSafetyBackup);
        }

        [TestMethod]
        public void Construct_Default_ProgressCallbackIsNull()
        {
            RestoreOptions sut = new();
            Assert.IsNull(sut.ProgressCallback);
        }

        [TestMethod]
        public void ForceCloseTransactions_SetTrue_ReturnsTrue()
        {
            RestoreOptions sut = new() { ForceCloseTransactions = true };
            Assert.IsTrue(sut.ForceCloseTransactions);
        }

        [TestMethod]
        public void ForceCloseTransactions_SetFalse_ReturnsFalse()
        {
            RestoreOptions sut = new() { ForceCloseTransactions = false };
            Assert.IsFalse(sut.ForceCloseTransactions);
        }

        [TestMethod]
        public void CreateSafetyBackup_SetFalse_ReturnsFalse()
        {
            RestoreOptions sut = new() { CreateSafetyBackup = false };
            Assert.IsFalse(sut.CreateSafetyBackup);
        }

        [TestMethod]
        public void CreateSafetyBackup_SetTrue_ReturnsTrue()
        {
            RestoreOptions sut = new() { CreateSafetyBackup = true };
            Assert.IsTrue(sut.CreateSafetyBackup);
        }

        [TestMethod]
        public void ProgressCallback_SetToDelegate_ReturnsSameDelegate()
        {
            bool called = false;
            void Callback(BackupProgressEventArgs args) => called = true;

            RestoreOptions sut = new() { ProgressCallback = Callback };

            Assert.IsNotNull(sut.ProgressCallback);
            sut.ProgressCallback(new BackupProgressEventArgs());
            Assert.IsTrue(called);
        }

        [TestMethod]
        public void ProgressCallback_SetToNull_ReturnsNull()
        {
            RestoreOptions sut = new() { ProgressCallback = _ => { } };
            sut.ProgressCallback = null;

            Assert.IsNull(sut.ProgressCallback);
        }

        [TestMethod]
        public void ProgressCallback_InvokedWithEventArgs_ReceivesSameInstance()
        {
            BackupProgressEventArgs received = null;
            RestoreOptions sut = new() { ProgressCallback = args => received = args };

            BackupProgressEventArgs eventArgs = new() { Stage = BackupProgressStage.Completed };
            sut.ProgressCallback(eventArgs);

            Assert.AreSame(eventArgs, received);
        }
    }
}
