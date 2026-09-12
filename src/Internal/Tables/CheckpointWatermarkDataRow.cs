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
 *  File: CheckpointWatermarkDataRow.cs
 *
 *  Purpose:  On-disk row representation of the last checkpoint watermark for a single user
 *            table, persisted to the internal system table "Sys$CheckpointWatermarks". One
 *            row exists per user table, updated in place on each checkpoint. Used at startup
 *            to determine which WAL entries (if any) still need to be replayed into a table's
 *            in-memory record set before it is considered consistent.
 *
 *            IMPORTANT: Code persisting CheckpointWatermarkDataRow instances must always call
 *            the plain non-transactional Insert(CheckpointWatermarkDataRow)/Update(...) -
 *            never the transactional overloads - to avoid recursively generating WAL entries
 *            for the watermark table itself.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using SimpleDB.Attributes;

namespace SimpleDB.Internal.Tables
{
    [SystemTable(StorageEngine.Paged, participatesInWal: false)]
    [Table("System", "Sys$CheckpointWatermarks", writeStrategy: WriteStrategy.Forced)]
    internal sealed class CheckpointWatermarkDataRow : TableRowDefinition
    {
        /// <summary>
        /// Name of the user table this watermark belongs to.
        /// </summary>
        [UniqueIndex(nameof(TableName), IndexType.Ascending)]
        public string TableName { get; set; }

        /// <summary>
        /// Highest WAL entry SequenceNumber for this table that has been flushed to disk as
        /// part of a checkpoint. WAL entries with a SequenceNumber greater than this value
        /// must be replayed at startup to reconstruct a consistent in-memory record set.
        /// </summary>
        public long LastFlushedSequence { get; set; }

        /// <summary>
        /// Ticks (UTC) recording when the checkpoint that produced LastFlushedSequence completed.
        /// </summary>
        public long CheckpointTimestampTicks { get; set; }
    }
}
