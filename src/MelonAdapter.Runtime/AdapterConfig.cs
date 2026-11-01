using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MelonAdapter.Runtime.Toml;

namespace MelonAdapter.Runtime
{
    public enum HarmonyConflictPolicy
    {
        /// <summary>The affected mod is refused and the conflict is reported (default).</summary>
        Refuse = 0,

        /// <summary>The affected mod is disabled but its assembly stays loaded.</summary>
        Disable = 1,

        /// <summary>The conflict is only reported; patching proceeds.</summary>
        Report = 2
    }

    /// <summary>
    /// Contents of <c>MelonAdapter/Config.toml</c>. Every key has a documented default so a missing value
    /// never changes behaviour silently.
    /// </summary>
    public sealed class AdapterConfig
    {
        public const string DefaultConfigFileName = "Config.toml";

        public bool Enabled { get; set; } = true;
        public bool DebugLogging { get; set; }
        public string ModsDirectory { get; set; } = "Mods";
        public string UserDataDirectory { get; set; } = "UserData";
        public string ApiBaseline { get; set; } = "0.7.3.0";
        public bool ResolverDiagnostics { get; set; } = true;
        public bool AllowUnannotatedMods { get; set; }
        public HarmonyConflictPolicy HarmonyPolicy { get; set; } = HarmonyConflictPolicy.Refuse;
        public int MaxConsecutiveExceptions { get; set; } = 5;

        /// <summary>
        /// One-way import of MelonLoader 0.7.x preferences on first start: when the adapter has no
        /// preference file yet, <c>&lt;GameRoot&gt;/UserData/MelonPreferences.cfg</c> is copied next to the
        /// adapter and a timestamped backup is kept.
        /// </summary>
        public bool MigrateFromMelonLoader { get; set; } = true;

        /// <summary>
        /// Interface language for the in-game settings page: <c>auto</c> (follow the system language),
        /// <c>zh</c> or <c>en</c>. The value is also written by the Settings tab; values that are not
        /// present in <c>lang.json</c> fall back to the system language and then to English.
        /// </summary>
        public string Language { get; set; } = "auto";

        /// <summary>Mod ids that are skipped even when present on disk (manual disable).</summary>
        public List<string> DisabledMods { get; set; } = new List<string>();

        public List<string> Warnings { get; } = new List<string>();

        public static AdapterConfig Load(string path, out bool createdDefault)
        {
            createdDefault = false;
            AdapterConfig config = new AdapterConfig();

            if (!File.Exists(path))
            {
                config.Save(path);
                createdDefault = true;
                return config;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                config.Warnings.Add("Cannot read '" + path + "': " + exception.Message + ". The default configuration is used and the unreadable file is kept.");
                return config;
            }

            List<string> errors;
            TomlTable table = Toml.Toml.Parse(text, out errors);
            if (errors.Count > 0)
            {
                string backup = path + ".invalid-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                try
                {
                    File.Move(path, backup);
                }
                catch (Exception moveException)
                {
                    config.Warnings.Add("Cannot preserve the invalid configuration file: " + moveException.Message);
                }

                foreach (string error in errors)
                {
                    config.Warnings.Add("Invalid configuration: " + error);
                }

                config.Warnings.Add("The invalid configuration file was kept as '" + backup + "' and a default configuration was written.");
                config.Save(path);
                createdDefault = true;
                return config;
            }

            config.Enabled = table.GetBool("enabled", config.Enabled);
            config.DebugLogging = table.GetBool("debugLogging", config.DebugLogging);
            config.ModsDirectory = table.GetString("modsDirectory", config.ModsDirectory);
            config.UserDataDirectory = table.GetString("userDataDirectory", config.UserDataDirectory);

            TomlTable api = table["api"] as TomlTable;
            if (api != null)
            {
                config.ApiBaseline = api.GetString("baseline", config.ApiBaseline);
            }

            TomlTable resolver = table["resolver"] as TomlTable;
            if (resolver != null)
            {
                config.ResolverDiagnostics = resolver.GetBool("diagnostics", config.ResolverDiagnostics);
                config.AllowUnannotatedMods = resolver.GetBool("allowUnannotatedMods", config.AllowUnannotatedMods);
            }

            TomlTable harmony = table["harmony"] as TomlTable;
            if (harmony != null)
            {
                string policy = harmony.GetString("conflictPolicy", "refuse");
                HarmonyConflictPolicy parsed;
                if (!Enum.TryParse(policy, true, out parsed))
                {
                    config.Warnings.Add("Unknown harmony.conflictPolicy '" + policy + "'; the default 'refuse' is used.");
                }
                else
                {
                    config.HarmonyPolicy = parsed;
                }
            }

            TomlTable preferences = table["preferences"] as TomlTable;
            if (preferences != null)
            {
                config.MigrateFromMelonLoader = preferences.GetBool("migrateFromMelonLoader", config.MigrateFromMelonLoader);
            }

            TomlTable ui = table["ui"] as TomlTable;
            if (ui != null)
            {
                string language = ui.GetString("language", config.Language);
                if (string.IsNullOrEmpty(language) || !IsLanguageToken(language))
                {
                    config.Warnings.Add("Unknown ui.language '" + language + "'; the system language is used.");
                }
                else
                {
                    config.Language = language.Trim().ToLowerInvariant();
                }
            }

            TomlTable lifecycle = table["lifecycle"] as TomlTable;
            if (lifecycle != null)
            {
                config.MaxConsecutiveExceptions = Math.Max(1, lifecycle.GetInt("maxConsecutiveExceptions", config.MaxConsecutiveExceptions));
                config.DisabledMods = lifecycle.GetStringList("disabledMods");
            }

            return config;
        }

