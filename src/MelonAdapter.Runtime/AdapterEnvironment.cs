using System;
using System.IO;
using MelonLoader;

namespace MelonAdapter.Runtime
{
    /// <summary>
    /// Facts that the host (Unity + UnityModManager) contributes to the adapter.
    /// </summary>
    public sealed class AdapterEnvironment : IAdapterContext
    {
        public AdapterEnvironment()
        {
            AdapterVersion = typeof(AdapterEnvironment).Assembly.GetName().Version?.ToString() ?? "1.2.2.0";
            ApiBaselineVersion = "0.7.3.0";
        }

        public string GameDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

        public string MelonLoaderDirectory { get; set; }

        public string UserDataDirectory { get; set; }

        public string GameDeveloper { get; set; } = string.Empty;

        public string GameName { get; set; } = string.Empty;

        public string GameVersion { get; set; } = string.Empty;

        public string UnityVersion { get; set; } = "unknown";

        /// <summary>Name of the mod host the adapter runs under (for example <c>UnityModManager</c>).</summary>
        public string HostName { get; set; } = "standalone";

        /// <summary>Version of the mod host, as reported by the host itself.</summary>
        public string HostVersion { get; set; } = "unknown";

        public string GameExecutableName
        {
            get
            {
                try
                {
                    return Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "unknown");
                }
                catch (Exception)
                {
                    return "unknown";
                }
            }
        }

        public string AdapterVersion { get; set; }

        public string ApiBaselineVersion { get; set; }

        public bool DebugLogging { get; set; }

        internal static string DefaultAdapterDirectory(string gameDirectory) => Path.Combine(gameDirectory, "MelonAdapter");
    }

    /// <summary>
    /// Resolves and creates the adapter's on-disk layout below <c>&lt;GameRoot&gt;/MelonAdapter</c>.
    /// </summary>
    public sealed class AdapterPaths
    {
        public AdapterPaths(string gameDirectory, string adapterDirectory, AdapterConfig config)
        {
            GameDirectory = gameDirectory;
            AdapterDirectory = adapterDirectory;
            ConfigPath = Path.Combine(adapterDirectory, AdapterConfig.DefaultConfigFileName);
            LanguagePath = Path.Combine(adapterDirectory, MelonAdapter.Runtime.Localization.LocalizationCatalog.FileName);
            LogsDirectory = Path.Combine(adapterDirectory, "Logs");
            LogPath = Path.Combine(LogsDirectory, "MelonAdapter.log");
            ModsDirectory = Resolve(config?.ModsDirectory, Path.Combine(adapterDirectory, "Mods"));
            UserDataDirectory = Resolve(config?.UserDataDirectory, Path.Combine(adapterDirectory, "UserData"));
            SupportDirectory = Path.Combine(adapterDirectory, "Dependencies");
            ReportPath = Path.Combine(LogsDirectory, "CompatibilityReport.txt");
        }

        public string GameDirectory { get; }
        public string AdapterDirectory { get; }
        public string ConfigPath { get; }

        /// <summary>Optional <c>lang.json</c> next to <c>Config.toml</c>; the built-in copy is used when absent.</summary>
        public string LanguagePath { get; }
        public string LogsDirectory { get; }
        public string LogPath { get; }
        public string ModsDirectory { get; }
        public string UserDataDirectory { get; }
        public string SupportDirectory { get; }
        public string ReportPath { get; }

        public string GetModUserDataDirectory(string melonId) => Path.Combine(UserDataDirectory, Sanitize(melonId));

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(AdapterDirectory);
            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(ModsDirectory);
            Directory.CreateDirectory(UserDataDirectory);
        }

        private static string Resolve(string configured, string fallback)
        {
            if (string.IsNullOrEmpty(configured))
            {
                return fallback;
            }

            return Path.IsPathRooted(configured) ? configured : Path.Combine(Path.GetDirectoryName(fallback), configured);
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "unknown";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] buffer = name.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (Array.IndexOf(invalid, buffer[i]) >= 0)
                {
                    buffer[i] = '_';
                }
            }

            return new string(buffer);
        }
    }
}