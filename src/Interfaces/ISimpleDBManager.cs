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
 *  File: ISimpleDBInitializer.cs
 *
 *  Purpose:  ISimpleDBInitializer interface for SimpleDB
 *
 *  Date        Name                Reason
 *  23/05/2022  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using PluginManager.Abstractions;

namespace SimpleDB
{
    /// <summary>
    /// Interface for managing SimpleDB initialization and other key areas of operation
    /// </summary>
    public interface ISimpleDBManager
    {
        /// <summary>
        /// Path to database files
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Registers a table with DB Manager
        /// </summary>
        /// <param name="simpleDBTable"></param>
        void RegisterTable(ISimpleDBTable simpleDBTable);

        /// <summary>
        /// Unregisters a table from the DB Manager
        /// </summary>
        /// <param name="simpleDBTable"></param>
        void UnregisterTable(ISimpleDBTable simpleDBTable);

        /// <summary>
        /// List of all tables that have been registered
        /// </summary>
        IReadOnlyDictionary<string, ISimpleDBTable> Tables { get; }

        /// <summary>
        /// Initializes all tables after they have been loaded
        /// </summary>
        void Initialize(IPluginClassesService pluginClassesService);

        /// <summary>
        /// Clears all memory used by tables that have a strategy to retain data in memory
        /// </summary>
        void ClearMemory();

        /// <summary>
        /// Event raised when memory is cleared
        /// </summary>
        event SimpleDbEvent OnMemoryCleared;

        /// <summary>
        /// Backs up the database to a single, Brotli compressed file.
        /// </summary>
        /// <remarks>
        /// The database is locked for the duration of the backup. Any pending WAL entries are
        /// checkpointed (committed to the table files) before the files are packed, so the
        /// resulting backup never depends on WAL contents to be consistent.
        /// </remarks>
        /// <param name="options">Options controlling where the backup is written and how active
        /// transactions are handled. When null, defaults are used.</param>
        /// <returns>Details of the backup that was created.</returns>
        BackupResult BackupDatabase(BackupOptions options = null);

        /// <summary>
        /// Restores the database from a backup file previously created by
        /// <see cref="BackupDatabase(BackupOptions)"/>.
        /// </summary>
        /// <remarks>
        /// The database is locked for the duration of the restore so no other operation can be
        /// performed while it runs. On completion, the transaction id sequence is reset to zero.
        /// </remarks>
        /// <param name="backupFilePath">Full path to the backup file to restore.</param>
        /// <param name="options">Options controlling how active transactions and the safety
        /// backup are handled. When null, defaults are used.</param>
        /// <returns>Details of the restore that was performed.</returns>
        RestoreResult RestoreDatabase(string backupFilePath, RestoreOptions options = null);
    }
}
