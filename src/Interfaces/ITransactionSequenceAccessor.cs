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
 *  File: ISequenceAccessor.cs
 *
 *  Purpose:  Internal-only accessor providing lazy, engine-owned access to the named sequence
 *            system table. Not exposed to hosting applications; used by TransactionManager (and
 *            any other engine component that needs a monotonically increasing, independently
 *            rolling-over counter) without borrowing a table's own PrimarySequence/
 *            SecondarySequence, which may be relied upon for other purposes (e.g. WAL ordering).
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using SimpleDB.Internal.Tables;

namespace SimpleDB
{
    /// <summary>
    /// Internal accessor providing lazy, engine-owned access to the named sequence system
    /// table, and helpers for reading/incrementing a single named sequence.
    /// </summary>
    internal interface ITransactionSequenceAccessor
    {
        /// <summary>
        /// Gets the sequence table operations instance, resolved lazily on first access.
        /// </summary>
        ISimpleDBOperations<TransactionSequenceDataRow> SequenceTable { get; }

        /// <summary>
        /// Gets the current value of the named sequence without incrementing it. Returns
        /// </summary>
        long CurrentValue();

        /// <summary>
        /// Retrieves the next value of the named sequence, incrementing it in the process.
        /// </summary>
        long NextValue();

        /// <summary>
        /// Resets the sequence back to zero.
        /// </summary>
        void Reset();
    }
}
