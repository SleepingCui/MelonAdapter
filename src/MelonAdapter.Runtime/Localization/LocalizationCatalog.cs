using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MelonAdapter.Runtime.Localization
{
    /// <summary>
    /// The interface language catalog behind <c>lang.json</c>. The file lives next to <c>Config.toml</c> and
    /// is optional: the same document is embedded in this assembly, so the settings UI keeps working when
    /// the file is missing or broken. Values missing from a language fall back to English, then to the
    /// built-in defaults, then to the key itself.
    /// </summary>
    public sealed class LocalizationCatalog
    {
        public const string FileName = "lang.json";
        public const string AutomaticLanguage = "auto";
        public const string FallbackLanguage = "en";

        /// <summary>Languages the adapter ships and that the settings UI can switch between.</summary>
        public static readonly string[] SupportedLanguages = { "zh", "en" };

        private readonly Dictionary<string, Dictionary<string, string>> _tables =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _languages = new List<string>();
        private readonly HashSet<string> _missingKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private LocalizationCatalog()
        {
            _displayNames["zh"] = "中文";
            _displayNames["en"] = "English";
        }

        /// <summary>Language used for <see cref="T"/>.</summary>
        public string Language { get; private set; } = FallbackLanguage;

        /// <summary>Languages present in the catalog, in the order declared by the file.</summary>
        public IReadOnlyList<string> Languages => _languages;

        /// <summary>Where the catalog came from: the <c>lang.json</c> path or a description of the built-in copy.</summary>
        public string SourceName { get; private set; } = "built-in";

        /// <summary>Path of the file that was read, or null when the built-in copy is used.</summary>
        public string SourcePath { get; private set; }

        /// <summary>Set when the file exists but could not be used; the built-in copy is then active.</summary>
        public string LoadError { get; private set; }

        /// <summary>Keys the UI asked for that no language provides.</summary>
        public IReadOnlyCollection<string> MissingKeys => _missingKeys;

        public int MissingKeyCount => _missingKeys.Count;

        public bool IsSupported(string language) =>
            !string.IsNullOrEmpty(language) && _tables.ContainsKey(language);

        public string DisplayName(string language)
        {
            string name;
            return language != null && _displayNames.TryGetValue(language, out name) ? name : language;
        }

        /// <summary>
        /// Loads <paramref name="path"/> and selects <paramref name="requestedLanguage"/>. A missing file is
        /// not an error: the embedded copy is used. A broken file is reported through
        /// <paramref name="error"/> and the embedded copy is used, so the UI always has text.
        /// </summary>
        public static LocalizationCatalog Load(string path, string requestedLanguage, out string error)
        {
            error = null;
            LocalizationCatalog catalog = new LocalizationCatalog();

            string embedded = ReadEmbedded();
            if (embedded != null)
            {
                string parseError;
                if (!catalog.Merge(embedded, null, out parseError))
                {
                    // The embedded copy is a build artifact; if it is broken there is nothing left to do.
                    error = "The built-in language catalog could not be read: " + parseError;
                }
            }
            else
            {
                error = "The built-in language catalog is missing from the assembly.";
            }

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string text = null;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception exception)
                {
                    error = "Cannot read '" + path + "': " + exception.Message + ". The built-in language catalog is used.";
                }

                if (text != null)
                {
                    string parseError;
                    if (catalog.Merge(text, path, out parseError))
                    {
                        catalog.SourcePath = path;
                        catalog.SourceName = path;
                    }
                    else
                    {
                        catalog.LoadError = parseError;
                        error = "Cannot use '" + path + "': " + parseError + " The built-in language catalog is used.";
                        catalog.SourceName = "built-in (lang.json is invalid)";
                    }
                }
            }
            else if (string.IsNullOrEmpty(path))
            {
                catalog.SourceName = "built-in";
            }
            else
            {
                catalog.SourceName = "built-in (lang.json not found)";
            }

            catalog.SelectLanguage(requestedLanguage);
            return catalog;
        }

        /// <summary>Language to use when the configuration says <c>auto</c>: Chinese systems get <c>zh</c>.</summary>
        public static string DetectLanguage()
        {
            try
            {
                CultureInfo culture = CultureInfo.CurrentUICulture;
                string twoLetter = culture == null ? null : culture.TwoLetterISOLanguageName;
                if (!string.IsNullOrEmpty(twoLetter) && twoLetter.Equals("zh", StringComparison.OrdinalIgnoreCase))
                {
                    return "zh";
                }
            }
            catch (Exception)
            {
                // A missing culture never happens in practice; fall through to English.
            }

            return FallbackLanguage;
        }

        /// <summary>
        /// Selects a language. Returns false when the catalog does not contain it; the current language is
        /// then kept.
        /// </summary>
        public bool SetLanguage(string language)
        {
            if (!IsSupported(language))
            {
                return false;
            }

            Language = Normalise(language);
            return true;
        }

        /// <summary>
        /// Looks a key up and formats it with <paramref name="args"/>. The current language is tried first,
        /// then English, then any other language, then the key itself. Unknown keys are recorded in
        /// <see cref="MissingKeys"/> so a translator can see what the UI still needs.
        /// </summary>
        public string T(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            string template = Lookup(Language, key) ?? Lookup(FallbackLanguage, key);
            if (template == null)
            {
                foreach (string language in _languages)
                {
                    template = Lookup(language, key);
                    if (template != null)
                    {
                        break;
                    }
                }
            }

            if (template == null)
            {
                _missingKeys.Add(key);
                template = key;
            }

            if (args == null || args.Length == 0)
            {
                return template;
            }

            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        /// <summary>Keys of the English table that <paramref name="language"/> does not translate.</summary>
        public IReadOnlyList<string> KeysMissingFrom(string language)
        {
            List<string> missing = new List<string>();
            Dictionary<string, string> reference;
            if (!_tables.TryGetValue(FallbackLanguage, out reference))
            {
                return missing;
            }

            Dictionary<string, string> table;
            if (!_tables.TryGetValue(language ?? string.Empty, out table))
            {
                return reference.Keys.ToList();
            }

            foreach (string key in reference.Keys)
            {
                if (!table.ContainsKey(key))
                {
                    missing.Add(key);
                }
            }

            return missing;
        }

        /// <summary>All keys known to the catalog.</summary>
        public IReadOnlyList<string> AllKeys()
        {
            SortedSet<string> keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Dictionary<string, string> table in _tables.Values)
            {
                foreach (string key in table.Keys)
                {
                    keys.Add(key);
                }
            }

            return keys.ToList();
        }

        /// <summary>Rebuilds the catalog from the same file, keeping the current language when possible.</summary>
        public LocalizationCatalog Reload(out string error)
        {
            string language = Language;
            LocalizationCatalog reloaded = Load(SourcePath, language, out error);
            return reloaded;
        }

        private void SelectLanguage(string requested)
        {
            string language = requested;
            if (string.IsNullOrEmpty(language) || language.Equals(AutomaticLanguage, StringComparison.OrdinalIgnoreCase))
            {
                language = DetectLanguage();
            }

            if (SetLanguage(language))
            {
                return;
            }

            if (SetLanguage(DetectLanguage()))
            {
                return;
            }

            Language = _languages.Count > 0 ? _languages[0] : FallbackLanguage;
        }

        private string Lookup(string language, string key)
        {
            if (string.IsNullOrEmpty(language))
            {
                return null;
            }

            Dictionary<string, string> table;
            if (!_tables.TryGetValue(language, out table))
            {
                return null;
            }

            string value;
            return table.TryGetValue(key, out value) ? value : null;
        }

        /// <summary>
        /// Merges one JSON document into the catalog. Later documents win, so the user file overrides the
        /// built-in copy key by key while keeping everything the user did not translate.
        /// </summary>
        private bool Merge(string json, string path, out string error)
        {
            error = null;
            JObject root;
            try
            {
                root = ParseDocument(json);
            }
            catch (JsonException exception)
            {
                error = exception.Message;
                return false;
            }

            List<string> declaredOrder = new List<string>();
            JArray declared = root["languages"] as JArray;
            if (declared != null)
            {
                foreach (JToken item in declared)
                {
                    string language = item.Type == JTokenType.String ? (string)item : null;
                    if (!string.IsNullOrEmpty(language))
                    {
                        declaredOrder.Add(Normalise(language));
                    }
                }
            }

            int merged = 0;
            foreach (JProperty pair in root.Properties())
            {
                JObject table = pair.Value as JObject;
                if (table == null)
                {
                    continue;
                }

                string language = Normalise(pair.Name);
                if (language.Length == 0)
                {
                    continue;
                }

                Dictionary<string, string> strings;
                if (!_tables.TryGetValue(language, out strings))
                {
                    strings = new Dictionary<string, string>(StringComparer.Ordinal);
                    _tables[language] = strings;
                }

                foreach (JProperty entry in table.Properties())
                {
                    string value = entry.Value.Type == JTokenType.String ? (string)entry.Value : null;
                    if (value == null)
                    {
                        continue;
                    }

                    strings[entry.Name] = value;
                    merged++;
                }

                if (!_languages.Contains(language))
                {
                    _languages.Add(language);
                }
            }

            if (merged == 0)
            {
                error = "the document does not contain any language tables.";
                return false;
            }

            ReorderLanguages(declaredOrder);
            return true;
        }

        /// <summary>
        /// Reads one catalog document. Newtonsoft.Json is already loaded by the game, so the adapter reads
        /// JSON with the same serializer the rest of the process uses instead of keeping its own reader.
        /// </summary>
        private static JObject ParseDocument(string json)
        {
            if (json == null)
            {
                throw new JsonException("The language catalog is empty.");
            }

            if (json.Length > 0 && json[0] == '\uFEFF')
            {
                json = json.Substring(1);
            }

            return JObject.Parse(json);
        }

        private void ReorderLanguages(List<string> declaredOrder)
        {
            List<string> ordered = new List<string>();
            foreach (string language in declaredOrder)
            {
                if (_tables.ContainsKey(language) && !ordered.Contains(language))
                {
                    ordered.Add(language);
                }
            }

            foreach (string language in SupportedLanguages)
            {
                if (_tables.ContainsKey(language) && !ordered.Contains(language))
                {
                    ordered.Add(language);
                }
            }

            foreach (string language in _languages)
            {
                if (!ordered.Contains(language))
                {
                    ordered.Add(language);
                }
            }

            _languages.Clear();
            _languages.AddRange(ordered);
        }

        private static string Normalise(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return string.Empty;
            }

            string trimmed = language.Trim().ToLowerInvariant();
            int separator = trimmed.IndexOfAny(new[] { '-', '_' });
            if (separator > 0)
            {
                trimmed = trimmed.Substring(0, separator);
            }

            return trimmed;
        }

        private static string ReadEmbedded()
        {
            Assembly assembly = typeof(LocalizationCatalog).Assembly;
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.EndsWith("lang.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using (Stream stream = assembly.GetManifestResourceStream(name))
                {
                    if (stream == null)
                    {
                        continue;
                    }

                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }

            return null;
        }
    }
}
