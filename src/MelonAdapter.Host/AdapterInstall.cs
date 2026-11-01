using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MelonAdapter.Host
{
    /// <summary>Resolves the adapter install layout on disk.</summary>
    public static class AdapterInstall
    {
        /// <summary>Environment variable that overrides every other location rule.</summary>
        public const string HomeVariable = "MELONADAPTER_HOME";

        private static readonly object ResolverGate = new object();
        private static bool _resolverInstalled;

        /// <summary>
        /// Names the adapter itself ships. These are resolved from the adapter folder before any
        /// other logic runs, because the host (UnityModManager) loads this DLL from a path that is
        /// not part of the AppDomain probing path.
        /// </summary>
        private static readonly string[] AdapterOwnedAssemblies =
        {
            "MelonLoader",
            "MelonAdapter.Runtime",
            "0Harmony"
        };

        /// <summary>
        /// Installs an <see cref="AppDomain.AssemblyResolve"/> handler that looks next to the adapter
        /// assembly. It is called from a module initializer, so it runs before the first adapter type
        /// is touched.
        /// </summary>
        public static void InstallAssemblyResolver()
        {
            lock (ResolverGate)
            {
                if (_resolverInstalled)
                {
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                _resolverInstalled = true;
            }
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string simpleName;
            try
            {
                simpleName = new AssemblyName(args.Name).Name;
            }
            catch (Exception)
            {
                return null;
            }

            if (string.IsNullOrEmpty(simpleName) || Array.IndexOf(AdapterOwnedAssemblies, simpleName) < 0)
            {
                return null;
            }

            foreach (string directory in ProbeDirectories())
            {
                string candidate = Path.Combine(directory, simpleName + ".dll");
                if (!File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    return Assembly.Load(File.ReadAllBytes(candidate));
                }
                catch (Exception)
                {
                    // Keep probing; another copy may still load.
                }
            }

            return null;
        }

        private static IEnumerable<string> ProbeDirectories()
        {
            string location = null;
            try
            {
                location = typeof(AdapterInstall).Assembly.Location;
            }
            catch (Exception)
            {
                // Dynamic or single-file style loads have no location.
            }

            if (!string.IsNullOrEmpty(location))
            {
                string directory = Path.GetDirectoryName(location);
                if (!string.IsNullOrEmpty(directory))
                {
                    yield return directory;
                    yield return Path.Combine(directory, "Dependencies");
                }
            }
        }

        /// <summary>
        /// Resolution order: MELONADAPTER_HOME, then <c>&lt;GameRoot&gt;/MelonAdapter</c> when it already
        /// contains an adapter installation, then the UnityModManager mod folder this DLL was loaded from.
        /// </summary>
        public static string ResolveDataDirectory(string gameRoot, string ummModPath)
        {
            string overridden = Environment.GetEnvironmentVariable(HomeVariable);
            if (!string.IsNullOrEmpty(overridden))
            {
                return Path.GetFullPath(overridden);
            }

            string conventional = Path.Combine(gameRoot, "MelonAdapter");
            if (File.Exists(Path.Combine(conventional, "Config.toml")) || Directory.Exists(Path.Combine(conventional, "Mods")))
            {
                return conventional;
            }

            return ummModPath;
        }

        /// <summary>
        /// Tries to derive the game root from the UnityModManager mod folder. UnityModManager hands
        /// out mod paths with a trailing separator, and <see cref="Directory.GetParent(string)"/>
        /// treats such a path as the folder itself - which used to make the game root point at
        /// <c>Mods/</c> and reject every mod as belonging to a different game.
        /// </summary>
        public static string ResolveGameRoot(string ummModPath)
        {
            try
            {
                string normalised = TrimTrailingSeparators(ummModPath);
                DirectoryInfo parent = Directory.GetParent(normalised);
                if (parent?.Parent != null)
                {
                    return parent.Parent.FullName;
                }
            }
            catch (Exception)
            {
                // Fall through to the process base directory.
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string TrimTrailingSeparators(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (trimmed.Length == 0)
            {
                return path;
            }

            if (trimmed.Length == 2 && trimmed[1] == ':')
            {
                return trimmed + Path.DirectorySeparatorChar;
            }

            return trimmed;
        }
    }
}
