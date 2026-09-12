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
 *  File: SimpleDBSettings.cs
 *
 *  Purpose:  Settings for SimpleDB
 *
 *  Date        Name                Reason
 *  31/05/2022  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using AppSettings;

namespace SimpleDB
{
    /// <summary>
    /// Settings applied to Simple Db
    /// </summary>
    public class SimpleDBSettings
    {
        /// <summary>
        /// Path where tables are located
        /// </summary>
        /// <value>string</value>
        public string Path { get; set; }

        /// <summary>
        /// Encryption key used to encrypt data in tables
        /// </summary>
        [SettingString(false, SharedPluginFeatures.Constants.MinimumKeyLength, SharedPluginFeatures.Constants.MaximumKeyLength)]
        [SettingDefault("DSFOIRTEWRasd/flkqw409r sdaedf2134A")]
        public string EnycryptionKey { get; set; }

        /// <summary>
        /// Timeout in milliseconds to wait for a lock on the database to be released before throwing an exception, 
        /// this is used when clearing the memory cache for tables that are not being used and have not been accessed for a period of time.
        /// </summary>
        public uint LockTimeoutClearMemoryMs { get; set; } = 300;

        /// <summary>
        /// Timeout in milliseconds to wait for a lock on the database to be released before throwing an exception,
        /// this is used when performing a checkpoint operation.
        /// </summary>
        public uint LockTimeoutCheckpointMs { get; set; } = 250;

        /// <summary>
        /// Timeout in milliseconds to wait for a lock on the database to be released before throwing an exception,
        /// this is used when performing a database operation.
        /// </summary>
        public uint LockTimeoutOperationMs { get; set; } = 30000;

        /// <summary>
        /// Timeout in milliseconds to wait for a lock on the database to be released before throwing an exception,
        /// this is used when performing an index manager operation.
        /// </summary>
        public uint LockTimeoutIndexManagerMs { get; set; } = 10000;


        /// <summary>
        /// Timeout in milliseconds to wait for a lock on the database to be released before throwing an exception,
        /// this is used when performing a foreign key manager operation.
        /// </summary>
        public uint LockTimeoutForeignKeyManagerMs { get; set; } = 10000;

        /// <summary>
        /// Number of WAL entries that must accumulate before a database wide checkpoint is attempted.
        /// </summary>
        public uint WalCheckpointThreshold { get; set; } = 1000;
    }
}
