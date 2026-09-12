using Microsoft.Extensions.DependencyInjection;

using SimpleDB.Console.Abstractions;

namespace SimpleDB.Console.Internal
{
    /// <summary>
    /// Owns the service collection describing a database and builds provider "sessions" from it.
    /// </summary>
    /// <remarks>
    /// Each call to BuildServiceProvider produces its own set of singletons, so building a second
    /// provider genuinely re-opens the table files and re-runs startup recovery. That is what
    /// allows a test to prove committed data is readable after a shutdown even when it only ever
    /// reached the WAL. The previous session is always disposed first because the underlying
    /// table files are held with an exclusive lock while open.
    /// </remarks>
    public sealed class TestDatabaseContext : ITestDatabaseContext, IDisposable
    {
        private readonly Func<string, IServiceCollection> _servicesFactory;
        private ServiceProvider _serviceProvider;
        private bool _disposed;

        public TestDatabaseContext(Func<string, IServiceCollection> servicesFactory, string databasePath)
        {
            _servicesFactory = servicesFactory ?? throw new ArgumentNullException(nameof(servicesFactory));
            DatabasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

            _serviceProvider = _servicesFactory(databasePath).BuildServiceProvider();
        }

        public string DatabasePath { get; }

        public T Resolve<T>() where T : notnull
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _serviceProvider.GetRequiredService<T>();
        }

        public void Restart()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Order matters - the old session must release its file handles before the new one
            // attempts to open the same tables.
            _serviceProvider.Dispose();
            _serviceProvider = _servicesFactory(DatabasePath).BuildServiceProvider();
        }

        public ICrashRecoverySession OpenCrashCopy()
        {
            return OpenCrashCopy(null);
        }

        public ICrashRecoverySession OpenCrashCopy(Action<string> mutateBeforeOpen)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Copy while the current session is still open and therefore has NOT checkpointed.
            // The files captured here are the genuine crash state: a durable WAL alongside table
            // files that are still behind it.
            string copyPath = Path.Combine(Path.GetDirectoryName(DatabasePath.TrimEnd(Path.DirectorySeparatorChar))!,
                $"crash-{Guid.NewGuid()}");

            CopyDirectory(DatabasePath, copyPath);

            // Applied before the provider is built, because building it is what runs startup
            // recovery - the very thing under test.
            mutateBeforeOpen?.Invoke(copyPath);

            return new CrashRecoverySession(_servicesFactory(copyPath), copyPath);
        }

        public void ExecuteWithDatabaseClosed(Action<ISimpleDBManager> action)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Releases every file handle the live session holds, so the standalone manager built
            // below can rewrite the table files in place without a sharing violation.
            _serviceProvider.Dispose();

            try
            {
                using ServiceProvider standaloneProvider = _servicesFactory(DatabasePath).BuildServiceProvider();
                ISimpleDBManager manager = standaloneProvider.GetRequiredService<ISimpleDBManager>();

                // Forces construction (but not table file access - its dependencies are resolved
                // lazily on first use) so it attaches itself to the manager above and backup/
                // restore can coordinate checkpoints, transaction closure and sequence reset.
                _ = standaloneProvider.GetRequiredService<ITransactionManager>();

                action(manager);
            }
            finally
            {
                // Reopen a genuine session over the (possibly just restored) files, exactly as
                // Restart does, so callers see the effect of action() through the normal Resolve.
                _serviceProvider = _servicesFactory(DatabasePath).BuildServiceProvider();
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                // The live session holds these files open, so they must be read with full sharing.
                string target = file.Replace(source, destination, StringComparison.Ordinal);

                using FileStream input = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using FileStream output = new(target, FileMode.Create, FileAccess.Write, FileShare.None);

                input.CopyTo(output);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _serviceProvider.Dispose();
        }

        private sealed class CrashRecoverySession : ICrashRecoverySession
        {
            private readonly ServiceProvider _serviceProvider;
            private bool _disposed;

            public CrashRecoverySession(IServiceCollection services, string databasePath)
            {
                DatabasePath = databasePath;
                _serviceProvider = services.BuildServiceProvider();
            }

            public string DatabasePath { get; }

            public T Resolve<T>() where T : notnull
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                return _serviceProvider.GetRequiredService<T>();
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _serviceProvider.Dispose();
            }
        }
    }
}
