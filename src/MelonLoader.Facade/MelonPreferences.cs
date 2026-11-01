using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using MelonLoader.Preferences;

namespace MelonLoader
{
    /// <summary>
    /// Converts preference values to and from the raw TOML token text stored in
    /// <c>UserData/MelonPreferences.cfg</c> by MelonAdapter.Runtime.
    /// </summary>
    internal static class PreferenceValueCodec
    {
        public static string Format(object value, Type type)
        {
            if (type == null)
            {
                type = value?.GetType() ?? typeof(string);
            }

            if (value == null)
            {
                return type.IsArray || IsList(type) ? "[]" : "\"\"";
            }

            if (type == typeof(string))
            {
                return Quote((string)value);
            }

            if (type == typeof(bool))
            {
                return (bool)value ? "true" : "false";
            }

            if (type == typeof(char))
            {
                return Quote(value.ToString());
            }

            if (type.IsEnum)
            {
                return Quote(value.ToString());
            }

            if (IsSimpleNumeric(type))
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                return FormatSequence((IEnumerable)value, elementType);
            }

            if (IsList(type))
            {
                Type elementType = type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
                return FormatSequence((IEnumerable)value, elementType);
            }

            return Quote(value.ToString());
        }

        public static bool TryParse(string raw, Type type, out object value)
        {
            value = null;
            if (type == null)
            {
                return false;
            }

            raw = raw ?? string.Empty;
            string token = raw.Trim();

            try
            {
                if (type == typeof(string))
                {
                    value = Unquote(token);
                    return true;
                }

                if (type == typeof(bool))
                {
                    bool parsed;
                    if (!bool.TryParse(Unquote(token), out parsed))
                    {
                        return false;
                    }

                    value = parsed;
                    return true;
                }

                if (type == typeof(char))
                {
                    string text = Unquote(token);
                    if (text.Length != 1)
                    {
                        return false;
                    }

                    value = text[0];
                    return true;
                }

                if (type.IsEnum)
                {
                    string text = Unquote(token);
                    if (!Enum.IsDefined(type, text))
                    {
                        return false;
                    }

                    value = Enum.Parse(type, text, true);
                    return true;
                }

                if (IsSimpleNumeric(type))
                {
                    value = Convert.ChangeType(Unquote(token), type, CultureInfo.InvariantCulture);
                    return true;
                }

                if (type.IsArray || IsList(type))
                {
                    Type elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                    List<object> items = ParseSequence(token, elementType);
                    if (items == null)
                    {
                        return false;
                    }

                    if (type.IsArray)
                    {
                        Array array = Array.CreateInstance(elementType, items.Count);
                        for (int i = 0; i < items.Count; i++)
                        {
                            array.SetValue(items[i], i);
                        }

                        value = array;
                    }
                    else
                    {
                        IList list = (IList)Activator.CreateInstance(type);
                        foreach (object item in items)
                        {
                            list.Add(item);
                        }

                        value = list;
                    }

                    return true;
                }
            }
            catch (Exception exception) when (exception is FormatException || exception is InvalidCastException || exception is OverflowException || exception is ArgumentException)
            {
                return false;
            }

            return false;
        }

