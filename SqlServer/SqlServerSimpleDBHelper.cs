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
 *  File: SqlServerSimpleDBHelper.cs
 *
 *  Purpose:  DI registration helper for the SimpleDB SQL Server provider
 *
 *  Date        Name                Reason
 *  09/04/2026  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using Microsoft.Extensions.DependencyInjection;

using SharedPluginFeatures;

using SimpleDB;
using SimpleDB.Internal;
using SimpleDB.SqlServer.Internal;

namespace SimpleDB.SqlServer
{
    /// <summary>
    /// Extension methods for registering SimpleDB with a SQL Server backend
    /// </summary>
    public static class SqlServerSimpleDBHelper
    {
        /// <summary>
        /// Registers all SimpleDB services using SQL Server as the storage backend.
        /// Replace the existing AddSimpleDB() call with this method to switch backends;
        /// all consumers of ISimpleDBOperations&lt;T&gt; remain unchanged.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="connectionString">SQL Server connection string</param>
        /// <returns>IServiceCollection</returns>
        public static IServiceCollection AddSimpleDBSqlServer(this IServiceCollection services, string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            SqlServerSimpleDBSettings settings = new() { ConnectionString = connectionString };

            services.AddSingleton(settings);
            services.AddSingleton<IForeignKeyManager, ForeignKeyManager>();
            services.AddSingleton<ISimpleDBManager, SqlServerSimpleDBManager>();
            services.AddSingleton(typeof(ISimpleDBOperations<>), typeof(SqlServerDBOperations<>));
            services.AddSingleton<IDatabaseTimings, DatabaseTimings>();

            return services;
        }
    }
}
