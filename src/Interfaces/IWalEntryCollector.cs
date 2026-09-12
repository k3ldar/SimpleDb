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
 *  File: IWalEntryCollector.cs
 *
 *  Purpose:  Internal-only contract allowing WalRecorder to append WalEntry instances onto the
 *            currently active ITransaction, without expanding the public ITransaction contract.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using SimpleDB.Internal;

namespace SimpleDB
{
    /// <summary>
    /// Internal-only contract implemented by Transaction, allowing WAL entries to be collected
    /// against the active transaction ahead of commit.
    /// </summary>
    internal interface IWalEntryCollector
    {
        /// <summary>
        /// Gets the WAL entries collected so far for this transaction.
        /// </summary>
        IReadOnlyList<WalEntry> Entries { get; }

        /// <summary>
        /// Adds a WAL entry to the transaction's in-memory log.
        /// </summary>
        /// <param name="entry">Entry to add.</param>
        void AddEntry(WalEntry entry);
    }
}
