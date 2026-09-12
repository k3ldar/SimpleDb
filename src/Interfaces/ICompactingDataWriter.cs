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
 *  File: ICompactingDataWriter.cs
 *
 *  Purpose:  Implemented by writers whose normal save path cannot shrink the file, so that
 *            a caller discarding records has an explicit way to reclaim the space.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

namespace SimpleDB
{
    /// <summary>
    /// Implemented by data writers that need an explicit rewrite path. The versioned writers do
    /// not - their normal WriteData already rewrites the whole file - but the append only writer
    /// only ever adds to the tail and therefore cannot reclaim space without this.
    /// </summary>
    internal interface ICompactingDataWriter
    {
        /// <summary>
        /// Replaces the entire contents of the file with <paramref name="recordsToSave"/>,
        /// releasing any space previously occupied by records no longer in the set.
        /// </summary>
        void RewriteData<T>(FileStream fileStream, List<T> recordsToSave, CompressionType compressionType,
            PageSize pageSize, ref byte compactPercent, ref int pageCount);
    }
}
