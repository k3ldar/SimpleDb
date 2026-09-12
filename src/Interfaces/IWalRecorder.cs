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
 *  File: IWalRecorder.cs
 *
 *  Purpose:  Strategy responsible for capturing WAL entries for DML performed against a table.
 *            Resolved once per table (see SimpleDBOperations<T>.ResolveWalRecorder) based on the
 *            table's SystemTableAttribute.ParticipatesInWal, so no typeof() checks are required
 *            as more internal system tables are added.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
namespace SimpleDB
{
    /// <summary>
    /// Strategy responsible for capturing WAL entries for DML performed against a table.
    /// </summary>
    internal interface IWalRecorder : IOperationalTimings
    {
        /// <summary>
        /// Records a DML operation against the supplied transaction, if the strategy participates in WAL.
        /// </summary>
        /// <param name="transaction">Transaction the operation is being performed within.</param>
        /// <param name="tableName">Name of the table the operation was performed against.</param>
        /// <param name="operation">Type of DML operation performed.</param>
        /// <param name="recordId">Id of the record the operation was performed against.</param>
        /// <param name="serializedRecord">Serialized bytes of the record.</param>
        /// <param name="expectedUpdatedTicks">The UpdatedTicks of the record as known when the operation was performed (-1 for Insert), used for optimistic concurrency checking at commit time.</param>
        /// <param name="undoRecord">Serialized bytes of the record as it existed before the operation, used to undo it on rollback. Null for Insert.</param>
        void Record(ITransaction transaction, string tableName, OperationType operation, long recordId, byte[] serializedRecord, long expectedUpdatedTicks, byte[] undoRecord);
    }
}
