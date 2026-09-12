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
 *  File: WalEntry.cs
 *
 *  Purpose:  Type-erased, in-memory representation of a single write-ahead log entry.
 *            Reuses the same serialization (IDataWriter/VersionedReadWriteFactory) as used
 *            to persist T to disk, so SerializedRecord is guaranteed consistent with the
 *            actual on-disk table format.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

namespace SimpleDB.Internal
{
    /// <summary>
    /// A single write-ahead log entry describing a DML operation performed against a table
    /// within the context of a transaction.
    /// </summary>
    internal sealed class WalEntry
    {
        /// <summary>
        /// Id of the transaction this entry belongs to.
        /// </summary>
        public long TransactionId { get; init; }

        /// <summary>
        /// Name of the table the operation was performed against.
        /// </summary>
        public string TableName { get; init; }

        /// <summary>
        /// Type of DML operation performed (Insert/Update/Delete).
        /// </summary>
        public OperationType Operation { get; init; }

        /// <summary>
        /// Id of the record the operation was performed against.
        /// </summary>
        public long RecordId { get; init; }

        /// <summary>
        /// Serialized bytes of the record, using the same serialization as the source table.
        /// </summary>
        public byte[] SerializedRecord { get; init; }

        /// <summary>
        /// Monotonically increasing sequence number, used to order entries across tables
        /// within a transaction.
        /// </summary>
        public long SequenceNumber { get; init; }

        /// <summary>
        /// The record's UpdatedTicks value as it was known when the operation was performed,
        /// used for optimistic concurrency checking at commit time. -1 for Insert operations,
        /// since there is no pre-existing record to compare against.
        /// </summary>
        public long ExpectedUpdatedTicks { get; init; }

        /// <summary>
        /// Serialized bytes of the record as it existed *before* the operation was performed,
        /// used to undo the operation. Null for Insert (undoing an insert simply removes the
        /// record).
        /// </summary>
        /// <remarks>
        /// This IS persisted to the WAL table alongside the entry. Operations are applied to the
        /// table's record set immediately, so uncommitted work can reach disk before the
        /// transaction ends; the before-image is what allows recovery to reverse it after a
        /// crash, when the in-memory copy no longer exists.
        /// </remarks>
        public byte[] UndoRecord { get; init; }
    }
}
