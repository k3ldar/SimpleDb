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
 *  Copyright (c) 2018 - 2023 Simon Carter.  All Rights Reserved.
 *
 *  Product:  SimpleDB
 *  
 *  File: IWalTableAccessor.cs
 *
 *  Purpose:  Internal-only accessor providing lazy, engine-owned access to the WAL system
 *            table. Not exposed to hosting applications; used exclusively by TransactionManager
 *            to break the constructor dependency cycle between SimpleDBOperations<WalEntryDataRow>
 *            (which requires ITransactionManager) and TransactionManager (which needs to write
 *            to the WAL table).
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using SimpleDB.Internal.Tables;

namespace SimpleDB
{
    /// <summary>
    /// Internal accessor providing lazy, engine-owned access to the WAL system table.
    /// </summary>
    internal interface IWalTableAccessor
    {
        /// <summary>
        /// Gets the WAL table operations instance, resolved lazily on first access.
        /// </summary>
        ISimpleDBOperations<WalEntryDataRow> WalTable { get; }
    }
}
