using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonAdapter.Runtime;
using MelonLoader;

namespace MelonAdapter.Runtime.Mods
{
    /// <summary>
    /// Creates the facade <see cref="MelonAssembly"/> and the mod instances for every accepted record.
    /// Each assembly is loaded inside its own try/catch so one broken mod never blocks the others.
    /// </summary>
    public sealed class ModLoader
    {
        private readonly IAdapterLogSink _log;
        private readonly AdapterConfig _config;

        public ModLoader(IAdapterLogSink log, AdapterConfig config)
        {
            _log = log;
            _config = config;
        }

        public void LoadAll(IEnumerable<ModRecord> records, ModAssemblyResolver resolver, HarmonyProvider harmony)
        {
            foreach (ModRecord record in records)
            {
                if (record.State == ModState.Rejected)
                {
                    continue;
                }

                if (IsManuallyDisabled(record))
                {
                    record.State = ModState.Disabled;
                    record.FailureReason = "Disabled by lifecycle.disabledMods in Config.toml.";
                    _log.Write("loader", AdapterLogLevel.Info, "'" + record.DisplayName + "' is disabled by configuration and was not loaded.");
                    continue;
                }

                string conflict = DescribeHarmonyConflict(record, harmony);
                if (conflict != null)
                {
                    record.Warnings.Add(conflict);
                    switch (_config.HarmonyPolicy)
                    {
                        case HarmonyConflictPolicy.Refuse:
                            record.State = ModState.Rejected;
                            record.FailureReason = conflict;
                            _log.Write("harmony", AdapterLogLevel.Error, "Refused '" + record.DisplayName + "': " + conflict);
                            continue;
                        case HarmonyConflictPolicy.Disable:
                            record.State = ModState.Disabled;
                            record.FailureReason = conflict;
                            _log.Write("harmony", AdapterLogLevel.Warning, "Disabled '" + record.DisplayName + "': " + conflict);
                            continue;
                        default:
                            _log.Write("harmony", AdapterLogLevel.Warning, "Harmony conflict reported for '" + record.DisplayName + "': " + conflict);
                            break;
                    }
                }

                try
                {
                    LoadRecord(record, resolver);
                }
                catch (Exception exception)
                {
                    record.State = ModState.Failed;
                    record.FailureReason = "Loading threw " + exception.GetType().Name + ": " + exception.Message;
                    _log.Write("loader", AdapterLogLevel.Error, "Failed to load '" + record.DisplayName + "': " + exception, exception);
                }
            }
        }

        private void LoadRecord(ModRecord record, ModAssemblyResolver resolver)
        {
            Assembly assembly = record.Assembly;
            if (assembly == null)
            {
                ModAssemblyLoader.LoadKind loadKind;
                assembly = ModAssemblyLoader.Load(record.AssemblyPath, out loadKind);
                string note = ModAssemblyLoader.Describe(loadKind, record.AssemblyPath);
                if (note != null)
                {
                    _log.Write("loader", AdapterLogLevel.Debug, "'" + record.FileName + "': " + note);
                }
            }

            record.Assembly = assembly;
            resolver.RegisterOwner(assembly, record);

            MelonAssembly melonAssembly = new MelonAssembly(assembly, record.AssemblyPath, record.Hash);
            MelonAssembly.AddAssembly(melonAssembly);

            foreach (Type type in record.MelonTypes)
            {
                MelonBase melon;
                try
                {
                    melon = (MelonBase)Activator.CreateInstance(type, true);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException("The constructor of '" + type.FullName + "' threw " + exception.GetType().Name + ": " + exception.Message, exception);
                }

                melon.SetAssembly(assembly, record.Hash);
                melon.MelonAssemblyInternal = melonAssembly;
                melon.ApplyMetadataDefaults();

                if (!melon.Register())
                {
                    throw new InvalidOperationException("MelonBase.Register() refused '" + type.FullName + "' (see the log above).");
                }

                record.Melons.Add(melon);
                _log.Write("loader", AdapterLogLevel.Debug, "Registered '" + melon.ID + "' from '" + record.FileName + "'.");
            }

            record.State = ModState.Loaded;
            _log.Write("loader", AdapterLogLevel.Info, "Loaded '" + record.DisplayName + "' " + record.VersionText + " (" + record.MelonTypes.Length + " melon type(s)).");
        }

        private bool IsManuallyDisabled(ModRecord record)
        {
            if (_config.DisabledMods == null || _config.DisabledMods.Count == 0)
            {
                return false;
            }

            return _config.DisabledMods.Any(id =>
                string.Equals(id, record.ModId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(DependencyGraph.NormalizeKey(id), DependencyGraph.NormalizeKey(record.AssemblySimpleName), StringComparison.OrdinalIgnoreCase));
        }

        public static string DescribeHarmonyConflict(ModRecord record, HarmonyProvider harmony)
        {
            if (record.DirectoryPath == null)
            {
                return null;
            }

            string privateHarmony = Path.Combine(record.DirectoryPath, "0Harmony.dll");
            if (!File.Exists(privateHarmony))
            {
                return null;
            }

            string version = "unknown";
            try
            {
                version = System.Reflection.AssemblyName.GetAssemblyName(privateHarmony).Version?.ToString() ?? "unknown";
            }
            catch (Exception)
            {
                // Keep "unknown"; the conflict is reported regardless.
            }

            if (string.Equals(version, harmony.ProviderVersion, StringComparison.Ordinal))
            {
                return null;
            }

            return "The mod ships its own 0Harmony " + version + " while MelonAdapter uses " + harmony.ProviderVersion + " (" + harmony.ProviderAssemblyLocation + "). Two Harmony versions cannot share one AppDomain.";
        }
    }
}