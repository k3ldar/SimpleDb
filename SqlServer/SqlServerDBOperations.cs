/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 *  .Net Core Plugin Manager is distributed under the GNU General Public License version 3 and  
 *  is also available under alternative licenses negotiated directly with Simon Carter.  
 *  If you obtained Service Manager under the GPL, then the GPL applies to all loadable 
 *  Service Manager modules used on your system as well. The GPL (version 3) is 
 *  available at https://opensource.org/licenses/GPL-3.0
 *
 *  This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY,
 *  without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 *  See the GNU General Public License for more details.
 *
 *  The Original Code was created by Simon Carter (s1cart3r@gmail.com)
 *
 *  Copyright (c) 2018 - 2026 Simon Carter.  All Rights Reserved.
 *
 *  Product:  SimpleDB.SqlServer
 *  
 *  File: SqlServerDBOperations.cs
 *
 *  Purpose:  Full ISimpleDBOperations<T> + ISimpleDBTable implementation backed by
 *            SQL Server.  Schema is auto-derived from the TableRowDefinition subclass
 *            at construction time using SchemaMapper<T>.
 *
 *  Date        Name                Reason
 *  09/04/2026  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Reflection;
using System.Text;

using Microsoft.Data.SqlClient;

using PluginManager.Abstractions;

using Shared.Classes;

using SharedPluginFeatures;

using SimpleDB;
using SimpleDB.Internal;
using SimpleDB.Internal.Schema;

