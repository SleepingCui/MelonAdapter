using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MelonAdapter.Runtime
{
    /// <summary>
    /// Resolves the running game's developer and product name.
    /// <para>
    /// MelonLoader mods declare <c>[MelonGame("developer", "product")]</c> and are rejected when the
    /// adapter reports a different pair, so these values must come from the game itself rather than
    /// from the folder it happens to live in. Unity writes both values to
    /// <c>&lt;GameRoot&gt;/&lt;Product&gt;_Data/app.info</c> (one value per line), which works for
    /// every Mono build the adapter supports.
    /// </para>
    /// </summary>
    public static class GameIdentity
    {
        /// <summary>Value used when nothing better could be resolved.</summary>
        public const string Unknown = "unknown";

        /// <summary>
        /// Fills the identity fields that are still empty or <c>unknown</c> from <c>app.info</c>,
        /// then from the game folder name. Values the host already provided are never overwritten.
        /// </summary>
        public static void ApplyDefaults(AdapterEnvironment environment)
        {
            if (environment == null)
            {
                return;
            }

            bool needDeveloper = IsUnknown(environment.GameDeveloper);
            bool needName = IsUnknown(environment.GameName);
            if (!needDeveloper && !needName)
            {
                return;
            }

            string developer;
            string name;
            if (TryReadAppInfo(environment.GameDirectory, out developer, out name))
            {
                if (needDeveloper && !string.IsNullOrEmpty(developer))
                {
                    environment.GameDeveloper = developer;
                }

                if (needName && !string.IsNullOrEmpty(name))
                {
                    environment.GameName = name;
                }

                needDeveloper = IsUnknown(environment.GameDeveloper);
                needName = IsUnknown(environment.GameName);
            }

            if (needName)
            {
                string folder = FolderName(environment.GameDirectory);
                if (!string.IsNullOrEmpty(folder))
                {
                    environment.GameName = folder;
                }
            }

            if (needDeveloper)
            {
                environment.GameDeveloper = Unknown;
            }
        }

        /// <summary>
        /// Reads Unity's <c>app.info</c>. The first non-empty line is the company name, the second is
        /// the product name.
        /// </summary>
        public static bool TryReadAppInfo(string gameRoot, out string developer, out string name)
        {
            developer = null;
            name = null;

            if (string.IsNullOrEmpty(gameRoot))
            {
                return false;
            }

            try
            {
                if (!Directory.Exists(gameRoot))
                {
                    return false;
                }

                foreach (string dataDirectory in Directory.GetDirectories(gameRoot, "*_Data"))
                {
                    string file = Path.Combine(dataDirectory, "app.info");
                    if (!File.Exists(file))
                    {
                        continue;
                    }

                    List<string> lines = new List<string>();
                    foreach (string line in File.ReadAllLines(file, Encoding.UTF8))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length > 0)
                        {
                            lines.Add(trimmed);
                        }
                    }

                    if (lines.Count == 0)
                    {
                        return false;
                    }

                    developer = lines[0];
                    name = lines.Count > 1 ? lines[1] : null;
                    return true;
                }
            }
            catch (Exception)
            {
                // Reading app.info is best effort; the caller falls back to the folder name.
            }

            return false;
        }

        private static string FolderName(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            try
            {
                string trimmed = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.IsNullOrEmpty(trimmed) ? null : Path.GetFileName(trimmed);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsUnknown(string value)
        {
            return string.IsNullOrEmpty(value) || string.Equals(value, Unknown, StringComparison.OrdinalIgnoreCase);
        }
    }
}
