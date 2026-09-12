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
 *  File: IWatermarkAccessor.cs
 *
 *  Purpose:  Internal-only accessor providing lazy, engine-owned access to the checkpoint
 *            watermark system table. Not exposed to hosting applications; used by
 *            SimpleDBOperations<T> at startup recovery time and by the (future) checkpoint
 *            coordinator to read/write a table's last-flushed WAL sequence.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using SimpleDB.Internal.Tables;

namespace SimpleDB
{
    /// <summary>
    /// Internal accessor providing lazy, engine-owned access to the checkpoint watermark
    /// system table, and helpers for reading/writing a single table's watermark row.
    /// </summary>
    internal interface IWatermarkAccessor
    {
        /// <summary>
        /// Gets the checkpoint watermark table operations instance, resolved lazily on first access.
        /// </summary>
        ISimpleDBOperations<CheckpointWatermarkDataRow> WatermarkTable { get; }

        /// <summary>
        /// Gets the last-flushed WAL sequence number recorded for the named table, or -1 if
        /// no checkpoint has ever been recorded for it (i.e. first run).
        /// </summary>
        /// <param name="tableName">Name of the user table to look up.</param>
        long GetLastFlushedSequence(string tableName);

        /// <summary>
        /// Records a new checkpoint watermark for the named table, inserting or updating the
        /// row as required. Always uses the plain, non-transactional Insert/Update overloads -
        /// this table must never generate WAL entries for its own writes.
        /// </summary>
        /// <param name="tableName">Name of the user table the checkpoint applies to.</param>
        /// <param name="lastFlushedSequence">Highest WAL SequenceNumber reflected on disk after the checkpoint.</param>
        void SetLastFlushedSequence(string tableName, long lastFlushedSequence);
    }
}
