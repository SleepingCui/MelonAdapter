using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MelonLoader.Utils;
using System.Reflection;

namespace MelonLoader
{
    /// <summary>
    /// Base type shared by every MelonLoader mod. MelonAdapter.Runtime instantiates a subclass once per
    /// loaded mod assembly, validates its metadata and then drives the virtual callbacks below.
    /// </summary>
    public abstract class MelonBase
    {
        public enum Incompatibility
        {
            MLVersion,
            MLBuild,
            Game,
            GameVersion,
            ProcessName,
            Domain,
            Platform
        }

        public static MelonEvent OnRegister = new MelonEvent();
        public static MelonEvent OnUnregister = new MelonEvent();
        public static MelonEvent<MelonBase> OnMelonRegistered = new MelonEvent<MelonBase>();
        public static MelonEvent<MelonBase> OnMelonUnregistered = new MelonEvent<MelonBase>();
        public static MelonEvent<MelonBase> OnMelonInitializing = new MelonEvent<MelonBase>();

        internal MelonAssembly MelonAssemblyInternal;
        internal string IdInternal;
        internal string HashInternal;
        internal bool RegisteredInternal;
        internal int PriorityInternal = 0;
        internal Logging.ColorARGB ConsoleColorInternal = MelonLogger.DefaultMelonColor;
        internal Logging.ColorARGB AuthorConsoleColorInternal = MelonLogger.DefaultMelonColor;
        internal MelonLogger.Instance LoggerInternal;
        internal Assembly AssemblyInternal;
        internal MelonInfoAttribute MelonInfoInternal;
        internal MelonGameAttribute[] MelonGamesInternal;
        internal MelonProcessAttribute[] MelonProcessesInternal;

        /// <summary>Every melon registered in this process, in registration order.</summary>
        public static ReadOnlyCollection<MelonBase> RegisteredMelons => MelonAssembly.RegisteredMelonsView;

        public MelonAssembly MelonAssembly => MelonAssemblyInternal;

        public Assembly Assembly => AssemblyInternal ?? GetType().Assembly;

        public virtual string Location => Assembly?.Location ?? string.Empty;

        public string Hash => HashInternal ?? string.Empty;

        public string ID
        {
            get
            {
                if (!string.IsNullOrEmpty(IdInternal))
                {
                    return IdInternal;
                }

                MelonIDAttribute idAttribute = GetType().GetCustomAttributes(typeof(MelonIDAttribute), true).OfType<MelonIDAttribute>().FirstOrDefault();
                if (idAttribute != null)
                {
                    return Convert.ToString(idAttribute.ID);
                }

                return Info?.Name ?? MelonTypeName;
            }
            internal set { IdInternal = value; }
        }

        public bool Registered => RegisteredInternal;

        public string MelonTypeName => GetType().FullName;

        public int Priority
        {
            get { return PriorityInternal; }
            set { PriorityInternal = value; }
        }

        public Logging.ColorARGB ConsoleColor
        {
            get { return ConsoleColorInternal; }
            set { ConsoleColorInternal = value; }
        }

        public Logging.ColorARGB AuthorConsoleColor
        {
            get { return AuthorConsoleColorInternal; }
            set { AuthorConsoleColorInternal = value; }
        }

        public MelonInfoAttribute Info => MelonInfoInternal ?? FindAttribute<MelonInfoAttribute>();

        public MelonAdditionalCreditsAttribute AdditionalCredits => FindAttribute<MelonAdditionalCreditsAttribute>();

        public MelonProcessAttribute[] SupportedProcesses => MelonProcessesInternal ?? FindAttributes<MelonProcessAttribute>();

        public MelonGameAttribute[] Games => MelonGamesInternal ?? FindAttributes<MelonGameAttribute>();

        public MelonGameVersionAttribute[] SupportedGameVersions => FindAttributes<MelonGameVersionAttribute>();

        public MelonOptionalDependenciesAttribute OptionalDependencies => FindAttribute<MelonOptionalDependenciesAttribute>();

        public MelonPlatformAttribute SupportedPlatforms => FindAttribute<MelonPlatformAttribute>();

        public MelonPlatformDomainAttribute SupportedDomain => FindAttribute<MelonPlatformDomainAttribute>();

