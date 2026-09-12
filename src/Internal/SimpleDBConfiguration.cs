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
 *  Copyright (c) 2018 - 2023 Simon Carter.  All Rights Reserved.
 *
 *  Product:  SimpleDB
 *  
 *  File: SimpleDBConfiguration.cs
 *
 *  Purpose:  Ambient, process wide tuning values for SimpleDB internals
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System.ComponentModel;
using System.Diagnostics;

namespace SimpleDB.Internal
{
    /// <summary>
    /// Process wide tuning values, taken once from <see cref="SimpleDBSettings"/> at startup and
    /// read directly by the internals that need them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists because the lock timeouts and the checkpoint threshold are needed by types that
    /// are not resolved from the container - index managers and foreign key managers are created by
    /// the tables that own them, several layers below where <see cref="SimpleDBSettings"/> is
    /// available. Threading settings down through those layers is pure plumbing that carries no
    /// domain meaning, and it cannot work at all for the
    /// <see cref="SimpleDBManager(string, string, SimpleDBSettings)"/> constructors, which are handed a path and key
    /// with no settings object in sight.
    /// </para>
    /// <para>
    /// Static mutable state is only safe here because of a strict invariant: the values are written
    /// exactly once, during <see cref="SimpleDBManager"/> construction, and are immutable
    /// afterwards. <see cref="Configure(SimpleDBSettings)"/> throws on a second, differing call
    /// rather than silently reconfiguring a running database. Consumers therefore read at the point
    /// of use rather than capturing at construction, which removes any dependency on the order in
    /// which the container happens to build things.
    /// </para>
    /// <para>
    /// Deliberately excluded are <see cref="SimpleDBSettings.Path"/> and
    /// <see cref="SimpleDBSettings.EnycryptionKey"/>. Those identify a particular database, and
    /// nothing prevents two managers over different directories in one process. Only genuinely
    /// process wide tuning knobs belong here.
    /// </para>
    /// </remarks>
    internal static class SimpleDBConfiguration
    {
        /// <summary>
        /// Smallest permitted timeout. A zero or near zero timeout would turn ordinary contention
        /// into spurious failures, so a supplied value below this is rejected outright.
        /// </summary>
        private const uint MinimumTimeoutMs = 10;

        /// <summary>
        /// Largest permitted timeout. Anything beyond this is indistinguishable from a hang and is
        /// far more likely to be a units mistake (seconds supplied where milliseconds were meant).
        /// </summary>
        private const uint MaximumTimeoutMs = 300000;

        private static readonly object _configureLock = new();

        private static Snapshot _current = Snapshot.Default;
        private static int _configured;

        /// <summary>
        /// Timeout for acquiring the table lock in order to release cached records from memory.
        /// </summary>
        internal static TimeSpan ClearMemoryLockTimeout => Read().ClearMemory;

        /// <summary>
        /// Timeout for acquiring the database lock in order to run a checkpoint. Short, because a
        /// checkpoint is optional and is cheaper to abandon than to stall real work for.
        /// </summary>
        internal static TimeSpan CheckpointLockTimeout => Read().Checkpoint;

        /// <summary>
        /// Timeout for acquiring the database lock for ordinary work. Generous, because the only
        /// long held section is a checkpoint flushing already in memory tables to disk.
        /// </summary>
        internal static TimeSpan OperationLockTimeout => Read().Operation;

        /// <summary>
        /// Timeout for acquiring an index manager's lock.
        /// </summary>
        internal static TimeSpan IndexManagerLockTimeout => Read().IndexManager;

        /// <summary>
        /// Timeout for acquiring the foreign key manager's lock.
        /// </summary>
        internal static TimeSpan ForeignKeyManagerLockTimeout => Read().ForeignKeyManager;

        /// <summary>
        /// Number of WAL entries that must accumulate before a database wide checkpoint is
        /// attempted.
        /// </summary>
        internal static uint WalCheckpointThreshold => Read().WalCheckpointThreshold;

        /// <summary>
        /// Indicates whether <see cref="Configure(SimpleDBSettings)"/> has been called. Built in
        /// defaults are in force until it has.
        /// </summary>
        internal static bool IsConfigured => Volatile.Read(ref _configured) == 1;

