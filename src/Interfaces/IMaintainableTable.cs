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
 *  File: IMaintainableTable.cs
 *
 *  Purpose:  Implemented by tables that can release and re-acquire their underlying file
 *            handle on demand. This allows a database wide maintenance operation, such as a
 *            restore, to overwrite table files on disk while table objects remain alive and
 *            registered, without needing to dispose and recreate the whole session.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

namespace SimpleDB
{
    /// <summary>
    /// Implemented by tables that support being closed and reopened while the process is running,
    /// so that database wide maintenance operations (such as restore) can safely overwrite the
    /// underlying table file on disk.
    /// </summary>
    internal interface IMaintainableTable
    {
        /// <summary>
        /// Flushes any in memory changes to disk and releases the underlying file handle so the
        /// table file can be safely overwritten by an external process. The table remains
        /// registered; only <see cref="ReopenAfterMaintenance"/> is valid to call afterwards.
        /// </summary>
        void CloseForMaintenance();

        /// <summary>
        /// Re-acquires the underlying file handle after <see cref="CloseForMaintenance"/>,
        /// revalidates the table file, replays any outstanding WAL entries and rebuilds indexes,
        /// exactly as though the table had just been constructed.
        /// </summary>
        void ReopenAfterMaintenance();
    }
}
