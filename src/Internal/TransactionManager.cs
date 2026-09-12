using Shared.Classes;

using SharedPluginFeatures;

using SimpleDB.Internal.Tables;

namespace SimpleDB.Internal
{
    internal sealed class TransactionManager : ITransactionMaintenance
    {
        /// <summary>
        /// Number of WAL entries that must accumulate before a database wide checkpoint is
        /// attempted. Read at the point of use from the database wide settings applied at startup.
        /// </summary>
        internal static uint WalCheckpointThreshold => SimpleDBConfiguration.WalCheckpointThreshold;

        /// <summary>
        /// Watermark value meaning "this table has never been checkpointed", matching the value
        /// <see cref="IWatermarkAccessor.GetLastFlushedSequence(string)"/> returns when no row exists.
        /// </summary>
        internal const long NeverFlushedSequence = -1L;

        /// <summary>
        /// Set for the duration of a checkpoint, on the thread performing it. A checkpoint writes
        /// watermark rows through the plain, non transactional overloads, but those still begin
        /// and commit a transaction internally. That nested work is bookkeeping rather than user
        /// work and must not be seen by the checkpoint barrier, otherwise every checkpoint would
        /// observe its own watermark write as an in flight transaction and abort itself.
        ///
        /// Thread static rather than a plain field, so that a genuine user transaction beginning
        /// on a different thread while a checkpoint runs is still counted by the barrier.
        /// </summary>
        [ThreadStatic]
        private static bool _performingCheckpoint;

        private readonly IWalTableAccessor _walTableAccessor;
        private readonly IWatermarkAccessor _watermarkAccessor;
        private readonly ITransactionSequenceAccessor _sequenceAccessor;
        private readonly ISimpleDBManager _simpleDBManager;
        private readonly IDatabaseLock _databaseLock;

        /// <summary>
        /// Name of the independent, engine-owned sequence used for transaction ids. Deliberately
        /// separate from the WAL table's own PrimarySequence/SecondarySequence, which must remain
        /// solely responsible for WAL entry ordering and must never be reset or reused.
        /// </summary>
        private const string TransactionIdSequenceName = "TransactionId";
        private readonly List<ITransaction> _activeTransactionList = [];
        private readonly object _activeTransactionListLock = new();
        private int _activeTransactions;
        private int _checkpointRunning;
        private long _checkpointsSkippedForActiveTransactions;
        private long _checkpointsCompleted;

        private const string TimingsBegin = "TimingsBegin";
        private const string TimingsCommit = "TimingsCommit";
        private const string TimingsRollback = "TimingsRollback";
        private const string TimingsRollbackUndo = "TimingsRollbackUndo";
        private const string TimingsCheckpoint = "TimingsCheckpoint";

        private readonly Dictionary<string, Timings> _timings = new()
        {
            { TimingsBegin, new() },
            { TimingsCommit, new() },
            { TimingsRollback, new() },
            { TimingsRollbackUndo, new() },
            { TimingsCheckpoint, new() }
        };

        public TransactionManager(IWalTableAccessor walTableAccessor, IWatermarkAccessor watermarkAccessor, ITransactionSequenceAccessor sequenceAccessor, ISimpleDBManager simpleDBManager)
        {
            _walTableAccessor = walTableAccessor ?? throw new ArgumentNullException(nameof(walTableAccessor));
            _watermarkAccessor = watermarkAccessor ?? throw new ArgumentNullException(nameof(watermarkAccessor));
            _sequenceAccessor = sequenceAccessor ?? throw new ArgumentNullException(nameof(sequenceAccessor));
            _simpleDBManager = simpleDBManager ?? throw new ArgumentNullException(nameof(simpleDBManager));

            // The database wide gate belongs to whoever owns the table registry, as a checkpoint
            // has to flush every registered table atomically. A manager that does not provide one
            // simply gets no checkpoint coordination rather than a hard failure.
            _databaseLock = simpleDBManager as IDatabaseLock;

            // Backup/restore is exposed on ISimpleDBManager, but flushing the WAL, closing
            // transactions and resetting the sequence are all owned here. Attaching back through
            // ITransactionManagerAttachable avoids both a circular constructor dependency between
            // the two singletons and a dependency on SimpleDBManager's concrete type.
            (simpleDBManager as ITransactionManagerAttachable)?.AttachTransactionManager(this);
        }

