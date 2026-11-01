using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonAdapter.Runtime;
using MelonLoader;

namespace MelonAdapter.Runtime.Mods
{
    public sealed class ResolutionAttempt
    {
        public string RequestedName { get; set; }
        public string RequestingAssembly { get; set; }
        public string CandidatePath { get; set; }
        public bool Resolved { get; set; }
        public string Note { get; set; }
    }

    /// <summary>
    /// The single <see cref="AppDomain.AssemblyResolve"/> handler of the adapter. Resolution order is
    /// deterministic: the requesting mod's private folder, the adapter support directory, the mods root,
    /// then the game's managed directory. Game/engine assemblies are never replaced by a mod copy.
    /// </summary>
    public sealed class ModAssemblyResolver
    {
        private static readonly string[] ProtectedPrefixes =
        {
            "mscorlib",
            "System",
            "netstandard",
            "Mono.",
            "UnityEngine",
            "Unity.",
            "Assembly-CSharp",
            "UnityModManager",
            "MelonLoader",
            "MelonAdapter"
        };

        private readonly AdapterPaths _paths;
        private readonly IAdapterLogSink _log;
        private readonly Dictionary<string, Assembly> _resolvedByName = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ModRecord> _owners = new Dictionary<string, ModRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _dependencyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ResolutionAttempt> _attempts = new List<ResolutionAttempt>();
        private readonly object _gate = new object();
        private bool _installed;

        public ModAssemblyResolver(AdapterPaths paths, IAdapterLogSink log, bool diagnostics)
        {
            _paths = paths;
            _log = log;
            Diagnostics = diagnostics;
        }

        public bool Diagnostics { get; set; }

        public string ProbingDirectory { get; set; }

        public IReadOnlyList<ResolutionAttempt> Attempts => _attempts;

        public void Install()
        {
            if (_installed)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            _installed = true;
        }

        public void Uninstall()
        {
            if (!_installed)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
            _installed = false;
        }

        public void RegisterOwner(Assembly assembly, ModRecord record)
        {
            if (assembly == null || record == null)
            {
                return;
            }

            string name = assembly.GetName().Name;
            if (!string.IsNullOrEmpty(name))
            {
                _owners[name] = record;
            }
        }

        /// <summary>
        /// Registers a dependency-only assembly discovered in a mods subdirectory so that it can be
        /// resolved by simple name even though the scanner never treats it as a mod.
        /// </summary>
        public void RegisterDependency(ModRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.AssemblyPath))
            {
                return;
            }