        /// <summary>
        /// Applies application supplied settings. Called once, from <see cref="SimpleDBManager"/>
        /// construction.
        /// </summary>
        /// <param name="settings">Settings to apply.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if a value is outside the permitted range.</exception>
        /// <exception cref="InvalidOperationException">Thrown if already configured with different values.</exception>
        internal static void Configure(SimpleDBSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            // The values are copied out rather than the instance being retained, so that an
            // application holding a reference to its own settings object cannot alter the
            // checkpoint threshold or a lock timeout while the database is running.
            Snapshot snapshot = new(
                ValidateTimeout(settings.LockTimeoutClearMemoryMs, nameof(settings.LockTimeoutClearMemoryMs)),
                ValidateTimeout(settings.LockTimeoutCheckpointMs, nameof(settings.LockTimeoutCheckpointMs)),
                ValidateTimeout(settings.LockTimeoutOperationMs, nameof(settings.LockTimeoutOperationMs)),
                ValidateTimeout(settings.LockTimeoutIndexManagerMs, nameof(settings.LockTimeoutIndexManagerMs)),
                ValidateTimeout(settings.LockTimeoutForeignKeyManagerMs, nameof(settings.LockTimeoutForeignKeyManagerMs)),
                ValidateThreshold(settings.WalCheckpointThreshold));

            lock (_configureLock)
            {
                // A second manager built from identical settings is normal and harmless. A second
                // manager asking for *different* tuning is not, because tables created under the
                // first manager are already running against the original values - so it fails
                // loudly here rather than producing a database with two different lock policies.
                if (IsConfigured && !_current.Equals(snapshot))
                {
                    throw new InvalidOperationException(
                        "SimpleDB has already been configured with different settings. Database wide " +
                        "settings are applied once at startup and cannot be changed afterwards.");
                }

                _current = snapshot;
                Volatile.Write(ref _configured, 1);
            }
        }

        /// <summary>
        /// Restores the built in defaults. Exists so that tests which exercise different settings
        /// do not leak values into one another; it is not part of the supported API.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static void Reset()
        {
            lock (_configureLock)
            {
                _current = Snapshot.Default;
                Volatile.Write(ref _configured, 0);
            }
        }

        private static Snapshot Read()
        {
            // Reading before configuration means the built in defaults are silently in force, so
            // an application supplied value would appear to be ignored for the lifetime of the
            // process. That is close to undiagnosable at runtime, hence the assertion during
            // development. Release builds fall back to the defaults, which are always valid.
            Debug.Assert(IsConfigured,
                "SimpleDBConfiguration was read before Configure was called; built in defaults are in force " +
                "and any application supplied setting has been ignored.");

            return _current;
        }

        private static TimeSpan ValidateTimeout(uint milliseconds, string propertyName)
        {
            if (milliseconds < MinimumTimeoutMs || milliseconds > MaximumTimeoutMs)
            {
                throw new ArgumentOutOfRangeException(propertyName, milliseconds,
                    $"Lock timeout must be between {MinimumTimeoutMs}ms and {MaximumTimeoutMs}ms.");
            }

            return TimeSpan.FromMilliseconds(milliseconds);
        }

        private static uint ValidateThreshold(uint threshold)
        {
            // A zero threshold would checkpoint on every single commit, destroying the point of
            // having a WAL at all.
            if (threshold == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(SimpleDBSettings.WalCheckpointThreshold), threshold,
                    "WAL checkpoint threshold must be greater than zero.");
            }

            return threshold;
        }

        /// <summary>
        /// Immutable set of tuning values. A single readonly struct means a consumer always sees a
        /// coherent set rather than a mixture from two different configurations.
        /// </summary>
        private readonly struct Snapshot : IEquatable<Snapshot>
        {
            internal Snapshot(TimeSpan clearMemory, TimeSpan checkpoint, TimeSpan operation,
                TimeSpan indexManager, TimeSpan foreignKeyManager, uint walCheckpointThreshold)
            {
                ClearMemory = clearMemory;
                Checkpoint = checkpoint;
                Operation = operation;
                IndexManager = indexManager;
                ForeignKeyManager = foreignKeyManager;
                WalCheckpointThreshold = walCheckpointThreshold;
            }

            /// <summary>
            /// Built in defaults, matching the property initialisers on <see cref="SimpleDBSettings"/>.
            /// In force until an application configures otherwise.
            /// </summary>
            internal static Snapshot Default => new(
                TimeSpan.FromMilliseconds(300),
                TimeSpan.FromMilliseconds(250),
                TimeSpan.FromMilliseconds(30000),
                TimeSpan.FromMilliseconds(10000),
                TimeSpan.FromMilliseconds(10000),
                1000);

            internal TimeSpan ClearMemory { get; }

            internal TimeSpan Checkpoint { get; }

            internal TimeSpan Operation { get; }

            internal TimeSpan IndexManager { get; }

            internal TimeSpan ForeignKeyManager { get; }

            internal uint WalCheckpointThreshold { get; }

            public bool Equals(Snapshot other)
            {
                return ClearMemory == other.ClearMemory &&
                    Checkpoint == other.Checkpoint &&
                    Operation == other.Operation &&
                    IndexManager == other.IndexManager &&
                    ForeignKeyManager == other.ForeignKeyManager &&
                    WalCheckpointThreshold == other.WalCheckpointThreshold;
            }

            public override bool Equals(object obj)
            {
                return obj is Snapshot other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ClearMemory, Checkpoint, Operation, IndexManager,
                    ForeignKeyManager, WalCheckpointThreshold);
            }
        }
    }
}
