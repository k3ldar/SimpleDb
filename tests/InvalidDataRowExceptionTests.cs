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
 *  File: InvalidDataRowExceptionTests.cs
 *
 *  Purpose:  Unit tests for InvalidDataRowException
 *
 *  Date        Name                Reason
 *  28/06/2022  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SimpleDB.Tests
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class InvalidDataRowExceptionTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullDataRow_Throws_ArgumentNullException()
        {
            new InvalidDataRowException(null, "property", "message");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_EmptyDataRow_Throws_ArgumentNullException()
        {
            new InvalidDataRowException("", "property", "message");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullProperty_Throws_ArgumentNullException()
        {
            new InvalidDataRowException("dataRow", null, "message");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_EmptyProperty_Throws_ArgumentNullException()
        {
            new InvalidDataRowException("dataRow", "", "message");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_NullMessage_Throws_ArgumentNullException()
        {
            new InvalidDataRowException("dataRow", "property", null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Construct_EmptyMessage_Throws_ArgumentNullException()
        {
            new InvalidDataRowException("dataRow", "property", "");
        }

        [TestMethod]
        public void Construct_ValidParams_SetsDataRowProperty()
        {
            InvalidDataRowException sut = new InvalidDataRowException("myTable", "myProperty", "some error");
            Assert.AreEqual("myTable", sut.DataRow);
        }

        [TestMethod]
        public void Construct_ValidParams_SetsPropertyProperty()
        {
            InvalidDataRowException sut = new InvalidDataRowException("myTable", "myProperty", "some error");
            Assert.AreEqual("myProperty", sut.Property);
        }

        [TestMethod]
        public void Construct_ValidParams_SetsOriginalMessage()
        {
            InvalidDataRowException sut = new InvalidDataRowException("myTable", "myProperty", "some error");
            Assert.AreEqual("some error", sut.OriginalMessage);
        }

        [TestMethod]
        public void Construct_ValidParams_MessageContainsAllParts()
        {
            InvalidDataRowException sut = new InvalidDataRowException("myTable", "myProperty", "some error");
            StringAssert.Contains(sut.Message, "some error");
            StringAssert.Contains(sut.Message, "myTable");
            StringAssert.Contains(sut.Message, "myProperty");
        }

        [TestMethod]
        public void Construct_ValidParams_IsException()
        {
            InvalidDataRowException sut = new InvalidDataRowException("myTable", "myProperty", "some error");
            Assert.IsInstanceOfType<Exception>(sut);
        }
    }
}