        public VerifyLoaderVersionAttribute SupportedMLVersion => FindAttribute<VerifyLoaderVersionAttribute>();

        public VerifyLoaderBuildAttribute SupportedMLBuild => FindAttribute<VerifyLoaderBuildAttribute>();

        /// <summary>
        /// MelonLoader 0.5 moved the loader attributes from the assembly onto the melon class.
        /// Mods written for 0.3/0.4 still annotate the assembly, and MelonLoader keeps accepting them,
        /// so the type is inspected first and the assembly is the fallback.
        /// </summary>
        private T FindAttribute<T>() where T : Attribute
        {
            T attribute = GetType().GetCustomAttributes(typeof(T), true).OfType<T>().FirstOrDefault();
            if (attribute != null)
            {
                return attribute;
            }

            Assembly assembly = GetType().Assembly;
            return assembly == null ? null : assembly.GetCustomAttributes(typeof(T), false).OfType<T>().FirstOrDefault();
        }

        private T[] FindAttributes<T>() where T : Attribute
        {
            T[] attributes = GetType().GetCustomAttributes(typeof(T), true).OfType<T>().ToArray();
            if (attributes.Length > 0)
            {
                return attributes;
            }

            Assembly assembly = GetType().Assembly;
            return assembly == null ? new T[0] : assembly.GetCustomAttributes(typeof(T), false).OfType<T>().ToArray();
        }

        public bool HarmonyDontPatchAll =>
            GetType().GetCustomAttributes(typeof(MelonLoader.HarmonyDontPatchAllAttribute), true).Length > 0;

        public MelonLogger.Instance LoggerInstance
        {
            get
            {
                if (LoggerInternal == null)
                {
                    LoggerInternal = new MelonLogger.Instance(Info?.Name ?? MelonTypeName, ConsoleColorInternal);
                }

                return LoggerInternal;
            }
        }

        public HarmonyLib.Harmony HarmonyInstance
        {
            get
            {
                IAdapterHarmonyProvider provider = AdapterBridge.HarmonyProvider;
                if (provider == null)
                {
                    MelonLogger.Publish(AdapterLogLevel.Error, AdapterBridge.DescribeMissingService("harmony provider") + " Requested by melon '" + (Info?.Name ?? MelonTypeName) + "'.", null, null);
                    return null;
                }

                return provider.GetInstance(ID);
            }
        }

        public virtual void OnEarlyInitializeMelon()
        {
        }

        public virtual void OnInitializeMelon()
        {
        }

        public virtual void OnLateInitializeMelon()
        {
        }

        public virtual void OnApplicationEarlyStart()
        {
        }

        public virtual void OnApplicationStart()
        {
        }

        public virtual void OnApplicationLateStart()
        {
        }

        public virtual void OnApplicationQuit()
        {
        }

        public virtual void OnApplicationDefiniteQuit()
        {
        }

        public virtual void OnUpdate()
        {
        }

        public virtual void OnFixedUpdate()
        {
        }

        public virtual void OnLateUpdate()
        {
        }

        public virtual void OnGUI()
        {
        }

        public virtual void OnDeinitializeMelon()
        {
        }

        public virtual void OnModSettingsApplied()
        {
        }

        public virtual void OnPreferencesLoaded()
        {
        }

        public virtual void OnPreferencesLoaded(string filePath)
        {
        }

        public virtual void OnPreferencesSaved()
        {
        }

        public virtual void OnPreferencesSaved(string filePath)
        {
        }

        public virtual void OnPreSupportModule()
        {
        }

        /// <summary>
        /// Wraps an existing melon type with metadata supplied at call time instead of attributes.
        /// MelonAdapter has no runtime registration flow, so the wrapper is created but not registered.
        /// </summary>
        public static T CreateWrapper<T>(string name, string author, string version, MelonGameAttribute[] games = null, MelonProcessAttribute[] processes = null, int priority = 0, Logging.ColorARGB? color = null, Logging.ColorARGB? authorColor = null, string id = null) where T : MelonBase
        {
            T wrapper = (T)Activator.CreateInstance(typeof(T), true);
            wrapper.IdInternal = id;
            wrapper.PriorityInternal = priority;
            wrapper.MelonInfoInternal = new MelonInfoAttribute(typeof(T), name, version, author);
            if (games != null)
            {
                wrapper.MelonGamesInternal = games;
            }

            if (processes != null)
            {
                wrapper.MelonProcessesInternal = processes;
            }

            if (color.HasValue)
            {
                wrapper.ConsoleColorInternal = color.Value;
            }

            if (authorColor.HasValue)
            {
                wrapper.AuthorConsoleColorInternal = authorColor.Value;
            }

            return wrapper;
        }

