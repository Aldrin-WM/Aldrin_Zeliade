using System;
using System.Collections.Generic;

namespace AldrinAnalytics.Excel
{
    public class GenericSet<K,V>
    {
        private readonly Dictionary<K, V> _data;


        public GenericSet()
        {
            _data = new Dictionary<K, V>();
        }

        public virtual GenericSet<K, V> Add(K key, V value)
        {
            if (_data.ContainsKey(key))
            {
                throw new ArgumentException(string.Format("The key {0} is already registred !", key));
            }
            _data.Add(key, value);
            return this;
        }

        public V Get(K key)
        {
            if (!_data.ContainsKey(key))
            {
                throw new ArgumentException(string.Format("The key {0} is not registred !", key));
            }
            return _data[key];
        }

        public bool TryGet(K key, out V value)
        {
            return _data.TryGetValue(key, out value);
        }
    }
}
