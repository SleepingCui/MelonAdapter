using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;

namespace MelonAdapter.Runtime.Mods
{
    public enum ModState
    {
        Discovered = 0,
        Loaded = 1,
        Rejected = 2,
        Disabled = 3,
        Failed = 4
    }

    /// <summary>One mod assembly discovered under the configured mods directory.</summary>
    public sealed class ModRecord
    {
        public string AssemblyPath { get; set; }
        public string FileName { get; set; }
        public string AssemblySimpleName { get; set; }
        public Version AssemblyVersion { get; set; }
        public string Hash { get; set; }
        public Assembly Assembly { get; set; }
        public bool IsDependencyOnly { get; set; }

        public MelonInfoAttribute Info { get; set; }
        public MelonGameAttribute[] Games { get; set; } = new MelonGameAttribute[0];
        public Type[] MelonTypes { get; set; } = new Type[0];
        public string[] Dependencies { get; set; } = new string[0];
        public string[] OptionalDependencies { get; set; } = new string[0];
        public string[] IncompatibleAssemblies { get; set; } = new string[0];
        public int Priority { get; set; }

        /// <summary>
        /// Directory depth of the assembly below the mods root (0 = directly inside it). Used only to
        /// make the load order deterministic: a mod placed in the mods root wins over a copy in a
        /// subdirectory when both declare the same mod id.
        /// </summary>
        public int Depth { get; set; }

        public ModState State { get; set; } = ModState.Discovered;
        public string FailureReason { get; set; }
        public readonly List<string> Notes = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<MelonBase> Melons = new List<MelonBase>();
        public readonly List<string> ConsecutiveFailures = new List<string>();

        public string ModId
        {
            get
            {
                if (Info != null && !string.IsNullOrEmpty(Info.Name))
                {
                    return Info.Name;
                }

                return AssemblySimpleName;
            }
        }

        public string DisplayName => Info != null && !string.IsNullOrEmpty(Info.Name) ? Info.Name : AssemblySimpleName;

        public string Author => Info != null ? Info.Author : string.Empty;

        public string VersionText => Info != null ? Info.Version : (AssemblyVersion?.ToString() ?? string.Empty);

        public string DirectoryPath => AssemblyPath == null ? null : System.IO.Path.GetDirectoryName(AssemblyPath);

        public bool IsActive => State == ModState.Loaded || State == ModState.Disabled;

        public override string ToString() => DisplayName + " " + VersionText;
    }
}