        public string Serialize()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("# MelonAdapter configuration. Generated defaults are safe to edit; a corrupt file is preserved as Config.toml.invalid-<timestamp>.").Append('\n');
            builder.Append("enabled = ").Append(Bool(this.Enabled)).Append('\n');
            builder.Append("debugLogging = ").Append(Bool(this.DebugLogging)).Append('\n');
            builder.Append("modsDirectory = ").Append(Toml.Toml.Quote(this.ModsDirectory ?? "Mods")).Append('\n');
            builder.Append("userDataDirectory = ").Append(Toml.Toml.Quote(this.UserDataDirectory ?? "UserData")).Append('\n');
            builder.Append('\n').Append("[api]").Append('\n');
            builder.Append("baseline = ").Append(Toml.Toml.Quote(this.ApiBaseline ?? "0.7.3.0")).Append('\n');
            builder.Append('\n').Append("[resolver]").Append('\n');
            builder.Append("diagnostics = ").Append(Bool(this.ResolverDiagnostics)).Append('\n');
            builder.Append("allowUnannotatedMods = ").Append(Bool(this.AllowUnannotatedMods)).Append('\n');
            builder.Append('\n').Append("[harmony]").Append('\n');
            builder.Append("conflictPolicy = ").Append(Toml.Toml.Quote(this.HarmonyPolicy.ToString().ToLowerInvariant())).Append('\n');
            builder.Append('\n').Append("[preferences]").Append('\n');
            builder.Append("migrateFromMelonLoader = ").Append(Bool(this.MigrateFromMelonLoader)).Append('\n');
            builder.Append('\n').Append("[ui]").Append('\n');
            builder.Append("language = ").Append(Toml.Toml.Quote(this.Language ?? "auto")).Append('\n');
            builder.Append('\n').Append("[lifecycle]").Append('\n');
            builder.Append("maxConsecutiveExceptions = ").Append(this.MaxConsecutiveExceptions.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("disabledMods = ").Append(Toml.Toml.FormatValue(this.DisabledMods ?? new List<string>())).Append('\n');
            return builder.ToString();
        }

        public void Save(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, Serialize());
        }

        private static string Bool(bool value) => value ? "true" : "false";

        /// <summary>Accepts <c>auto</c>, a two or three letter code and an optional region suffix.</summary>
        private static bool IsLanguageToken(string value)
        {
            string trimmed = value.Trim().ToLowerInvariant();
            if (trimmed.Length == 0 || trimmed.Length > 16)
            {
                return false;
            }

            if (trimmed == "auto")
            {
                return true;
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                char current = trimmed[i];
                bool letter = (current >= 'a' && current <= 'z') || (current >= '0' && current <= '9');
                if (!letter && current != '-' && current != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }
}