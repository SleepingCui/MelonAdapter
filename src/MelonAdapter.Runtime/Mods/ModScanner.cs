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
    /// Enumerates mod assemblies, loads their metadata without running mod code and validates it
    /// against the adapter baseline.
    /// </summary>
    public sealed class ModScanner
    {
        private readonly IAdapterLogSink _log;
        private readonly AdapterEnvironment _environment;
        private readonly AdapterConfig _config;
        private readonly ModAssemblyResolver _resolver;

        private readonly List<ModRecord> _dependencyAssemblies = new List<ModRecord>();

        public IReadOnlyList<ModRecord> DependencyAssemblies => _dependencyAssemblies;

        public ModScanner(IAdapterLogSink log, AdapterEnvironment environment, AdapterConfig config, ModAssemblyResolver resolver)
        {
            _log = log;
            _environment = environment;
            _config = config;
            _resolver = resolver;
        }

        public List<ModRecord> Scan(string modsDirectory)
        {
            List<ModRecord> records = new List<ModRecord>();
            if (!Directory.Exists(modsDirectory))
            {
                _log.Write("scanner", AdapterLogLevel.Warning, "The mods directory '" + modsDirectory + "' does not exist; no mods are loaded.");
                return records;
            }

            // "*" plus a suffix test instead of "*.dll": the .NET Framework treats an extension of exactly
            // three characters in a search pattern as a prefix, so "*.dll" also returns "Mod.dllx" and
            // "Mod.dllbak", which only show up later as BadImageFormatException noise while scanning.
            List<string> files = Directory.GetFiles(modsDirectory, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Where(path => !IsAdapterAssembly(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string path in files)
            {
                string fileName = Path.GetFileName(path);
                bool inSubdirectory = !string.Equals(Path.GetDirectoryName(path), modsDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
                ModRecord record = new ModRecord
                {
                    AssemblyPath = path,
                    FileName = fileName,
                    Depth = GetDepth(modsDirectory, path)
                };

                _resolver.ProbingDirectory = Path.GetDirectoryName(path);
                try
                {
                    LoadAndInspect(record, inSubdirectory);
                }
                catch (BadImageFormatException)
                {
                    if (inSubdirectory)
                    {
                        _log.Write("scanner", AdapterLogLevel.Debug, "'" + fileName + "' is not a managed assembly; treated as a native dependency.");
                        continue;
                    }

                    record.State = ModState.Rejected;
                    record.FailureReason = "Not a managed .NET assembly.";
                }
                catch (Exception exception)
                {
                    record.State = ModState.Rejected;
                    record.FailureReason = "Failed to inspect assembly: " + exception.GetType().Name + ": " + exception.Message;
                }
                finally
                {
                    _resolver.ProbingDirectory = null;
                }

                if (record.IsDependencyOnly)
                {
                    _dependencyAssemblies.Add(record);
                    _log.Write("scanner", AdapterLogLevel.Debug, "'" + fileName + "' contains no MelonBase type; treated as a private dependency.");
                    continue;
                }

                if (record.State == ModState.Rejected)
                {
                    _log.Write("scanner", AdapterLogLevel.Error, "Rejected '" + fileName + "': " + (record.FailureReason ?? "the assembly did not pass validation."));
                }

                records.Add(record);
            }

            return records;
        }

        private static int GetDepth(string modsDirectory, string path)
        {
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            string root = modsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            string relative = directory.Substring(root.Length).Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrEmpty(relative) ? 0 : relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;
        }

        private static bool IsAdapterAssembly(string path)
        {
            string name = Path.GetFileName(path);
            return string.Equals(name, "MelonLoader.dll", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "MelonAdapter.Runtime.dll", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "MelonAdapter.Harmony.dll", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "0Harmony.dll", StringComparison.OrdinalIgnoreCase);
        }

        private void LoadAndInspect(ModRecord record, bool inSubdirectory)
        {
            _resolver.ProbingDirectory = Path.GetDirectoryName(record.AssemblyPath);

            Assembly assembly;
            ModAssemblyLoader.LoadKind loadKind;
            try
            {
                assembly = ModAssemblyLoader.Load(record.AssemblyPath, out loadKind);
            }
            finally
            {
                _resolver.ProbingDirectory = null;
            }

            string loadNote = ModAssemblyLoader.Describe(loadKind, record.AssemblyPath);
            if (loadNote != null)
            {
                _log.Write("scanner", AdapterLogLevel.Debug, "'" + record.FileName + "': " + loadNote);
            }

            record.Assembly = assembly;
            record.AssemblySimpleName = assembly.GetName().Name;
            record.AssemblyVersion = assembly.GetName().Version;
            record.Hash = MelonUtils.ComputeHash(record.AssemblyPath);

            string typeLoadFailure;
            Type[] types = GetLoadableTypes(assembly, record, out typeLoadFailure);
            List<Type> melonTypes = new List<Type>();
            foreach (Type type in types)
            {
                try
                {
                    if (type != null && type.IsClass && !type.IsAbstract && typeof(MelonBase).IsAssignableFrom(type))
                    {
                        melonTypes.Add(type);
                    }
                }
                catch (Exception)
                {
                    // A type whose base type cannot be loaded simply is not a mod type.
                }
            }

            if (melonTypes.Count == 0)
            {
                // A DLL that does not reference MelonLoader at all is a library that happens to sit
                // in the mods folder (a UnityModManager mod, a shared helper, an embedded resource
                // assembly). MelonLoader itself ignores those; so does the adapter, but it keeps them
                // registered so a mod can depend on them.
                if (!ReferencesMelonLoader(assembly))
                {
                    record.IsDependencyOnly = true;
                    _log.Write("scanner", AdapterLogLevel.Debug, "'" + record.FileName + "' is not a MelonLoader mod; it is only resolved when a mod depends on it.");
                    return;
                }

                record.State = ModState.Rejected;
                record.FailureReason = typeLoadFailure == null
                    ? "The assembly references MelonLoader but contains no MelonMod/MelonBase subclass, so it cannot be loaded as a MelonLoader mod."
                    : "The assembly references MelonLoader but none of its types could be loaded: " + typeLoadFailure;
                return;
            }

            record.MelonTypes = melonTypes.ToArray();
            ReadMetadata(record, assembly, melonTypes);

            if (record.State != ModState.Rejected)
            {
                Validate(record, melonTypes);
            }
        }

        private static bool ReferencesMelonLoader(Assembly assembly)
        {
            try
            {
                return assembly.GetReferencedAssemblies()
                    .Any(reference => string.Equals(reference.Name, "MelonLoader", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Type[] GetLoadableTypes(Assembly assembly, ModRecord record, out string firstFailure)
        {
            firstFailure = null;
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                Exception first = exception.LoaderExceptions.FirstOrDefault(loader => loader != null);
                if (first != null)
                {
                    firstFailure = first.Message;
                }

                int failures = exception.Types == null ? 0 : exception.Types.Count(type => type == null);
                record.Warnings.Add(failures + " type(s) could not be loaded from the assembly; the mod may depend on an assembly that is missing from its folder.");
                foreach (Exception loaderException in exception.LoaderExceptions.Where(e => e != null).Take(3))
                {
                    record.Warnings.Add("Type load failure: " + loaderException.Message);
                }

                return exception.Types == null ? new Type[0] : exception.Types.Where(type => type != null).ToArray();
            }
        }

        private void ReadMetadata(ModRecord record, Assembly assembly, List<Type> melonTypes)
        {
            MelonInfoAttribute info = null;
            List<MelonGameAttribute> games = new List<MelonGameAttribute>();
            List<string> dependencies = new List<string>();
            List<string> optionalDependencies = new List<string>();
            List<string> incompatible = new List<string>();
            int priority = 0;

            foreach (Type type in melonTypes)
            {
                MelonInfoAttribute typeInfo = (MelonInfoAttribute)Attribute.GetCustomAttribute(type, typeof(MelonInfoAttribute), true);
                if (info == null)
                {
                    info = typeInfo;
                }

                games.AddRange(Attribute.GetCustomAttributes(type, typeof(MelonGameAttribute), true).Cast<MelonGameAttribute>());
                MelonAdditionalDependenciesAttribute required = (MelonAdditionalDependenciesAttribute)Attribute.GetCustomAttribute(type, typeof(MelonAdditionalDependenciesAttribute), true);
                if (required != null)
                {
                    dependencies.AddRange(required.AssemblyNames.Where(name => !string.IsNullOrEmpty(name)));
                }

                MelonOptionalDependenciesAttribute optional = (MelonOptionalDependenciesAttribute)Attribute.GetCustomAttribute(type, typeof(MelonOptionalDependenciesAttribute), true);
                if (optional != null)
                {
                    optionalDependencies.AddRange(optional.AssemblyNames.Where(name => !string.IsNullOrEmpty(name)));
                }

                MelonIncompatibleAssembliesAttribute incompatibleAttribute = (MelonIncompatibleAssembliesAttribute)Attribute.GetCustomAttribute(type, typeof(MelonIncompatibleAssembliesAttribute), true);
                if (incompatibleAttribute != null)
                {
                    incompatible.AddRange(incompatibleAttribute.AssemblyNames.Where(name => !string.IsNullOrEmpty(name)));
                }

                MelonPriorityAttribute priorityAttribute = (MelonPriorityAttribute)Attribute.GetCustomAttribute(type, typeof(MelonPriorityAttribute), true);
                if (priorityAttribute != null && priorityAttribute.Priority > priority)
                {
                    priority = priorityAttribute.Priority;
                }
            }

            if (info == null)
            {
                info = (MelonInfoAttribute)Attribute.GetCustomAttribute(assembly, typeof(MelonInfoAttribute), false);
            }

            if (games.Count == 0)
            {
                games.AddRange(Attribute.GetCustomAttributes(assembly, typeof(MelonGameAttribute), false).Cast<MelonGameAttribute>());
            }

            // MelonLoader 0.4.x mods declare these on the assembly instead of on the mod class; the
            // dependency graph has to see them either way or a missing dependency stays undetected.
            if (dependencies.Count == 0)
            {
                MelonAdditionalDependenciesAttribute requiredAssembly = (MelonAdditionalDependenciesAttribute)Attribute.GetCustomAttribute(assembly, typeof(MelonAdditionalDependenciesAttribute), false);
                if (requiredAssembly != null)
                {
                    dependencies.AddRange(requiredAssembly.AssemblyNames.Where(name => !string.IsNullOrEmpty(name)));
                }
            }

            if (optionalDependencies.Count == 0)
            {
                MelonOptionalDependenciesAttribute optionalAssembly = (MelonOptionalDependenciesAttribute)Attribute.GetCustomAttribute(assembly, typeof(MelonOptionalDependenciesAttribute), false);
                if (optionalAssembly != null)
                {
                    optionalDependencies.AddRange(optionalAssembly.AssemblyNames.Where(name => !string.IsNullOrEmpty(name)));
                }
            }

            if (incompatible.Count == 0)
            {
                MelonIncompatibleAssembliesAttribute incompatibleAssembly = (MelonIncompatibleAssembliesAttribute)Attribute.GetCustomAttribute(assembly, typeof(MelonIncompatibleAssembliesAttribute), false);
                if (incompatibleAssembly != null)
                {
                    incompatible.AddRange(incompatibleAssembly.AssemblyNames.Where(name => !string.IsNullOrEmpty(name)));
                }
            }

            record.Info = info;
            record.Games = games.ToArray();
            record.Dependencies = dependencies.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            record.OptionalDependencies = optionalDependencies.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            record.IncompatibleAssemblies = incompatible.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            record.Priority = priority;
        }

        private void Validate(ModRecord record, List<Type> melonTypes)
        {
            List<string> errors = new List<string>();

            if (record.Info == null)
            {
                if (_config.AllowUnannotatedMods)
                {
                    record.Warnings.Add("No MelonInfo attribute; resolver.allowUnannotatedMods is enabled so the mod is loaded with a derived identity.");
                    record.Info = SynthesizeInfo(record);
                }
                else
                {
                    errors.Add("No MelonInfo attribute. Set resolver.allowUnannotatedMods = true in Config.toml only for development builds.");
                }
            }

            if (record.Info != null)
            {
                if (string.IsNullOrEmpty(record.Info.Name))
                {
                    errors.Add("MelonInfo.Name is empty.");
                }

                if (string.IsNullOrEmpty(record.Info.Author))
                {
                    record.Warnings.Add("MelonInfo.Author is empty.");
                }
            }

            if (record.Games.Length > 0)
            {
                bool compatible = record.Games.Any(game => game.IsCompatible(_environment.GameDeveloper, _environment.GameName));
                if (!compatible)
                {
                    errors.Add("The mod targets " + DescribeGames(record.Games) + ", but the running game is '" + _environment.GameName + "' by '" + _environment.GameDeveloper + "'.");
                }
            }

            foreach (Type type in melonTypes)
            {
                ValidateType(type, errors, record);
            }

            if (errors.Count > 0)
            {
                record.State = ModState.Rejected;
                record.FailureReason = string.Join(" ", errors.ToArray());
            }
        }

        private void ValidateType(Type type, List<string> errors, ModRecord record)
        {
            MelonPlatformAttribute platform = (MelonPlatformAttribute)Attribute.GetCustomAttribute(type, typeof(MelonPlatformAttribute), true);
            if (platform != null)
            {
                MelonPlatformAttribute.CompatiblePlatforms current = MelonUtils.CurrentPlatform;
                bool compatible = platform.Platforms.Contains(MelonPlatformAttribute.CompatiblePlatforms.UNIVERSAL) || platform.Platforms.Contains(current);
                if (!compatible)
                {
                    errors.Add("The mod does not support platform " + current + ".");
                }
            }

            MelonPlatformDomainAttribute domain = (MelonPlatformDomainAttribute)Attribute.GetCustomAttribute(type, typeof(MelonPlatformDomainAttribute), true);
            if (domain != null && domain.Domain == MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)
            {
                errors.Add("The mod requires the IL2CPP domain, which MelonAdapter does not support.");
            }

            MelonProcessAttribute process = (MelonProcessAttribute)Attribute.GetCustomAttribute(type, typeof(MelonProcessAttribute), true);
            if (process != null && !process.Universal && !string.Equals(process.EXE_Name, _environment.GameExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("The mod targets process '" + process.EXE_Name + "', but the running process is '" + _environment.GameExecutableName + "'.");
            }

            VerifyLoaderVersionAttribute version = (VerifyLoaderVersionAttribute)Attribute.GetCustomAttribute(type, typeof(VerifyLoaderVersionAttribute), true);
            if (version != null)
            {
                Version baseline = ParseVersion(_config.ApiBaseline, new Version(0, 7, 3, 0));
                Version required = new Version(version.Major, version.Minor, version.Patch);
                if (baseline < required)
                {
                    errors.Add("The mod requires MelonLoader " + required + " or newer, but the adapter API baseline is " + baseline + ".");
                }
            }

            VerifyLoaderBuildAttribute build = (VerifyLoaderBuildAttribute)Attribute.GetCustomAttribute(type, typeof(VerifyLoaderBuildAttribute), true);
            if (build != null)
            {
                record.Warnings.Add("VerifyLoaderBuild is not enforced by MelonAdapter; the requested build hash '" + build.HashCode + "' is ignored.");
            }
        }

        private static string DescribeGames(IEnumerable<MelonGameAttribute> games) =>
            string.Join(", ", games.Select(game => game.Universal ? "any game" : "'" + game.Name + "' by '" + game.Developer + "'").ToArray());

        private static Version ParseVersion(string text, Version fallback)
        {
            Version parsed;
            return Version.TryParse(text, out parsed) ? parsed : fallback;
        }

        private static MelonInfoAttribute SynthesizeInfo(ModRecord record)
        {
            Type first = record.MelonTypes.FirstOrDefault();
            string name = first != null ? first.Name : record.AssemblySimpleName;
            return new MelonInfoAttribute(record.MelonTypes.FirstOrDefault(), name, record.AssemblyVersion?.ToString() ?? "0.0.0", "unknown", string.Empty);
        }
    }
}