using System;
using System.IO;
using System.Reflection;

namespace MelonAdapter.Runtime.Mods
{
    /// <summary>
    /// Loads a mod assembly from disk.
    /// <para>
    /// <see cref="Assembly.LoadFrom(string)"/> is preferred because it keeps
    /// <see cref="Assembly.Location"/> populated, which mods use to read files next to themselves.
    /// The .NET Framework refuses that call for files on a network location (or a drive the OS reports
    /// as remote) with HRESULT 0x80131515 and the message about sandboxed assemblies and
    /// <c>loadFromRemoteSources</c>. That has nothing to do with the assembly itself, so the bytes are
    /// loaded from memory instead; the adapter resolves the mod's dependencies through its own
    /// <see cref="AppDomain.AssemblyResolve"/> handler, so a missing Location is harmless.
    /// </para>
    /// </summary>
    public static class ModAssemblyLoader
    {
        public enum LoadKind
        {
            /// <summary>Assembly.LoadFrom succeeded; Assembly.Location is available.</summary>
            File,

            /// <summary>The file lives on a network location, so it was loaded from memory.</summary>
            Memory,

            /// <summary>
            /// An assembly with the same identity is already loaded in this process, so it was reused
            /// instead of being loaded a second time. A second copy would split static state between two
            /// type identities - for example a mod whose runtime half was already loaded by the host.
            /// </summary>
            Reused
        }

        private const int NetworkZoneHResult = unchecked((int)0x80131515);

        public static Assembly Load(string path, out LoadKind kind)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A mod assembly path is required.", nameof(path));
            }

            Assembly live = FindLoadedByFileIdentity(path);
            if (live != null)
            {
                kind = LoadKind.Reused;
                return live;
            }

            try
            {
                kind = LoadKind.File;
                return Assembly.LoadFrom(path);
            }
            catch (Exception exception)
            {
                if (!IsNetworkZoneFailure(exception))
                {
                    throw;
                }
            }

            kind = LoadKind.Memory;
            return Assembly.Load(File.ReadAllBytes(path));
        }

        /// <summary>Explains an unusual load in the log, so a missing Assembly.Location is never a mystery.</summary>
        public static string Describe(LoadKind kind, string path)
        {
            switch (kind)
            {
                case LoadKind.Memory:
                    return "The assembly is on a network location, so it was loaded from memory instead of from its path. " +
                           "Assembly.Location is empty for this mod; MelonAdapter resolves its dependencies by name.";
                case LoadKind.Reused:
                    return "An assembly with the same identity is already loaded in this process, so the existing copy " +
                           "was reused; loading the file again would split static state between two type identities.";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Reads the assembly identity from the file and returns the copy that is already loaded with that
        /// identity, if there is one. A host such as UnityModManager loads its own mod DLLs from the same
        /// folder, so loading them again here would produce a second copy of the same mod.
        /// </summary>
        private static Assembly FindLoadedByFileIdentity(string path)
        {
            AssemblyName identity;
            try
            {
                identity = AssemblyName.GetAssemblyName(path);
            }
            catch (Exception)
            {
                // Not a loadable managed assembly (or unreadable); let the real load path report it.
                return null;
            }

            if (identity == null || string.IsNullOrEmpty(identity.Name))
            {
                return null;
            }

            foreach (Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
            {
                AssemblyName loaded;
                try
                {
                    loaded = candidate.GetName();
                }
                catch (Exception)
                {
                    continue;
                }

                if (!string.Equals(loaded.Name, identity.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (identity.Version != null && loaded.Version != null && identity.Version != loaded.Version)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static bool IsNetworkZoneFailure(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current is NotSupportedException || current.HResult == NetworkZoneHResult)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