        /// <summary>
        /// Number of user transactions that have been started but not yet committed or rolled
        /// back. Transactions begun by the checkpoint itself are excluded, see
        /// <see cref="_performingCheckpoint"/>.
        ///
        /// This is the checkpoint barrier: <see cref="InternalCheckpointDatabase"/> refuses to
        /// flush any table while this is non zero. Uncommitted rows are applied eagerly to a
        /// table's in memory record set, so flushing mid transaction would steal them to disk,
        /// and the undo path could then only repair memory.
        /// </summary>
        private int InternalActiveTransactionCount => Volatile.Read(ref _activeTransactions);

        /// <summary>
        /// Number of checkpoints abandoned because a transaction was in flight. A long lived
        /// transaction blocks checkpointing for its whole lifetime, which lets the WAL grow
        /// without bound - a steadily climbing value here is the symptom of that.
        /// </summary>
        private long InternalCheckpointsSkippedForActiveTransactions => Interlocked.Read(ref _checkpointsSkippedForActiveTransactions);

        /// <summary>
        /// Number of checkpoints that have successfully run to completion.
        /// </summary>
        public long CheckpointsCompleted => Interlocked.Read(ref _checkpointsCompleted);

        /// <inheritdoc/>
        public int ActiveTransactionCount => InternalActiveTransactionCount;

        public long CheckpointsSkippedForActiveTransactions => InternalCheckpointsSkippedForActiveTransactions;

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

        public IReadOnlyList<ITransaction> ActiveTransactions
        {
            get
            {
                using (TimedLock listLock = TimedLock.Lock(_activeTransactionListLock))
                    return [.. _activeTransactionList];
            }
        }

        public long NextTransactionId => _sequenceAccessor.CurrentValue();

        public void CheckpointDatabase()
        {
            InternalCheckpointDatabase(force: true);
        }

        public int ForceCloseActiveTransactions()
        {
            List<ITransaction> snapshot;

            using (TimedLock listLock = TimedLock.Lock(_activeTransactionListLock))
                snapshot = [.. _activeTransactionList];

            foreach (ITransaction transaction in snapshot)
            {
                try
                {
                    RollbackTransaction(transaction);
                }
                catch (TransactionException)
                {
                    // Already committed/rolled back on another thread - nothing left to close.
                }
            }

            return snapshot.Count;
        }

        public void ResetTransactionSequence()
        {
            _sequenceAccessor.Reset();
        }

        public ITransaction BeginTransaction(
            TransactionAccessMode accessMode = TransactionAccessMode.ReadWrite,
            TransactionIsolationLevel isolationLevel = TransactionIsolationLevel.DirtyRead)
        {
            using (StopWatchTimer timer = StopWatchTimer.Initialise(_timings[TimingsBegin]))
            {
                // Held for the lifetime of the transaction and released on commit or rollback. The
                // lock is reentrant, so the nested WAL and watermark writes performed during commit
                // reacquire it harmlessly on the same thread.
                IDisposable scope = _databaseLock?.AcquireForOperation();

                try
                {
                    // Uses an independent, engine-owned sequence rather than the WAL table's own
                    // SecondarySequence, which must remain solely responsible for WAL entry
                    // ordering (SequenceNumber) and must never be reset or reused - see
                    // SequenceDataRow.cs and SequenceAccessor.cs for the rollover behaviour and
                    // its documented limitations.
                    long transactionId = _sequenceAccessor.NextValue();

                    // Classified at the point the transaction begins, and carried on the transaction
                    // itself so the matching decrement cannot disagree with the increment - by the
                    // time a checkpoint's watermark write commits, the flag may already be cleared.
                    bool internalTransaction = _performingCheckpoint;

                    if (!internalTransaction)
                        Interlocked.Increment(ref _activeTransactions);

                    ITransaction result = new Transaction(transactionId, this, scope, accessMode, isolationLevel, internalTransaction);

                    using (TimedLock listLock = TimedLock.Lock(_activeTransactionListLock))
                        _activeTransactionList.Add(result);

                    return result;
                }
                catch
                {
                    scope?.Dispose();
                    throw;
                }
            }
        }

