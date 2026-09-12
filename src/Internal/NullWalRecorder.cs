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
 *  File: NullWalRecorder.cs
 *
 *  Purpose:  Default IWalRecorder that appends a WalEntry onto the active transaction's
 *            in-memory log, for tables that participate in the WAL (see SystemTableAttribute
 *            .ParticipatesInWal and SimpleDBOperations<T>.ResolveWalRecorder).
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using SharedPluginFeatures;

namespace SimpleDB.Internal
{
    /// <summary>
    /// No-op recorder used for system tables that do not participate in the WAL (e.g. the
    /// WAL table itself), preventing WAL entries being generated for their own writes.
    /// </summary>
    internal sealed class NullWalRecorder : IWalRecorder
    {
        public void Record(ITransaction transaction, string tableName, OperationType operation, long recordId, byte[] serializedRecord, long expectedUpdatedTicks, byte[] undoRecord)
        {
            // Intentionally does nothing - this table does not participate in the WAL.
        }

        public Dictionary<string, Timings> GetAllTimings => [];
    }
}
