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
 *  File: WalEntryRow.cs
 *
 *  Purpose:  On-disk row representation of a write-ahead log entry, persisted to the
 *            internal system table "_walentries". Uses StorageEngine.AppendOnly so entries
 *            are appended sequentially rather than the full table being rewritten on save.
 *
 *            IMPORTANT: Code persisting WalEntryRow instances must always call the plain
 *            non-transactional Insert(WalEntryRow) - never Insert(WalEntryRow, transaction) -
 *            to avoid recursively generating WAL entries for the WAL table itself.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using SimpleDB.Attributes;

namespace SimpleDB.Internal.Tables
{
    /// <summary>
    /// On-disk row representation of a write-ahead log entry, persisted to the internal system
    /// </summary>
    [SystemTable(StorageEngine.AppendOnly, participatesInWal: false)]
    [Table("System", "Sys$WalEntries", writeStrategy: WriteStrategy.Forced)]
    internal sealed class WalEntryDataRow : TableRowDefinition
    {
        /// <summary>
        /// Id of the transaction this entry belongs to.
        /// </summary>
        public long TransactionId { get; set; }

        /// <summary>
        /// Name of the table the operation was performed against.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Type of DML operation performed (Insert/Update/Delete).
        /// </summary>
        public OperationType Operation { get; set; }

        /// <summary>
        /// Id of the record the operation was performed against.
        /// </summary>
        public long RecordId { get; set; }

        /// <summary>
        /// Serialized bytes of the record, using the same serialization as the source table.
        /// </summary>
        public byte[] SerializedRecord { get; set; }

        /// <summary>
        /// Monotonically increasing sequence number, used to order entries across tables
        /// within a transaction.
        /// </summary>
        public long SequenceNumber { get; set; }

        /// <summary>
        /// The record's UpdatedTicks value as it was known when the operation was performed,
        /// used for optimistic concurrency checking at commit time. -1 for Insert operations.
        /// </summary>
        public long ExpectedUpdatedTicks { get; set; }

        /// <summary>
        /// Serialized bytes of the record as it existed BEFORE the operation was applied, used
        /// to reverse it. Null for Insert (nothing existed beforehand) and for
        /// <see cref="OperationType.Commit"/> markers.
        /// </summary>
        /// <remarks>
        /// Writes are applied eagerly to a table's in memory record set before the owning
        /// transaction commits, so a crash mid transaction can leave them on disk. Persisting
        /// the before-image is what allows recovery to undo that work - without it the undo
        /// path can only repair memory, which is lost by the crash anyway.
        /// </remarks>
        public byte[] UndoRecord { get; set; }
    }
}
