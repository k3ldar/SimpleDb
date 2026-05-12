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
 *  File: SortAscendingDescendingTests.cs
 *
 *  Purpose:  Unit tests for SortAscending and SortDescending
 *
 *  Date        Name                Reason
 *  05/06/2022  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimpleDB.Internal;

namespace SimpleDB.Tests
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class SortAscendingTests
    {
        [TestMethod]
        public void Compare_EqualValues_ReturnsZero()
        {
            SortAscending sut = new();
            Assert.AreEqual(0, sut.Compare(5L, 5L));
        }

        [TestMethod]
        public void Compare_EqualZeroValues_ReturnsZero()
        {
            SortAscending sut = new();
            Assert.AreEqual(0, sut.Compare(0L, 0L));
        }

        [TestMethod]
        public void Compare_XLessThanY_ReturnsNegativeOne()
        {
            SortAscending sut = new();
            Assert.AreEqual(-1, sut.Compare(1L, 2L));
        }

        [TestMethod]
        public void Compare_XGreaterThanY_ReturnsOne()
        {
            SortAscending sut = new();
            Assert.AreEqual(1, sut.Compare(2L, 1L));
        }

        [TestMethod]
        public void Compare_NegativeXLessThanNegativeY_ReturnsNegativeOne()
        {
            SortAscending sut = new();
            Assert.AreEqual(-1, sut.Compare(-10L, -5L));
        }

        [TestMethod]
        public void Compare_NegativeXGreaterThanNegativeY_ReturnsOne()
        {
            SortAscending sut = new();
            Assert.AreEqual(1, sut.Compare(-5L, -10L));
        }

        [TestMethod]
        public void Compare_NegativeXLessThanPositiveY_ReturnsNegativeOne()
        {
            SortAscending sut = new();
            Assert.AreEqual(-1, sut.Compare(-1L, 1L));
        }

        [TestMethod]
        public void Compare_MaxLongVsMinLong_ReturnsOne()
        {
            SortAscending sut = new();
            Assert.AreEqual(1, sut.Compare(long.MaxValue, long.MinValue));
        }

        [TestMethod]
        public void Compare_MinLongVsMaxLong_ReturnsNegativeOne()
        {
            SortAscending sut = new();
            Assert.AreEqual(-1, sut.Compare(long.MinValue, long.MaxValue));
        }

        [TestMethod]
        public void Sort_ListOfLongs_SortedAscending()
        {
            SortAscending sut = new();
            List<long> values = new() { 5L, 3L, 8L, 1L, 4L };

            values.Sort(sut);

            CollectionAssert.AreEqual(new List<long> { 1L, 3L, 4L, 5L, 8L }, values);
        }
    }

    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class SortDescendingTests
    {
        [TestMethod]
        public void Compare_EqualValues_ReturnsZero()
        {
            SortDescending sut = new();
            Assert.AreEqual(0, sut.Compare(5L, 5L));
        }

        [TestMethod]
        public void Compare_EqualZeroValues_ReturnsZero()
        {
            SortDescending sut = new();
            Assert.AreEqual(0, sut.Compare(0L, 0L));
        }

        [TestMethod]
        public void Compare_XLessThanY_ReturnsOne()
        {
            SortDescending sut = new();
            Assert.AreEqual(1, sut.Compare(1L, 2L));
        }

        [TestMethod]
        public void Compare_XGreaterThanY_ReturnsNegativeOne()
        {
            SortDescending sut = new();
            Assert.AreEqual(-1, sut.Compare(2L, 1L));
        }

        [TestMethod]
        public void Compare_NegativeXLessThanNegativeY_ReturnsOne()
        {
            SortDescending sut = new();
            Assert.AreEqual(1, sut.Compare(-10L, -5L));
        }

        [TestMethod]
        public void Compare_NegativeXGreaterThanNegativeY_ReturnsNegativeOne()
        {
            SortDescending sut = new();
            Assert.AreEqual(-1, sut.Compare(-5L, -10L));
        }

        [TestMethod]
        public void Compare_NegativeXLessThanPositiveY_ReturnsOne()
        {
            SortDescending sut = new();
            Assert.AreEqual(1, sut.Compare(-1L, 1L));
        }

        [TestMethod]
        public void Compare_MaxLongVsMinLong_ReturnsNegativeOne()
        {
            SortDescending sut = new();
            Assert.AreEqual(-1, sut.Compare(long.MaxValue, long.MinValue));
        }

        [TestMethod]
        public void Compare_MinLongVsMaxLong_ReturnsOne()
        {
            SortDescending sut = new();
            Assert.AreEqual(1, sut.Compare(long.MinValue, long.MaxValue));
        }

        [TestMethod]
        public void Sort_ListOfLongs_SortedDescending()
        {
            SortDescending sut = new();
            List<long> values = new() { 5L, 3L, 8L, 1L, 4L };

            values.Sort(sut);

            CollectionAssert.AreEqual(new List<long> { 8L, 5L, 4L, 3L, 1L }, values);
        }
    }
}
