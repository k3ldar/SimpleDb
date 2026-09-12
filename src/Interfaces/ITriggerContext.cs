namespace SimpleDB
{
    /// <summary>
    /// Interface describing the context in which a trigger is executed
    /// </summary>
    public interface ITriggerContext
    {
        /// <summary>
        /// Gets the transaction associated with the trigger context
        /// </summary>
        ITransaction Transaction { get; }
    }
}