        public static MelonBase FindMelon(string melonName, string melonAuthor)
        {
            return RegisteredMelons.FirstOrDefault(m =>
            {
                bool nameMatches = string.Equals(m.ID, melonName, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(m.Info?.Name, melonName, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(m.MelonTypeName, melonName, StringComparison.OrdinalIgnoreCase);
                if (!nameMatches)
                {
                    return false;
                }

                return string.IsNullOrEmpty(melonAuthor) || string.Equals(m.Info?.Author, melonAuthor, StringComparison.OrdinalIgnoreCase);
            });
        }

        public static void ExecuteAll(LemonAction<MelonBase> func, bool unregisterOnFail = false, string unregistrationReason = null)
        {
            ExecuteList(func, RegisteredMelons.ToList(), unregisterOnFail, unregistrationReason);
        }

        public static void ExecuteList<T>(LemonAction<T> func, List<T> melons, bool unregisterOnFail = false, string unregistrationReason = null)
        {
            if (func == null || melons == null)
            {
                return;
            }

            foreach (T melon in melons.ToArray())
            {
                try
                {
                    func(melon);
                }
                catch (Exception exception)
                {
                    MelonBase target = melon as MelonBase;
                    string label = target != null ? (target.Info?.Name ?? target.MelonTypeName) : Convert.ToString(melon);
                    MelonLogger.Publish(AdapterLogLevel.Error, "Exception while executing an action for melon '" + label + "': " + exception.Message, null, exception);
                    if (unregisterOnFail && target != null && target.Registered)
                    {
                        target.Unregister(string.IsNullOrEmpty(unregistrationReason) ? "an exception was thrown while executing a melon action" : unregistrationReason, true);
                    }
                }
            }
        }

        public static void SendMessageAll(string name, object[] arguments)
        {
            foreach (MelonBase melon in RegisteredMelons.ToArray())
            {
                try
                {
                    melon.SendMessage(name, arguments);
                }
                catch (Exception exception)
                {
                    MelonLogger.Publish(AdapterLogLevel.Error, "Exception while sending message '" + name + "' to melon '" + (melon.Info?.Name ?? melon.MelonTypeName) + "': " + exception.Message, null, exception);
                }
            }
        }

        /// <summary>Registers every melon, highest priority first (the MelonLoader default order).</summary>
        public static void RegisterSorted<T>(IEnumerable<T> melons) where T : MelonBase
        {
            if (melons == null)
            {
                return;
            }

            foreach (T melon in melons.OrderByDescending(m => m.Priority).ThenBy(m => m.Info?.Name, StringComparer.OrdinalIgnoreCase).ToList())
            {
                melon.Register();
            }
        }

        public static void PrintIncompatibilities(Incompatibility[] incompatibilities, MelonBase melon)
        {
            if (incompatibilities == null || incompatibilities.Length == 0)
            {
                return;
            }

            string name = melon != null ? (melon.Info?.Name ?? melon.MelonTypeName) : "<unknown>";
            foreach (Incompatibility incompatibility in incompatibilities)
            {
                MelonLogger.Publish(AdapterLogLevel.Warning, "Melon '" + name + "' is incompatible: " + incompatibility + ".", null, null);
            }
        }

        /// <summary>
        /// Reports every way this melon does not match the supplied host description. MelonAdapter's own
        /// loader performs the authoritative check; this helper exists because mods call it.
        /// </summary>
        public Incompatibility[] FindIncompatiblities(MelonGameAttribute game, string processName, string gameVersion, string mlVersion, string mlBuildHashCode, MelonPlatformAttribute.CompatiblePlatforms platform, MelonPlatformDomainAttribute.CompatibleDomains domain)
        {
            List<Incompatibility> found = new List<Incompatibility>();

            MelonGameAttribute[] ownGames = Games ?? new MelonGameAttribute[0];
            if (game != null && ownGames.Length > 0 && !ownGames.Any(own => own.IsCompatible(game) || game.IsCompatible(own)))
            {
                found.Add(Incompatibility.Game);
            }

            MelonGameVersionAttribute[] versions = SupportedGameVersions ?? new MelonGameVersionAttribute[0];
            if (gameVersion != null && versions.Length > 0 && !versions.Any(v => v.Universal || string.Equals(v.Version, gameVersion, StringComparison.OrdinalIgnoreCase)))
            {
                found.Add(Incompatibility.GameVersion);
            }

            MelonProcessAttribute[] processes = SupportedProcesses ?? new MelonProcessAttribute[0];
            if (!string.IsNullOrEmpty(processName) && processes.Length > 0 && !processes.Any(p => p.Universal || string.Equals(p.EXE_Name, processName, StringComparison.OrdinalIgnoreCase)))
            {
                found.Add(Incompatibility.ProcessName);
            }

            VerifyLoaderVersionAttribute supportedVersion = SupportedMLVersion;
            if (supportedVersion != null && !VersionMatches(supportedVersion, mlVersion))
            {
                found.Add(Incompatibility.MLVersion);
            }

            VerifyLoaderBuildAttribute supportedBuild = SupportedMLBuild;
            if (supportedBuild != null && !string.IsNullOrEmpty(supportedBuild.HashCode) && !string.IsNullOrEmpty(mlBuildHashCode)
                && !string.Equals(supportedBuild.HashCode, mlBuildHashCode, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(Incompatibility.MLBuild);
            }

            MelonPlatformAttribute supportedPlatforms = SupportedPlatforms;
            if (supportedPlatforms != null && supportedPlatforms.Platforms != null && supportedPlatforms.Platforms.Length > 0
                && !supportedPlatforms.Platforms.Contains(MelonPlatformAttribute.CompatiblePlatforms.UNIVERSAL)
                && !supportedPlatforms.Platforms.Contains(platform))
            {
                found.Add(Incompatibility.Platform);
            }

            MelonPlatformDomainAttribute supportedDomain = SupportedDomain;
            if (supportedDomain != null && supportedDomain.Domain != MelonPlatformDomainAttribute.CompatibleDomains.UNIVERSAL && supportedDomain.Domain != domain)
            {
                found.Add(Incompatibility.Domain);
            }

            return found.ToArray();
        }

        /// <summary>Runs <see cref="FindIncompatiblities(MelonGameAttribute, string, string, string, string, MelonPlatformAttribute.CompatiblePlatforms, MelonPlatformDomainAttribute.CompatibleDomains)"/> against the current process.</summary>
        public Incompatibility[] FindIncompatiblitiesFromContext()
        {
            Version adapterVersion = typeof(MelonBase).Assembly.GetName().Version;
            return FindIncompatiblities(
                MelonUtils.CurrentGameAttribute,
                MelonEnvironment.GameExecutableName,
                MelonUtils.GameVersion,
                adapterVersion == null ? "0.0.0.0" : adapterVersion.ToString(),
                null,
                MelonUtils.CurrentPlatform,
                MelonUtils.CurrentDomain);
        }

        private static bool VersionMatches(VerifyLoaderVersionAttribute supported, string mlVersion)
        {
            Version hostVersion;
            if (string.IsNullOrEmpty(mlVersion) || !Version.TryParse(mlVersion, out hostVersion))
            {
                return true;
            }

            if (supported.IsMinimum)
            {
                return hostVersion.Major > supported.Major
                       || (hostVersion.Major == supported.Major && (hostVersion.Minor > supported.Minor
                           || (hostVersion.Minor == supported.Minor && (hostVersion.Build < 0 ? 0 : hostVersion.Build) >= supported.Patch)));
            }

            return hostVersion.Major == supported.Major
                   && hostVersion.Minor == supported.Minor
                   && (hostVersion.Build < 0 ? 0 : hostVersion.Build) == supported.Patch;
        }

        public bool Register()
        {
            if (RegisteredInternal)
            {
                return true;
            }

            if (Info == null)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "Cannot register melon '" + MelonTypeName + "' because it has no MelonInfoAttribute.", null, null);
                return false;
            }

            if (MelonAssemblyInternal == null)
            {
                // Mods normally get their assembly assigned by the loader; a wrapper created by mod
                // code only knows its own type, so fall back to the assembly it was discovered in.
                MelonAssemblyInternal = MelonAssembly.GetMelonAssemblyOfMember(GetType(), null);
            }

            if (MelonAssemblyInternal == null)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "Cannot register melon '" + Info.Name + "' because it does not belong to a MelonAssembly.", null, null);
                return false;
            }

