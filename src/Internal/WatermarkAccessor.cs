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
 *  File: WatermarkAccessor.cs
 *
 *  Purpose:  Default IWatermarkAccessor implementation. Resolves
 *            ISimpleDBOperations<CheckpointWatermarkDataRow> lazily from the container on
 *            first access, mirroring WalTableAccessor, to avoid constructor cycles for
 *            consumers (e.g. SimpleDBOperations<T> startup recovery) that need to read/write
 *            watermark rows.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using Microsoft.Extensions.DependencyInjection;

using SimpleDB.Internal.Tables;

namespace SimpleDB.Internal
{
    internal sealed class WatermarkAccessor : IWatermarkAccessor
    {
        private readonly Lazy<ISimpleDBOperations<CheckpointWatermarkDataRow>> _watermarkTable;

        public WatermarkAccessor(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _watermarkTable = new Lazy<ISimpleDBOperations<CheckpointWatermarkDataRow>>(
                () => serviceProvider.GetRequiredService<ISimpleDBOperations<CheckpointWatermarkDataRow>>());
        }

        public ISimpleDBOperations<CheckpointWatermarkDataRow> WatermarkTable => _watermarkTable.Value;

        public long GetLastFlushedSequence(string tableName)
        {
            if (String.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            CheckpointWatermarkDataRow existing = WatermarkTable.Select(r => r.TableName == tableName).FirstOrDefault();

            return existing?.LastFlushedSequence ?? -1L;
        }

        public void SetLastFlushedSequence(string tableName, long lastFlushedSequence)
        {
            if (String.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            // MUST use the plain, non-transactional Insert/Update - see CheckpointWatermarkDataRow.cs
            // header, using the transactional overloads here would recursively generate WAL entries
            // for the watermark table itself.
            CheckpointWatermarkDataRow existing = WatermarkTable.Select(r => r.TableName == tableName).FirstOrDefault();

            if (existing == null)
            {
                WatermarkTable.Insert(new CheckpointWatermarkDataRow
                {
                    TableName = tableName,
                    LastFlushedSequence = lastFlushedSequence,
                    CheckpointTimestampTicks = DateTime.UtcNow.Ticks,
                });
            }
            else
            {
                existing.LastFlushedSequence = lastFlushedSequence;
                existing.CheckpointTimestampTicks = DateTime.UtcNow.Ticks;
                WatermarkTable.Update(existing);
            }
        }
    }
}