        private static string FormatSequence(IEnumerable sequence, Type elementType)
        {
            StringBuilder builder = new StringBuilder("[");
            bool first = true;
            foreach (object item in sequence)
            {
                if (!first)
                {
                    builder.Append(", ");
                }

                first = false;
                builder.Append(Format(item, elementType));
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static List<object> ParseSequence(string token, Type elementType)
        {
            if (!token.StartsWith("[", StringComparison.Ordinal) || !token.EndsWith("]", StringComparison.Ordinal))
            {
                return null;
            }

            string inner = token.Substring(1, token.Length - 2).Trim();
            List<object> items = new List<object>();
            if (inner.Length == 0)
            {
                return items;
            }

            foreach (string part in SplitTopLevel(inner))
            {
                object item;
                if (!TryParse(part, elementType, out item))
                {
                    return null;
                }

                items.Add(item);
            }

            return items;
        }

        private static IEnumerable<string> SplitTopLevel(string text)
        {
            int depth = 0;
            bool inString = false;
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (c == '\\' && i + 1 < text.Length)
                    {
                        i++;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        break;
                    case '[':
                        depth++;
                        break;
                    case ']':
                        depth--;
                        break;
                    case ',':
                        if (depth == 0)
                        {
                            yield return text.Substring(start, i - start).Trim();
                            start = i + 1;
                        }

                        break;
                }
            }

            yield return text.Substring(start).Trim();
        }

        private static bool IsList(Type type) =>
            type != typeof(string) && typeof(IList).IsAssignableFrom(type) && !type.IsArray;

        private static bool IsSimpleNumeric(Type type) =>
            type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
            type == typeof(float) || type == typeof(double) || type == typeof(decimal);

        public static string Quote(string text)
        {
            StringBuilder builder = new StringBuilder("\"");
            foreach (char c in text ?? string.Empty)
            {
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default: builder.Append(c); break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        public static string Unquote(string token)
        {
            if (token == null)
            {
                return string.Empty;
            }

            token = token.Trim();
            if (token.Length < 2)
            {
                return token;
            }

            char first = token[0];
            char last = token[token.Length - 1];
            if (first != last || (first != '"' && first != '\''))
            {
                return token;
            }

            string inner = token.Substring(1, token.Length - 2);
            if (first == '\'')
            {
                return inner;
            }

            StringBuilder builder = new StringBuilder(inner.Length);
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c != '\\' || i + 1 >= inner.Length)
                {
                    builder.Append(c);
                    continue;
                }

                i++;
                switch (inner[i])
                {
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    default:
                        builder.Append('\\');
                        builder.Append(inner[i]);
                        break;
                }
            }

            return builder.ToString();
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class MelonPreferences_DontSaveAttribute : Attribute
    {
    }

    public static class MelonPreferences
    {
        public static readonly List<MelonPreferences_Category> Categories = new List<MelonPreferences_Category>();
        public static readonly List<MelonPreferences_ReflectiveCategory> ReflectiveCategories = new List<MelonPreferences_ReflectiveCategory>();

        public static MelonEvent<string> OnPreferencesLoaded = new MelonEvent<string>();
        public static MelonEvent<string> OnPreferencesSaved = new MelonEvent<string>();

        public static string FilePath => AdapterBridge.PreferencesStorage?.FilePath;

        public static MelonPreferences_Category CreateCategory(string identifier) =>
            CreateCategoryCore(identifier, identifier, false, false, Assembly.GetCallingAssembly());

        public static MelonPreferences_Category CreateCategory(string identifier, string displayName) =>
            CreateCategoryCore(identifier, displayName, false, false, Assembly.GetCallingAssembly());

        public static MelonPreferences_Category CreateCategory(string identifier, string displayName, bool isHidden, bool isInlined) =>
            CreateCategoryCore(identifier, displayName, isHidden, isInlined, Assembly.GetCallingAssembly());

        private static MelonPreferences_Category CreateCategoryCore(string identifier, string displayName, bool isHidden, bool isInlined, Assembly creator)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException("Preference category identifier must not be empty.", nameof(identifier));
            }

            MelonPreferences_Category existing = GetCategory(identifier);
            if (existing != null)
            {
                return existing;
            }

            MelonPreferences_Category category = new MelonPreferences_Category(identifier, string.IsNullOrEmpty(displayName) ? identifier : displayName)
            {
                IsHidden = isHidden,
                IsInlined = isInlined,
                CreatorAssembly = creator
            };

            Categories.Add(category);
            return category;
        }

        public static MelonPreferences_ReflectiveCategory CreateCategory<T>(string identifier, string displayName = null)
        {
            MelonPreferences_ReflectiveCategory category = new MelonPreferences_ReflectiveCategory(identifier, displayName ?? identifier, typeof(T));
            ReflectiveCategories.Add(category);
            return category;
        }

        public static MelonPreferences_Category GetCategory(string identifier) =>
            Categories.FirstOrDefault(c => string.Equals(c.Identifier, identifier, StringComparison.Ordinal));

        public static T GetCategory<T>(string identifier)
        {
            MelonPreferences_ReflectiveCategory reflective = ReflectiveCategories.FirstOrDefault(c => string.Equals(c.Identifier, identifier, StringComparison.Ordinal));
            if (reflective != null && typeof(T).IsAssignableFrom(typeof(MelonPreferences_ReflectiveCategory)))
            {
                return (T)(object)reflective;
            }

            MelonPreferences_Category category = GetCategory(identifier);
            return category is T match ? match : default(T);
        }

        public static MelonPreferences_Entry GetEntry(string category, string name) =>
            GetCategory(category)?.GetEntry(name);

        public static MelonPreferences_Entry<T> GetEntry<T>(string category, string name) =>
            GetCategory(category)?.GetEntry<T>(name);

        public static T GetEntryValue<T>(string category, string name)
        {
            MelonPreferences_Entry<T> entry = GetEntry<T>(category, name);
            if (entry == null)
            {
                throw new KeyNotFoundException("Preference entry '" + name + "' in category '" + category + "' does not exist.");
            }

            return entry.Value;
        }

        public static bool HasEntry(string category, string name) => GetEntry(category, name) != null;

        public static void SetEntryValue<T>(string category, string name, T value)
        {
            MelonPreferences_Entry<T> entry = GetEntry<T>(category, name);
            if (entry == null)
            {
                throw new KeyNotFoundException("Preference entry '" + name + "' in category '" + category + "' does not exist.");
            }

            entry.Value = value;
        }

        public static void SetEntryValue<T>(string category, string name, T value, object valueValidator)
        {
            SetEntryValue(category, name, value);
        }

        public static MelonPreferences_Entry CreateEntry<T>(string categoryIdentifier, string entryIdentifier, T defaultValue, string displayName, bool isHidden) =>
            CreateCategoryCore(categoryIdentifier, categoryIdentifier, false, false, Assembly.GetCallingAssembly()).CreateEntry(entryIdentifier, defaultValue, displayName, isHidden);

        public static MelonPreferences_Entry<T> CreateEntry<T>(string categoryIdentifier, string entryIdentifier, T defaultValue, string displayName = null, string description = null, bool isHidden = false, bool dontSaveDefault = false, ValueValidator validator = null) =>
            CreateCategoryCore(categoryIdentifier, categoryIdentifier, false, false, Assembly.GetCallingAssembly()).CreateEntry(entryIdentifier, defaultValue, displayName, description, isHidden, dontSaveDefault, validator);

        public static void Load()
        {
            IAdapterPreferencesStorage storage = AdapterBridge.PreferencesStorage;
            if (storage == null)
            {
                return;
            }

            foreach (MelonPreferences_Category category in Categories)
            {
                foreach (MelonPreferences_Entry entry in category.Entries)
                {
                    entry.Reload();
                }
            }

            OnPreferencesLoaded.Invoke(FilePath);
        }

        public static void Save()
        {
            IAdapterPreferencesStorage storage = AdapterBridge.PreferencesStorage;
            if (storage == null)
            {
                MelonLogger.Publish(AdapterLogLevel.Warning, AdapterBridge.DescribeMissingService("preferences storage"), null, null);
                return;
            }

            storage.SaveAll();
            OnPreferencesSaved.Invoke(storage.FilePath);
        }

        public static void SaveCategory(string category, bool printmsg = true)
        {
            if (GetCategory(category) == null)
            {
                MelonLogger.Publish(AdapterLogLevel.Warning, "Cannot save unknown preference category '" + category + "'.", null, null);
                return;
            }

            Save();
        }

        /// <summary>Generic form of <see cref="SaveCategory"/>; the generic argument is only a type hint.</summary>
        public static void SaveCategory<T>(string identifier, bool printmsg = true) => SaveCategory(identifier, printmsg);

        public static void RemoveCategoryFromFile(string category, string entry = null)
        {
            MelonPreferences_Category found = GetCategory(category);
            if (found == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(entry))
            {
                Categories.Remove(found);
            }
            else
            {
                found.DeleteEntry(entry);
            }

            IAdapterPreferencesStorage storage = AdapterBridge.PreferencesStorage;
            storage?.SaveAll();
        }

        internal static void Reset()
        {
            Categories.Clear();
            ReflectiveCategories.Clear();
            OnPreferencesLoaded = new MelonEvent<string>();
            OnPreferencesSaved = new MelonEvent<string>();
        }

        internal static void LoadEntryFromStorage(MelonPreferences_Entry entry)
        {
            entry.Reload();
        }
    }

