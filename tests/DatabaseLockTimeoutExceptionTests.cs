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
 *  File: DatabaseLockTimeoutExceptionTests.cs
 *
 *  Purpose:  Unit tests for DatabaseLockTimeoutException
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
    public sealed class DatabaseLockTimeoutExceptionTests
    {
        [TestMethod]
        public void Construct_Parameterless_IsException()
        {
            DatabaseLockTimeoutException sut = new();
            Assert.IsInstanceOfType<Exception>(sut);
        }

        [TestMethod]
        public void Construct_Parameterless_MessageIsDefault()
        {
            DatabaseLockTimeoutException sut = new();
            StringAssert.Contains(sut.Message, "Exception");
        }

        [TestMethod]
        public void Construct_Parameterless_InnerExceptionIsNull()
        {
            DatabaseLockTimeoutException sut = new();
            Assert.IsNull(sut.InnerException);
        }

        [TestMethod]
        public void Construct_WithMessage_SetsMessage()
        {
            DatabaseLockTimeoutException sut = new("lock timed out");
            Assert.AreEqual("lock timed out", sut.Message);
        }

        [TestMethod]
        public void Construct_WithMessage_InnerExceptionIsNull()
        {
            DatabaseLockTimeoutException sut = new("lock timed out");
            Assert.IsNull(sut.InnerException);
        }

        [TestMethod]
        public void Construct_WithNullMessage_DoesNotThrow()
        {
            DatabaseLockTimeoutException sut = new(message: null);
            Assert.IsNotNull(sut);
        }

        [TestMethod]
        public void Construct_WithEmptyMessage_SetsEmptyMessage()
        {
            DatabaseLockTimeoutException sut = new(String.Empty);
            Assert.AreEqual(String.Empty, sut.Message);
        }

        [TestMethod]
        public void Construct_WithMessageAndInnerException_SetsMessage()
        {
            InvalidOperationException innerException = new("inner failure");
            DatabaseLockTimeoutException sut = new("lock timed out", innerException);
            Assert.AreEqual("lock timed out", sut.Message);
        }

        [TestMethod]
        public void Construct_WithMessageAndInnerException_SetsInnerException()
        {
            InvalidOperationException innerException = new("inner failure");
            DatabaseLockTimeoutException sut = new("lock timed out", innerException);
            Assert.AreSame(innerException, sut.InnerException);
        }

        [TestMethod]
        public void Construct_WithMessageAndNullInnerException_DoesNotThrow()
        {
            DatabaseLockTimeoutException sut = new("lock timed out", innerException: null);
            Assert.IsNull(sut.InnerException);
        }

        [TestMethod]
        public void Construct_WithNullMessageAndInnerException_DoesNotThrow()
        {
            InvalidOperationException innerException = new("inner failure");
            DatabaseLockTimeoutException sut = new(null, innerException);
            Assert.AreSame(innerException, sut.InnerException);
        }
    }
}
