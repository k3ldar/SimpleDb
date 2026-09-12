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
 *  File: ICheckpointableTable.cs
 *
 *  Purpose:  Non generic checkpoint hook. ForceWrite is declared on the generic
 *            ISimpleDBOperations<T> interface, but a database wide checkpoint has to flush
 *            every registered table without knowing their row types.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

namespace SimpleDB
{
    /// <summary>
    /// Implemented by tables that can flush their in memory record set to disk on demand as
    /// part of a database wide checkpoint.
    /// </summary>
    internal interface ICheckpointableTable
    {
        /// <summary>
        /// Indicates whether the table generates WAL entries. Only participating tables are
        /// included when calculating the minimum watermark that governs WAL truncation - a non
        /// participating table has no entries in the log and must never hold truncation back.
        /// </summary>
        bool ParticipatesInWal { get; }

        /// <summary>
        /// Flushes the current record set to the table file. Deliberately does not touch the
        /// checkpoint watermark - the caller advances it only once this has returned successfully,
        /// otherwise a failed flush would be recorded as durable and the WAL entries needed to
        /// recover it discarded.
        /// </summary>
        void ForceWrite();

        /// <summary>
        /// Records that every WAL entry up to and including <paramref name="lastFlushedSequence"/>
        /// is now reflected in this table's file. MUST only be called after a successful
        /// <see cref="ForceWrite"/>.
        /// </summary>
        /// <param name="lastFlushedSequence">Highest WAL SequenceNumber reflected on disk.</param>
        void AdvanceCheckpointWatermark(long lastFlushedSequence);
    }
}
