namespace SimpleDB.Internal
{
    /// <summary>
    /// Internal, engine-only extension of <see cref="ITransactionManager"/> exposing the
    /// housekeeping operations that back backup/restore. These are deliberately not part of the
    /// public <see cref="ITransactionManager"/> contract, as they are only meaningful to
    /// <see cref="SimpleDBManager"/> while it coordinates a database wide snapshot.
    /// </summary>
    internal interface ITransactionMaintenance : ITransactionManager
    {
        /// <summary>
        /// Rolls back every currently active transaction.
        /// </summary>
        /// <returns>The number of transactions that were rolled back.</returns>
        int ForceCloseActiveTransactions();

        /// <summary>
        /// Resets the transaction id sequence back to zero.
        /// </summary>
        void ResetTransactionSequence();
    }
}
