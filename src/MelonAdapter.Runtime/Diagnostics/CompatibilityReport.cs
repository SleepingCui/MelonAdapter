using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MelonAdapter.Runtime.Mods;
using MelonLoader;

namespace MelonAdapter.Runtime.Diagnostics
{
    /// <summary>
    /// Writes the plain-text compatibility report requested by the specification
    /// (<c>MelonAdapter/Logs/CompatibilityReport.txt</c>).
    /// </summary>
    public sealed class CompatibilityReport
    {
        private readonly AdapterEnvironment _environment;
        private readonly AdapterConfig _config;
        private readonly HarmonyProvider _harmony;

        public CompatibilityReport(AdapterEnvironment environment, AdapterConfig config, HarmonyProvider harmony)
        {
            _environment = environment;
            _config = config;
            _harmony = harmony;
        }

        /// <summary>Set by the runtime so the report can state where preferences came from.</summary>
        public string PreferencesOrigin { get; set; }

        public void Write(string path, IEnumerable<ModRecord> mods, IEnumerable<ResolutionAttempt> attempts, bool enabled)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("MelonAdapter compatibility report");
            builder.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine();
            builder.AppendLine("[Environment]");
            builder.AppendLine("Game             : " + _environment.GameName + " (" + _environment.GameDeveloper + ") " + _environment.GameVersion);
            builder.AppendLine("Game directory   : " + _environment.GameDirectory);
            builder.AppendLine("Unity            : " + _environment.UnityVersion);
            builder.AppendLine("Host loader      : " + _environment.HostName + " " + _environment.HostVersion);
            builder.AppendLine("Process          : " + _environment.GameExecutableName + " / " + MelonUtils.CurrentPlatform);
            builder.AppendLine("Adapter          : " + _environment.AdapterVersion);
            builder.AppendLine("API baseline     : " + _config.ApiBaseline);
            builder.AppendLine("Harmony provider : " + _harmony.ProviderVersion + " (" + _harmony.ProviderAssemblyLocation + ")");
            builder.AppendLine("Adapter enabled  : " + enabled);
            builder.AppendLine("Preferences      : " + (PreferencesOrigin ?? "adapter file"));
            builder.AppendLine();

            builder.AppendLine("[Mods]");
            List<ModRecord> records = (mods ?? Enumerable.Empty<ModRecord>()).ToList();
            if (records.Count == 0)
            {
                builder.AppendLine("(none discovered)");
            }

            foreach (ModRecord record in records)
            {
                builder.AppendLine("- " + record.DisplayName + " " + record.VersionText + " by " + record.Author);
                builder.AppendLine("    assembly : " + record.FileName + " (" + record.AssemblySimpleName + " " + record.AssemblyVersion + ")");
                builder.AppendLine("    state    : " + record.State);
                builder.AppendLine("    hash     : " + (record.Hash ?? "<not computed>"));
                if (!string.IsNullOrEmpty(record.FailureReason))
                {
                    builder.AppendLine("    reason   : " + record.FailureReason);
                }

                if (record.Dependencies.Length > 0)
                {
                    builder.AppendLine("    requires : " + string.Join(", ", record.Dependencies));
                }

                if (record.OptionalDependencies.Length > 0)
                {
                    builder.AppendLine("    optional : " + string.Join(", ", record.OptionalDependencies));
                }

                foreach (string warning in record.Warnings)
                {
                    builder.AppendLine("    warning  : " + warning);
                }

                foreach (string note in record.Notes)
                {
                    builder.AppendLine("    note     : " + note);
                }
            }

            builder.AppendLine();
            builder.AppendLine("[Assembly resolution]");
            List<ResolutionAttempt> resolutionAttempts = (attempts ?? Enumerable.Empty<ResolutionAttempt>()).ToList();
            if (resolutionAttempts.Count == 0)
            {
                builder.AppendLine("(no resolution probes were logged)");
            }

            foreach (ResolutionAttempt attempt in resolutionAttempts)
            {
                builder.AppendLine("- " + attempt.RequestedName + " <- " + attempt.RequestingAssembly + " : " + (attempt.Resolved ? "resolved " + attempt.CandidatePath : "unresolved") + (string.IsNullOrEmpty(attempt.Note) ? string.Empty : " (" + attempt.Note + ")"));
            }

            builder.AppendLine();
            builder.AppendLine("[Known API gaps]");
            builder.AppendLine("- MelonLoader.LoadMelonAssembly / LoadFromFile / LoadFromByteArray are not supported (v1); place assemblies in the mods directory and restart.");
            builder.AppendLine("- IL2CPP domain, MelonPlugin, MelonLoader embedded Tomlet/TinyJSON and the legacy 'Harmony.' namespace are not reproduced.");
            builder.AppendLine("- VerifyLoaderBuild hashes are reported but not enforced.");
            builder.AppendLine();

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }
    }
}
