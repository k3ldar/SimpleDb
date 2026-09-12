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
 *  File: ICompactableTable.cs
 *
 *  Purpose:  Reclaim path for append only tables. The append only writer can only add
 *            records to the tail of the file, so a table whose rows become obsolete - the
 *            WAL after a checkpoint - needs an explicit rewrite to reclaim the space.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

namespace SimpleDB
{
    /// <summary>
    /// Implemented by tables that can permanently discard records and reclaim the disk space
    /// they occupied, rather than only appending.
    /// </summary>
    /// <typeparam name="T">Row type held by the table.</typeparam>
    internal interface ICompactableTable<T>
        where T : TableRowDefinition
    {
        /// <summary>
        /// Discards every record for which <paramref name="keep"/> returns false and rewrites the
        /// table file so the space is reclaimed. Deliberately generates no WAL entries and fires
        /// no triggers - the discarded records are obsolete, not deleted by a user.
        /// </summary>
        /// <param name="keep">Predicate identifying the records that must survive.</param>
        void Compact(Func<T, bool> keep);
    }
}
