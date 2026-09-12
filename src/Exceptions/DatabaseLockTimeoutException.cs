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
 *  File: DatabaseLockTimeoutException.cs
 *
 *  Purpose:  Database lock timeout exception for SimpleDB
 *
 *  Date        Name                Reason
 *  01/01/2024  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

namespace SimpleDB
{
    /// <summary>
    /// Exception thrown when the database wide lock could not be acquired within the permitted
    /// time. The lock is only held for short, in memory operations and for checkpoints that flush
    /// already loaded tables to disk, so this indicates a defect - a deadlock, a scope that was
    /// never disposed, or a pathologically slow disk - rather than ordinary contention.
    /// </summary>
    /// <remarks>
    /// This exists so callers are never handed the underlying <c>LockTimeoutException</c> from the
    /// locking primitive, which is an implementation detail of how the gate happens to be built.
    /// The original exception is always preserved as the inner exception.
    /// </remarks>
    [Serializable]
    public sealed class DatabaseLockTimeoutException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public DatabaseLockTimeoutException()
            : base()
        {

        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Exception message</param>
        public DatabaseLockTimeoutException(string message)
            : base(message)
        {

        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner Exception</param>
        public DatabaseLockTimeoutException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }
}
