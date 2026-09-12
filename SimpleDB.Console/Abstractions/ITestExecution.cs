namespace SimpleDB.Console.Abstractions
{
    internal interface ITestExecution
    {
        /// <summary>
        /// Determines the order in which tests are executed, lowest first.
        /// </summary>
        /// <remarks>
        /// Tests are discovered by reflection, and the CLR gives no ordering guarantee for
        /// Type.GetTypes(), so it can change between builds. Order matters here because the tests
        /// share a single database session: execution order determines how many WAL entries have
        /// accumulated, which in turn determines whether a checkpoint has fired and flushed the
        /// table files. SimpleDbTest4 in particular requires its table file to still be behind the
        /// WAL, so a silent reordering could make it fail for reasons unrelated to a real defect.
        ///
        /// Ties are broken by TestName so the sort is always total and stable.
        /// </remarks>
        int Order { get; }

        string TestName { get; }

        void Execute();

        void Validate();
    }
}
