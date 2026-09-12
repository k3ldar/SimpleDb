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
 *  File: ColumnAttributeTests.cs
 *
 *  Purpose:  Unit tests for ColumnAttribute
 *
 *  Date        Name                Reason
 *  12/12/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SimpleDB.Tests
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public sealed class ColumnAttributeTests
    {
        [TestMethod]
        public void Construct_Default_IsNullableTrue()
        {
            ColumnAttribute sut = new ColumnAttribute();

            Assert.IsTrue(sut.IsNullable);
        }

        [TestMethod]
        public void Construct_Default_NameIsNull()
        {
            ColumnAttribute sut = new ColumnAttribute();

            Assert.IsNull(sut.Name);
        }

        [TestMethod]
        public void Construct_Default_DefaultValueIsNull()
        {
            ColumnAttribute sut = new ColumnAttribute();

            Assert.IsNull(sut.DefaultValue);
        }

        [TestMethod]
        public void Construct_Default_MaxLengthIsZero()
        {
            ColumnAttribute sut = new ColumnAttribute();

            Assert.AreEqual(0, sut.MaxLength);
        }

        [TestMethod]
        public void Construct_Default_PrecisionIsZero()
        {
            ColumnAttribute sut = new ColumnAttribute();

            Assert.AreEqual(0, sut.Precision);
        }

        [TestMethod]
        public void Construct_Default_ScaleIsZero()
        {
            ColumnAttribute sut = new ColumnAttribute();

            Assert.AreEqual(0, sut.Scale);
        }

        [TestMethod]
        public void Construct_Default_IsIndexedFalse()
        {
            ColumnAttribute sut = new ColumnAttribute();

            Assert.IsFalse(sut.IsIndexed);
        }

        [TestMethod]
        public void Name_SetAndGet_Success()
        {
            ColumnAttribute sut = new ColumnAttribute
            {
                Name = "CustomName"
            };

            Assert.AreEqual("CustomName", sut.Name);
        }

        [TestMethod]
        public void MaxLength_SetAndGet_Success()
        {
            ColumnAttribute sut = new ColumnAttribute
            {
                MaxLength = 255
            };

            Assert.AreEqual(255, sut.MaxLength);
        }

        [TestMethod]
        public void Precision_SetAndGet_Success()
        {
            ColumnAttribute sut = new ColumnAttribute
            {
                Precision = 18
            };

            Assert.AreEqual(18, sut.Precision);
        }

        [TestMethod]
        public void Scale_SetAndGet_Success()
        {
            ColumnAttribute sut = new ColumnAttribute
            {
                Scale = 2
            };

            Assert.AreEqual(2, sut.Scale);
        }

        [TestMethod]
        public void IsNullable_SetFalse_Success()
        {
            ColumnAttribute sut = new ColumnAttribute
            {
                IsNullable = false
            };

            Assert.IsFalse(sut.IsNullable);
        }

        [TestMethod]
        public void DefaultValue_SetAndGet_Success()
        {
            ColumnAttribute sut = new ColumnAttribute
            {
                DefaultValue = "0"
            };

            Assert.AreEqual("0", sut.DefaultValue);
        }

        [TestMethod]
        public void IsIndexed_SetTrue_Success()
        {
            ColumnAttribute sut = new ColumnAttribute
            {
                IsIndexed = true
            };

            Assert.IsTrue(sut.IsIndexed);
        }

        [TestMethod]
        public void AttributeUsage_ValidatesTargetsAndSettings()
        {
            AttributeUsageAttribute usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
                typeof(ColumnAttribute), typeof(AttributeUsageAttribute));

            Assert.IsNotNull(usage);
            Assert.AreEqual(AttributeTargets.Property, usage.ValidOn);
            Assert.IsFalse(usage.AllowMultiple);
            Assert.IsFalse(usage.Inherited);
        }

        [TestMethod]
        public void AppliedToProperty_CanBeRetrieved()
        {
            System.Reflection.PropertyInfo propertyInfo = typeof(SampleModelWithColumnAttribute)
                .GetProperty(nameof(SampleModelWithColumnAttribute.Amount));

            ColumnAttribute columnAttribute = (ColumnAttribute)Attribute.GetCustomAttribute(
                propertyInfo, typeof(ColumnAttribute));

            Assert.IsNotNull(columnAttribute);
            Assert.AreEqual("AmountColumn", columnAttribute.Name);
            Assert.AreEqual(18, columnAttribute.Precision);
            Assert.AreEqual(2, columnAttribute.Scale);
            Assert.IsFalse(columnAttribute.IsNullable);
            Assert.AreEqual("0", columnAttribute.DefaultValue);
            Assert.IsTrue(columnAttribute.IsIndexed);
        }

        private sealed class SampleModelWithColumnAttribute
        {
            [Column(Name = "AmountColumn", Precision = 18, Scale = 2, IsNullable = false, DefaultValue = "0", IsIndexed = true)]
            public decimal Amount { get; set; }
        }
    }
}
