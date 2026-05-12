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
 *  File: SqlServerTypeMapper.cs
 *
 *  Purpose:  Maps C# types to SQL Server-specific type strings
 *
 *  Date        Name                Reason
 *  12/05/2026  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.ComponentModel.DataAnnotations;
using System.Reflection;

using SimpleDB.Internal.Schema;

namespace SimpleDB.SqlServer.Internal
{
    /// <summary>
    /// Implements SQL Server-specific type mapping from C# property types to SQL Server data types.
    /// </summary>
    internal sealed class SqlServerTypeMapper : ITypeMapper
    {
        /// <summary>
        /// Maps a C# property to its SQL Server type string and determines nullability.
        /// </summary>
        /// <param name="property">The PropertyInfo to map.</param>
        /// <param name="isNullable">Output parameter indicating whether the column allows NULL.</param>
        /// <returns>The SQL Server type string (e.g., "NVARCHAR(256)", "INT", "DATETIME2").</returns>
        public string GetDatabaseType(PropertyInfo property, out bool isNullable)
        {
            Type propType = property.PropertyType;
            Type underlyingType = Nullable.GetUnderlyingType(propType);

            // Reference types (string) and Nullable<T> are inherently nullable
            isNullable = underlyingType != null || propType.IsClass;

            Type typeToMap = underlyingType ?? propType;

            if (typeToMap == typeof(long) || typeToMap == typeof(ulong))
                return "BIGINT";

            if (typeToMap == typeof(int) || typeToMap == typeof(uint))
                return "INT";

            if (typeToMap == typeof(short) || typeToMap == typeof(ushort))
                return "SMALLINT";

            if (typeToMap == typeof(byte))
                return "TINYINT";

            if (typeToMap == typeof(bool))
                return "BIT";

            if (typeToMap == typeof(decimal))
                return "DECIMAL(18,4)";

            if (typeToMap == typeof(float))
                return "REAL";

            if (typeToMap == typeof(double))
                return "FLOAT";

            if (typeToMap == typeof(string))
            {
                MaxLengthAttribute maxLength = property.GetCustomAttribute<MaxLengthAttribute>();
                return maxLength != null ? $"NVARCHAR({maxLength.Length})" : "NVARCHAR(MAX)";
            }

            if (typeToMap == typeof(DateTime))
                return "DATETIME2";

            if (typeToMap == typeof(Guid))
                return "UNIQUEIDENTIFIER";

            if (typeToMap.IsEnum)
                return "INT";

            throw new InvalidOperationException(
                $"Unsupported property type '{propType.FullName}' on property '{property.Name}'. " +
                $"Add an explicit type mapping in {nameof(SqlServerTypeMapper)}.{nameof(GetDatabaseType)}.");
        }
    }
}
