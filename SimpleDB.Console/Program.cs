using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using PluginManager.Abstractions;

using SharedPluginFeatures;

using SimpleDB.Console.Abstractions;
using SimpleDB.Console.Internal;
using SimpleDB.Console.Tables;

using static System.Console;

namespace SimpleDB.Console
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            string mainFolder = Path.Combine(Path.GetTempPath(), "SimpleDBTest");
            string testDirectory = Path.Combine(mainFolder, Guid.NewGuid().ToString());

            if (Path.Exists(mainFolder))
            {
                Directory.Delete(mainFolder, true);
            }

            Directory.CreateDirectory(testDirectory);
            try
            {
                using (TestDatabaseContext databaseContext = new(BuildServices, testDirectory))
                {
                    RunAllTests(databaseContext);

                    PrintMetrics(databaseContext);
                }

                WriteLine("Finished, done, going home!");
            }
            finally
            {
                Directory.Delete(testDirectory, true);
            }
        }

        private static void RunAllTests(ITestDatabaseContext databaseContext)
        {
            WriteLine("Building Tests");
            var tests = new List<ITestExecution>();

            var testClasses = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && !t.IsAbstract && !t.IsInterface && t.IsAssignableTo(typeof(ITestExecution))).ToList();

            foreach (var t in testClasses)
            {
                var instance = (ITestExecution)Activator.CreateInstance(t, databaseContext)!;
                tests.Add(instance);
            }

            // Reflection order is not deterministic, so impose an explicit one - see ITestExecution.Order.
            tests = [.. tests.OrderBy(t => t.Order).ThenBy(t => t.TestName, StringComparer.Ordinal)];

            WriteLine("Running Tests");

            foreach (var test in tests)
            {
                WriteLine($"Executing test: {test.TestName} (order {test.Order})");
                test.Execute();

                WriteLine($"Validating test: {test.TestName}");
                test.Validate();
            }
        }

        private static void PrintMetrics(ITestDatabaseContext databaseContext)
        {
            IDatabaseMetrics metrics = databaseContext.Resolve<IDatabaseMetrics>();

            WriteLine();
            WriteLine("===================== Database Metrics =====================");

            WriteLine();
            WriteLine("Transaction Metrics");
            WriteLine($"  Active Transaction Count               : {metrics.TransactionMetrics.ActiveTransactionCount}");
            WriteLine($"  Checkpoints Completed                   : {metrics.TransactionMetrics.CheckpointsCompleted}");
            WriteLine($"  Checkpoints Skipped (active txn)        : {metrics.TransactionMetrics.CheckpointsSkippedForActiveTransactions}");
            WriteLine($"  Next Transaction ID                     : {metrics.TransactionMetrics.NextTransactionId}");
            WriteLine();
            PrintActiveTransactions(metrics.TransactionMetrics.ActiveTransactions);
            WriteLine();
            PrintTimingsTable(metrics.TransactionMetrics.GetAllTimings);

            foreach (var kvp in metrics.TableMetrics.OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                ITableMetrics tableMetrics = kvp.Value;

                WriteLine();
                WriteLine($"Table: {tableMetrics.TableName}");
                WriteLine($"  Caching Strategy                        : {tableMetrics.CachingStrategy}");
                WriteLine($"  Write Strategy                          : {tableMetrics.WriteStrategy}");

                if (tableMetrics.CachingStrategy == CachingStrategy.SlidingMemory)
                    WriteLine($"  Sliding Memory Timeout                  : {tableMetrics.SlidingMemoryTimeout}");

                // New data from ITableMetrics
                try
                {
                    WriteLine($"  Logical Data Size (bytes)               : {tableMetrics.LogicalDataSizeBytes}");
                    WriteLine($"  Physical Data Size (bytes)              : {tableMetrics.PhysicalDataSizeBytes}");
                    WriteLine($"  In-Memory Cache Size (bytes)            : {tableMetrics.InMemoryCacheSizeBytes}");
                }
                catch
                {
                    // If an implementation doesn't provide the new properties, just ignore
                }

                PrintTimingsTable(tableMetrics.GetAllTimings);
            }

            WriteLine();
            WriteLine("=============================================================");
        }

        private static void PrintActiveTransactions(IReadOnlyList<ITransaction> activeTransactions)
        {
            WriteLine($"  Active Transactions ({activeTransactions.Count})");

            if (activeTransactions.Count == 0)
            {
                WriteLine("    (none)");
                return;
            }

            const string format = "    {0,-20}{1,-12}{2,-12}";
            WriteLine(String.Format(format, "TransactionId", "AccessMode", "Isolation", "Start Date Time"));

            foreach (ITransaction transaction in activeTransactions)
            {
                WriteLine(String.Format(format, transaction.TransactionId, transaction.AccessMode, transaction.IsolationLevel, transaction.Started));
            }
        }

        private static void PrintTimingsTable(Dictionary<string, Timings> timings)
        {
            const string format = "  {0,-30}{1,10}{2,12}{3,12}{4,12}";

            if (timings.Count == 0)
            {
                WriteLine("  (no timings recorded)");
                return;
            }

            WriteLine(String.Format(format, "Operation", "Requests", "Average", "Fastest", "Slowest"));
            WriteLine(new string('-', 76));

            foreach (var kvp in timings.OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                Timings timing = kvp.Value;
                WriteLine(String.Format(format, kvp.Key, timing.Requests, timing.Average, timing.Fastest, timing.Slowest));
            }
        }

        private static IServiceCollection BuildServices(string testDirectory)
        {
            WriteLine("Building Services");
            IServiceCollection services = new ServiceCollection();
            services.AddSimpleDB();

            SimpleDBSettings settings = new SimpleDBSettings
            {
                Path = testDirectory,
                EnycryptionKey = "TestEncryptionKey"
            };

            services.AddSingleton(settings);
            services.AddTransient<ISettingsProvider, SettingsProvider>();

            services.AddSingleton(typeof(TableRowDefinition), typeof(SettingsDataRow));
            services.AddSingleton(typeof(TableRowDefinition), typeof(TestDataDataRow));
            return services;
        }
    }
}
