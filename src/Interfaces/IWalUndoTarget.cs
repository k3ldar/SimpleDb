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
 *  File: IWalUndoTarget.cs
 *
 *  Purpose:  Non generic entry point allowing a rolled back transaction to reverse the
 *            operations it applied to a table. TransactionManager works with type erased
 *            WalEntry instances and resolves tables by name, so it cannot use the generic
 *            ITransactionalSimpleDBOperations<T> API to undo work.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

namespace SimpleDB
{
    /// <summary>
    /// Implemented by tables that are able to reverse a previously applied operation when the
    /// owning transaction is rolled back.
    /// </summary>
    /// <remarks>
    /// Undo operations deliberately bypass triggers, foreign key validation and WAL recording;
    /// they restore state that the table itself previously produced, so re-validating or
    /// re-logging it would be incorrect.
    /// </remarks>
    internal interface IWalUndoTarget
    {
        /// <summary>
        /// Reverses an insert by removing the record with the supplied id.
        /// </summary>
        /// <param name="recordId">Id of the record that was inserted.</param>
        void UndoInsert(long recordId);

        /// <summary>
        /// Reverses an update by restoring the record to its pre-update state.
        /// </summary>
        /// <param name="recordId">Id of the record that was updated.</param>
        /// <param name="undoRecord">Serialized bytes of the record as it existed before the update.</param>
        void UndoUpdate(long recordId, byte[] undoRecord);

        /// <summary>
        /// Reverses a delete by reinstating the record that was removed.
        /// </summary>
        /// <param name="undoRecord">Serialized bytes of the record as it existed before the delete.</param>
        void UndoDelete(byte[] undoRecord);
    }
}