        public void CommitTransaction(ITransaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_timings[TimingsCommit]))
            {
                try
                {
                    InternalCommitTransaction(transaction);

                    using (TimedLock listLock = TimedLock.Lock(_activeTransactionListLock))
                        _activeTransactionList.Remove(transaction);
                }
                finally
                {
                    if (transaction is not Transaction { IsInternalTransaction: true })
                        Interlocked.Decrement(ref _activeTransactions);

                    (transaction as IDatabaseLockScope)?.ReleaseDatabaseLock();
                }

                // The WAL is the durability record for lazily written tables, so it must not grow
                // without bound. Once it passes the threshold the table files are brought up to date
                // and the WAL has served its purpose for everything flushed.
                if (_walTableAccessor.WalTable.RecordCount >= WalCheckpointThreshold)
                    InternalCheckpointDatabase(force: false);
            }
        }

        private void InternalCommitTransaction(ITransaction transaction)
        {
            using (TimedLock listLock = TimedLock.Lock(_activeTransactionListLock))
                _activeTransactionList.Remove(transaction);

            if (transaction is IWalEntryCollector collector)
            {
                if (collector.Entries.Count == 0)
                    return;

                // The data entries are NOT written here - WalRecorder already persisted each one
                // at the moment the operation was performed, which is what keeps the log ahead of
                // the table writes. Committing is therefore just a matter of marking them valid.

                // The commit marker. Every data entry of this transaction is already durable, so
                // once this marker is flushed below the transaction is committed; if the process
                // dies before that, the entries are present without a marker and recovery undoes
                // them.
                _walTableAccessor.WalTable.Insert(new WalEntryDataRow
                {
                    TransactionId = transaction.TransactionId,
                    TableName = String.Empty,
                    Operation = OperationType.Commit,
                    RecordId = 0,
                    ExpectedUpdatedTicks = -1,
                    SequenceNumber = _walTableAccessor.WalTable.NextSecondarySequence(1),
                });

                // The WAL must be durable at commit time, otherwise a crash before the next
                // checkpoint loses both the table data and the log intended to recover it. This is
                // a no-op when the WAL table is declared WriteStrategy.Forced (each Insert has
                // already been flushed), but guarantees durability independently of that attribute.
                _walTableAccessor.WalTable.ForceWrite();
            }
        }

        public void RollbackTransaction(ITransaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_timings[TimingsRollback]))
            {
                try
                {
                    InternalRollbackTransaction(transaction);
                }
                finally
                {
                    using (TimedLock listLock = TimedLock.Lock(_activeTransactionListLock))
                        _activeTransactionList.Remove(transaction);

                    if (transaction is not Transaction { IsInternalTransaction: true })
                        Interlocked.Decrement(ref _activeTransactions);

                    (transaction as IDatabaseLockScope)?.ReleaseDatabaseLock();
                }
            }
        }

        private void InternalRollbackTransaction(ITransaction transaction)
        {
            if (transaction is not IWalEntryCollector collector)
                return;

            if (collector.Entries.Count == 0)
                return;

            // The table data was applied eagerly, so each operation must be reversed. Entries are
            // undone in reverse order so that multiple operations against the same record unwind
            // correctly.
            using (StopWatchTimer timer = StopWatchTimer.Initialise(_timings[TimingsRollbackUndo]))
            {
                for (int i = collector.Entries.Count - 1; i >= 0; i--)
                {
                    WalEntry entry = collector.Entries[i];

                    if (!_simpleDBManager.Tables.TryGetValue(entry.TableName, out ISimpleDBTable table))
                        continue;

                    if (table is not IWalUndoTarget undoTarget)
                        continue;

                    switch (entry.Operation)
                    {
                        case OperationType.Insert:
                            undoTarget.UndoInsert(entry.RecordId);
                            break;

                        case OperationType.Update:
                            undoTarget.UndoUpdate(entry.RecordId, entry.UndoRecord);
                            break;

                        case OperationType.Delete:
                            undoTarget.UndoDelete(entry.UndoRecord);
                            break;
                    }
                }
            }

            // The abort marker. WalRecorder already persisted this transaction's entries, so
            // without an explicit outcome they would look identical to those of a transaction
            // interrupted by a crash - permanently in doubt, and holding WAL truncation back for
            // ever. Recovery uses it to reverse the entries on disk without replaying them.
            _walTableAccessor.WalTable.Insert(new WalEntryDataRow
            {
                TransactionId = transaction.TransactionId,
                TableName = String.Empty,
                Operation = OperationType.Abort,
                RecordId = 0,
                ExpectedUpdatedTicks = -1,
                SequenceNumber = _walTableAccessor.WalTable.NextSecondarySequence(1),
            });

            _walTableAccessor.WalTable.ForceWrite();
        }

        /// <summary>
        /// Flushes every registered table to disk under the database lock, so no other thread can
        /// mutate a table midway through being written. The WAL table is skipped because it is
        /// already force written at commit time and is itself the record used to recover the
        /// other tables.
        ///
        /// A single global WAL sequence snapshot is captured before any table is flushed, and is
        /// stamped onto a table only once that table's flush has succeeded. Capturing first is
        /// what makes the snapshot safe - any entry logged after it is simply replayed next time -
        /// whereas stamping a table that failed to flush would discard the very entries needed to
        /// recover it.
        ///
        /// If the lock cannot be obtained promptly the checkpoint is abandoned rather than
        /// stalling live work; the WAL remains over threshold so the next commit retries.
        ///
        /// A checkpoint never runs while a user transaction is in flight. Transactional writes are
        /// applied eagerly to a table's in memory record set, so flushing mid transaction would
        /// steal uncommitted rows to disk where the in memory undo path cannot reach them. The
        /// database lock alone does NOT provide this guarantee - it is reentrant, so a nested
        /// transaction committing on the thread that already owns it would otherwise checkpoint
        /// straight through its own parent's uncommitted work.
        /// </summary>
        private void InternalCheckpointDatabase(bool force)
        {
            using (StopWatchTimer timer = StopWatchTimer.Initialise(_timings[TimingsCheckpoint]))
            {
                // A checkpoint advances watermarks, and the watermark table's plain Update overload
                // begins and commits its own transaction - whose commit re-enters this method. The
                // database lock is reentrant so it does not stop that, and RecordCount cannot fall
                // until InternalTruncateWal runs AFTER the loop below, so the threshold check never
                // stops it either. Without this guard the first watermark write of a checkpoint
                // recurses until the stack overflows, while holding the lock every other thread needs.
                //
                // A concurrent thread also skips rather than duplicating the work; that is harmless
                // because the WAL stays over threshold and the next commit retries.
                if (Interlocked.CompareExchange(ref _checkpointRunning, 1, 0) != 0)
                    return;

                try
                {
                    // The checkpoint barrier. Checked before the lock is even requested so an in
                    // flight transaction costs nothing, and again under the lock below to close the
                    // window where one begins while this thread is waiting.
                    if (IsTransactionInFlight())
                        return;

                    IDisposable scope = _databaseLock?.TryAcquireForCheckpoint();

                    if (scope == null)
                        return;

                    try
                    {
                        // Re-checked under the lock: another thread may have checkpointed while this one
                        // was waiting to acquire it. A forced checkpoint (e.g. from a backup) runs
                        // regardless of how small the WAL currently is.
                        if (!force && _walTableAccessor.WalTable.RecordCount < WalCheckpointThreshold)
                            return;

                        // Re-checked under the lock for the same reason. Holding the lock does not by
                        // itself prove no transaction is active: the lock is reentrant, so this thread
                        // may already own it on behalf of an outer transaction that is still open.
                        if (IsTransactionInFlight())
                            return;

                        // Read only snapshot of the WAL sequence counter, taken before the first flush.
                        long checkpointSequence = _walTableAccessor.WalTable.SecondarySequence;

                        List<string> walParticipants = [];

                        // Marks the nested transactions produced by SeedWatermark and
                        // AdvanceCheckpointWatermark below as internal, so the barrier above does not
                        // count the checkpoint's own bookkeeping as user work on a later re-entry.
                        _performingCheckpoint = true;

                        try
                        {
                            foreach (KeyValuePair<string, ISimpleDBTable> table in _simpleDBManager.Tables)
                            {
                                if (table.Value is not ICheckpointableTable checkpointable)
                                    continue;

                                if (ReferenceEquals(table.Value, _walTableAccessor.WalTable))
                                    continue;

                                if (checkpointable.ParticipatesInWal)
                                {
                                    walParticipants.Add(table.Key);

                                    // A registered table that has never flushed has no watermark row at all,
                                    // so a naive MIN over existing rows would silently ignore it and truncate
                                    // away entries it still needs. Seeding the row makes the set complete.
                                    SeedWatermark(table.Key);
                                }

                                checkpointable.ForceWrite();

                                // Only reached when the flush above completed without throwing. A table that
                                // throws propagates out, skipping truncation entirely for this cycle - its WAL
                                // entries remain the only durable copy of its data.
                                checkpointable.AdvanceCheckpointWatermark(checkpointSequence);
                            }

                            InternalTruncateWal(walParticipants);

                            // Reached only when every participating table flushed and the WAL was
                            // truncated without throwing - i.e. the checkpoint genuinely completed.
                            Interlocked.Increment(ref _checkpointsCompleted);
                        }
                        finally
                        {
                            _performingCheckpoint = false;
                        }
                    }
                    finally
                    {
                        scope.Dispose();
                    }
                }
                finally
                {
                    Volatile.Write(ref _checkpointRunning, 0);
                }
            }
        }

        /// <summary>
        /// The checkpoint barrier test. Returns true, and records the skip, when a user
        /// transaction is in flight and the checkpoint must therefore be abandoned.
        /// </summary>
        /// <remarks>
        /// Skipping is always safe: the WAL stays over threshold, so the next commit that finds
        /// no transaction in flight retries. It is not free though - until then the WAL keeps
        /// growing, which is why the skip is counted rather than silently swallowed.
        /// </remarks>
        private bool IsTransactionInFlight()
        {
            if (Volatile.Read(ref _activeTransactions) <= 0)
                return false;

            Interlocked.Increment(ref _checkpointsSkippedForActiveTransactions);

            return true;
        }

        /// <summary>
        /// Ensures a watermark row exists for the named table before the minimum watermark is
        /// calculated. A table that has never been checkpointed reports -1, which is exactly the
        /// value seeded, so this neither advances nor rewinds any real watermark.
        /// </summary>
        private void SeedWatermark(string tableName)
        {
            if (_watermarkAccessor.GetLastFlushedSequence(tableName) == NeverFlushedSequence)
                _watermarkAccessor.SetLastFlushedSequence(tableName, NeverFlushedSequence);
        }

        /// <summary>
        /// Discards WAL entries that every participating table has already flushed to its own
        /// file, then compacts the log so the space is actually reclaimed.
        ///
        /// The safe cut off is the MINIMUM watermark across all participants, not the maximum: an
        /// entry may only be discarded once the slowest table has it on disk. A participant that
        /// has never flushed reports -1, which correctly suppresses truncation entirely.
        ///
        /// The cut off is additionally bounded by the oldest unresolved transaction. Entries are
        /// logged as each operation happens, so the log can contain work for a transaction that
        /// has neither committed nor aborted; discarding those entries would strip away the
        /// before-images recovery needs to reverse it.
        /// </summary>
        private void InternalTruncateWal(List<string> walParticipants)
        {
            if (walParticipants.Count == 0)
                return;

            long minWatermark = Int64.MaxValue;

            foreach (string tableName in walParticipants)
            {
                long watermark = _watermarkAccessor.GetLastFlushedSequence(tableName);

                if (watermark <= NeverFlushedSequence)
                    return;

                if (watermark < minWatermark)
                    minWatermark = watermark;
            }

            if (_walTableAccessor.WalTable is not ICompactableTable<WalEntryDataRow> compactable)
                return;

            long oldestUnresolved = OldestUnresolvedSequence();

            if (oldestUnresolved != NeverFlushedSequence && oldestUnresolved - 1 < minWatermark)
                minWatermark = oldestUnresolved - 1;

            // Entries beyond the minimum watermark are not yet reflected in every table file and
            // remain the only durable record of that work. Compact rewrites the WAL file in place
            // and flushes it before returning, so no further ForceWrite is needed here - and
            // RecordCount now genuinely drops, ending the re-checkpoint-on-every-commit cliff.
            compactable.Compact(w => w.SequenceNumber > minWatermark);
        }

        /// <summary>
        /// Lowest SequenceNumber belonging to a transaction that has neither a Commit nor an
        /// Abort marker, or <see cref="NeverFlushedSequence"/> when every transaction in the log
        /// has reached a decided outcome.
        /// </summary>
        /// <remarks>
        /// An unresolved transaction is either still running or was interrupted by a crash. In
        /// both cases its entries, and crucially their before-images, must survive truncation so
        /// that recovery can reverse the work. This is also why a rollback writes an explicit
        /// Abort marker - without one, every rolled back transaction would pin the log for ever.
        /// </remarks>
        private long OldestUnresolvedSequence()
        {
            HashSet<long> resolved = _walTableAccessor.WalTable
                .Select(w => w.Operation == OperationType.Commit || w.Operation == OperationType.Abort)
                .Select(w => w.TransactionId)
                .ToHashSet();

            long oldest = Int64.MaxValue;

            foreach (WalEntryDataRow entry in _walTableAccessor.WalTable.Select())
            {
                if (resolved.Contains(entry.TransactionId))
                    continue;

                if (entry.SequenceNumber < oldest)
                    oldest = entry.SequenceNumber;
            }

            return oldest == Int64.MaxValue ? NeverFlushedSequence : oldest;
        }
    }
}
