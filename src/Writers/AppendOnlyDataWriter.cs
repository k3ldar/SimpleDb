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
 *  File: AppendOnlyDataWriter.cs
 *
 *  Purpose:  Sequential append-only data writer, used by system tables such as the WAL
 *            (see SystemTableAttribute / StorageEngine.AppendOnly). Only new records
 *            (beyond the record count already persisted) are appended to the end of the
 *            file on each save, rather than rewriting the entire record set.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Text;
using System.Text.Json;

using SimpleDB.Internal;

namespace SimpleDB.Writers
{
    internal sealed class AppendOnlyDataWriter : IDataWriter, ICompactingDataWriter
    {
        // Reserved version identifier for the append-only format, distinct from the paged
        // versioned formats (1-3) so the two are never confused.
        public ushort Version => Consts.AppendTableReaderWriterId;

        public void WriteData<T>(FileStream fileStream, List<T> recordsToSave, CompressionType compressionType,
            PageSize pageSize, ref byte compactPercent, ref int pageCount)
        {
            using BinaryReader reader = new(fileStream, Encoding.UTF8, true);
            fileStream.Seek(Consts.StartOfRecordCount, SeekOrigin.Begin);
            _ = reader.ReadByte();
            int existingRecordCount = reader.ReadInt32();

            if (existingRecordCount < 0)
                existingRecordCount = 0;

            if (recordsToSave.Count > existingRecordCount)
            {
                fileStream.Seek(0, SeekOrigin.End);

                using BinaryWriter appendWriter = new(fileStream, Encoding.UTF8, true);

                for (int i = existingRecordCount; i < recordsToSave.Count; i++)
                {
                    byte[] data = JsonSerializer.SerializeToUtf8Bytes(recordsToSave[i], Consts.JsonSerializerOptions);
                    appendWriter.Write(data.Length);
                    appendWriter.Write(data, 0, data.Length);
                }
            }

            int dataLength = (int)(fileStream.Length - Consts.TotalHeaderLength);

            using BinaryWriter headerWriter = new(fileStream, Encoding.UTF8, true);
            headerWriter.Seek(Consts.StartOfRecordCount, SeekOrigin.Begin);
            headerWriter.Write((byte)CompressionType.None);
            headerWriter.Write(recordsToSave.Count);
            headerWriter.Write(dataLength);
            headerWriter.Write(dataLength);

            pageCount = -1;
            compactPercent = 0;
        }

        /// <summary>
        /// Discards the existing contents and writes the supplied records from the start of the
        /// data area. WriteData cannot be used for this: it only ever appends records beyond the
        /// count already on disk, so a shrinking set would leave the obsolete bytes in place while
        /// the header claimed a smaller count - the reader would then return the OLDEST records
        /// rather than the survivors.
        /// </summary>
        public void RewriteData<T>(FileStream fileStream, List<T> recordsToSave, CompressionType compressionType,
            PageSize pageSize, ref byte compactPercent, ref int pageCount)
        {
            fileStream.SetLength(Consts.TotalHeaderLength);
            fileStream.Seek(Consts.TotalHeaderLength, SeekOrigin.Begin);

            using (BinaryWriter recordWriter = new(fileStream, Encoding.UTF8, true))
            {
                foreach (T record in recordsToSave)
                {
                    byte[] data = JsonSerializer.SerializeToUtf8Bytes(record, Consts.JsonSerializerOptions);
                    recordWriter.Write(data.Length);
                    recordWriter.Write(data, 0, data.Length);
                }
            }

            // The records must be durable before the header advertises them, otherwise a crash
            // between the two leaves a count that describes bytes which were never written.
            fileStream.Flush(true);

            int dataLength = (int)(fileStream.Length - Consts.TotalHeaderLength);

            using BinaryWriter headerWriter = new(fileStream, Encoding.UTF8, true);
            headerWriter.Seek(Consts.StartOfRecordCount, SeekOrigin.Begin);
            headerWriter.Write((byte)CompressionType.None);
            headerWriter.Write(recordsToSave.Count);
            headerWriter.Write(dataLength);
            headerWriter.Write(dataLength);

            pageCount = -1;
            compactPercent = 0;
        }
    }
}
