# SimpleDB
## About

SimpleDB allows you to design your tables using standard C# classes, there are a number of methods for standard CRUD operations and support for the following features:

- Foreign Keys
- Unique indexes across multiple properties
- Before and after triggers for insert, update and delete
- Sequences for unique indexes
- Select Caching Strategy for individual classes (tables)
    - None - Records are read from storage as required.
    - Memory - Records are kept in memory.
    - Sliding - Records are retained in memory for n ms and when no longer used, memory is released.
- Select Write Strategy for individual classes (tables)
    - Forced - Records are saved immediately to storage
    - Lazy - Records are saved periodically to storage or after specific time.
- Compression types for saving data to storage
    - None - Data is not compressed
    - Brotli - Data is compressed prior to saving
- Atomic, multi-table transactions with commit/rollback, backed by a Write-Ahead Log (WAL) for durability and crash recovery

## How it works

The class represents a record (row) of data, which exposes properties for the data (columns), there is a TableAttribute that defines the policies for the table of data.  The class must descend from TableRowDefinition class in order to work.

Only properties which are public get/set are saved to storage, read only properties are not saved.  One important caveat is that the set method must call the Update() method which is defined in TableRowDefinition, this ensures that any changes are recognised when performing insert or update actions.

```csharp
    [Table("Settings", CompressionType.Brotli, CachingStrategy.None)]
    internal class SettingsDataRow : TableRowDefinition
    {
        string _name;
        string _value;

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;

                _name = value;
                Update();
            }
        }

        public string Value
        {
            get
            {
                return _value;
            }
            set
            {
                if (_value == value)
                    return;

                _value = value;
                Update();
            }
        }
    }
```

## Registering Tables

SimpleDB has been designed with IoC in mind, tables can be registered and retrieved through DI engines.

```csharp
    services.AddSingleton(typeof(TableRowDefinition), typeof(SettingsDataRow));
```

## Using tables

```csharp
    internal sealed class SettingsProvider : IApplicationSettingsProvider
    {
        private readonly ISimpleDBOperations<SettingsDataRow> _settingsData;

        public SettingsProvider(ISimpleDBOperations<SettingsDataRow> settingsData)
        {
            _settingsData = settingsData ?? throw new ArgumentNullException(nameof(settingsData));
        }
    }
```

You can then call methods on _settingsData to perform normal CRUD operations.

## Transactions and the Write-Ahead Log (WAL)

SimpleDB (file backend) supports atomic, multi-table transactions backed by a Write-Ahead Log. Every change made under a transaction is first recorded in the WAL before it is applied to the in-memory/on-disk table state, so a crash or an explicit rollback can always restore the pre-transaction state.

### Getting a transaction

Transactions are created and finalized through `ITransactionManager`, which is registered automatically by `AddSimpleDB()`:

```csharp
    services.AddSingleton<ITransactionManager, TransactionManager>(); // registered for you
```

```csharp
    public interface ITransactionManager : ITransactionMetrics
    {
        ITransaction BeginTransaction(TransactionAccessMode accessMode = TransactionAccessMode.ReadWrite,
            TransactionIsolationLevel isolationLevel = TransactionIsolationLevel.DirtyRead);
        void CommitTransaction(ITransaction transaction);
        void RollbackTransaction(ITransaction transaction);
    }

    public interface ITransaction
    {
        long TransactionId { get; }
        void Commit();
        void Rollback();
        TransactionAccessMode AccessMode { get; }
        TransactionIsolationLevel IsolationLevel { get; }
    }
```

`ITransaction` exposes `Commit()`/`Rollback()` directly, so most code only needs `ITransactionManager.BeginTransaction()`.

### Access mode and isolation level

`BeginTransaction` accepts an optional `TransactionAccessMode` and `TransactionIsolationLevel`:

