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
 *  File: SqlServerSchemaMapper.cs
 *
 *  Purpose:  Reflection-driven DDL generator that creates and migrates SQL Server tables
 *            to match the current C# TableRowDefinition subclass at runtime
 *
 *  Date        Name                Reason
 *  09/04/2026  Simon Carter        Initially Created
 *  12/05/2026  Simon Carter        Refactored to implement ISchemaMapper interface
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

using Microsoft.Data.SqlClient;

using SimpleDB;
using SimpleDB.Internal.Schema;

namespace SimpleDB.SqlServer.Internal
{
    /// <summary>
    /// SQL Server implementation that derives schema from a TableRowDefinition subclass using reflection
    /// and ensures the live database matches that schema at startup.
    /// </summary>
    internal sealed class SqlServerSchemaMapper : ISchemaMapper
    {
        // Properties on TableRowDefinition that are decorated with [JsonIgnore] and therefore
        // must not become SQL columns.
        private static readonly HashSet<string> IgnoredPropertyNames = new(StringComparer.Ordinal)
        {
            nameof(TableRowDefinition.Created),
            nameof(TableRowDefinition.Updated),
            nameof(TableRowDefinition.HasChanged),
        };

        private readonly ITypeMapper _typeMapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerSchemaMapper"/> class.
        /// </summary>
        public SqlServerSchemaMapper()
        {
            _typeMapper = new SqlServerTypeMapper();
        }

        #region ISchemaMapper Implementation

        /// <summary>
        /// Returns the SQL Server schema name and table name for <typeparamref name="T"/>.
        /// Domain maps to schema (defaults to "dbo"); TableAttribute.TableName maps to table.
        /// </summary>
        public (string schemaName, string tableName) GetTableName<T>() where T : TableRowDefinition
        {
            TableAttribute attr = GetTableAttributes<T>();
            string schema = String.IsNullOrEmpty(attr.Domain) ? "dbo" : SanitizeIdentifier(attr.Domain);
            string table = SanitizeIdentifier(attr.TableName);
            return (schema, table);
        }

        /// <summary>
        /// Reads the TableAttribute from <typeparamref name="T"/>.
        /// </summary>
        public TableAttribute GetTableAttributes<T>() where T : TableRowDefinition
        {
            return (TableAttribute)typeof(T)
                .GetCustomAttributes(true)
                .FirstOrDefault(a => a.GetType() == typeof(TableAttribute));
        }

        /// <summary>
        /// Builds the ordered list of ColumnDefinitions for <typeparamref name="T"/> by
        /// reflecting all public instance properties that should be persisted.
        /// </summary>
        public IReadOnlyList<ColumnDefinition> GetColumns<T>() where T : TableRowDefinition
        {
            List<ColumnDefinition> columns = [];

            foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Skip [JsonIgnore] properties (Created, Updated, HasChanged …)
                if (property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;

                // Skip properties explicitly listed as transient state
                if (IgnoredPropertyNames.Contains(property.Name))
                    continue;

                // Skip properties with no public getter (internal/protected getters etc.)
                if (property.GetGetMethod() == null)
                    continue;

                string databaseType = _typeMapper.GetDatabaseType(property, out bool isNullable);

                bool isPrimaryKey = property.Name == nameof(TableRowDefinition.Id);

                UniqueIndexAttribute uniqueIndex = property.GetCustomAttribute<UniqueIndexAttribute>();
                bool isUniqueIndex = uniqueIndex != null;
                string uniqueIndexName = isUniqueIndex
                    ? (String.IsNullOrEmpty(uniqueIndex.Name) ? property.Name : uniqueIndex.Name)
                    : null;

                columns.Add(new ColumnDefinition(
                    columnName: property.Name,
                    databaseType: databaseType,
                    property: property,
                    isNullable: isNullable,
                    isPrimaryKey: isPrimaryKey,
                    isUniqueIndex: isUniqueIndex,
                    uniqueIndexName: uniqueIndexName,
                    indexType: uniqueIndex?.IndexType ?? IndexType.Ascending));
            }

            return columns.AsReadOnly();
        }

        /// <summary>
        /// Ensures that the SQL Server table exists and is
        /// up-to-date with the current column set.  Any columns present in C# but missing
        /// from the database are added as nullable.  Columns that no longer exist in C#
        /// are left in place (no destructive changes are ever made automatically).
        /// </summary>
        /// <returns>True if the table was newly created; false if it already existed.</returns>
        public bool EnsureTable(
            string connectionString,
            string schemaName,
            string tableName,
            IReadOnlyList<ColumnDefinition> columns)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            EnsureSchema(conn, schemaName);