    public class MelonPreferences_Category
    {
        private string _filePath;

        public MelonPreferences_Category(string identifier) : this(identifier, identifier)
        {
        }

        public MelonPreferences_Category(string identifier, string displayName)
        {
            Identifier = identifier;
            DisplayName = displayName;
        }

        private MelonPreferences_Category()
        {
        }

        public string Identifier { get; private set; }
        public string DisplayName { get; set; }
        public bool IsHidden { get; set; }
        public bool IsInlined { get; set; }

        /// <summary>
        /// Assembly that asked for this category. MelonAdapter's settings UI uses it to group
        /// preference categories by mod; a category created by a helper library is attributed to that
        /// library. Null when the creator cannot be determined.
        /// </summary>
        internal Assembly CreatorAssembly { get; set; }
        public readonly List<MelonPreferences_Entry> Entries = new List<MelonPreferences_Entry>();

        public string FilePath
        {
            get { return _filePath ?? MelonPreferences.FilePath; }
            private set { _filePath = value; }
        }

        /// <summary>
        /// Creates an entry and returns the untyped base class. MelonLoader 0.7.3 declares this overload
        /// without default arguments and with a base-class return type; both details matter because a mod
        /// compiled against it binds by the full signature (return type included).
        /// </summary>
        public MelonPreferences_Entry CreateEntry<T>(string identifier, T default_value, string display_name, bool is_hidden) =>
            CreateEntryCore(identifier, default_value, display_name, null, is_hidden, false, null);

