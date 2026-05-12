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
 *  Copyright (c) 2018 - 2026 Simon Carter.  All Rights Reserved.
 *
 *  Product:  SimpleDB
 *
 *  File: ColumnDefinition.cs
 *
 *  Purpose:  Represents database-agnostic column metadata derived from a C# property
 *            via reflection in ISchemaMapper implementations.
 *
 *  Date        Name                Reason
 *  09/04/2026  Simon Carter        Initially Created
 *  12/05/2026  Simon Carter        Refactored to be database-agnostic
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Reflection;

namespace SimpleDB.Internal.Schema
{
    /// <summary>
    /// Immutable descriptor for a single database column, built by
    /// <see cref="ISchemaMapper.GetColumns{T}"/> from a reflected C# property.
    /// </summary>
    internal sealed class ColumnDefinition
    {
        /// <summary>
        /// Initialises a new <see cref="ColumnDefinition"/>.
        /// </summary>
        /// <param name="columnName">The column name (matches the C# property name).</param>
        /// <param name="databaseType">The database-specific type string, e.g. <c>NVARCHAR(256)</c> for SQL Server.</param>
        /// <param name="property">The reflected <see cref="PropertyInfo"/> this column maps to.</param>
        /// <param name="isNullable">Whether the column allows <c>NULL</c>.</param>
        /// <param name="isPrimaryKey">Whether the column is the primary key (<c>Id</c>).</param>
        /// <param name="isUniqueIndex">Whether a unique index should be created for this column.</param>
        /// <param name="uniqueIndexName">The name of the unique index, or <c>null</c> when <paramref name="isUniqueIndex"/> is <c>false</c>.</param>
        /// <param name="indexType">The sort direction used when creating the unique index.</param>
        internal ColumnDefinition(
            string columnName,
            string databaseType,
            PropertyInfo property,
            bool isNullable,
            bool isPrimaryKey,
            bool isUniqueIndex,
            string uniqueIndexName,
            IndexType indexType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
            ArgumentException.ThrowIfNullOrWhiteSpace(databaseType);
            ArgumentNullException.ThrowIfNull(property);

            ColumnName = columnName;
            DatabaseType = databaseType;
            Property = property;
            IsNullable = isNullable;
            IsPrimaryKey = isPrimaryKey;
            IsUniqueIndex = isUniqueIndex;
            UniqueIndexName = uniqueIndexName;
            IndexType = indexType;
        }

        /// <summary>Gets the column name.</summary>
        public string ColumnName { get; }

        /// <summary>Gets the database-specific data type string (e.g. <c>INT</c>, <c>NVARCHAR(256)</c>).</summary>
        public string DatabaseType { get; }

        /// <summary>Gets the reflected <see cref="PropertyInfo"/> this column maps to.</summary>
        public PropertyInfo Property { get; }

        /// <summary>Gets a value indicating whether the column allows <c>NULL</c>.</summary>
        public bool IsNullable { get; }

        /// <summary>Gets a value indicating whether this column is the primary key.</summary>
        public bool IsPrimaryKey { get; }

        /// <summary>Gets a value indicating whether a unique index exists for this column.</summary>
        public bool IsUniqueIndex { get; }

        /// <summary>
        /// Gets the unique index name, or <c>null</c> when <see cref="IsUniqueIndex"/> is <c>false</c>.
        /// </summary>
        public string UniqueIndexName { get; }

        /// <summary>Gets the index sort direction for the unique index.</summary>
        public IndexType IndexType { get; }
    }
}