            RegisteredInternal = true;
            MelonAssemblyInternal.AddMelon(this);
            MelonAssembly.AddRegistered(this);
            OnRegisteredInternals();

            try
            {
                OnRegister?.Invoke();
                OnMelonRegistered.Invoke(this);
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "Exception while notifying registration of melon '" + Info.Name + "': " + exception.Message, null, exception);
            }

            AdapterBridge.Registry?.NotifyRegistered(this);

            if (MelonDebug.IsEnabled())
            {
                MelonLogger.Publish(AdapterLogLevel.Debug, "Registered melon '" + Info.Name + "' v" + Info.Version + " (priority " + Priority + ", id '" + ID + "').", null, null);
            }

            return true;
        }

        public void Unregister(string reason = null, bool silent = false)
        {
            if (!RegisteredInternal)
            {
                return;
            }

            RegisteredInternal = false;
            MelonAssemblyInternal?.RemoveMelon(this);
            MelonAssembly.RemoveRegistered(this);
            OnUnregisteredInternals();

            try
            {
                OnDeinitializeMelon();
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "Exception while deinitializing melon '" + (Info?.Name ?? MelonTypeName) + "': " + exception.Message, null, exception);
            }

            try
            {
                AdapterBridge.HarmonyProvider?.UnpatchOwner(ID);
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "Exception while unpatching harmony owners of melon '" + (Info?.Name ?? MelonTypeName) + "': " + exception.Message, null, exception);
            }

            try
            {
                OnUnregister?.Invoke();
                OnMelonUnregistered.Invoke(this);
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "Exception while notifying unregistration of melon '" + (Info?.Name ?? MelonTypeName) + "': " + exception.Message, null, exception);
            }

            AdapterBridge.Registry?.NotifyUnregistered(this);

            if (!silent)
            {
                MelonLogger.Publish(AdapterLogLevel.Warning, "Unregistered melon '" + (Info?.Name ?? MelonTypeName) + "'" + (string.IsNullOrEmpty(reason) ? "." : " (" + reason + ")."), null, null);
            }
        }

        public object SendMessage(string name, params object[] arguments)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            MethodInfo method = GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, arguments?.Select(a => a?.GetType()).ToArray() ?? Type.EmptyTypes, null);
            if (method == null)
            {
                return null;
            }

            return method.Invoke(method.IsStatic ? null : this, arguments);
        }

        internal virtual void OnRegisteredInternals()
        {
        }

        internal virtual void OnUnregisteredInternals()
        {
        }

        internal void SetAssembly(Assembly assembly, string hash = null)
        {
            AssemblyInternal = assembly;
            if (string.IsNullOrEmpty(HashInternal))
            {
                // The host already hashed the file on disk; that works even when the assembly was loaded
                // from memory (no Assembly.Location) and is the value MelonAssembly reports.
                HashInternal = !string.IsNullOrEmpty(hash) ? hash : MelonUtils.ComputeHash(assembly);
            }
        }

        internal void ApplyMetadataDefaults()
        {
            MelonColorAttribute color = GetType().GetCustomAttributes(typeof(MelonAuthorColorAttribute), true).OfType<MelonAuthorColorAttribute>().FirstOrDefault()
                                       ?? GetType().GetCustomAttributes(typeof(MelonColorAttribute), true).OfType<MelonColorAttribute>().FirstOrDefault();
            if (color != null)
            {
                ConsoleColorInternal = color.DrawingColor;
                AuthorConsoleColorInternal = color.DrawingColor;
            }

            MelonPriorityAttribute priority = GetType().GetCustomAttributes(typeof(MelonPriorityAttribute), true).OfType<MelonPriorityAttribute>().FirstOrDefault();
            if (priority != null)
            {
                PriorityInternal = priority.Priority;
            }
        }
    }
}