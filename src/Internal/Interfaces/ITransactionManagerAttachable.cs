namespace SimpleDB.Internal
{
    /// <summary>
    /// Implemented by <see cref="SimpleDBManager"/> so that <see cref="TransactionManager"/> can
    /// attach itself back after construction, without either singleton needing to depend on the
    /// other's concrete type. This avoids a circular constructor dependency between the two
    /// while keeping the attach step free of the housekeeping interface's public contract.
    /// </summary>
    internal interface ITransactionManagerAttachable
    {
        /// <summary>
        /// Attaches the transaction manager instance so its maintenance operations can be
        /// invoked during backup/restore.
        /// </summary>
        /// <param name="transactionManager">The transaction manager to attach.</param>
        void AttachTransactionManager(ITransactionMaintenance transactionManager);
    }
}
