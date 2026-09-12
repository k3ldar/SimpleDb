namespace SimpleDB.Internal
{
    internal class TriggerContext : ITriggerContext
    {
        internal TriggerContext(ITransaction transaction)
        {
            Transaction = transaction;
        }

        public ITransaction Transaction { get; }
    }
}
