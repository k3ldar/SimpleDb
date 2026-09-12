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
 *  File: WalRecorder.cs
 *
 *  Purpose:  Default IWalRecorder for tables that participate in the WAL (see
 *            SystemTableAttribute.ParticipatesInWal and SimpleDBOperations<T>.ResolveWalRecorder).
 *
 *            Persists each entry to the WAL table IMMEDIATELY, before returning to the caller
 *            that is about to mutate the table, and also retains it on the transaction's
 *            in-memory log for rollback. See the class remarks for why both are required.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using SharedPluginFeatures;

using SimpleDB.Internal.Tables;

namespace SimpleDB.Internal
{
    /// <summary>
    /// Default recorder: persists the entry to the WAL table immediately, and retains it on the
    /// transaction's in-memory log so a rollback can reverse it.
    /// </summary>
    /// <remarks>
    /// The immediate persist is what makes the write-ahead rule hold. Callers apply the mutation
    /// to the table's record set as soon as Record returns, and a WriteStrategy.Forced table
    /// flushes that straight to its file - so if the log entry were only written at commit time,
    /// a crash mid transaction would leave data on disk with no log record describing how to
    /// reverse it. The WAL table is itself WriteStrategy.Forced, so the Insert below is durable
    /// before this method returns.
    ///
    /// The in-memory copy is still required: it carries the UndoRecord used by an ordinary
    /// (non-crash) rollback, and avoids re-reading the log to unwind a transaction.
    /// </remarks>
    internal sealed class WalRecorder : IWalRecorder
    {
        private const string TimingsRecord = "TimingsRecord";

        private readonly IWalTableAccessor _walTableAccessor;
        private readonly Dictionary<string, Timings> _timings = new()
        {
            { TimingsRecord, new() }
        };

        public WalRecorder(IWalTableAccessor walTableAccessor)
        {
            _walTableAccessor = walTableAccessor ?? throw new ArgumentNullException(nameof(walTableAccessor));
        }

        /// <inheritdoc/>
        public Dictionary<string, Timings> GetAllTimings
        {
            get
            {
                Dictionary<string, Timings> Result = [];

                foreach (KeyValuePair<string, Timings> item in _timings)
                {
                    Result.Add(item.Key, item.Value.Clone());
                }

                return Result;
            }
        }

        public void Record(ITransaction transaction, string tableName, OperationType operation, long recordId, byte[] serializedRecord, long expectedUpdatedTicks, byte[] undoRecord)
        {
            using (StopWatchTimer timer = StopWatchTimer.Initialise(_timings[TimingsRecord]))
            {
                if (transaction is not IWalEntryCollector collector)
                    return;

                long sequenceNumber = _walTableAccessor.WalTable.NextSecondarySequence(1);

                collector.AddEntry(new WalEntry
                {
                    TransactionId = transaction.TransactionId,
                    TableName = tableName,
                    Operation = operation,
                    RecordId = recordId,
                    SerializedRecord = serializedRecord,
                    ExpectedUpdatedTicks = expectedUpdatedTicks,
                    SequenceNumber = sequenceNumber,
                    UndoRecord = undoRecord,
                });

                // MUST use the plain, non-transactional Insert - see WalEntryDataRow.cs header, using
                // the transactional overload here would recursively generate WAL entries for the WAL
                // table itself. The entry is uncommitted at this point; it is only honoured at
                // recovery if a matching OperationType.Commit marker is found later in the log.
                _walTableAccessor.WalTable.Insert(new WalEntryDataRow
                {
                    TransactionId = transaction.TransactionId,
                    TableName = tableName,
                    Operation = operation,
                    RecordId = recordId,
                    SerializedRecord = serializedRecord,
                    ExpectedUpdatedTicks = expectedUpdatedTicks,
                    SequenceNumber = sequenceNumber,
                    UndoRecord = undoRecord,
                });
            }
        }
    }
}