            if (!TableExists(conn, schemaName, tableName))
            {
                CreateTable(conn, schemaName, tableName, columns);
                return true;
            }

            MigrateTable(conn, schemaName, tableName, columns);
            return false;
        }

        #endregion ISchemaMapper Implementation

        #region DDL Helpers

        private static void EnsureSchema(SqlConnection conn, string schemaName)
        {
            if (schemaName.Equals("dbo", StringComparison.OrdinalIgnoreCase))
                return;

            string sql = $"""
                IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = @schema)
                BEGIN
                    EXEC('CREATE SCHEMA [{schemaName}]')
                END
                """;

            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@schema", schemaName);
            cmd.ExecuteNonQuery();
        }

        private static bool TableExists(SqlConnection conn, string schemaName, string tableName)
        {
            const string sql = """
                SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                """;

            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@schema", schemaName);
            cmd.Parameters.AddWithValue("@table", tableName);
            return (int)cmd.ExecuteScalar() > 0;
        }

        private static void CreateTable(
            SqlConnection conn,
            string schemaName,
            string tableName,
            IReadOnlyList<ColumnDefinition> columns)
        {
            StringBuilder sb = new();
            sb.AppendLine($"CREATE TABLE [{schemaName}].[{tableName}] (");

            List<string> columnDefs = [];
            foreach (ColumnDefinition col in columns)
            {
                string nullability = col.IsNullable ? "NULL" : "NOT NULL";
                columnDefs.Add($"    [{col.ColumnName}] {col.DatabaseType} {nullability}");
            }

            sb.AppendLine(String.Join(",\r\n", columnDefs) + ",");
            sb.AppendLine($"    CONSTRAINT [PK_{tableName}] PRIMARY KEY ([Id] ASC)");
            sb.AppendLine(");");

            using SqlCommand cmd = new(sb.ToString(), conn);
            cmd.ExecuteNonQuery();

            // Create unique indexes for non-PK columns decorated with [UniqueIndex]
            IEnumerable<IGrouping<string, ColumnDefinition>> indexGroups = columns
                .Where(c => c.IsUniqueIndex && !c.IsPrimaryKey)
                .GroupBy(c => c.UniqueIndexName);

            foreach (IGrouping<string, ColumnDefinition> group in indexGroups)
                CreateUniqueIndex(conn, schemaName, tableName, group.Key, [.. group]);
        }

        private static void CreateUniqueIndex(
            SqlConnection conn,
            string schemaName,
            string tableName,
            string indexName,
            List<ColumnDefinition> cols)
        {
            string colList = String.Join(", ", cols.Select(c =>
                $"[{c.ColumnName}] {(c.IndexType == IndexType.Descending ? "DESC" : "ASC")}"));

            string sql = $"CREATE UNIQUE INDEX [{indexName}] ON [{schemaName}].[{tableName}] ({colList});";

            using SqlCommand cmd = new(sql, conn);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Adds columns that exist in the C# class but are missing from the database.
        /// Always adds as nullable to avoid breaking existing rows.
        /// </summary>
        private static void MigrateTable(
            SqlConnection conn,
            string schemaName,
            string tableName,
            IReadOnlyList<ColumnDefinition> columns)
        {
            const string sql = """
                SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                """;

            HashSet<string> existingColumns = new(StringComparer.OrdinalIgnoreCase);

            using (SqlCommand cmd = new(sql, conn))
            {
                cmd.Parameters.AddWithValue("@schema", schemaName);
                cmd.Parameters.AddWithValue("@table", tableName);

                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                    existingColumns.Add(reader.GetString(0));
            }

            foreach (ColumnDefinition col in columns)
            {
                if (existingColumns.Contains(col.ColumnName))
                    continue;

                // New property added to the C# class – add as NULL to avoid breaking existing rows
                string alterSql = $"ALTER TABLE [{schemaName}].[{tableName}] ADD [{col.ColumnName}] {col.DatabaseType} NULL;";

                using SqlCommand alterCmd = new(alterSql, conn);
                alterCmd.ExecuteNonQuery();
            }

            // Columns removed from the C# class are intentionally left in the database.
        }

        /// <summary>
        /// Strips characters that would break a bracketed SQL identifier.
        /// Property names in C# are already safe, but Domain values come from user-supplied attributes.
        /// </summary>
        private static string SanitizeIdentifier(string identifier)
        {
            return identifier
                .Replace("[", "")
                .Replace("]", "")
                .Replace(";", "")
                .Replace("'", "")
                .Replace("\"", "");
        }

        #endregion DDL Helpers
    }
}
