using System;

namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonInfoAttribute : Attribute
    {
        public MelonInfoAttribute(Type systemType, string name, string version, string author, string downloadLink = null)
        {
            SystemType = systemType;
            Name = name;
            Version = version;
            Author = author;
            DownloadLink = downloadLink;
        }

        public MelonInfoAttribute(Type systemType, string name, int major, int minor, int patch, string author = null, string downloadLink = null)
            : this(systemType, name, major + "." + minor + "." + patch, author, downloadLink)
        {
        }

        public MelonInfoAttribute(Type systemType, string name, int major, int minor, int patch, string author)
            : this(systemType, name, major + "." + minor + "." + patch, author, null)
        {
        }

        /// <summary>
        /// Numeric version with a pre-release identifier, e.g. <c>(1, 9, 3, "alpha")</c>. The identifier is
        /// appended to the version string, which is how MelonLoader 0.7.3 renders it.
        /// </summary>
        public MelonInfoAttribute(Type systemType, string name, int major, int minor, int patch, string versionIdentifier, string author, string downloadLink = null)
            : this(
                systemType,
                name,
                major + "." + minor + "." + patch + (string.IsNullOrEmpty(versionIdentifier) ? string.Empty : "-" + versionIdentifier),
                author,
                downloadLink)
        {
        }

        public Type SystemType { get; }
        public string Name { get; }
        public string Version { get; }
        public string Author { get; }
        public string DownloadLink { get; }

        /// <summary>Semantic version parsed from <see cref="Version"/>, e.g. <c>1.9.3-alpha</c>.</summary>
        public Semver.SemVersion SemanticVersion => Semver.SemVersion.Parse(Version, false);

        /// <summary>Parsed <see cref="Version"/>; null when the version string is not a System.Version.</summary>
        public Version ParsedVersion
        {
            get
            {
                System.Version parsed;
                return System.Version.TryParse(Version, out parsed) ? parsed : null;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public class MelonGameAttribute : Attribute
    {
        public MelonGameAttribute(string developer, string name)
        {
            Developer = developer;
            Name = name;
            Universal = string.IsNullOrEmpty(developer) && string.IsNullOrEmpty(name);
        }

        public MelonGameAttribute() : this(null, null)
        {
        }

        public string Developer { get; }
        public string Name { get; }
        public bool Universal { get; }

        public bool IsCompatible(string developer, string name) =>
            Universal || (string.Equals(Developer, developer, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(Name, name, StringComparison.OrdinalIgnoreCase));

        public bool IsCompatible(MelonGameAttribute other) =>
            other != null && IsCompatible(other.Developer, other.Name);

        public bool IsCompatible(MelonModGameAttribute other) =>
            other != null && IsCompatible(other.Developer, other.GameName);

        public bool IsCompatible(MelonPluginGameAttribute other) =>
            other != null && IsCompatible(other.Developer, other.GameName);

        /// <summary>True when the declaration targets every game, so no developer/name comparison is needed.</summary>
        public bool IsCompatibleBecauseUniversal(MelonModGameAttribute other) =>
            Universal || (other != null && other.Universal);

        /// <summary>True when the declaration targets every game, so no developer/name comparison is needed.</summary>
        public bool IsCompatibleBecauseUniversal(MelonPluginGameAttribute other) =>
            Universal || (other != null && other.Universal);
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonGameVersionAttribute : Attribute
    {
        public MelonGameVersionAttribute(string version)
        {
            Version = version;
        }

        public string Version { get; }
        public bool Universal => string.IsNullOrEmpty(Version);
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonPriorityAttribute : Attribute
    {
        public MelonPriorityAttribute(int priority)
        {
            Priority = priority;
        }

        public int Priority { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonAdditionalDependenciesAttribute : Attribute
    {
        public MelonAdditionalDependenciesAttribute(params string[] assemblyNames)
        {
            AssemblyNames = assemblyNames ?? new string[0];
        }

        public string[] AssemblyNames { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonOptionalDependenciesAttribute : Attribute
    {
        public MelonOptionalDependenciesAttribute(params string[] assemblyNames)
        {
            AssemblyNames = assemblyNames ?? new string[0];
        }

        public string[] AssemblyNames { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonIDAttribute : Attribute
    {
        public MelonIDAttribute(string id)
        {
            ID = id;
        }

        public MelonIDAttribute(int id)
        {
            ID = id.ToString();
        }

        public string ID { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonPlatformAttribute : Attribute
    {
        public enum CompatiblePlatforms
        {
            UNIVERSAL = 0,
            WINDOWS_X86 = 1,
            WINDOWS_X64 = 2,
            ANDROID = 3,
            LINUX = 4,
            MAC = 5
        }

        public MelonPlatformAttribute(params CompatiblePlatforms[] platforms)
        {
            Platforms = platforms ?? new[] { CompatiblePlatforms.UNIVERSAL };
        }

        public CompatiblePlatforms[] Platforms { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonPlatformDomainAttribute : Attribute
    {
        public enum CompatibleDomains
        {
            UNIVERSAL = 0,
            MONO = 1,
            IL2CPP = 2
        }

        public MelonPlatformDomainAttribute(CompatibleDomains domain)
        {
            Domain = domain;
        }

        public CompatibleDomains Domain { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonProcessAttribute : Attribute
    {
        public MelonProcessAttribute(string exeName)
        {
            EXE_Name = exeName;
        }

        public string EXE_Name { get; }
        public bool Universal => string.IsNullOrEmpty(EXE_Name);
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonColorAttribute : Attribute
    {
        public MelonColorAttribute()
        {
            Color = ConsoleColor.Gray;
        }

        public MelonColorAttribute(ConsoleColor color)
        {
            Color = color;
        }

        public MelonColorAttribute(int a, int r, int g, int b)
        {
            Color = ConsoleColor.Gray;
            DrawingColor = Logging.ColorARGB.FromArgb((byte)a, (byte)r, (byte)g, (byte)b);
        }

        public virtual ConsoleColor Color { get; set; }

        public Logging.ColorARGB DrawingColor { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonAuthorColorAttribute : MelonColorAttribute
    {
        public MelonAuthorColorAttribute()
        {
        }

        public MelonAuthorColorAttribute(ConsoleColor color) : base(color)
        {
        }

        public MelonAuthorColorAttribute(int a, int r, int g, int b) : base(a, r, g, b)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonAdditionalCreditsAttribute : Attribute
    {
        public MelonAdditionalCreditsAttribute(string credits)
        {
            Credits = credits;
        }

        public string Credits { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonIncompatibleAssembliesAttribute : Attribute
    {
        public MelonIncompatibleAssembliesAttribute(params string[] assemblyNames)
        {
            AssemblyNames = assemblyNames ?? new string[0];
        }

        public string[] AssemblyNames { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class VerifyLoaderVersionAttribute : Attribute
    {
        public VerifyLoaderVersionAttribute(string version, bool isMinimum = false)
        {
            SemVer = Semver.SemVersion.Parse(version, false);
            IsMinimum = isMinimum;
            Version parsed;
            if (Version.TryParse(version, out parsed))
            {
                Major = parsed.Major;
                Minor = parsed.Minor;
                Patch = parsed.Build < 0 ? 0 : parsed.Build;
            }
        }

        public VerifyLoaderVersionAttribute(int major, int minor, int patch, bool isMinimum = false)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            IsMinimum = isMinimum;
            SemVer = new Semver.SemVersion(major, minor, patch);
        }

        public Semver.SemVersion SemVer { get; }
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string Prerelease { get; }
        public bool IsMinimum { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class VerifyLoaderBuildAttribute : Attribute
    {
        public VerifyLoaderBuildAttribute(string hashCode)
        {
            HashCode = hashCode;
        }

        public string HashCode { get; }
    }

    /// <summary>Mod-specific alias of <see cref="MelonInfoAttribute"/> retained by the API baseline.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class MelonModInfoAttribute : MelonInfoAttribute
    {
        public MelonModInfoAttribute(Type systemType, string name, string version, string author, string downloadLink = null)
            : base(systemType, name, version, author, downloadLink)
        {
        }

        public MelonModInfoAttribute(Type systemType, string name, int major, int minor, int patch, string author = null, string downloadLink = null)
            : base(systemType, name, major, minor, patch, author, downloadLink)
        {
        }
    }

    /// <summary>Mod-specific alias of <see cref="MelonGameAttribute"/> retained by the API baseline.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public class MelonModGameAttribute : MelonGameAttribute
    {
        public MelonModGameAttribute(string developer = null, string name = null) : base(developer, name)
        {
        }

        public MelonModGameAttribute()
        {
        }

        public new string Developer => base.Developer;

        public string GameName => Name;
    }

    /// <summary>
    /// Opts a melon out of the automatic <c>HarmonyInstance.PatchAll()</c> call that the runtime
    /// performs after initialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class HarmonyDontPatchAllAttribute : Attribute
    {
        public HarmonyDontPatchAllAttribute()
        {
        }
    }
}
