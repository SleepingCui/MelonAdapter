using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MelonAdapter.Runtime.Toml
{
    /// <summary>
    /// Minimal ordered TOML table. Values are <see cref="string"/>, <see cref="long"/>,
    /// <see cref="double"/>, <see cref="bool"/>, <see cref="List{Object}"/> or <see cref="TomlTable"/>.
    /// </summary>
    public sealed class TomlTable : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly List<string> _order = new List<string>();
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);

        public int Count => _values.Count;

        public IEnumerable<string> Keys => _order;

        public object this[string key]
        {
            get { object value; return _values.TryGetValue(key, out value) ? value : null; }
            set { Set(key, value); }
        }

        public void Set(string key, object value)
        {
            if (!_values.ContainsKey(key))
            {
                _order.Add(key);
            }

            _values[key] = value;
        }

        public bool TryGetValue(string key, out object value) => _values.TryGetValue(key, out value);

        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public void Remove(string key)
        {
            if (_values.Remove(key))
            {
                _order.Remove(key);
            }
        }

        public TomlTable GetTable(string key)
        {
            object value;
            if (_values.TryGetValue(key, out value))
            {
                return value as TomlTable;
            }

            TomlTable table = new TomlTable();
            Set(key, table);
            return table;
        }

        public string GetString(string key, string fallback = null)
        {
            object value;
            if (!_values.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }

            return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public bool GetBool(string key, bool fallback)
        {
            object value;
            if (!_values.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            bool parsed;
            return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) ? parsed : fallback;
        }

        public List<string> GetStringList(string key)
        {
            object value;
            if (!_values.TryGetValue(key, out value) || value == null)
            {
                return new List<string>();
            }

            List<string> result = new List<string>();
            IEnumerable sequence = value as IEnumerable;
            if (sequence == null)
            {
                result.Add(Convert.ToString(value, CultureInfo.InvariantCulture));
                return result;
            }

            foreach (object item in sequence)
            {
                if (item != null)
                {
                    result.Add(Convert.ToString(item, CultureInfo.InvariantCulture));
                }
            }

            return result;
        }

        public int GetInt(string key, int fallback)
        {
            object value;
            if (!_values.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }

            if (value is long)
            {
                return (int)(long)value;
            }

            if (value is double)
            {
                return (int)(double)value;
            }

            int parsed;
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            foreach (string key in _order)
            {
                yield return new KeyValuePair<string, object>(key, _values[key]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}