            string name = record.AssemblySimpleName;
            if (string.IsNullOrEmpty(name))
            {
                name = Path.GetFileNameWithoutExtension(record.AssemblyPath);
            }

            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            lock (_gate)
            {
                if (!_dependencyPaths.ContainsKey(name))
                {
                    _dependencyPaths[name] = record.AssemblyPath;
                }
            }
        }

        /// <summary>
        /// Unity keeps its managed assemblies in <c>&lt;Game&gt;/&lt;Product&gt;_Data/Managed</c>; the older
        /// <c>&lt;Game&gt;/Managed</c> layout is probed as well. UnityModManager adds its own folder there.
        /// </summary>
        private IEnumerable<string> GameManagedDirectories()
        {
            List<string> directories = new List<string>();
            try
            {
                string legacy = Path.Combine(_paths.GameDirectory, "Managed");
                if (Directory.Exists(legacy))
                {
                    directories.Add(legacy);
                }

                foreach (string data in Directory.GetDirectories(_paths.GameDirectory, "*_Data"))
                {
                    string managed = Path.Combine(data, "Managed");
                    if (!Directory.Exists(managed))
                    {
                        continue;
                    }

                    directories.Add(managed);
                    string unityModManager = Path.Combine(managed, "UnityModManager");
                    if (Directory.Exists(unityModManager))
                    {
                        directories.Add(unityModManager);
                    }
                }
            }
            catch (Exception)
            {
                // A probing failure simply leaves the game's managed folders out of the search.
            }

            return directories;
        }

        private static string SafeLocation(Assembly assembly)
        {
            try
            {
                return assembly.Location;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Assembly FindLoadedAssembly(AssemblyName request)
        {
            Assembly[] loaded;
            try
            {
                loaded = AppDomain.CurrentDomain.GetAssemblies();
            }
            catch (Exception)
            {
                return null;
            }

            foreach (Assembly assembly in loaded)
            {
                AssemblyName name;
                try
                {
                    name = assembly.GetName();
                }
                catch (Exception)
                {
                    continue;
                }

                if (!string.Equals(name.Name, request.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (request.Version != null && name.Version != null && !request.Version.Equals(name.Version))
                {
                    continue;
                }

                return assembly;
            }

            return null;
        }

        public ModRecord GetOwner(Assembly assembly)
        {
            if (assembly == null)
            {
                return null;
            }

            ModRecord record;
            return _owners.TryGetValue(assembly.GetName().Name ?? string.Empty, out record) ? record : null;
        }

        private Assembly Resolve(object sender, ResolveEventArgs args)
        {
            AssemblyName request;
            try
            {
                request = new AssemblyName(args.Name);
            }
            catch (Exception)
            {
                return null;
            }

            string requestedName = request.Name;
            if (string.IsNullOrEmpty(requestedName))
            {
                return null;
            }

            if (IsProtected(requestedName))
            {
                if (Diagnostics)
                {
                    Record(new ResolutionAttempt
                    {
                        RequestedName = requestedName,
                        RequestingAssembly = SafeName(args.RequestingAssembly),
                        Note = "Skipped: game, engine or framework assemblies are never resolved from mod folders."
                    });
                }

                return null;
            }

            lock (_gate)
            {
                Assembly alreadyLoaded;
                if (_resolvedByName.TryGetValue(requestedName, out alreadyLoaded))
                {
                    return alreadyLoaded;
                }
            }

            // A UnityModManager mod that also ships a MelonLoader loader keeps its code in a shared
            // assembly that UMM already loaded. Returning that live assembly (instead of a second
            // copy from disk) keeps both halves of the mod looking at the same static state.
            Assembly live = FindLoadedAssembly(request);
            if (live != null)
            {
                if (Diagnostics)
                {
                    Record(new ResolutionAttempt
                    {
                        RequestedName = requestedName,
                        RequestingAssembly = SafeName(args.RequestingAssembly),
                        CandidatePath = SafeLocation(live),
                        Note = "Reused the copy that is already loaded in the process."
                    });
                }

                return live;
            }

            List<string> candidates = new List<string>();
            ModRecord owner = GetOwner(args.RequestingAssembly);
            if (owner != null && owner.DirectoryPath != null)
            {
                candidates.Add(Path.Combine(owner.DirectoryPath, requestedName + ".dll"));
            }

            if (!string.IsNullOrEmpty(ProbingDirectory))
            {
                candidates.Add(Path.Combine(ProbingDirectory, requestedName + ".dll"));
            }

            string dependencyPath;
            lock (_gate)
            {
                _dependencyPaths.TryGetValue(requestedName, out dependencyPath);
            }

            if (!string.IsNullOrEmpty(dependencyPath))
            {
                candidates.Add(dependencyPath);
            }

            if (_paths.SupportDirectory != null)
            {
                candidates.Add(Path.Combine(_paths.SupportDirectory, requestedName + ".dll"));
            }

            if (_paths.ModsDirectory != null)
            {
                candidates.Add(Path.Combine(_paths.ModsDirectory, requestedName + ".dll"));
            }

            if (_paths.GameDirectory != null)
            {
                candidates.Add(Path.Combine(_paths.GameDirectory, requestedName + ".dll"));
                foreach (string managed in GameManagedDirectories())
                {
                    candidates.Add(Path.Combine(managed, requestedName + ".dll"));
                }
            }

            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    AssemblyName candidateName = AssemblyName.GetAssemblyName(candidate);
                    ModAssemblyLoader.LoadKind loadKind;
                    Assembly loaded = ModAssemblyLoader.Load(candidate, out loadKind);
                    if (loadKind == ModAssemblyLoader.LoadKind.Memory)
                    {
                        _log.Write("resolver", AdapterLogLevel.Debug, "'" + candidate + "': " + ModAssemblyLoader.Describe(loadKind, candidate));
                    }

                    lock (_gate)
                    {
                        _resolvedByName[requestedName] = loaded;
                    }

                    Record(new ResolutionAttempt
                    {
                        RequestedName = requestedName,
                        RequestingAssembly = SafeName(args.RequestingAssembly),
                        CandidatePath = candidate,
                        Resolved = true,
                        Note = "Resolved by MelonAdapter" + (candidateName.Version != null ? " (version " + candidateName.Version + ")" : string.Empty)
                    });

                    _log.Write("resolver", AdapterLogLevel.Debug, "Resolved '" + requestedName + "' for '" + SafeName(args.RequestingAssembly) + "' to '" + candidate + "'.");
                    return loaded;
                }
                catch (Exception exception)
                {
                    Record(new ResolutionAttempt
                    {
                        RequestedName = requestedName,
                        RequestingAssembly = SafeName(args.RequestingAssembly),
                        CandidatePath = candidate,
                        Note = "Load failed: " + exception.Message
                    });
                    _log.Write("resolver", AdapterLogLevel.Warning, "Found '" + candidate + "' for '" + requestedName + "' but could not load it: " + exception.Message);
                }
            }

            Record(new ResolutionAttempt
            {
                RequestedName = requestedName,
                RequestingAssembly = SafeName(args.RequestingAssembly),
                Note = "Not found in any MelonAdapter search location."
            });

            if (Diagnostics)
            {
                _log.Write("resolver", AdapterLogLevel.Warning, "Could not resolve assembly '" + requestedName + "' requested by '" + SafeName(args.RequestingAssembly) + "'.");
            }

            return null;
        }

        private static string SafeName(Assembly assembly)
        {
            try
            {
                return assembly == null ? "unknown" : assembly.GetName().Name;
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        private static bool IsProtected(string requestedName)
        {
            foreach (string prefix in ProtectedPrefixes)
            {
                if (string.Equals(requestedName, prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (prefix.EndsWith(".", StringComparison.Ordinal) && requestedName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (prefix == "System" && requestedName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if ((prefix == "UnityEngine" || prefix == "Unity.") && requestedName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if ((prefix == "Assembly-CSharp" || prefix == "MelonAdapter" || prefix == "MelonLoader") && requestedName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void Record(ResolutionAttempt attempt)
        {
            lock (_gate)
            {
                _attempts.Add(attempt);
                if (_attempts.Count > 500)
                {
                    _attempts.RemoveAt(0);
                }
            }
        }
    }
}
