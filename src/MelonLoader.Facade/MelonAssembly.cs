using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace MelonLoader
{
    /// <summary>
    /// A melon that was discovered inside a mod assembly but could not be created or registered.
    /// The field names and constructor overloads match the MelonLoader 0.7.3 baseline because mods
    /// read these fields directly.
    /// </summary>
    public class RottenMelon
    {
        public Assembly assembly;
        public Type type;
        public string errorMessage;
        public string exception;

        public RottenMelon(MelonAssembly assembly, string errorMessage)
            : this(assembly == null ? null : assembly.Assembly, errorMessage, (string)null)
        {
        }

        public RottenMelon(MelonAssembly assembly, string errorMessage, string exception)
            : this(assembly == null ? null : assembly.Assembly, errorMessage, exception)
        {
        }

        public RottenMelon(MelonAssembly assembly, string errorMessage, Exception exception)
            : this(assembly == null ? null : assembly.Assembly, errorMessage, exception)
        {
        }

        public RottenMelon(Type type, string errorMessage)
            : this(type, errorMessage, (string)null)
        {
        }

        public RottenMelon(Type type, string errorMessage, string exception)
        {
            this.type = type;
            this.assembly = type == null ? null : type.Assembly;
            this.errorMessage = errorMessage;
            this.exception = exception;
        }

        public RottenMelon(Type type, string errorMessage, Exception exception)
            : this(type, errorMessage, exception == null ? null : exception.ToString())
        {
        }

        public RottenMelon(Assembly assembly, string errorMessage)
            : this(assembly, errorMessage, (string)null)
        {
        }

        public RottenMelon(Assembly assembly, string errorMessage, string exception)
        {
            this.assembly = assembly;
            this.errorMessage = errorMessage;
            this.exception = exception;
        }

        public RottenMelon(Assembly assembly, string errorMessage, Exception exception)
            : this(assembly, errorMessage, exception == null ? null : exception.ToString())
        {
        }
    }

    /// <summary>
    /// Result of a custom melon resolver: the melons that were created and the ones that failed.
    /// </summary>
    public class ResolvedMelons
    {
        public MelonBase[] loadedMelons;
        public RottenMelon[] rottenMelons;

        public ResolvedMelons(MelonBase[] loadedMelons, RottenMelon[] rottenMelons)
        {
            this.loadedMelons = loadedMelons ?? new MelonBase[0];
            this.rottenMelons = rottenMelons ?? new RottenMelon[0];
        }
    }

    /// <summary>
    /// Groups the melons that were discovered inside a single managed assembly.
    /// MelonAdapter.Runtime creates exactly one instance per mod assembly it loads.
    /// </summary>
    public class MelonAssembly
    {
        private static readonly List<MelonAssembly> LoadedAssembliesInternal = new List<MelonAssembly>();
        private static readonly ReadOnlyCollection<MelonAssembly> LoadedAssembliesView = LoadedAssembliesInternal.AsReadOnly();

        internal static readonly List<MelonBase> RegisteredMelonsInternal = new List<MelonBase>();
        internal static readonly ReadOnlyCollection<MelonBase> RegisteredMelonsView = RegisteredMelonsInternal.AsReadOnly();

        private readonly List<MelonBase> _loadedMelons = new List<MelonBase>();
        private readonly ReadOnlyCollection<MelonBase> _loadedMelonsView;
        private readonly List<RottenMelon> _rottenMelons = new List<RottenMelon>();
        private readonly ReadOnlyCollection<RottenMelon> _rottenMelonsView;

        /// <summary>
        /// Raised for every assembly before its melons are resolved, matching the loader event of the same name.
        /// </summary>
        public MelonEvent<Assembly> OnAssemblyResolving = new MelonEvent<Assembly>();

        /// <summary>
        /// Raised when this assembly unregisters its melons.
        /// </summary>
        public MelonEvent OnUnregister = new MelonEvent();

        /// <summary>
        /// Lets a MelonPlugin provide the melons of an assembly itself instead of letting the loader
        /// scan the assembly for MelonBase subclasses.
        /// </summary>
        public static event LemonFunc<Assembly, ResolvedMelons> CustomMelonResolvers;

        internal MelonAssembly(Assembly assembly, string location, string hash)
        {
            Assembly = assembly;
            LocationInternal = location;
            Hash = hash ?? string.Empty;
            _loadedMelonsView = _loadedMelons.AsReadOnly();
            _rottenMelonsView = _rottenMelons.AsReadOnly();
        }

        public static ReadOnlyCollection<MelonAssembly> LoadedAssemblies => LoadedAssembliesView;

        public Assembly Assembly { get; }

        internal string LocationInternal { get; }

        public virtual string Location => LocationInternal ?? (Assembly == null ? string.Empty : Assembly.Location);

        public string Hash { get; }

        /// <summary>
        /// True when the assembly carries [HarmonyDontPatchAll]; the loader must never call
        /// Harmony.PatchAll() for melons of this assembly.
        /// </summary>
        public bool HarmonyDontPatchAll
        {
            get
            {
                try
                {
                    Assembly assembly = Assembly;
                    if (assembly != null && assembly.GetCustomAttributes(typeof(MelonLoader.HarmonyDontPatchAllAttribute), false).Length > 0)
                    {
                        return true;
                    }

                    return _loadedMelons.Any(melon => melon.HarmonyDontPatchAll);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public ReadOnlyCollection<MelonBase> LoadedMelons => _loadedMelonsView;

        public ReadOnlyCollection<RottenMelon> RottenMelons => _rottenMelonsView;

        public static T FindMelonInstance<T>() where T : MelonBase
        {
            foreach (MelonAssembly assembly in LoadedAssembliesInternal)
            {
                T match = assembly._loadedMelons.OfType<T>().FirstOrDefault();
                if (match != null)
                {
                    return match;
                }
            }

            return RegisteredMelonsInternal.OfType<T>().FirstOrDefault();
        }

        public static MelonAssembly GetMelonAssemblyOfMember(MemberInfo member, object obj = null)
        {
            if (obj is MelonBase melon && melon.MelonAssembly != null)
            {
                return melon.MelonAssembly;
            }

            Assembly assembly = member == null || member.DeclaringType == null ? null : member.DeclaringType.Assembly;
            if (assembly == null)
            {
                return null;
            }

            return LoadedAssembliesInternal.FirstOrDefault(a => a.Assembly == assembly);
        }

        /// <summary>
        /// MelonAdapter loads every mod from the configured Mods directory during startup; loading an
        /// extra assembly at runtime is not supported by v1 and is reported instead of failing silently.
        /// </summary>
        public static MelonAssembly LoadMelonAssembly(string path, bool loadMelons = true)
        {
            MelonLogger.Publish(
                AdapterLogLevel.Error,
                "MelonAssembly.LoadMelonAssembly('" + path + "') is not supported by MelonAdapter v1. MelonAdapter loads every mod from the configured Mods directory exactly once during startup; place the assembly there and restart the game.",
                null,
                null);
            return null;
        }

        public static MelonAssembly LoadRawMelonAssembly(string path, byte[] assemblyData, byte[] symbolsData = null, bool loadMelons = true)
        {
            MelonLogger.Publish(
                AdapterLogLevel.Error,
                "MelonAssembly.LoadRawMelonAssembly('" + path + "') is not supported by MelonAdapter v1. MelonAdapter loads every mod from the configured Mods directory exactly once during startup; place the assembly there and restart the game.",
                null,
                null);
            return null;
        }

        public static MelonAssembly LoadMelonAssembly(string path, Assembly assembly, bool loadMelons = true)
        {
            MelonLogger.Publish(
                AdapterLogLevel.Error,
                "MelonAssembly.LoadMelonAssembly('" + path + "') is not supported by MelonAdapter v1. MelonAdapter loads every mod from the configured Mods directory exactly once during startup; place the assembly there and restart the game.",
                null,
                null);
            return null;
        }

        /// <summary>
        /// Unregisters every melon of this assembly. Used by the loader on shutdown and by plugins
        /// that replace the built-in resolution.
        /// </summary>
        public void UnregisterMelons(string reason = null, bool silent = false)
        {
            foreach (MelonBase melon in _loadedMelons.ToArray())
            {
                try
                {
                    melon.Unregister(reason, silent);
                }
                catch (Exception exception)
                {
                    MelonLogger.Publish(
                        AdapterLogLevel.Error,
                        "Exception while unregistering melon '" + (melon.Info == null ? melon.MelonTypeName : melon.Info.Name) + "': " + exception.Message,
                        null,
                        exception);
                }
            }

            try
            {
                OnUnregister.Invoke();
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "Exception while notifying the assembly unregister event: " + exception.Message, null, exception);
            }
        }

        /// <summary>
        /// Resolves and registers the melons of <see cref="Assembly"/> the same way MelonLoader does.
        /// MelonAdapter already performs this during startup, so mods rarely need to call it directly.
        /// </summary>
        public void LoadMelons()
        {
            Assembly assembly = Assembly;
            if (assembly == null)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "MelonAssembly.LoadMelons() cannot run because the assembly is not available.", null, null);
                return;
            }

            try
            {
                OnAssemblyResolving.Invoke(assembly);
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "Exception while notifying the assembly resolving event: " + exception.Message, null, exception);
            }

            List<MelonBase> resolved = new List<MelonBase>();
            LemonFunc<Assembly, ResolvedMelons> resolver = CustomMelonResolvers;
            if (resolver != null)
            {
                try
                {
                    ResolvedMelons custom = resolver(assembly);
                    if (custom != null)
                    {
                        if (custom.loadedMelons != null)
                        {
                            resolved.AddRange(custom.loadedMelons);
                        }

                        if (custom.rottenMelons != null)
                        {
                            foreach (RottenMelon rotten in custom.rottenMelons)
                            {
                                AddRottenMelon(rotten);
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    AddRottenMelon(new RottenMelon(this, "The custom melon resolver threw " + exception.GetType().Name + ": " + exception.Message, exception));
                }
            }
            else
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.IsAbstract || !typeof(MelonBase).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    try
                    {
                        MelonBase melon = (MelonBase)Activator.CreateInstance(type, true);
                        melon.SetAssembly(assembly, Hash);
                        melon.MelonAssemblyInternal = this;
                        melon.ApplyMetadataDefaults();
                        resolved.Add(melon);
                    }
                    catch (Exception exception)
                    {
                        AddRottenMelon(new RottenMelon(type, "The constructor of the melon threw " + exception.GetType().Name + ": " + exception.Message, exception));
                    }
                }
            }

            foreach (MelonBase melon in resolved)
            {
                AddMelon(melon);
                if (!melon.Register())
                {
                    AddRottenMelon(new RottenMelon(this, "Melon '" + melon.MelonTypeName + "' was refused during registration."));
                }
            }
        }

        internal static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
            catch (Exception)
            {
                return new Type[0];
            }
        }

        internal void AddMelon(MelonBase melon)
        {
            if (melon == null || _loadedMelons.Contains(melon))
            {
                return;
            }

            _loadedMelons.Add(melon);
        }

        internal void RemoveMelon(MelonBase melon)
        {
            _loadedMelons.Remove(melon);
        }

        internal void AddRottenMelon(RottenMelon melon)
        {
            if (melon != null && !_rottenMelons.Contains(melon))
            {
                _rottenMelons.Add(melon);
            }
        }

        internal static void AddRegistered(MelonBase melon)
        {
            if (melon != null && !RegisteredMelonsInternal.Contains(melon))
            {
                RegisteredMelonsInternal.Add(melon);
            }
        }

        internal static void RemoveRegistered(MelonBase melon)
        {
            RegisteredMelonsInternal.Remove(melon);
        }

        internal static void AddAssembly(MelonAssembly assembly)
        {
            if (assembly != null && !LoadedAssembliesInternal.Contains(assembly))
            {
                LoadedAssembliesInternal.Add(assembly);
            }
        }

        internal static void Reset()
        {
            LoadedAssembliesInternal.Clear();
            RegisteredMelonsInternal.Clear();
        }
    }
}
