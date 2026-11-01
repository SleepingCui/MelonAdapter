using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace MelonAdapter.Host
{
    /// <summary>
    /// Refuses to start when the process already runs a real MelonLoader, when the game is IL2CPP
    /// or when the install layout suggests MelonLoader would inject itself into this process.
    /// Two MelonMod type identities can never coexist, so failing early is the only safe option.
    /// </summary>
    public static class ConflictDetector
    {
        private static readonly string[] RealMelonLoaderAssemblies =
        {
            "MelonLoader.Core",
            "MelonStartScreen",
            "MelonLoader.ModHandler",
            "MelonLoader.Utils",
            "MelonLoader.Bootstrap"
        };

        public static string Describe(string gameRoot)
        {
            List<string> problems = new List<string>();
            string facadeLocation = SafeLocation(typeof(MelonMod).Assembly);

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                string location = SafeLocation(assembly);

                if (string.Equals(name, "MelonLoader", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(location) && !SameRoot(location, facadeLocation))
                    {
                        problems.Add("a MelonLoader assembly is already loaded from '" + location + "' (the adapter facade lives at '" + facadeLocation + "')");
                    }

                    continue;
                }

                if (RealMelonLoaderAssemblies.Any(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)))
                {
                    problems.Add("the real MelonLoader component '" + name + "' is loaded from '" + location + "'");
                }
            }

            if (!string.IsNullOrEmpty(gameRoot) && Directory.Exists(gameRoot))
            {
                if (File.Exists(Path.Combine(gameRoot, "GameAssembly.dll")) || Directory.Exists(Path.Combine(gameRoot, "il2cpp_data")))
                {
                    problems.Add("the game is IL2CPP ('GameAssembly.dll' found); IL2CPP games are out of scope");
                }

                if (Directory.Exists(Path.Combine(gameRoot, "MelonLoader")))
                {
                    problems.Add("a MelonLoader installation folder exists at '" + Path.Combine(gameRoot, "MelonLoader") + "'");
                }

                if (File.Exists(Path.Combine(gameRoot, "MelonLoader.dll")))
                {
                    problems.Add("a MelonLoader DLL sits in the game root ('" + Path.Combine(gameRoot, "MelonLoader.dll") + "')");
                }
            }

            if (problems.Count == 0)
            {
                return null;
            }

            return "MelonAdapter refuses to load mods because " + string.Join("; ", problems) +
                   ". Remove MelonLoader from this game (or use MelonLoader directly) and restart.";
        }

        private static bool SameRoot(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetDirectoryName(left), Path.GetDirectoryName(right), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string SafeLocation(Assembly assembly)
        {
            try
            {
                return assembly.IsDynamic ? "<dynamic>" : assembly.Location;
            }
            catch (Exception)
            {
                return "<unknown>";
            }
        }
    }
}