        public MelonPreferences_Entry<T> CreateEntry<T>(string identifier, T default_value, string display_name, string description, bool is_hidden, bool dont_save_default, ValueValidator validator) =>
            CreateEntryCore(identifier, default_value, display_name, description, is_hidden, dont_save_default, validator);

        /// <summary>
        /// The full overload. <paramref name="oldIdentifier"/> lets a mod rename an entry that was saved by an
        /// earlier version without losing the stored value.
        /// </summary>
        public MelonPreferences_Entry<T> CreateEntry<T>(string identifier, T default_value, string display_name = null, string description = null, bool is_hidden = false, bool dont_save_default = false, ValueValidator validator = null, string oldIdentifier = null)
        {
            bool created = GetEntry(identifier) == null;
            string carriedValue = null;
            if (!string.IsNullOrEmpty(oldIdentifier)
                && !string.Equals(oldIdentifier, identifier, StringComparison.Ordinal)
                && created)
            {
                if (HasEntry(oldIdentifier))
                {
                    RenameEntry(oldIdentifier, identifier);
                }
                else
                {
                    // The previous version may have saved the entry under the old name in an earlier
                    // session. Pick that value up so a rename does not silently reset the setting.
                    IAdapterPreferencesStorage storage = AdapterBridge.PreferencesStorage;
                    if (storage != null)
                    {
                        storage.TryGetValue(Identifier, oldIdentifier, out carriedValue);
                    }
                }
            }

            MelonPreferences_Entry<T> entry = CreateEntryCore(identifier, default_value, display_name, description, is_hidden, dont_save_default, validator);
            if (created && entry != null && carriedValue != null)
            {
                object parsed;
                if (PreferenceValueCodec.TryParse(carriedValue, typeof(T), out parsed))
                {
                    entry.Value = (T)parsed;
                }
            }

            return entry;
        }

        private MelonPreferences_Entry<T> CreateEntryCore<T>(string name, T defaultValue, string displayName, string description, bool isHidden, bool dontSaveDefault, ValueValidator validator)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Preference entry name must not be empty.", nameof(name));
            }

