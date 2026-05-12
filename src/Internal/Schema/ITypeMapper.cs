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
 *  File: ITypeMapper.cs
 *
 *  Purpose:  Database-agnostic interface for mapping C# types to database types
 *
 *  Date        Name                Reason
 *  12/05/2026  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Reflection;

namespace SimpleDB.Internal.Schema
{
    /// <summary>
    /// Defines database-agnostic type mapping operations for converting C# property types
    /// to database-specific type strings.
    /// </summary>
    internal interface ITypeMapper
    {
        /// <summary>
        /// Maps a C# property to its database-specific type string and determines nullability.
        /// </summary>
        /// <param name="property">The PropertyInfo to map.</param>
        /// <param name="isNullable">Output parameter indicating whether the column allows NULL.</param>
        /// <returns>The database-specific type string (e.g., "NVARCHAR(256)", "INT").</returns>
        string GetDatabaseType(PropertyInfo property, out bool isNullable);
    }
}
