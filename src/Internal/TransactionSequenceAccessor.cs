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
 *  File: SequenceAccessor.cs
 *
 *  Purpose:  Default ISequenceAccessor implementation. Resolves
 *            ISimpleDBOperations<SequenceDataRow> lazily from the container on first access,
 *            mirroring WatermarkAccessor, to avoid constructor cycles for consumers (e.g.
 *            TransactionManager) that need a named, independently rolling-over counter.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using Microsoft.Extensions.DependencyInjection;

using Shared.Classes;

using SimpleDB.Internal.Tables;

namespace SimpleDB.Internal
{
    internal sealed class TransactionSequenceAccessor : ITransactionSequenceAccessor
    {
        private readonly Lazy<ISimpleDBOperations<TransactionSequenceDataRow>> _sequenceTable;
        private readonly object _lockObject = new();

        public TransactionSequenceAccessor(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _sequenceTable = new Lazy<ISimpleDBOperations<TransactionSequenceDataRow>>(
                () => serviceProvider.GetRequiredService<ISimpleDBOperations<TransactionSequenceDataRow>>());
        }

        public ISimpleDBOperations<TransactionSequenceDataRow> SequenceTable => _sequenceTable.Value;

        public long CurrentValue()
        {
            using (TimedLock timedLock = TimedLock.Lock(_lockObject))
            {
                return SequenceTable.NextSequence(0L);
            }
        }

        public long NextValue()
        {
            // A single lock spanning read-modify-write is required here, unlike the simpler
            // watermark accessor, because concurrent callers (e.g. BeginTransaction on multiple
            // threads) must never observe or persist the same value twice.
            using (TimedLock timedLock = TimedLock.Lock(_lockObject))
            {
                return SequenceTable.NextSequence(1L);
            }
        }

        public void Reset()
        {
            using (TimedLock timedLock = TimedLock.Lock(_lockObject))
            {
                SequenceTable.ResetSequence(0L, 0L);
            }
        }
    }
}
