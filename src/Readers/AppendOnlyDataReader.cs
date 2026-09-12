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
 *  File: AppendOnlyDataReader.cs
 *
 *  Purpose:  Sequential append-only data reader, counterpart to AppendOnlyDataWriter, used
 *            by system tables such as the WAL (see SystemTableAttribute / StorageEngine.AppendOnly).
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System.Text;
using System.Text.Json;

using SimpleDB.Internal;

namespace SimpleDB.Readers
{
    internal sealed class AppendOnlyDataReader : IDataReader
    {
        public ushort Version => Consts.AppendTableReaderWriterId;

        /// <summary>
        /// Reads the records physically present in the file, which may be fewer than the header
        /// advertises.
        /// </summary>
        /// <remarks>
        /// The header record count is treated as an upper bound rather than a guarantee. Unlike the
        /// paged writers, which emit the header and the body in a single forward pass,
        /// <see cref="Writers.AppendOnlyDataWriter.RewriteData"/> truncates the file to the header
        /// BEFORE rewriting the survivors and only fixes the header up afterwards. A process that
        /// dies inside that window leaves a header describing a body that no longer exists.
        ///
        /// Reading such a file must not throw. The records are appended in sequence order, so the
        /// survivors are contiguous from the start of the data area: everything before the tear is
        /// intact and everything after it is unrecoverable regardless. Stopping cleanly at the last
        /// complete record therefore recovers the maximum that can be recovered. Throwing instead
        /// would prevent the table from being constructed at all, which for the WAL means the whole
        /// database fails to open - including tables whose own files are perfectly intact.
        ///
        /// <paramref name="recordCount"/> is set to the number of records actually read, so the
        /// caller can compare it against the header and repair the file. That matters because
        /// <see cref="Writers.AppendOnlyDataWriter.WriteData"/> re-reads the record count directly
        /// from the header to decide where to append; left uncorrected, it would skip the slots the
        /// missing records occupied and silently diverge from the data.
        /// </remarks>
        public List<T> ReadRecords<T>(FileStream fileStream, ref int pageCount, ref int recordCount, ref int dataLength)
        {
            using BinaryReader reader = new(fileStream, Encoding.UTF8, true);
            fileStream.Seek(Consts.StartOfRecordCount, SeekOrigin.Begin);
            _ = reader.ReadByte();
            int declaredRecordCount = reader.ReadInt32();
            _ = reader.ReadInt32();
            dataLength = reader.ReadInt32();
            pageCount = -1;

            List<T> Result = [];

            if (declaredRecordCount <= 0)
            {
                recordCount = 0;
                return Result;
            }

            long fileLength = fileStream.Length;
            fileStream.Seek(Consts.TotalHeaderLength, SeekOrigin.Begin);

            for (int i = 0; i < declaredRecordCount; i++)
            {
                // The length prefix itself may be missing or only partly written.
                if (fileStream.Position + sizeof(int) > fileLength)
                    break;

                int length = reader.ReadInt32();

                // A non positive length can only come from uninitialised or torn bytes, and a
                // length running past the end of the file describes a record that was never
                // fully written.
                if (length <= 0 || fileStream.Position + length > fileLength)
                    break;

                byte[] data = reader.ReadBytes(length);

                // ReadBytes returns a short array at end of stream rather than throwing, so a
                // partial read has to be detected explicitly.
                if (data.Length != length)
                    break;

                Result.Add(JsonSerializer.Deserialize<T>(data, Consts.JsonSerializerOptions));
            }

            recordCount = Result.Count;

            return Result;
        }
    }
}