- `TransactionAccessMode.ReadWrite` (default) - the transaction can Insert/Update/Delete/Truncate.
- `TransactionAccessMode.ReadOnly` - any attempt to Insert, Update, Delete or Truncate under the transaction throws a `TransactionException`. Use this for read-only workloads where you want a consistent view without incurring WAL writes.
- `TransactionIsolationLevel.DirtyRead` is currently the **only** isolation level supported, and is also the default. It means uncommitted changes made by a transaction are visible to other transactions before the owning transaction commits. The `TransactionIsolationLevel` enum exists to allow additional isolation levels to be added in future without a breaking API change, but no other isolation level is implemented yet.

```csharp
    ITransaction readOnlyTransaction = transactionManager.BeginTransaction(TransactionAccessMode.ReadOnly);
```

### Performing operations within a transaction

Each table's `ISimpleDBOperations<T>` also implements `ITransactionalSimpleDBOperations<T>`, which mirrors the standard Insert/Update/Delete/Truncate methods but accepts an `ITransaction`:

```csharp
    void Insert(T record, InsertOptions insertOptions, ITransaction transaction);
    void Insert(List<T> records, InsertOptions insertOptions, ITransaction transaction);
    void Update(T record, ITransaction transaction);
    void Update(List<T> records, ITransaction transaction);
    void Delete(T record, ITransaction transaction);
    void Delete(List<T> records, ITransaction transaction);
    void Truncate(ITransaction transaction);
```

Example combining two tables in a single transaction:

```csharp
    ITransactionManager transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
    ISimpleDBOperations<SettingsDataRow> settingsTable = serviceProvider.GetRequiredService<ISimpleDBOperations<SettingsDataRow>>();
    ITransactionalSimpleDBOperations<SettingsDataRow> transactionalSettings =
        (ITransactionalSimpleDBOperations<SettingsDataRow>)settingsTable;

    ITransaction transaction = transactionManager.BeginTransaction();

    try
    {
        transactionalSettings.Insert(new SettingsDataRow { Name = "Theme", Value = "Dark" }, new InsertOptions(), transaction);
        transactionalSettings.Insert(new SettingsDataRow { Name = "Locale", Value = "en-GB" }, new InsertOptions(), transaction);

        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
```

If `Rollback()` is called (or a failure occurs before `Commit()`), every Insert/Update/Delete recorded against that transaction is undone using the before-image (`UndoRecord`) captured in the WAL entry, and none of the changes become visible.

### How the WAL works

- Every Insert, Update or Delete performed inside a transaction is written as a `WalEntry` (transaction id, table name, operation type, record id, serialized record, sequence number and, for updates/deletes, the before-image needed for rollback) to a durable WAL table **before** the change is applied.
- `Commit()` writes a `Commit` marker for the transaction; `Rollback()` writes an `Abort` marker and replays the collected undo records.
- Tables that should not participate in the WAL (e.g. internal/system tables) can use the no-op `NullWalRecorder`; all regular tables use the durable `WalRecorder` by default.
- Periodically, once the WAL table's record count reaches `SimpleDBSettings.WalCheckpointThreshold` (default `1000`), `TransactionManager` performs a checkpoint: committed entries are flushed to their owning tables, watermarks are advanced, and the WAL is truncated. Checkpoints are skipped while other transactions are still active, so in-flight work is never checkpointed out from under a concurrent transaction.
- `ITransactionMetrics` (implemented by `ITransactionManager`) exposes `ActiveTransactionCount`, `CheckpointsCompleted`, `CheckpointsSkippedForActiveTransactions` and operation timings, which is useful for monitoring transaction/WAL throughput.

### Configuration

`SimpleDBSettings.WalCheckpointThreshold` (default `1000`) controls how many WAL entries accumulate before an automatic checkpoint runs:

```csharp
    services.AddSimpleDB(options =>
    {
        options.Path = "C:\\Data\\";
        options.WalCheckpointThreshold = 500;
    });
```

### Failure handling

Transaction and WAL failures (e.g. an invalid transaction state, or an I/O failure while writing WAL entries) are surfaced as `TransactionException`.

## More Information
More information is available at https://www.pluginmanager.website/Docs/ or by visiting the GitHub Homepage https://github.com/k3ldar/.NetCorePluginManager