namespace SimpleDB.SqlServer.Internal
{
    /// <summary>
    /// SQL Server implementation of ISimpleDBOperations&lt;T&gt; and ISimpleDBTable.
    /// Each CRUD operation maps directly to individual SQL statements; the schema is
    /// created or migrated automatically at startup using SchemaMapper.
    /// </summary>
    internal sealed class SqlServerDBOperations<T> : ISimpleDBOperations<T>, ISimpleDBTable
        where T : TableRowDefinition
    {
        #region Private Classes

        private sealed class ForeignKeyRelation
        {
            public ForeignKeyRelation(string name, ForeignKeyAttributes foreignKeyAttributes)
            {
                if (String.IsNullOrEmpty(name))
                    throw new ArgumentNullException(nameof(name));

                Name = name;
                Attributes = foreignKeyAttributes;
            }

            public string Name { get; }
            public ForeignKeyAttributes Attributes { get; }
        }

        #endregion Private Classes

        #region Timing Keys

        private const string TimingsSelectAll = "TimingsSelectAll";
        private const string TimingsSelectId = "TimingsSelectId";
        private const string TimingsSelectPredicate = "TimingsSelectPredicate";
        private const string TimingsInsertList = "TimingsInsertList";
        private const string TimingsInsert = "TimingsInsert";
        private const string TimingsDeleteList = "TimingsDeleteList";
        private const string TimingsDelete = "TimingsDelete";
        private const string TimingsTruncate = "TimingsTruncate";
        private const string TimingsUpdateList = "TimingsUpdateList";
        private const string TimingsUpdate = "TimingsUpdate";
        private const string TimingsInsertOrUpdate = "TimingsInsertOrUpdate";
        private const string TimingsForceWrite = "TimingsForceWrite";

        private const long DefaultSequenceId = -1;

        #endregion Timing Keys

        #region Private Members

        private readonly string _connectionString;
        private readonly string _schemaName;
        private readonly string _tableName;
        private readonly string _fullTableName;   // pre-built: [schema].[table]
        private readonly bool _tableCreated;
        private readonly TableAttribute _tableAttributes;
        private readonly IReadOnlyList<ColumnDefinition> _columns;

        // Maps column name (case-insensitive) → PropertyInfo for columns with a public setter.
        // Used when mapping SqlDataReader rows back to T.
        private readonly Dictionary<string, PropertyInfo> _columnPropertyMap;

        // Cached list of columns that participate in UPDATE (all writable columns except Id and CreatedTicks)
        private readonly IReadOnlyList<ColumnDefinition> _updateColumns;

        private readonly Dictionary<string, ForeignKeyRelation> _foreignKeys;
        private readonly Dictionary<TriggerType, List<ITableTriggers<T>>> _triggersMap;
        private readonly IForeignKeyManager _foreignKeyManager;
        private readonly ISimpleDBManager _simpleDBManager;
        private readonly object _lockObject = new();
        private readonly bool _isMemoryCaching;

        private bool _disposed;
        private bool _hasInitialized;
        private long _primarySequence;
        private long _secondarySequence;
        private List<T> _allRecords = null;
        private int _recordCount = 0;

        private readonly Dictionary<string, Timings> _ReadWriteTimes = new()
        {
            { TimingsSelectAll, new() },
            { TimingsSelectId, new() },
            { TimingsSelectPredicate, new() },
            { TimingsInsertList, new() },
            { TimingsInsert, new() },
            { TimingsDeleteList, new() },
            { TimingsDelete, new() },
            { TimingsTruncate, new() },
            { TimingsUpdateList, new() },
            { TimingsUpdate, new() },
            { TimingsInsertOrUpdate, new() },
            { TimingsForceWrite, new() },
        };

        #endregion Private Members

        #region Constructor / Destructor

        public SqlServerDBOperations(
            ISimpleDBManager simpleDBManager,
            IForeignKeyManager foreignKeyManager,
            SqlServerSimpleDBSettings settings)
        {
            _simpleDBManager = simpleDBManager ?? throw new ArgumentNullException(nameof(simpleDBManager));
            _foreignKeyManager = foreignKeyManager ?? throw new ArgumentNullException(nameof(foreignKeyManager));

            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (String.IsNullOrEmpty(settings.ConnectionString))
                throw new ArgumentException("ConnectionString cannot be null or empty", nameof(settings));

            _connectionString = settings.ConnectionString;

			_tableAttributes = SchemaMapper.GetTableAttributes<T>() ?? throw new InvalidOperationException($"TableAttribute is missing from class {typeof(T).FullName}");
			_isMemoryCaching = _tableAttributes.CachingStrategy == CachingStrategy.Memory ||
                _tableAttributes.CachingStrategy == CachingStrategy.SlidingMemory ||
                _tableAttributes.WriteStrategy == WriteStrategy.Lazy;

            _triggersMap = [];
            foreach (TriggerType triggerType in Enum.GetValues(typeof(TriggerType)))
                _triggersMap.Add(triggerType, []);

            (_schemaName, _tableName) = SchemaMapper.GetTableName<T>();
            _fullTableName = $"[{_schemaName}].[{_tableName}]";

            _columns = SchemaMapper.GetColumns<T>();

            // Only map columns whose property has a public setter; properties with internal
            // setters (e.g. ReadOnly) are excluded here and handled separately via direct code.
            _columnPropertyMap = _columns
                .Where(c => c.Property.GetSetMethod() != null)
                .ToDictionary(c => c.ColumnName, c => c.Property, StringComparer.OrdinalIgnoreCase);

            // Pre-compute the subset used by UPDATE (everything except Id and CreatedTicks)
            _updateColumns = _columns
                .Where(c => !c.IsPrimaryKey && c.ColumnName != nameof(TableRowDefinition.CreatedTicks))
                .ToList()
                .AsReadOnly();

            _foreignKeys = GetForeignKeysForTable();

            SequenceManager.EnsureTable(_connectionString);
            _tableCreated = SchemaMapper.EnsureTable(_connectionString, _schemaName, _tableName, _columns);

            (_primarySequence, _secondarySequence) = SequenceManager.GetOrCreate(_connectionString, _tableName);

            _recordCount = QueryRecordCount();

            _simpleDBManager.RegisterTable(this);
            _foreignKeyManager.RegisterTable(this);
        }

        ~SqlServerDBOperations()
        {
            Dispose(false);
        }

        #endregion Constructor / Destructor

        #region ISimpleDBTable

        public string TableName => _tableAttributes?.TableName;

        public CachingStrategy CachingStrategy => _tableAttributes.CachingStrategy;

        public WriteStrategy WriteStrategy => _tableAttributes.WriteStrategy;

        public TimeSpan SlidingMemoryTimeout => _tableAttributes.SlidingMemoryTimeout;

        public Dictionary<string, Timings> GetAllTimings
        {
            get
            {
                Dictionary<string, Timings> result = [];
                foreach (KeyValuePair<string, Timings> item in _ReadWriteTimes)
                    result.Add(item.Key, item.Value.Clone());
                return result;
            }
        }

        public void Initialize(IPluginClassesService pluginClassesService)
        {
            if (pluginClassesService == null)
                throw new ArgumentNullException(nameof(pluginClassesService));

            if (_hasInitialized)
                return;

            ITableDefaults<T> tableDefaults = pluginClassesService
                .GetPluginClasses<ITableDefaults<T>>()
                .FirstOrDefault();

            List<ITableTriggers<T>> triggers = pluginClassesService.GetPluginClasses<ITableTriggers<T>>();

            foreach (TriggerType triggerType in Enum.GetValues(typeof(TriggerType)))
                _triggersMap[triggerType].AddRange(triggers.Where(t => t.TriggerTypes.HasFlag(triggerType)));

            if (_tableCreated && tableDefaults != null)
            {
                if (_primarySequence == DefaultSequenceId)
                {
                    _primarySequence = tableDefaults.PrimarySequence;
                    SequenceManager.Reset(_connectionString, _tableName, _primarySequence, _secondarySequence);
                }

                if (_secondarySequence == DefaultSequenceId)
                {
                    _secondarySequence = tableDefaults.SecondarySequence;
                    SequenceManager.Reset(_connectionString, _tableName, _primarySequence, _secondarySequence);
                }

                if (tableDefaults.InitialData != null)
                {
                    for (ushort version = 1; version < ushort.MaxValue; version++)
                    {
                        List<T> initialData = tableDefaults.InitialData(version);

                        if (initialData == null || initialData.Count == 0)
                            break;

                        Insert(initialData);
                    }
                }
            }

            _hasInitialized = true;
        }

        public bool IdExists(long id)
        {
            if (_isMemoryCaching && _allRecords != null)
                return _allRecords.Any(r => r.Id == id);

            using SqlConnection conn = new(_connectionString);
            conn.Open();
            using SqlCommand cmd = new($"SELECT COUNT(1) FROM {_fullTableName} WHERE [Id] = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return (int)cmd.ExecuteScalar() > 0;
        }

        public bool IdIsInUse(string propertyName, long value)
        {
            if (String.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));

            if (!_columnPropertyMap.ContainsKey(propertyName))
                throw new ArgumentException($"Unknown property '{propertyName}'", nameof(propertyName));

            if (_isMemoryCaching && _allRecords != null)
            {
                PropertyInfo prop = _columnPropertyMap[propertyName];
                foreach (T record in _allRecords)
                {
                    if (Convert.ToInt64(prop.GetValue(record)) == value)
                        return true;
                }
                return false;
            }

            using SqlConnection conn = new(_connectionString);
            conn.Open();
            using SqlCommand cmd = new($"SELECT COUNT(1) FROM {_fullTableName} WHERE [{propertyName}] = @value", conn);
            cmd.Parameters.AddWithValue("@value", value);
            return (int)cmd.ExecuteScalar() > 0;
        }

        public void ClearAllMemory()
        {
            using (TimedLock timedLock = TimedLock.Lock(_lockObject, TimeSpan.FromMilliseconds(30)))
            {
                _allRecords = null;
            }
        }

        public event SimpleDbEvent OnAction;

        #endregion ISimpleDBTable

        #region ISimpleDBOperations<T> — Properties

        /// <summary>Not applicable for the SQL Server backend; always returns 0.</summary>
        public int DataLength => 0;

        public int RecordCount => _recordCount;

        public long PrimarySequence => _primarySequence;

        public long SecondarySequence => _secondarySequence;

        /// <summary>Not applicable for the SQL Server backend; always returns 0.</summary>
        public byte CompactPercent => 0;

        public object TableLock => _lockObject;

        #endregion ISimpleDBOperations<T> — Properties

        #region ISimpleDBOperations<T> — Select

        public IReadOnlyList<T> Select()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_ReadWriteTimes[TimingsSelectAll]))
            {
                return InternalReadAllRecords().AsReadOnly();
            }
        }

        public T Select(long id)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_ReadWriteTimes[TimingsSelectId]))
            {
                // Use the cache when available to avoid an extra round-trip
                if (_isMemoryCaching && _allRecords != null)
                    return _allRecords.Find(r => r.Id == id);

                return ExecuteSelectById(id);
            }
        }

        public IReadOnlyList<T> Select(Func<T, bool> predicate)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_ReadWriteTimes[TimingsSelectPredicate]))
            {
                return InternalReadAllRecords().Where(predicate).ToList().AsReadOnly();
            }
        }

        #endregion ISimpleDBOperations<T> — Select

        #region ISimpleDBOperations<T> — Insert

        public void Insert(List<T> records)
        {
            Insert(records, new InsertOptions());
        }

        public void Insert(List<T> records, InsertOptions insertOptions)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            if (records == null)
                throw new ArgumentNullException(nameof(records));

            if (records.Count == 0)
                throw new ArgumentException("Does not contain any records", nameof(records));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_ReadWriteTimes[TimingsInsertList]))
            {
                InternalInsertRecords(records, insertOptions ?? new InsertOptions());
            }
        }

        public void Insert(T record)
        {
            Insert(record, new InsertOptions());
        }

        public void Insert(T record, InsertOptions insertOptions)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            if (record == null)
                throw new ArgumentNullException(nameof(record));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_ReadWriteTimes[TimingsInsert]))
            {
                InternalInsertRecords([record], insertOptions ?? new InsertOptions());
            }
        }

        #endregion ISimpleDBOperations<T> — Insert

        #region ISimpleDBOperations<T> — Delete

        public void Delete(List<T> records)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            if (records == null)
                throw new ArgumentNullException(nameof(records));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_ReadWriteTimes[TimingsDeleteList]))
            {
                InternalDeleteRecords(records);
            }
        }

        public void Delete(T record)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            if (record == null)
                throw new ArgumentNullException(nameof(record));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_ReadWriteTimes[TimingsDelete]))
            {
                InternalDeleteRecords([record]);
            }
        }

        public void Truncate()
        {
            using (StopWatchTimer timer = StopWatchTimer.Initialise(_ReadWriteTimes[TimingsTruncate]))
            {
                InternalDeleteRecords(InternalReadAllRecords());
            }
        }

        #endregion ISimpleDBOperations<T> — Delete

        #region ISimpleDBOperations<T> — Update

        public void Update(List<T> records)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            if (records == null)
                throw new ArgumentNullException(nameof(records));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_ReadWriteTimes[TimingsUpdateList]))
            {
                InternalUpdateRecords(records);
            }
        }

        public void Update(T record)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            if (record == null)
                throw new ArgumentNullException(nameof(record));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_ReadWriteTimes[TimingsUpdate]))
            {
                InternalUpdateRecords([record]);
            }
        }

        public void InsertOrUpdate(T record)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            if (record == null)
                throw new ArgumentNullException(nameof(record));

            using (StopWatchTimer timer = StopWatchTimer.Initialise(_ReadWriteTimes[TimingsInsertOrUpdate]))
            {
                if (IdExists(record.Id))
                    InternalUpdateRecords([record]);
                else
                    InternalInsertRecords([record], new InsertOptions());
            }
        }

        #endregion ISimpleDBOperations<T> — Update

        #region ISimpleDBOperations<T> — Misc

        /// <summary>
        /// All writes in the SQL Server backend are immediately committed to the database.
        /// This method is intentionally a no-op for the SQL Server provider.
        /// </summary>
        public void ForceWrite()
        {
            // no-op: SQL Server commits each statement immediately
        }

        #endregion ISimpleDBOperations<T> — Misc

        #region ISimpleDBOperations<T> — Sequences

        public long NextSequence() => InternalNextSequence(1);

        public long NextSequence(long increment)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            return InternalNextSequence(increment);
        }

        public long NextSecondarySequence(long increment)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            return InternalNextSecondarySequence(increment);
        }

        public void ResetSequence(long primarySequence, long secondarySequence)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlServerDBOperations<T>));

            using (TimedLock timedLock = TimedLock.Lock(_lockObject))
            {
                SequenceManager.Reset(_connectionString, _tableName, primarySequence, secondarySequence);
                _primarySequence = primarySequence;
                _secondarySequence = secondarySequence;
            }
        }

        #endregion ISimpleDBOperations<T> — Sequences

        #region ISimpleDBOperations<T> — Index checks

        public bool IndexExists(string name, object value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            List<ColumnDefinition> indexCols = _columns
                .Where(c => c.IsUniqueIndex && c.UniqueIndexName == name)
                .ToList();

            if (indexCols.Count == 0)
                throw new ArgumentOutOfRangeException(nameof(name), $"Index '{name}' does not exist on table '{TableName}'");

            // Primary key check — delegate to IdExists for a clean DB-level query
            if (indexCols.Count == 1 && indexCols[0].IsPrimaryKey)
                return IdExists(Convert.ToInt64(value));

            // Single-column unique index
            if (indexCols.Count == 1)
            {
                if (_isMemoryCaching && _allRecords != null)
                    return _allRecords.Any(r => Equals(indexCols[0].Property.GetValue(r), value));

                using SqlConnection conn = new(_connectionString);
                conn.Open();
                using SqlCommand cmd = new(
                    $"SELECT COUNT(1) FROM {_fullTableName} WHERE [{indexCols[0].ColumnName}] = @val", conn);
                cmd.Parameters.AddWithValue("@val", value ?? DBNull.Value);
                return (int)cmd.ExecuteScalar() > 0;
            }

            // Composite index: replicate the file-backend concatenation and scan
            string searchValue = value?.ToString() ?? String.Empty;
            return InternalReadAllRecords().Any(r =>
            {
                StringBuilder sb = new();
                foreach (ColumnDefinition col in indexCols)
                    sb.Append(col.Property.GetValue(r)?.ToString() ?? String.Empty);
                return sb.ToString() == searchValue;
            });
        }

        #endregion ISimpleDBOperations<T> — Index checks

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable

        #region Private Methods — Sequences

        private long InternalNextSequence(long increment)
        {
            using (TimedLock timedLock = TimedLock.Lock(_lockObject))
            {
                _primarySequence = SequenceManager.IncrementPrimary(_connectionString, _tableName, increment);
                return _primarySequence;
            }
        }

        private long InternalNextSecondarySequence(long increment)
        {
            using (TimedLock timedLock = TimedLock.Lock(_lockObject))
            {
                _secondarySequence = SequenceManager.IncrementSecondary(_connectionString, _tableName, increment);
                return _secondarySequence;
            }
        }

        #endregion Private Methods — Sequences

        #region Private Methods — Read

        private List<T> InternalReadAllRecords()
        {
            OnAction?.Invoke(this);

            if (_allRecords != null)
                return _allRecords;

            using (TimedLock timedLock = TimedLock.Lock(_lockObject))
            {
                List<T> result = ExecuteSelectAll();
                result.ForEach(r => { r.Immutable = true; r.Loaded = true; });

                if (_isMemoryCaching)
                    _allRecords = result;

                return result;
            }
        }

        private List<T> ExecuteSelectAll()
        {
            using SqlConnection conn = new(_connectionString);
            conn.Open();
            using SqlCommand cmd = new($"SELECT * FROM {_fullTableName}", conn);
            using SqlDataReader reader = cmd.ExecuteReader();
            return MapRecords(reader);
        }

        private T ExecuteSelectById(long id)
        {
            using SqlConnection conn = new(_connectionString);
            conn.Open();
            using SqlCommand cmd = new($"SELECT * FROM {_fullTableName} WHERE [Id] = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using SqlDataReader reader = cmd.ExecuteReader();
            return MapRecords(reader).Find(r => r.Id == id);
        }

        private T ExecuteSelectByIdWithConnection(SqlConnection conn, long id)
        {
            using SqlCommand cmd = new($"SELECT * FROM {_fullTableName} WHERE [Id] = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using SqlDataReader reader = cmd.ExecuteReader();
            List<T> results = MapRecords(reader);
            return results.Count > 0 ? results[0] : null;
        }

        /// <summary>
        /// Maps every row from the open SqlDataReader into T instances.
        /// Properties with a public setter are mapped by column name (case-insensitive).
        /// After populating all properties, Immutable and Loaded are set via InternalsVisibleTo
        /// so that subsequent property mutations correctly raise HasChanged.
        /// </summary>
        private List<T> MapRecords(SqlDataReader reader)
        {
            List<T> result = [];

            while (reader.Read())
            {
                T record = Activator.CreateInstance<T>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                        continue;

                    string colName = reader.GetName(i);

                    if (!_columnPropertyMap.TryGetValue(colName, out PropertyInfo property))
                        continue;

                    object dbValue = reader.GetValue(i);
                    Type targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                    property.SetValue(record, Convert.ChangeType(dbValue, targetType));
                }

                // Mark as loaded AFTER all properties are set so that property setters do not
                // prematurely flag HasChanged = true during the mapping phase.
                record.Immutable = true;
                record.Loaded = true;

                result.Add(record);
            }

            return result;
        }

        private int QueryRecordCount()
        {
            using SqlConnection conn = new(_connectionString);
            conn.Open();
            using SqlCommand cmd = new($"SELECT COUNT(1) FROM {_fullTableName}", conn);
            return (int)cmd.ExecuteScalar();
        }

        #endregion Private Methods — Read

        #region Private Methods — Insert

        private void InternalInsertRecords(List<T> records, InsertOptions insertOptions)
        {
            using (TimedLock timedLock = TimedLock.Lock(_lockObject))
            {
                ValidateUniqueIndexesBeforeInsert(records);

                long nextSequence;

                if (insertOptions.AssignPrimaryKey)
                {
                    nextSequence = _primarySequence + 1;
                    _ = NextSequence(records.Count);
                }
                else
                {
                    nextSequence = 0;
                }

                _triggersMap[TriggerType.BeforeInsert].ForEach(t => t.BeforeInsert(records));

                using SqlConnection conn = new(_connectionString);
                conn.Open();

                foreach (T record in records)
                {
                    ValidateForeignKeys(record);

                    if (insertOptions.AssignPrimaryKey)
                        record.Id = nextSequence++;

                    try
                    {
                        ExecuteInsert(conn, record);
                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                    {
                        // 2627 = unique constraint violation; 2601 = unique index violation
                        throw new UniqueIndexException(
                            $"Unique index violation: Table={TableName}; {ex.Message}");
                    }

                    if (_isMemoryCaching && _allRecords != null)
                        _allRecords.Add(record);
                }

                _recordCount += records.Count;

                _triggersMap[TriggerType.AfterInsert].ForEach(t => t.AfterInsert(records));

                OnAction?.Invoke(this);
            }
        }

        private void ExecuteInsert(SqlConnection conn, T record)
        {
            string colNames = String.Join(", ", _columns.Select(c => $"[{c.ColumnName}]"));
            string paramNames = String.Join(", ", _columns.Select(c => $"@{c.ColumnName}"));

            using SqlCommand cmd = new($"INSERT INTO {_fullTableName} ({colNames}) VALUES ({paramNames})", conn);

            foreach (ColumnDefinition col in _columns)
            {
                object value = col.Property.GetValue(record);
                cmd.Parameters.AddWithValue($"@{col.ColumnName}", value ?? DBNull.Value);
            }

            cmd.ExecuteNonQuery();
        }

        #endregion Private Methods — Insert

        #region Private Methods — Update

        private void InternalUpdateRecords(List<T> records)
        {
            using (TimedLock timedLock = TimedLock.Lock(_lockObject))
            {
                records = records.Where(r => r.HasChanged).ToList();

                if (records.Count == 0)
                    return;

                if (_foreignKeys.Count > 0)
                    ValidateForeignKeys(records);

                _triggersMap[TriggerType.BeforeUpdate].ForEach(t => t.BeforeUpdate(records));

                using SqlConnection conn = new(_connectionString);
                conn.Open();

                foreach (T record in records)
                {
                    if (_triggersMap[TriggerType.BeforeUpdateCompare].Count > 0)
                    {
                        T oldRecord = ExecuteSelectByIdWithConnection(conn, record.Id);

                        if (oldRecord != null)
                            _triggersMap[TriggerType.BeforeUpdateCompare]
                                .ForEach(t => t.BeforeUpdate(record, oldRecord));
                    }

                    ExecuteUpdate(conn, record);

                    if (_isMemoryCaching && _allRecords != null)
                    {
                        int idx = _allRecords.FindIndex(r => r.Id == record.Id);
                        if (idx >= 0)
                            _allRecords[idx] = record;
                    }
                }

                _triggersMap[TriggerType.AfterUpdate].ForEach(t => t.AfterUpdate(records));

                records.ForEach(r => r.HasChanged = false);

                OnAction?.Invoke(this);
            }
        }

        private void ExecuteUpdate(SqlConnection conn, T record)
        {
            string setClauses = String.Join(", ", _updateColumns.Select(c => $"[{c.ColumnName}] = @{c.ColumnName}"));

            using SqlCommand cmd = new(
                $"UPDATE {_fullTableName} SET {setClauses} WHERE [Id] = @Id", conn);

            foreach (ColumnDefinition col in _updateColumns)
            {
                object value = col.Property.GetValue(record);
                cmd.Parameters.AddWithValue($"@{col.ColumnName}", value ?? DBNull.Value);
            }

            cmd.Parameters.AddWithValue("@Id", record.Id);
            cmd.ExecuteNonQuery();
        }

        #endregion Private Methods — Update

        #region Private Methods — Delete

        private void InternalDeleteRecords(List<T> records)
        {
            using (TimedLock timedLock = TimedLock.Lock(_lockObject))
            {
                ValidateForeignKeysPriorToDelete(records);

                _triggersMap[TriggerType.BeforeDelete].ForEach(t => t.BeforeDelete(records));

                using SqlConnection conn = new(_connectionString);
                conn.Open();

                foreach (T record in records)
                {
                    ExecuteDelete(conn, record.Id);

                    if (_isMemoryCaching && _allRecords != null)
                        _allRecords.RemoveAll(r => r.Id == record.Id);
                }

                _recordCount -= records.Count;
                if (_recordCount < 0)
                    _recordCount = 0;

                _triggersMap[TriggerType.AfterDelete].ForEach(t => t.AfterDelete(records));

                OnAction?.Invoke(this);
            }
        }

        private void ExecuteDelete(SqlConnection conn, long id)
        {
            using SqlCommand cmd = new($"DELETE FROM {_fullTableName} WHERE [Id] = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        #endregion Private Methods — Delete

        #region Private Methods — Validation

        private void ValidateUniqueIndexesBeforeInsert(List<T> records)
        {
			List<IGrouping<string, ColumnDefinition>> indexGroups = _columns
                .Where(c => c.IsUniqueIndex)
                .GroupBy(c => c.UniqueIndexName)
                .ToList();

            foreach (T record in records)
            {
                foreach (IGrouping<string, ColumnDefinition> group in indexGroups)
                {
                    List<ColumnDefinition> cols = [.. group];

                    List<string> whereParts = [];
                    for (int i = 0; i < cols.Count; i++)
                        whereParts.Add($"[{cols[i].ColumnName}] = @p{i}");

                    string sql = $"SELECT COUNT(1) FROM {_fullTableName} WHERE {String.Join(" AND ", whereParts)}";

                    using SqlConnection conn = new(_connectionString);
                    conn.Open();
                    using SqlCommand cmd = new(sql, conn);

                    for (int i = 0; i < cols.Count; i++)
                    {
                        object val = cols[i].Property.GetValue(record) ?? DBNull.Value;
                        cmd.Parameters.AddWithValue($"@p{i}", val);
                    }

                    if ((int)cmd.ExecuteScalar() > 0)
                        throw new UniqueIndexException(
                            $"Unique index violation: Table={TableName}, Index={group.Key}, " +
                            $"Property={String.Join(",", cols.Select(c => c.ColumnName))}");
                }
            }
        }

        private void ValidateForeignKeys(List<T> records)
        {
            foreach (KeyValuePair<string, ForeignKeyRelation> foreignKey in _foreignKeys)
            {
                foreach (T record in records)
                {
                    long keyValue = Convert.ToInt64(
                        record.GetType().GetProperty(foreignKey.Key)?.GetValue(record, null));

                    ForeignKeyRelation relation = foreignKey.Value;

                    if (!_foreignKeyManager.ValueExists(relation.Name, keyValue) &&
                        !(relation.Attributes == ForeignKeyAttributes.DefaultValue && keyValue == 0))
                    {
                        throw new ForeignKeyException(
                            $"Foreign key value {keyValue} does not exist in table {relation.Name}; " +
                            $"Table: {TableName}; Property: {foreignKey.Key}");
                    }
                }
            }
        }

        private void ValidateForeignKeys(T record)
        {
            foreach (KeyValuePair<string, ForeignKeyRelation> foreignKey in _foreignKeys)
            {
                long keyValue = Convert.ToInt64(
                    record.GetType().GetProperty(foreignKey.Key)?.GetValue(record, null));

                ForeignKeyRelation relation = foreignKey.Value;

                if (!_foreignKeyManager.ValueExists(relation.Name, keyValue) &&
                    !(relation.Attributes == ForeignKeyAttributes.DefaultValue && keyValue == 0))
                {
                    throw new ForeignKeyException(
                        $"Foreign key value {keyValue} does not exist in table {relation.Name}; " +
                        $"Table: {TableName}; Property: {foreignKey.Key}");
                }
            }
        }

        private void ValidateForeignKeysPriorToDelete(List<T> records)
        {
            // Check all long-typed unique-indexed columns (they are candidates for FK references)
            List<ColumnDefinition> indexedLongCols = _columns
                .Where(c => c.IsUniqueIndex &&
                    (c.Property.PropertyType == typeof(long) ||
                     c.Property.PropertyType == typeof(long?)))
                .ToList();

            foreach (ColumnDefinition col in indexedLongCols)
            {
                foreach (T record in records)
                {
                    object keyValueObj = col.Property.GetValue(record);

                    if (keyValueObj != null && Int64.TryParse(keyValueObj.ToString(), out long keyValue))
                    {
                        ForeignKeyUsage usage = _foreignKeyManager.ValueInUse(
                            TableName, col.ColumnName, keyValue, out string table, out string propertyName);

                        if (usage == ForeignKeyUsage.Referenced &&
                            usage != ForeignKeyUsage.AllowDefault &&
                            usage != ForeignKeyUsage.CascadeDelete)
                        {
                            throw new ForeignKeyException(
                                $"Foreign key value {keyValue} from table {TableName} is being used " +
                                $"in Table: {table}; Property: {propertyName}");
                        }
                    }
                }
            }
        }

        #endregion Private Methods — Validation

        #region Private Methods — Reflection / Schema

        private Dictionary<string, ForeignKeyRelation> GetForeignKeysForTable()
        {
            Dictionary<string, ForeignKeyRelation> result = [];

            foreach (PropertyInfo property in typeof(T).GetProperties())
            {
                ForeignKeyAttribute foreignKey = property.GetCustomAttribute<ForeignKeyAttribute>();

                if (foreignKey != null && property.PropertyType == typeof(long))
                {
                    _foreignKeyManager.AddRelationShip(
                        TableName, foreignKey.TableName, property.Name,
                        foreignKey.PropertyName, foreignKey.Attributes);

                    result.Add(property.Name, new ForeignKeyRelation(foreignKey.TableName, foreignKey.Attributes));
                }
            }

            return result;
        }

        #endregion Private Methods — Reflection / Schema

        #region Private Methods — Dispose

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _simpleDBManager?.UnregisterTable(this);
                _foreignKeyManager?.UnregisterTable(this);
            }

            _disposed = true;
        }

        #endregion Private Methods — Dispose
    }
}
