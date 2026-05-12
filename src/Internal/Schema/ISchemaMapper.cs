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
 *  File: ISchemaMapper.cs
 *
 *  Purpose:  Database-agnostic interface for schema mapping and table operations
 *
 *  Date        Name                Reason
 *  12/05/2026  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

namespace SimpleDB.Internal.Schema
{
    /// <summary>
    /// Defines database-agnostic schema mapping operations for creating and maintaining
    /// tables based on C# TableRowDefinition subclasses.
    /// </summary>
    internal interface ISchemaMapper
    {
        /// <summary>
        /// Returns the schema name and table name for the specified type.
        /// </summary>
        /// <typeparam name="T">The TableRowDefinition subclass.</typeparam>
        /// <returns>A tuple containing the schema name and table name.</returns>
        (string schemaName, string tableName) GetTableName<T>() where T : TableRowDefinition;

        /// <summary>
        /// Reads the TableAttribute from the specified type.
        /// </summary>
        /// <typeparam name="T">The TableRowDefinition subclass.</typeparam>
        /// <returns>The TableAttribute associated with the type.</returns>
        TableAttribute GetTableAttributes<T>() where T : TableRowDefinition;

        /// <summary>
        /// Builds the ordered list of ColumnDefinitions for the specified type by
        /// reflecting all public instance properties that should be persisted.
        /// </summary>
        /// <typeparam name="T">The TableRowDefinition subclass.</typeparam>
        /// <returns>A read-only list of column definitions.</returns>
        IReadOnlyList<ColumnDefinition> GetColumns<T>() where T : TableRowDefinition;

        /// <summary>
        /// Ensures that the database table exists and is up-to-date with the current column set.
        /// Any columns present in C# but missing from the database are added as nullable.
        /// Columns that no longer exist in C# are left in place.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="schemaName">The schema name.</param>
        /// <param name="tableName">The table name.</param>
        /// <param name="columns">The list of column definitions.</param>
        /// <returns>True if the table was newly created; false if it already existed.</returns>
        bool EnsureTable(
            string connectionString,
            string schemaName,
            string tableName,
            IReadOnlyList<ColumnDefinition> columns);
    }
}
