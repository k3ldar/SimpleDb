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
 *  File: SequenceManager.cs
 *
 *  Purpose:  Manages primary and secondary sequences for each table in a shared
 *            [dbo].[__sequences] SQL Server table using atomic UPDATE…OUTPUT
 *
 *  Date        Name                Reason
 *  09/04/2026  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using Microsoft.Data.SqlClient;

namespace SimpleDB.SqlServer.Internal
{
    internal static class SequenceManager
    {
        private const long DefaultSequenceValue = -1;

        /// <summary>
        /// Ensures the shared [dbo].[__sequences] table exists.
        /// Safe to call repeatedly; uses IF NOT EXISTS.
        /// </summary>
        public static void EnsureTable(string connectionString)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            const string sql = """
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = '__sequences'
                )
                BEGIN
                    CREATE TABLE [dbo].[__sequences] (
                        [TableName]    NVARCHAR(200) NOT NULL,
                        [PrimarySeq]   BIGINT        NOT NULL DEFAULT -1,
                        [SecondarySeq] BIGINT        NOT NULL DEFAULT -1,
                        CONSTRAINT [PK___sequences] PRIMARY KEY ([TableName])
                    );
                END
                """;

            using SqlCommand cmd = new(sql, conn);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Returns the current primary and secondary sequences for the given table,
        /// inserting a row with default values if none exists yet.
        /// </summary>
        public static (long primary, long secondary) GetOrCreate(
            string connectionString,
            string tableName,
            long defaultPrimary = DefaultSequenceValue,
            long defaultSecondary = DefaultSequenceValue)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            const string selectSql = """
                SELECT [PrimarySeq], [SecondarySeq]
                FROM   [dbo].[__sequences]
                WHERE  [TableName] = @tableName
                """;

            using (SqlCommand selectCmd = new(selectSql, conn))
            {
                selectCmd.Parameters.AddWithValue("@tableName", tableName);

                using SqlDataReader reader = selectCmd.ExecuteReader();
                if (reader.Read())
                    return (reader.GetInt64(0), reader.GetInt64(1));
            }

            const string insertSql = """
                INSERT INTO [dbo].[__sequences] ([TableName], [PrimarySeq], [SecondarySeq])
                VALUES (@tableName, @primary, @secondary)
                """;

            using SqlCommand insertCmd = new(insertSql, conn);
            insertCmd.Parameters.AddWithValue("@tableName", tableName);
            insertCmd.Parameters.AddWithValue("@primary", defaultPrimary);
            insertCmd.Parameters.AddWithValue("@secondary", defaultSecondary);
            insertCmd.ExecuteNonQuery();

            return (defaultPrimary, defaultSecondary);
        }

        /// <summary>
        /// Atomically increments PrimarySeq by <paramref name="increment"/> and
        /// returns the new value.
        /// </summary>
        public static long IncrementPrimary(string connectionString, string tableName, long increment)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            const string sql = """
                UPDATE [dbo].[__sequences]
                SET    [PrimarySeq] = [PrimarySeq] + @increment
                OUTPUT INSERTED.[PrimarySeq]
                WHERE  [TableName] = @tableName
                """;

            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@increment", increment);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            return (long)cmd.ExecuteScalar();
        }

        /// <summary>
        /// Atomically increments SecondarySeq by <paramref name="increment"/> and
        /// returns the new value.
        /// </summary>
        public static long IncrementSecondary(string connectionString, string tableName, long increment)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            const string sql = """
                UPDATE [dbo].[__sequences]
                SET    [SecondarySeq] = [SecondarySeq] + @increment
                OUTPUT INSERTED.[SecondarySeq]
                WHERE  [TableName] = @tableName
                """;

            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@increment", increment);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            return (long)cmd.ExecuteScalar();
        }

        /// <summary>
        /// Resets both sequences to the specified values.
        /// </summary>
        public static void Reset(string connectionString, string tableName, long primary, long secondary)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            const string sql = """
                UPDATE [dbo].[__sequences]
                SET    [PrimarySeq]   = @primary,
                       [SecondarySeq] = @secondary
                WHERE  [TableName]   = @tableName
                """;

            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@primary", primary);
            cmd.Parameters.AddWithValue("@secondary", secondary);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            cmd.ExecuteNonQuery();
        }
    }
}
