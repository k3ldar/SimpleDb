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
 *  File: WalTableAccessor.cs
 *
 *  Purpose:  Default IWalTableAccessor implementation. Resolves ISimpleDBOperations<WalEntryDataRow>
 *            lazily from the container on first access (i.e. on the first transaction commit),
 *            rather than during construction, which is what avoids the TransactionManager <->
 *            SimpleDBOperations<WalEntryDataRow> constructor cycle.
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using Microsoft.Extensions.DependencyInjection;

using SimpleDB.Internal.Tables;

namespace SimpleDB.Internal
{
    internal sealed class WalTableAccessor : IWalTableAccessor
    {
        private readonly Lazy<ISimpleDBOperations<WalEntryDataRow>> _walTable;

        public WalTableAccessor(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _walTable = new Lazy<ISimpleDBOperations<WalEntryDataRow>>(
                () => serviceProvider.GetRequiredService<ISimpleDBOperations<WalEntryDataRow>>());
        }

        public ISimpleDBOperations<WalEntryDataRow> WalTable => _walTable.Value;
    }
}
