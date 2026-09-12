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
 *  File: IndexManager.cs
 *
 *  Purpose:  IndexManager for SimpleDB
 *
 *  Date        Name                Reason
 *  05/06/2022  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using Shared.Classes;

namespace SimpleDB.Internal
{
    /// <summary>
    /// This saves all index in memory and is rebuilt every time the file is loaded, this could
    /// prove very inneficient with lots of data, if that is the case look at converting the 
    /// internals of this class to disk i/o
    /// 
    /// Another potential saving if more than 20 records would be to change to a binary search
    /// </summary>
    internal sealed class IndexManager<T> : IIndexManager
    {
        private readonly List<T> _keys;
        private readonly object _lock = new();
        private bool _sortRequired = false;
        private readonly List<string> _propertyNames;

        public IndexManager(IndexType indexType, params string[] propertyNames)
        {
            if (propertyNames.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(propertyNames));

            _keys = [];
            _propertyNames = [.. propertyNames];
            IndexType = indexType;
        }

        public IndexType IndexType { get; }

        public List<string> PropertyNames => _propertyNames;

        public bool IsUpdating { get; private set; } = false;

        public bool Contains(object value)
        {
            using (TimedLock timedLock = TimedLock.Lock(_lock, SimpleDBConfiguration.IndexManagerLockTimeout))
            {
                // BinarySearch requires _keys to be fully sorted. While a batch update is in
                // progress (IsUpdating), or once one has left the list needing a re-sort
                // (_sortRequired), new keys are appended/prepended without re-sorting until
                // EndUpdate/Sort runs - so _keys can be transiently out of order. A re-entrant
                // call landing in that window (e.g. a BeforeInsert/BeforeUpdate trigger calling
                // back into Select/IdExists on the same thread) must fall back to a linear scan,
                // otherwise BinarySearch can spuriously report a genuinely present key as missing.
                if (IsUpdating || _sortRequired)
                    return _keys.Contains((T)value);

                // _keys is sorted descending for IndexType.Descending, but List<T>.BinarySearch's
                // default comparer assumes ascending order, so it must be given a reversed
                // comparer to search a descending list correctly.
                if (IndexType == IndexType.Descending)
                    return _keys.BinarySearch((T)value, Comparer<T>.Create((a, b) => Comparer<T>.Default.Compare(b, a))) > -1;

                return _keys.BinarySearch((T)value) > -1;
            }
        }

        public void Add(List<object> items)
        {
            if (items == null)
                return;

            using (TimedLock timedLock = TimedLock.Lock(_lock, SimpleDBConfiguration.IndexManagerLockTimeout))
            {
                foreach (object item in items)
                {
                    if (item is T tItem && !Contains(tItem))
                    {
                        _keys.Add(tItem);
                        // Unlike the single value Add(object) overload, items here are appended in
                        // whatever order the caller supplied and are not known to already be in
                        // the correct position relative to IndexType, so a re-sort must always be
                        // scheduled once anything is added this way.
                        _sortRequired = true;
                    }
                }
            }

            if (!IsUpdating)
                Sort();
        }

        public void Add(object value)
        {
            using (TimedLock timedLock = TimedLock.Lock(_lock, SimpleDBConfiguration.IndexManagerLockTimeout))
            {

                if (Contains((T)value))
                    return;

                switch (IndexType)
                {
                    case IndexType.Ascending:
                        // Accumulate (never overwrite) - a later insert that happens to land in
                        // order relative to whatever is currently last must not erase the fact
                        // that an earlier insert in this same batch already broke the sort order.
                        if (typeof(T).Equals(typeof(long)))
                            _sortRequired |= _keys.Count > 0 && Convert.ToInt64(value) < Convert.ToInt64(_keys[^1]);
                        else if (typeof(T).Equals(typeof(int)))
                            _sortRequired |= _keys.Count > 0 && Convert.ToInt32(value) < Convert.ToInt32(_keys[^1]);
                        else
                            _sortRequired = true;

                        _keys.Add((T)value);

                        break;

                    case IndexType.Descending:
                        if (typeof(T).Equals(typeof(long)))
                            _sortRequired |= _keys.Count > 0 && Convert.ToInt64(value) > Convert.ToInt64(_keys[^1]);
                        else if (typeof(T).Equals(typeof(int)))
                            _sortRequired |= _keys.Count > 0 && Convert.ToInt32(value) > Convert.ToInt32(_keys[^1]);
                        else
                            _sortRequired = true;

                        _keys.Insert(0, (T)value);

                        break;
                }
            }

            if (!IsUpdating)
                Sort();
        }

        public void Remove(object value)
        {
            if (Contains(value))
            {
                using (TimedLock timedLock = TimedLock.Lock(_lock, SimpleDBConfiguration.IndexManagerLockTimeout))
                {
                    _keys.Remove((T)value);
                }

                if (!IsUpdating)
                    Sort();
            }
        }

        private void Sort()
        {
            if (!_sortRequired)
                return;

            using (TimedLock timedLock = TimedLock.Lock(_lock, SimpleDBConfiguration.IndexManagerLockTimeout))
            {
                _keys.Sort();

                switch (IndexType)
                {
                    case IndexType.Descending:
                        _keys.Reverse();
                        break;
                }

                // Reset now that _keys is genuinely sorted, otherwise every future Add would
                // needlessly re-sort and Contains would be stuck on the linear scan fallback.
                _sortRequired = false;
            }
        }

        public void BeginUpdate()
        {
            if (IsUpdating)
                throw new InvalidOperationException();

            IsUpdating = true;
        }

        public void EndUpdate()
        {
            if (!IsUpdating)
                throw new InvalidOperationException();

            IsUpdating = false;
            Sort();
        }
    }
}
