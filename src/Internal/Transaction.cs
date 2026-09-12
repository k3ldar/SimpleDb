namespace SimpleDB.Internal
{
    internal class Transaction : ITransaction, IWalEntryCollector, IDatabaseLockScope
    {
        private readonly ITransactionManager _transactionManager;
        private readonly List<WalEntry> _entries = [];
        private IDisposable _databaseLockScope;
        private bool _completed;

        public Transaction(
            long transactionId, ITransactionManager transactionManager,
            IDisposable databaseLockScope,
            TransactionAccessMode accessMode = TransactionAccessMode.ReadWrite,
            TransactionIsolationLevel isolationLevel = TransactionIsolationLevel.DirtyRead,
            bool internalTransaction = false)
        {
            TransactionId = transactionId;
            _transactionManager = transactionManager;
            _databaseLockScope = databaseLockScope;
            IsInternalTransaction = internalTransaction;
            AccessMode = accessMode;
            IsolationLevel = isolationLevel;
        }

        /// <summary>
        /// True when this transaction was begun by the checkpoint's own watermark bookkeeping
        /// rather than by user code. Such transactions are excluded from the active transaction
        /// count, so a checkpoint does not observe its own nested writes as in flight work and
        /// abort itself via the checkpoint barrier.
        /// </summary>
        public bool IsInternalTransaction { get; }

        /// <summary>
        /// Released by the transaction manager once the transaction has been committed or rolled
        /// back. Idempotent, so an abandoned transaction cannot release the lock twice.
        /// </summary>
        public void ReleaseDatabaseLock()
        {
            IDisposable scope = _databaseLockScope;
            _databaseLockScope = null;
            scope?.Dispose();
        }

        public long TransactionId { get; }

        public TransactionAccessMode AccessMode { get; }

        public TransactionIsolationLevel IsolationLevel { get; }

        public IReadOnlyList<WalEntry> Entries => _entries;

        public DateTime Started { get; } = DateTime.UtcNow;

        public void AddEntry(WalEntry entry)
        {
            ThrowIfCompleted();

            _entries.Add(entry);
        }

        public void Commit()
        {
            ThrowIfCompleted();

            _completed = true;
            _transactionManager.CommitTransaction(this);
        }

        public void Rollback()
        {
            ThrowIfCompleted();

            _completed = true;
            _transactionManager.RollbackTransaction(this);
        }

        private void ThrowIfCompleted()
        {
            if (_completed)
                throw new TransactionException($"Transaction {TransactionId} has already been committed or rolled back and cannot be reused.");
        }
    }
}
