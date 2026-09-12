using System.Diagnostics.CodeAnalysis;

using SharedPluginFeatures;

using SimpleDB.Internal;

namespace SimpleDB.Tests.Mocks
{
    [ExcludeFromCodeCoverage]
    internal class MockTransactionManager : ITransactionManager
    {
        public ITransaction BeginTransaction() => new MockTransaction();

        public void CommitTransaction(ITransaction transaction)
        {
            // no-op for tests
        }

        public void RollbackTransaction(ITransaction transaction)
        {
            // no-op for tests
        }

        public ITransaction BeginTransaction(TransactionAccessMode accessMode = TransactionAccessMode.ReadWrite, TransactionIsolationLevel isolationLevel = TransactionIsolationLevel.DirtyRead)
        {
            return new MockTransaction();
        }

        public Dictionary<string, Timings> GetAllTimings => [];

        public int ActiveTransactionCount => 0;

        public long CheckpointsSkippedForActiveTransactions => 0;

        public long CheckpointsCompleted => 0;

        public long NextTransactionId => throw new NotImplementedException();

        public IReadOnlyList<ITransaction> ActiveTransactions => throw new NotImplementedException();

        public void CheckpointDatabase()
        {
            // no-op for tests
        }

        public int ForceCloseActiveTransactions() => 0;

        public void ResetTransactionSequence()
        {
            // no-op for tests
        }

        private class MockTransaction : ITransaction
        {
            public long TransactionId { get; } = 1;

            public TransactionAccessMode AccessMode => TransactionAccessMode.ReadWrite;

            public TransactionIsolationLevel IsolationLevel => TransactionIsolationLevel.DirtyRead;

            public DateTime Started => DateTime.UtcNow;

            public void Commit()
            {
                // no-op
            }

            public void Rollback()
            {
                // no-op
            }
        }
    }
}
