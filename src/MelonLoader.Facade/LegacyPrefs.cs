using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace MelonLoader
{
    /// <summary>
    /// Legacy (0.5.x era) preference API. Every registration is backed by a <see cref="MelonPreferences"/>
    /// entry in the same category namespace, so values survive the migration to the modern API.
    /// </summary>
    public static class MelonPrefs
    {
        private static readonly Dictionary<string, MelonPreference> Preferences = new Dictionary<string, MelonPreference>(StringComparer.Ordinal);

        /// <summary>Value kind of a legacy preference; matches the ordering of the 0.7.3 baseline.</summary>
        public enum MelonPreferenceType
        {
            STRING = 0,
            BOOL = 1,
            INT = 2,
            FLOAT = 3
        }

        public static bool GetBool(string category, string name) => GetValue<bool>(category, name);

        public static float GetFloat(string category, string name) => GetValue<float>(category, name);

        public static int GetInt(string category, string name) => GetValue<int>(category, name);

        public static string GetString(string category, string name) => GetValue<string>(category, name);

        public static bool HasKey(string category, string name) => MelonPreferences.HasEntry(category, name);

        public static string GetCategoryDisplayName(string category) => MelonPreferences.GetCategory(category)?.DisplayName;

        public static void RegisterCategory(string category, string displayText)
        {
            MelonPreferences_Category created = MelonPreferences.CreateCategory(category, displayText);
            if (created != null && created.CreatorAssembly == null)
            {
                created.CreatorAssembly = Assembly.GetCallingAssembly();
            }
        }

        public static void RegisterBool(string category, string name, bool defaultValue, string displayText = null, bool hidden = false) =>
            Register(category, name, defaultValue, displayText, hidden, Assembly.GetCallingAssembly());

        public static void RegisterFloat(string category, string name, float defaultValue, string displayText = null, bool hidden = false) =>
            Register(category, name, defaultValue, displayText, hidden, Assembly.GetCallingAssembly());

        public static void RegisterInt(string category, string name, int defaultValue, string displayText = null, bool hidden = false) =>
            Register(category, name, defaultValue, displayText, hidden, Assembly.GetCallingAssembly());

        public static void RegisterString(string category, string name, string defaultValue, string displayText = null, bool hidden = false) =>
            Register(category, name, defaultValue ?? string.Empty, displayText, hidden, Assembly.GetCallingAssembly());

        public static void SetBool(string category, string name, bool value) => SetValue(category, name, value);

        public static void SetFloat(string category, string name, float value) => SetValue(category, name, value);

        public static void SetInt(string category, string name, int value) => SetValue(category, name, value);

        public static void SetString(string category, string name, string value) => SetValue(category, name, value ?? string.Empty);

        public static void SaveConfig() => MelonPreferences.Save();

        internal static Dictionary<string, MelonPreference> GetPreferences() => Preferences;

        private static void Register<T>(string category, string name, T defaultValue, string displayText, bool hidden, Assembly creator)
        {
            string key = MakeKey(category, name);
            MelonPreferences_Category prefsCategory = MelonPreferences.CreateCategory(category);
            if (prefsCategory != null && prefsCategory.CreatorAssembly == null)
            {
                prefsCategory.CreatorAssembly = creator;
            }

            // The eight argument overload is the one that returns the typed entry.
            MelonPreferences_Entry<T> entry = prefsCategory?.CreateEntry(name, defaultValue, displayText ?? name, null, hidden, false, null, null);
            if (entry == null)
            {
                return;
            }

            MelonPreference preference = new MelonPreference(entry)
            {
                Hidden = hidden,
                DisplayText = displayText ?? name
            };

            Preferences[key] = preference;
        }

        private static T GetValue<T>(string category, string name)
        {
            string key = MakeKey(category, name);
            if (Preferences.TryGetValue(key, out MelonPreference preference) && preference.Entry is MelonPreferences_Entry<T> typed)
            {
                return typed.Value;
            }

            MelonPreferences_Entry<T> entry = MelonPreferences.GetEntry<T>(category, name);
            if (entry != null)
            {
                return entry.Value;
            }

            MelonLogger.Publish(AdapterLogLevel.Warning, "Legacy preference '" + key + "' was read before it was registered.", null, null);
            return default(T);
        }

        private static void SetValue<T>(string category, string name, T value)
        {
            string key = MakeKey(category, name);
            MelonPreferences_Entry<T> entry = Preferences.TryGetValue(key, out MelonPreference preference) && preference.Entry is MelonPreferences_Entry<T> typed
                ? typed
                : MelonPreferences.GetEntry<T>(category, name);

            if (entry == null)
            {
                MelonLogger.Publish(AdapterLogLevel.Warning, "Legacy preference '" + key + "' was written before it was registered.", null, null);
                return;
            }

            entry.Value = value;
        }

        private static string MakeKey(string category, string name) => category + "." + name;

        /// <summary>Legacy preference descriptor exposed through <see cref="ModPrefs.GetPrefs"/>.</summary>
        public class MelonPreference
        {
            internal MelonPreferences_Entry Entry;

            public MelonPreference()
            {
            }

            public MelonPreference(MelonPreferences_Entry entry)
            {
                Entry = entry;
            }

            public MelonPreference(MelonPreference preference)
            {
                if (preference == null)
                {
                    return;
                }

                Entry = preference.Entry;
                Hidden = preference.Hidden;
                DisplayText = preference.DisplayText;
            }

            public string Value
            {
                get { return Entry == null ? null : Convert.ToString(Entry.BoxedValue, CultureInfo.InvariantCulture); }
                set { SetValue(value, false); }
            }

            public string ValueEdited
            {
                get { return Entry == null ? null : Convert.ToString(Entry.BoxedEditedValue, CultureInfo.InvariantCulture); }
                set { SetValue(value, true); }
            }

            public MelonPreferenceType Type
            {
                get { return MapType(Entry?.GetReflectedType()); }
            }

            public bool Hidden { get; internal set; }

            public string DisplayText { get; internal set; }

            internal static MelonPreferenceType MapType(Type type)
            {
                if (type == typeof(bool))
                {
                    return MelonPreferenceType.BOOL;
                }

                if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
                {
                    return MelonPreferenceType.INT;
                }

                if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                {
                    return MelonPreferenceType.FLOAT;
                }

                return MelonPreferenceType.STRING;
            }

            private void SetValue(string value, bool edited)
            {
                if (Entry == null)
                {
                    return;
                }

                Type type = Entry.GetReflectedType();
                object converted = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                if (edited)
                {
                    Entry.BoxedEditedValue = converted;
                }
                else
                {
                    Entry.BoxedValue = converted;
                    Entry.BoxedEditedValue = converted;
                }
            }
        }
    }

    public static class ModPrefs
    {
        /// <summary>Legacy value kind exposed by <see cref="PrefDesc"/>.</summary>
        public enum PrefType
        {
            STRING = 0,
            BOOL = 1,
            INT = 2,
            FLOAT = 3
        }

        /// <summary>A legacy preference grouped by category, as returned by <see cref="GetPrefs"/>.</summary>
        public class PrefDesc : MelonPrefs.MelonPreference
        {
            public PrefDesc()
            {
            }

            public PrefDesc(MelonPreferences_Entry entry) : base(entry)
            {
            }

            public PrefDesc(MelonPrefs.MelonPreference preference) : base(preference)
            {
            }

            public new PrefType Type => (PrefType)(int)base.Type;
        }

        public static Dictionary<string, Dictionary<string, PrefDesc>> GetPrefs()
        {
            Dictionary<string, Dictionary<string, PrefDesc>> result = new Dictionary<string, Dictionary<string, PrefDesc>>(StringComparer.Ordinal);
            foreach (MelonPreferences_Category category in MelonPreferences.Categories)
            {
                if (category.Entries == null || category.Entries.Count == 0)
                {
                    continue;
                }

                Dictionary<string, PrefDesc> entries = new Dictionary<string, PrefDesc>(StringComparer.Ordinal);
                foreach (MelonPreferences_Entry entry in category.Entries)
                {
                    entries[entry.Identifier] = new PrefDesc(entry);
                }

                result[category.Identifier] = entries;
            }

            return result;
        }

        public static void RegisterPrefBool(string category, string name, bool defaultValue, string displayText = null, bool hidden = false) =>
            MelonPrefs.RegisterBool(category, name, defaultValue, displayText, hidden);

        public static void RegisterPrefFloat(string category, string name, float defaultValue, string displayText = null, bool hidden = false) =>
            MelonPrefs.RegisterFloat(category, name, defaultValue, displayText, hidden);

        public static void RegisterPrefInt(string category, string name, int defaultValue, string displayText = null, bool hidden = false) =>
            MelonPrefs.RegisterInt(category, name, defaultValue, displayText, hidden);

        public static void RegisterPrefString(string category, string name, string defaultValue, string displayText = null, bool hidden = false) =>
            MelonPrefs.RegisterString(category, name, defaultValue, displayText, hidden);

        public static bool GetBool(string category, string name) => MelonPrefs.GetBool(category, name);

        public static float GetFloat(string category, string name) => MelonPrefs.GetFloat(category, name);

        public static int GetInt(string category, string name) => MelonPrefs.GetInt(category, name);

        public static string GetString(string category, string name) => MelonPrefs.GetString(category, name);
    }
}
