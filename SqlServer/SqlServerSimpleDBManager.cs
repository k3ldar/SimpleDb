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
 *  File: SqlServerSimpleDBManager.cs
 *
 *  Purpose:  ISimpleDBManager implementation for the SQL Server backend
 *
 *  Date        Name                Reason
 *  09/04/2026  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using PluginManager.Abstractions;

using Shared.Classes;

using SimpleDB;

#pragma warning disable CA2208

namespace SimpleDB.SqlServer
{
    /// <summary>
    /// SQL Server implementation of ISimpleDBManager that manages table instances and memory cleanup
    /// </summary>
    public sealed class SqlServerSimpleDBManager : ThreadManager, ISimpleDBManager
    {
        private const int ThreadRuntime = 500;

        private readonly Dictionary<string, ISimpleDBTable> _tables = [];
        private readonly Dictionary<ISimpleDBTable, DateTime> _tableLastAction = [];
        private readonly object _lock = new();

        #region Constructors

        private SqlServerSimpleDBManager()
            : base(null, TimeSpan.FromMilliseconds(ThreadRuntime))
        {
            ContinueIfGlobalException = true;
        }

        public SqlServerSimpleDBManager(SqlServerSimpleDBSettings settings)
            : this()
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (String.IsNullOrEmpty(settings.ConnectionString))
                throw new ArgumentException("ConnectionString cannot be null or empty", nameof(settings));

            ConnectionString = settings.ConnectionString;
        }

        #endregion Constructors

        /// <summary>
        /// SQL Server connection string used by this manager
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        /// Not applicable for the SQL Server backend; always returns null.
        /// </summary>
        public string Path => null;

        public void Initialize(IPluginClassesService pluginClassesService)
        {
            foreach (KeyValuePair<string, ISimpleDBTable> table in _tables)
                table.Value.Initialize(pluginClassesService);
        }

        public void ClearMemory()
        {
            foreach (string table in _tables.Keys)
                _tables[table].ClearAllMemory();
        }

        public void RegisterTable(ISimpleDBTable simpleDBTable)
        {
            if (simpleDBTable == null)
                throw new ArgumentNullException(nameof(simpleDBTable));

            using (TimedLock timedLock = TimedLock.Lock(_lock))
            {
                if (String.IsNullOrEmpty(simpleDBTable.TableName))
                    throw new InvalidOperationException("Null table name");

                if (_tables.ContainsKey(simpleDBTable.TableName))
                    throw new InvalidOperationException($"Table {simpleDBTable.TableName} already exists");

                _tables.Add(simpleDBTable.TableName, simpleDBTable);

                if (simpleDBTable.CachingStrategy == CachingStrategy.SlidingMemory)
                {
                    _tableLastAction.Add(simpleDBTable, DateTime.UtcNow);
                    simpleDBTable.OnAction += SimpleDBTable_OnAction;

                    if (!ThreadManager.Exists(nameof(SqlServerSimpleDBManager)))
                        ThreadManager.ThreadStart(this, nameof(SqlServerSimpleDBManager), ThreadPriority.Lowest);
                }
            }
        }

        public void UnregisterTable(ISimpleDBTable simpleDBTable)
        {
            if (simpleDBTable == null)
                throw new ArgumentNullException(nameof(simpleDBTable));

            using (TimedLock timedLock = TimedLock.Lock(_lock))
            {
                if (String.IsNullOrEmpty(simpleDBTable.TableName))
                    return;

                if (!_tables.ContainsKey(simpleDBTable.TableName))
                    throw new ArgumentException($"Table {simpleDBTable.TableName} is not registered");

                _tables.Remove(simpleDBTable.TableName);

                if (_tableLastAction.ContainsKey(simpleDBTable))
                {
                    simpleDBTable.OnAction -= SimpleDBTable_OnAction;
                    _tableLastAction.Remove(simpleDBTable);
                }
            }
        }

        public IReadOnlyDictionary<string, ISimpleDBTable> Tables => new Dictionary<string, ISimpleDBTable>(_tables);

        public event SimpleDbEvent OnMemoryCleared;

        protected override bool Run(object parameters)
        {
            using (TimedLock timedLock = TimedLock.Lock(_lock))
            {
                foreach (ISimpleDBTable simpleDBTable in _tableLastAction.Keys)
                {
                    DateTime lastRun = _tableLastAction[simpleDBTable];
                    TimeSpan timeFromLastRun = DateTime.UtcNow - lastRun;

                    if (timeFromLastRun > simpleDBTable.SlidingMemoryTimeout)
                    {
                        try
                        {
                            simpleDBTable.ClearAllMemory();
                            _tableLastAction[simpleDBTable] = DateTime.UtcNow;
                        }
                        catch (LockTimeoutException)
                        {
                            // ignore specific exception
                        }

                        OnMemoryCleared?.Invoke(simpleDBTable);
                    }
                }
            }

            return !HasCancelled();
        }

        #region Private Methods

        private void SimpleDBTable_OnAction(ISimpleDBTable sender)
        {
            using (TimedLock timedLock = TimedLock.Lock(_lock))
            {
                if (!_tableLastAction.ContainsKey(sender))
                    throw new InvalidOperationException($"Table {sender.TableName} failed to register sliding memory");

                _tableLastAction[sender] = DateTime.UtcNow;
            }
        }

        #endregion Private Methods
    }
}

#pragma warning restore CA2208
