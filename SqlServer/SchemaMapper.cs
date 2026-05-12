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
 *  Product:  SimpleDB.SqlServer
 *  
 *  File: SchemaMapper.cs
 *
 *  Purpose:  Static facade for backward compatibility that delegates to SqlServerSchemaMapper
 *
 *  Date        Name                Reason
 *  09/04/2026  Simon Carter        Initially Created
 *  12/05/2026  Simon Carter        Converted to static facade for backward compatibility
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using SimpleDB;
using SimpleDB.Internal.Schema;
using SimpleDB.SqlServer.Internal;

namespace SimpleDB.SqlServer.Internal
{
	/// <summary>
	/// Static facade that provides backward compatibility by delegating to SqlServerSchemaMapper.
	/// This class maintains the existing API while the actual implementation has moved to
	/// the new database-agnostic architecture.
	/// </summary>
	internal static class SchemaMapper
	{
		private static readonly SqlServerSchemaMapper _mapper = new();

		/// <summary>
		/// Returns the SQL Server schema name and table name for <typeparamref name="T"/>.
		/// Domain maps to schema (defaults to "dbo"); TableAttribute.TableName maps to table.
		/// </summary>
		public static (string schemaName, string tableName) GetTableName<T>() where T : TableRowDefinition
		{
			return _mapper.GetTableName<T>();
		}

		/// <summary>
		/// Reads the TableAttribute from <typeparamref name="T"/>.
		/// </summary>
		public static TableAttribute GetTableAttributes<T>() where T : TableRowDefinition
		{
			return _mapper.GetTableAttributes<T>();
		}

		/// <summary>
		/// Builds the ordered list of ColumnDefinitions for <typeparamref name="T"/> by
		/// reflecting all public instance properties that should be persisted.
		/// </summary>
		public static IReadOnlyList<ColumnDefinition> GetColumns<T>() where T : TableRowDefinition
		{
			return _mapper.GetColumns<T>();
		}

		/// <summary>
		/// Ensures that the SQL Server table exists and is
		/// up-to-date with the current column set.  Any columns present in C# but missing
		/// from the database are added as nullable.  Columns that no longer exist in C#
		/// are left in place (no destructive changes are ever made automatically).
		/// </summary>
		/// <returns>True if the table was newly created; false if it already existed.</returns>
		public static bool EnsureTable(
			string connectionString,
			string schemaName,
			string tableName,
			IReadOnlyList<ColumnDefinition> columns)
		{
			return _mapper.EnsureTable(connectionString, schemaName, tableName, columns);
		}
	}
}
