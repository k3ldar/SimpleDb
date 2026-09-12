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
 *  Product:  SimpleDB
 *  
 *  File: DbColumnAttribute.cs
 *
 *  Purpose:  DbColumnAttribute for SimpleDB schema generation
 *
 *  Date        Name                Reason
 *  12/12/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

namespace SimpleDB
{
    /// <summary>
    /// Defines column constraints and metadata for database schema generation
    /// Used by schema generators to map C# properties to database columns
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class ColumnAttribute : Attribute
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public ColumnAttribute()
        {
            IsNullable = true;
        }

        /// <summary>
        /// Custom column name (if different from property name)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Maximum length for string columns (SQLite: VARCHAR(n))
        /// </summary>
        public int MaxLength { get; set; }

        /// <summary>
        /// Precision for decimal/numeric columns (total digits)
        /// Default: 18
        /// </summary>
        public int Precision { get; set; }

        /// <summary>
        /// Scale for decimal/numeric columns (digits after decimal point)
        /// Default: 2
        /// </summary>
        public int Scale { get; set; }

        /// <summary>
        /// Whether this column allows null values (default: true)
        /// Note: Id property is always NOT NULL and AUTOINCREMENT
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// Default value for this column if not specified during insert
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// Whether this column is indexed for faster lookups
        /// </summary>
        public bool IsIndexed { get; set; }
    }
}