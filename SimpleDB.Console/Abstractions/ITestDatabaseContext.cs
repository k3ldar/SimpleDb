namespace SimpleDB.Console.Abstractions
{
    /// <summary>
    /// Provides a test with access to the database and, critically, the ability to close it and
    /// reopen it against the same files.
    /// </summary>
    /// <remarks>
    /// Tests receive this rather than a single built provider so they can verify behaviour that
    /// only manifests across a shutdown boundary - most importantly that committed data survives
    /// a restart by being recovered from the WAL when the table files are still behind the
    /// checkpoint watermark.
    /// </remarks>
    public interface ITestDatabaseContext
    {
        /// <summary>
        /// Directory containing the database files for the current test run.
        /// </summary>
        string DatabasePath { get; }

        /// <summary>
        /// Resolves a service from the currently open database session.
        /// </summary>
        T Resolve<T>() where T : notnull;

        /// <summary>
        /// Disposes the current database session and opens a brand new one over the same files.
        /// </summary>
        /// <remarks>
        /// The existing session is fully disposed before the new one is created; table files are
        /// exclusively locked while open, so sessions must never overlap. Any service previously
        /// obtained from Resolve is invalid after calling this.
        /// </remarks>
        void Restart();

        /// <summary>
        /// Copies the database files exactly as they currently are on disk and opens a separate
        /// database session over that copy, simulating a restart after an abrupt termination.
        /// </summary>
        /// <remarks>
        /// This is how WAL crash recovery is exercised. A clean shutdown checkpoints every table,
        /// so restarting normally always finds up to date table files and never replays the WAL.
        /// Copying the files while the current session is still open captures the true mid-flight
        /// state - the WAL is durable because it is force written at commit, but lazily written
        /// table files are still behind. Opening that copy forces startup recovery to do real work.
        /// The returned session must be disposed by the caller.
        /// </remarks>
        ICrashRecoverySession OpenCrashCopy();

        /// <summary>
        /// As <see cref="OpenCrashCopy()"/>, but invokes <paramref name="mutateBeforeOpen"/> against
        /// the copied directory before any database session is opened over it.
        /// </summary>
        /// <remarks>
        /// Some crash windows cannot be captured simply by copying a live database, because they
        /// exist only for the microseconds during which the engine is midway through rewriting a
        /// file. The only practical way to exercise recovery against such a state is to reproduce
        /// the half written file on disk directly. The mutation must run before the session is
        /// created, since opening the database is what triggers startup recovery.
        /// </remarks>
        /// <param name="mutateBeforeOpen">Receives the path of the copied database directory.</param>
        ICrashRecoverySession OpenCrashCopy(Action<string> mutateBeforeOpen);

        /// <summary>
        /// Closes the current session, releasing every file handle held against the database
        /// files, then invokes <paramref name="action"/> against a standalone
        /// <see cref="ISimpleDBManager"/> that has no table resolved against it (and therefore no
        /// file open), before finally reopening a fresh session over the same files.
        /// </summary>
        /// <remarks>
        /// Restoring a backup rewrites the table files on disk in place, which cannot be done
        /// while the current session's tables still hold them open. This is the only way to
        /// exercise <see cref="ISimpleDBManager.RestoreDatabase(string, RestoreOptions)"/> from
        /// within the same process as the live session.
        /// </remarks>
        /// <param name="action">Receives the standalone manager to act against.</param>
        void ExecuteWithDatabaseClosed(Action<ISimpleDBManager> action);
    }

    /// <summary>
    /// A database session opened over a crash-state copy of the database files.
    /// </summary>
    public interface ICrashRecoverySession : IDisposable
    {
        /// <summary>
        /// Directory containing the copied database files.
        /// </summary>
        string DatabasePath { get; }

        /// <summary>
        /// Resolves a service from the recovered database session.
        /// </summary>
        T Resolve<T>() where T : notnull;
    }
}
