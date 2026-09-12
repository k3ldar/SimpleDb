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
 *  File: SequenceDataRow.cs
 *
 *  Purpose:  On-disk row representation of a single named, engine-owned sequence, persisted
 *            to the internal system table "Sys$Sequences". One row exists per sequence name
 *            (e.g. "TransactionId"), and is deliberately independent of any single table's own
 *            PrimarySequence/SecondarySequence counters - in particular it is independent of
 *            the WAL table's SecondarySequence, which must remain solely responsible for WAL
 *            entry ordering (SequenceNumber) and must never be reset or reused.
 *
 *            IMPORTANT: Code persisting SequenceDataRow instances must always call the plain
 *            non-transactional Insert(SequenceDataRow)/Update(...) - never the transactional
 *            overloads - to avoid recursively generating WAL entries for the sequence table
 *            itself.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using SimpleDB.Attributes;

namespace SimpleDB.Internal.Tables
{
    /// <summary>
    /// On-disk row representation of a single named, engine-owned sequence.
    /// </summary>
    [SystemTable(StorageEngine.Paged, participatesInWal: false)]
    [Table("System", "Sys$TransactionSequence", writeStrategy: WriteStrategy.Forced)]
    internal sealed class TransactionSequenceDataRow : TableRowDefinition
    {
    }
}
