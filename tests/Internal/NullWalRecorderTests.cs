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
 *  File: NullWalRecorderTests.cs
 *
 *  Purpose:  Unit tests for NullWalRecorder
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SharedPluginFeatures;

using SimpleDB.Internal;

namespace SimpleDB.Tests.Internal
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class NullWalRecorderTests
    {
        [TestMethod]
        public void GetAllTimings_WhenCalled_ReturnsEmptyDictionary()
        {
            NullWalRecorder sut = new();

            Dictionary<string, Timings> result = sut.GetAllTimings;

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void GetAllTimings_CalledMultipleTimes_ReturnsNewInstanceEachTime()
        {
            NullWalRecorder sut = new();

            Dictionary<string, Timings> first = sut.GetAllTimings;
            Dictionary<string, Timings> second = sut.GetAllTimings;

            Assert.AreNotSame(first, second);
        }

        [TestMethod]
        public void Record_AllArgumentsNull_DoesNotThrow()
        {
            NullWalRecorder sut = new();

            sut.Record(null, null, OperationType.Insert, 0L, null, 0L, null);
        }

        [TestMethod]
        public void Record_ValidArguments_DoesNotThrow()
        {
            NullWalRecorder sut = new();

            sut.Record(null, "TableName", OperationType.Update, 123L, [1, 2, 3], 456L, [4, 5, 6]);
        }

        [TestMethod]
        public void Record_WhenCalled_DoesNotAffectGetAllTimings()
        {
            NullWalRecorder sut = new();

            sut.Record(null, "TableName", OperationType.Delete, 1L, null, 0L, null);

            Assert.AreEqual(0, sut.GetAllTimings.Count);
        }

        [TestMethod]
        public void Construct_ValidInstance_ImplementsIWalRecorder()
        {
            NullWalRecorder sut = new();

            Assert.IsInstanceOfType<IWalRecorder>(sut);
        }
    }
}