            MelonPreferences_Entry existing = GetEntry(name);
            if (existing is MelonPreferences_Entry<T> typedExisting)
            {
                return typedExisting;
            }

            if (existing != null)
            {
                MelonLogger.Publish(
                    AdapterLogLevel.Error,
                    "Preference entry '" + Identifier + "." + name + "' already exists with type '" + existing.GetReflectedType().Name + "' and cannot be recreated as '" + typeof(T).Name + "'.",
                    null,
                    null);
                return null;
            }

            MelonPreferences_Entry<T> entry = new MelonPreferences_Entry<T>
            {
                Identifier = name,
                DisplayName = string.IsNullOrEmpty(displayName) ? name : displayName,
                Description = description,
                IsHidden = isHidden,
                DontSaveDefault = dontSaveDefault,
                DefaultValue = defaultValue,
                Category = this,
                Validator = validator
            };

            if (validator != null)
            {
                entry.Value = (T)validator.EnsureValid(defaultValue);
            }
            else
            {
                entry.Value = defaultValue;
            }

            entry.Reload();
            Entries.Add(entry);
            return entry;
        }

        public MelonPreferences_Entry GetEntry(string name) =>
            Entries.FirstOrDefault(e => string.Equals(e.Identifier, name, StringComparison.Ordinal));

        public MelonPreferences_Entry<T> GetEntry<T>(string name) => GetEntry(name) as MelonPreferences_Entry<T>;

        public bool HasEntry(string name) => GetEntry(name) != null;

        public bool DeleteEntry(string name)
        {
            MelonPreferences_Entry entry = GetEntry(name);
            if (entry == null)
            {
                return false;
            }

            Entries.Remove(entry);
            return true;
        }

        public bool RenameEntry(string oldName, string newName)
        {
            MelonPreferences_Entry entry = GetEntry(oldName);
            if (entry == null || GetEntry(newName) != null)
            {
                return false;
            }

            entry.Identifier = newName;
            return true;
        }

        public void LoadFromFile(bool printmsg = true) => MelonPreferences.Load();

        public void SaveToFile(bool printmsg = true) => MelonPreferences.Save();

        public void SetFilePath(string path) => SetFilePath(path, false, false);

        public void SetFilePath(string path, bool createIfNotExist) => SetFilePath(path, createIfNotExist, false);

        public void SetFilePath(string path, bool createIfNotExist, bool isSecondary)
        {
            _filePath = path;
            MelonLogger.Publish(
                AdapterLogLevel.Debug,
                "Category '" + Identifier + "' requested a dedicated file '" + path + "'. MelonAdapter v1 stores every category in the single file '" + (MelonPreferences.FilePath ?? "<unavailable>") + "'.",
                null,
                null);
        }

        public void ResetFilePath() => _filePath = null;

        public void DestroyFileWatcher()
        {
        }
    }

    public abstract class MelonPreferences_Entry
    {
        public string Identifier { get; internal set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Comment { get; set; }
        public bool IsHidden { get; set; }
        public bool DontSaveDefault { get; set; }
        public MelonPreferences_Category Category { get; internal set; }
        public ValueValidator Validator { get; internal set; }

        public MelonEvent<object, object> OnEntryValueChangedUntyped = new MelonEvent<object, object>();

        public abstract object BoxedValue { get; set; }
        public abstract object BoxedEditedValue { get; set; }

        public abstract string GetDefaultValueAsString();
        public abstract string GetValueAsString();
        public abstract string GetEditedValueAsString();
        public abstract Type GetReflectedType();
        public abstract void ResetToDefault();
        public abstract void FireUntypedValueChanged(object oldValue, object newValue);

        internal abstract void Reload();
        internal abstract string SerializeRaw();

        internal virtual bool ShouldPersist() => !(DontSaveDefault && Equals(BoxedValue, BoxedDefaultValue));

        internal abstract object BoxedDefaultValue { get; }

        public string GetExceptionMessage(string message) =>
            "[" + (Category?.Identifier ?? "?") + "." + Identifier + "] " + message;
    }

    public class MelonPreferences_Entry<T> : MelonPreferences_Entry
    {
        private T _value;
        private T _defaultValue;

        public T DefaultValue
        {
            get { return _defaultValue; }
            set { _defaultValue = value; }
        }

        public T Value
        {
            get { return _value; }
            set { SetValue(value, true); }
        }

        public T EditedValue
        {
            get { return _value; }
            set { SetValue(value, true); }
        }

        public MelonEvent<T, T> OnEntryValueChanged = new MelonEvent<T, T>();

        public override object BoxedValue
        {
            get { return _value; }
            set { SetValue((T)value, true); }
        }

        public override object BoxedEditedValue
        {
            get { return _value; }
            set { SetValue((T)value, true); }
        }

        internal override object BoxedDefaultValue => _defaultValue;

        public override Type GetReflectedType() => typeof(T);

        public override string GetDefaultValueAsString() => PreferenceValueCodec.Format(_defaultValue, typeof(T));

        public override string GetValueAsString() => PreferenceValueCodec.Format(_value, typeof(T));

        public override string GetEditedValueAsString() => GetValueAsString();

        public override void ResetToDefault() => SetValue(_defaultValue, true);

        internal override string SerializeRaw() => PreferenceValueCodec.Format(_value, typeof(T));

        internal override void Reload()
        {
            IAdapterPreferencesStorage storage = AdapterBridge.PreferencesStorage;
            if (storage == null)
            {
                return;
            }

            string raw;
            string categoryIdentifier = Category?.Identifier;
            if (string.IsNullOrEmpty(categoryIdentifier) || !storage.TryGetValue(categoryIdentifier, Identifier, out raw))
            {
                return;
            }

            object parsed;
            if (!PreferenceValueCodec.TryParse(raw, typeof(T), out parsed))
            {
                MelonLogger.Publish(
                    AdapterLogLevel.Warning,
                    "Ignoring malformed stored value for preference '" + categoryIdentifier + "." + Identifier + "': " + raw,
                    null,
                    null);
                return;
            }

            T old = _value;
            _value = Validate((T)parsed);
            if (!Equals(old, _value))
            {
                FireUntypedValueChanged(old, _value);
                OnEntryValueChanged.Invoke(old, _value);
            }
        }

        private void SetValue(T value, bool invokeEvents)
        {
            T validated = Validate(value);
            if (Equals(_value, validated))
            {
                return;
            }

            T old = _value;
            _value = validated;
            if (!invokeEvents)
            {
                return;
            }

            FireUntypedValueChanged(old, _value);
            OnEntryValueChanged.Invoke(old, _value);
        }

        private T Validate(T value)
        {
            if (Validator == null)
            {
                return value;
            }

            try
            {
                return (T)Validator.EnsureValid(value);
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, GetExceptionMessage("Value validator threw: " + exception.Message), null, exception);
                return _value;
            }
        }

        public override void FireUntypedValueChanged(object oldValue, object newValue) =>
            OnEntryValueChangedUntyped.Invoke(oldValue, newValue);
    }
}

namespace MelonLoader.Preferences
{
    public abstract class ValueValidator
    {
        public abstract bool IsValid(object value);
        public abstract object EnsureValid(object value);
    }

    public interface IValueRange
    {
        object MinValue { get; }
        object MaxValue { get; }
    }

    public class ValueRange<T> : ValueValidator, IValueRange where T : IComparable<T>
    {
        public ValueRange(T minValue, T maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public T Min { get; }
        public T Max { get; }

        object IValueRange.MinValue => MinValue;
        object IValueRange.MaxValue => MaxValue;

        public T MinValue { get; }
        public T MaxValue { get; }

        public override bool IsValid(object value)
        {
            if (!(value is T typed))
            {
                return false;
            }

            return typed.CompareTo(MinValue) >= 0 && typed.CompareTo(MaxValue) <= 0;
        }

        public override object EnsureValid(object value)
        {
            if (!(value is T typed))
            {
                return MinValue;
            }

            if (typed.CompareTo(MinValue) < 0)
            {
                return MinValue;
            }

            return typed.CompareTo(MaxValue) > 0 ? MaxValue : typed;
        }
    }
}