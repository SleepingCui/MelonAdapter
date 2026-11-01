using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MelonAdapter.Runtime.Toml;
using MelonLoader;

namespace MelonAdapter.Runtime.Preferences
{
    /// <summary>
    /// Stores every preference category in the single <c>UserData/MelonPreferences.cfg</c> file.
    /// Values are written as raw TOML tokens so the facade codec round-trips without re-quoting.
    /// </summary>
    public sealed class PreferencesStorage : IAdapterPreferencesStorage
    {
        private readonly object _gate = new object();
        private TomlTable _document = new TomlTable();

        public PreferencesStorage(string filePath)
        {
            FilePath = filePath;
            Reload();
        }

        public string FilePath { get; }

        public void Reload()
        {
            lock (_gate)
            {
                if (!File.Exists(FilePath))
                {
                    _document = new TomlTable();
                    return;
                }

                List<string> errors;
                _document = Toml.Toml.Parse(File.ReadAllText(FilePath), out errors);
            }
        }

        public bool TryGetValue(string categoryIdentifier, string entryIdentifier, out string rawValue)
        {
            rawValue = null;
            if (string.IsNullOrEmpty(categoryIdentifier) || string.IsNullOrEmpty(entryIdentifier))
            {
                return false;
            }

            lock (_gate)
            {
                TomlTable category = _document[categoryIdentifier] as TomlTable;
                object value;
                if (category == null || !category.TryGetValue(entryIdentifier, out value))
                {
                    return false;
                }

                rawValue = Toml.Toml.FormatValue(value);
                return true;
            }
        }

        public void SaveAll()
        {
            lock (_gate)
            {
                HashSet<string> writtenCategories = new HashSet<string>(StringComparer.Ordinal);
                StringBuilder builder = new StringBuilder();
                builder.Append("# MelonPreferences file written by MelonAdapter. Values survive a switch back to MelonLoader 0.7.x.").Append('\n');

                foreach (MelonPreferences_Category category in MelonPreferences.Categories)
                {
                    if (category?.Identifier == null)
                    {
                        continue;
                    }

                    List<MelonPreferences_Entry> entries = new List<MelonPreferences_Entry>();
                    foreach (MelonPreferences_Entry entry in category.Entries)
                    {
                        if (entry != null && entry.ShouldPersist())
                        {
                            entries.Add(entry);
                        }
                    }

                    if (entries.Count == 0)
                    {
                        continue;
                    }

                    writtenCategories.Add(category.Identifier);
                    builder.Append('\n').Append('[').Append(Toml.Toml.FormatValue(category.Identifier)).Append(']').Append('\n');
                    foreach (MelonPreferences_Entry entry in entries)
                    {
                        builder.Append(Toml.Toml.FormatValue(entry.Identifier)).Append(" = ").Append(entry.SerializeRaw()).Append('\n');
                    }
                }

                foreach (KeyValuePair<string, object> pair in _document)
                {
                    if (writtenCategories.Contains(pair.Key))
                    {
                        continue;
                    }

                    TomlTable leftover = pair.Value as TomlTable;
                    if (leftover == null)
                    {
                        continue;
                    }

                    builder.Append('\n').Append('[').Append(pair.Key).Append(']').Append('\n');
                    foreach (KeyValuePair<string, object> entry in leftover)
                    {
                        builder.Append(entry.Key).Append(" = ").Append(Toml.Toml.FormatValue(entry.Value)).Append('\n');
                    }
                }

                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(FilePath, builder.ToString(), new UTF8Encoding(false));
                _document = Toml.Toml.Parse(builder.ToString(), out _);
            }
        }

        public string DescribeValue(string categoryIdentifier, string entryIdentifier)
        {
            string raw;
            return TryGetValue(categoryIdentifier, entryIdentifier, out raw) ? raw : null;
        }

        public static string GetDefaultPath(AdapterPaths paths) => Path.Combine(paths.UserDataDirectory, "MelonPreferences.cfg");

        /// <summary>File name MelonLoader 0.7.x writes below <c>&lt;GameRoot&gt;/UserData</c>.</summary>
        public const string LegacyFileName = "MelonPreferences.cfg";

        /// <summary>
        /// One-way import of a MelonLoader preference file. It only runs when the adapter has no
        /// preference file yet, so user edits are never overwritten. The MelonLoader file is left
        /// untouched and the imported copy is backed up next to the adapter file.
        /// </summary>
        public static bool TryMigrateLegacyFile(string targetPath, string legacyPath, out string backupPath, out string error)
        {
            backupPath = null;
            error = null;

            try
            {
                if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(legacyPath))
                {
                    return false;
                }

                if (File.Exists(targetPath))
                {
                    return false;
                }

                if (!File.Exists(legacyPath))
                {
                    return false;
                }

                string directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                byte[] contents = File.ReadAllBytes(legacyPath);
                File.WriteAllBytes(targetPath, contents);

                backupPath = Path.Combine(
                    directory ?? string.Empty,
                    "MelonPreferences.melonloader-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".bak");
                File.WriteAllBytes(backupPath, contents);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                backupPath = null;
                return false;
            }
        }
    }
}
