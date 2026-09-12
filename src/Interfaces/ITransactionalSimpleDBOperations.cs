namespace SimpleDB
{
    /// <summary>
    /// Interface for transactional operations on a SimpleDB table
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ITransactionalSimpleDBOperations<T> where T : TableRowDefinition
    {
        /// <summary>
        /// Inserts a record into the table within the context of a transaction
        /// </summary>
        /// <param name="record">The record to insert</param>
        /// <param name="insertOptions">The insert options</param>
        /// <param name="transaction">The transaction context</param>
        void Insert(T record, InsertOptions insertOptions, ITransaction transaction);

        /// <summary>
        /// Inserts a record into the table within the context of a transaction
        /// </summary>
        /// <param name="records">The records to insert</param>
        /// <param name="insertOptions">The insert options</param>
        /// <param name="transaction">The transaction context</param>
        void Insert(List<T> records, InsertOptions insertOptions, ITransaction transaction);

        /// <summary>
        /// Updates a record in the table within the context of a transaction
        /// </summary>
        /// <param name="record">The record to update</param>
        /// <param name="transaction">The transaction context</param>
        void Update(T record, ITransaction transaction);

        /// <summary>
        /// Updates a list of records in the table within the context of a transaction
        /// </summary>
        /// <param name="records">The records to update</param>
        /// <param name="transaction">The transaction context</param>
        void Update(List<T> records, ITransaction transaction);

        /// <summary>
        /// Deletes a list of records from the table within the context of a transaction
        /// </summary>
        /// <param name="record">The record to delete</param>
        /// <param name="transaction">The transaction context</param>
        void Delete(T record, ITransaction transaction);

        /// <summary>
        /// Deletes a list of records from the table within the context of a transaction
        /// </summary>
        /// <param name="records">The records to delete</param>
        /// <param name="transaction">The transaction context</param>
        void Delete(List<T> records, ITransaction transaction);

        /// <summary>
        /// Truncates the table within the context of a transaction
        /// </summary>
        /// <param name="transaction">The transaction context</param>
        void Truncate(ITransaction transaction);
    }
}
