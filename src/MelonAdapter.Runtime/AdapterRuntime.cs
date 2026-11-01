using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonAdapter.Runtime.Diagnostics;
using MelonAdapter.Runtime.Localization;
using MelonAdapter.Runtime.Mods;
using MelonAdapter.Runtime.Preferences;
using MelonLoader;

namespace MelonAdapter.Runtime
{
    /// <summary>
    /// The MelonLoader emulation runtime. It owns discovery, loading, the lifecycle dispatcher,
    /// preferences, coroutines, Harmony ownership and diagnostics. It has no UnityEngine dependency,
    /// so it can also run head-less in the self-test harness.
    /// </summary>
    public sealed class AdapterRuntime : IAdapterMelonRegistry, IDisposable
    {
        private readonly AdapterConfig _config;
        private readonly AdapterPaths _paths;
        private readonly AdapterEnvironment _environment;
        private readonly AdapterLog _log;
        private readonly PreferencesStorage _preferences;
        private readonly HarmonyProvider _harmony;
        private readonly CoroutinePump _coroutines;
        private readonly ModAssemblyResolver _resolver;
        private readonly ModLoader _loader;
        private readonly EventSubscriptionTracker _events;
        private readonly CompatibilityReport _report;

        private LocalizationCatalog _language;

        private readonly List<ModRecord> _mods = new List<ModRecord>();
        private readonly List<ModRecord> _loadOrder = new List<ModRecord>();
        private readonly Dictionary<ModRecord, Dictionary<string, int>> _consecutiveExceptions = new Dictionary<ModRecord, Dictionary<string, int>>();
        private readonly Dictionary<Assembly, ModRecord> _assemblyOwners = new Dictionary<Assembly, ModRecord>();

        private bool _initialized;
        private bool _applicationStarted;
        private bool _enabled;
        private bool _shuttingDown;
        private bool _disposed;

        public AdapterRuntime(string gameDirectory, string adapterDirectory, AdapterConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _paths = new AdapterPaths(gameDirectory, adapterDirectory, config);
            _paths.EnsureDirectories();

            _environment = new AdapterEnvironment
            {
                GameDirectory = _paths.GameDirectory,
                MelonLoaderDirectory = _paths.AdapterDirectory,
                UserDataDirectory = _paths.UserDataDirectory,
                GameDeveloper = "unknown",
                GameName = "unknown",
                GameVersion = "unknown",
                UnityVersion = "unknown",
                AdapterVersion = typeof(AdapterRuntime).Assembly.GetName().Version.ToString(),
                ApiBaselineVersion = config.ApiBaseline,
                DebugLogging = config.DebugLogging
            };

            _log = new AdapterLog(_paths.LogPath, config.DebugLogging);

            string languageError;
            _language = LocalizationCatalog.Load(_paths.LanguagePath, config.Language, out languageError);
            LanguageError = languageError;
            _log.Write(
                "language",
                languageError == null ? AdapterLogLevel.Info : AdapterLogLevel.Warning,
                "Interface language '" + _language.Language + "' from " + _language.SourceName +
                (languageError == null ? "." : ". " + languageError));

            string preferencesPath = PreferencesStorage.GetDefaultPath(_paths);
            string legacyPreferencesPath = Path.Combine(_paths.GameDirectory, "UserData", PreferencesStorage.LegacyFileName);
            if (config.MigrateFromMelonLoader)
            {
                string backupPath;
                string migrationError;
                if (PreferencesStorage.TryMigrateLegacyFile(preferencesPath, legacyPreferencesPath, out backupPath, out migrationError))
                {
                    PreferencesOrigin = "imported from '" + legacyPreferencesPath + "' (backup: '" + backupPath + "')";
                    _log.Write("preferences", AdapterLogLevel.Info, "Imported MelonLoader preferences. " + PreferencesOrigin + ". The MelonLoader file was left untouched.");
                }
                else if (migrationError != null)
                {
                    PreferencesOrigin = "adapter file (MelonLoader import failed: " + migrationError + ")";
                    _log.Write("preferences", AdapterLogLevel.Warning, "MelonLoader preferences were not imported: " + migrationError);
                }
                else
                {
                    PreferencesOrigin = File.Exists(preferencesPath) ? "adapter file" : "new file";
                }
            }
            else
            {
                PreferencesOrigin = "adapter file (migration disabled)";
            }

            _preferences = new PreferencesStorage(preferencesPath);
            _harmony = new HarmonyProvider(_log);
            _coroutines = new CoroutinePump(_log);
            _resolver = new ModAssemblyResolver(_paths, _log, config.ResolverDiagnostics);
            _loader = new ModLoader(_log, config);
            _events = new EventSubscriptionTracker(_log);
            _report = new CompatibilityReport(_environment, config, _harmony);
            _enabled = config.Enabled;
        }

        public AdapterConfig Config => _config;
        public AdapterPaths Paths => _paths;
        public AdapterEnvironment Environment => _environment;
        public AdapterLog Log => _log;
        public HarmonyProvider Harmony => _harmony;
        public CoroutinePump Coroutines => _coroutines;
        public ModAssemblyResolver Resolver => _resolver;
        public IReadOnlyList<ModRecord> Mods => _mods;
        public IReadOnlyList<ModRecord> LoadOrder => _loadOrder;
        /// <summary>Where the preferences file came from, for the compatibility report.</summary>
        public string PreferencesOrigin { get; private set; }

        /// <summary>Interface language catalog used by the settings page (see <c>lang.json</c>).</summary>
        public LocalizationCatalog Language => _language;

        /// <summary>Set when <c>lang.json</c> exists but could not be used; the built-in copy is active.</summary>
        public string LanguageError { get; private set; }

        /// <summary>
        /// Re-reads <c>lang.json</c> without restarting the game and keeps the current language when the new
        /// file still provides it. Returns false and reports the reason when the file could not be used.
        /// </summary>
        public bool ReloadLanguage(out string error)
        {
            LocalizationCatalog reloaded = LocalizationCatalog.Load(_paths.LanguagePath, _language.Language, out error);
            _language = reloaded;
            LanguageError = error;
            _log.Write(
                "language",
                error == null ? AdapterLogLevel.Info : AdapterLogLevel.Warning,
                "Reloaded the language file: '" + reloaded.Language + "' from " + reloaded.SourceName +
                (error == null ? "." : ". " + error));
            return error == null;
        }

        public bool IsEnabled => _enabled;
        public bool IsInitialized => _initialized;
        public bool IsApplicationStarted => _applicationStarted;

        public int ActiveModCount => _loadOrder.Count(m => m.State == ModState.Loaded);

        public string StatusSummary
        {
            get
            {
                int total = _loadOrder.Count;
                int active = ActiveModCount;
                int rejected = _loadOrder.Count(m => m.State == ModState.Rejected || m.State == ModState.Failed);
                string state = _enabled ? "enabled" : "disabled (restart required)";
                return active + "/" + total + " mods active, " + rejected + " rejected/failed, adapter " + state;
            }
        }

        /// <summary>
        /// Disables one loaded mod immediately: its Harmony patches are removed, its event
        /// subscriptions are dropped, its melons are unregistered and no further callbacks are
        /// dispatched to it. The assembly stays loaded (the runtime cannot unload it), so a restart is
        /// required before the mod can be loaded again. The requested change is not persisted here;
        /// callers that want it to survive a restart must also add the mod id to
        /// <see cref="AdapterConfig.DisabledMods"/> and save the configuration.
        /// </summary>
        /// <param name="modId">Mod id (MelonInfo name) or display name, matched case-insensitively.</param>
        /// <param name="reason">Reason recorded in the log and the compatibility report.</param>
        /// <returns>True when a loaded mod was disabled by this call.</returns>
        public bool DisableMod(string modId, string reason)
        {
            if (string.IsNullOrEmpty(modId))
            {
                return false;
            }

            ModRecord record = _loadOrder.FirstOrDefault(m =>
                string.Equals(m.ModId, modId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.DisplayName, modId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.AssemblySimpleName, modId, StringComparison.OrdinalIgnoreCase));

            if (record == null || record.State != ModState.Loaded)
            {
                return false;
            }

            DisableRecord(record, string.IsNullOrEmpty(reason) ? "Disabled manually from the adapter settings UI." : reason);
            return true;
        }

        // ---------------------------------------------------------------- startup

        public void Initialize()
        {
            if (_initialized)
            {
                throw new InvalidOperationException("AdapterRuntime.Initialize was called twice.");
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AdapterRuntime));
            }

            GameIdentity.ApplyDefaults(_environment);

            AttachBridge();
            LogBanner();

            foreach (string warning in _config.Warnings)
            {
                _log.Write("config", AdapterLogLevel.Warning, warning);
            }

            if (!_enabled)
            {
                _log.Write("runtime", AdapterLogLevel.Warning, "The adapter is disabled in Config.toml; MelonLoader mods will not be loaded. Set 'enabled = true' and restart the game.");
                _initialized = true;
                WriteReport();
                return;
            }

            _resolver.Install();

            ModScanner scanner = new ModScanner(_log, _environment, _config, _resolver);
            _mods.AddRange(scanner.Scan(_paths.ModsDirectory));

            foreach (ModRecord dependency in scanner.DependencyAssemblies)
            {
                _resolver.RegisterDependency(dependency);
            }

            DependencyGraph.Result graph = DependencyGraph.Sort(_mods, scanner.DependencyAssemblies);
            _loadOrder.AddRange(graph.Order);

            foreach (ModRecord missing in graph.MissingDependencies)
            {
                missing.State = ModState.Rejected;
                _log.Write("graph", AdapterLogLevel.Error, "Rejected '" + missing.DisplayName + "': " + missing.FailureReason);
            }

            foreach (ModRecord cycle in graph.Cycles)
            {
                cycle.State = ModState.Rejected;
                _log.Write("graph", AdapterLogLevel.Error, "Rejected '" + cycle.DisplayName + "': " + cycle.FailureReason);
            }

            RejectDuplicateIds();

            _loader.LoadAll(_loadOrder, _resolver, _harmony);

            foreach (ModRecord record in _loadOrder)
            {
                if (record.Assembly != null)
                {
                    _assemblyOwners[record.Assembly] = record;
                }

                CreateModUserDataDirectories(record);
            }

            // Mods may already subscribe during their constructors, so the pre-initialization
            // notifications fire after discovery but before the mod initialization callbacks.
            InvokeEvent("OnPreInitialization", MelonEvents.OnPreInitialization);
            InvokeEvent("OnPreSupportModule", MelonEvents.OnPreSupportModule);

            DispatchAll("OnEarlyInitializeMelon", melon => melon.OnEarlyInitializeMelon());
            DispatchAll("OnInitializeMelon", melon => melon.OnInitializeMelon());
            DispatchAll("OnLateInitializeMelon", melon => melon.OnLateInitializeMelon());

            InvokeEvent("OnPreModsLoaded", MelonEvents.OnPreModsLoaded);

            _initialized = true;
            _log.Write("runtime", AdapterLogLevel.Info, "Initialization complete: " + StatusSummary);
            WriteReport();
        }

        private void AttachBridge()
        {
            AdapterBridge.LogSink = _log;
            AdapterBridge.Context = _environment;
            AdapterBridge.PreferencesStorage = _preferences;
            AdapterBridge.CoroutineRunner = _coroutines;
            AdapterBridge.HarmonyProvider = _harmony;
            AdapterBridge.Registry = this;
        }

        private void LogBanner()
        {
            _log.Write("banner", AdapterLogLevel.Info, "MelonAdapter " + _environment.AdapterVersion + " (MelonLoader API baseline " + _config.ApiBaseline + ")");
            _log.Write("banner", AdapterLogLevel.Info, "Game: " + _environment.GameName + " | Unity: " + _environment.UnityVersion + " | Process: " + _environment.GameExecutableName + " | Mono: " + (MelonUtils.IsOldMono() ? "old" : "current"));
            _log.Write("banner", AdapterLogLevel.Info, "Host: " + _environment.HostName + " " + _environment.HostVersion);
            _log.Write("banner", AdapterLogLevel.Info, "Harmony: " + _harmony.ProviderVersion + " (" + _harmony.ProviderAssemblyLocation + ")");
            _log.Write("banner", AdapterLogLevel.Info, "Mods directory: " + _paths.ModsDirectory);
            _log.Write("banner", AdapterLogLevel.Info, "User data directory: " + _paths.UserDataDirectory);
            _log.Write("banner", AdapterLogLevel.Info, "Config: " + _paths.ConfigPath);
        }

        private void RejectDuplicateIds()
        {
            Dictionary<string, ModRecord> seen = new Dictionary<string, ModRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (ModRecord record in _loadOrder)
            {
                if (record.State == ModState.Rejected)
                {
                    continue;
                }

                if (seen.TryGetValue(record.ModId, out ModRecord first))
                {
                    record.State = ModState.Rejected;
                    record.FailureReason = "Duplicate mod id '" + record.ModId + "': already provided by " + first.FileName + ".";
                    _log.Write("loader", AdapterLogLevel.Error, "Rejected '" + record.FileName + "': " + record.FailureReason);
                }
                else
                {
                    seen[record.ModId] = record;
                }
            }
        }

        /// <summary>
        /// Every melon gets <c>UserData/&lt;melon id&gt;/</c> before its first callback, so mods that keep
        /// their own files there never have to create the tree themselves.
        /// </summary>
        private void CreateModUserDataDirectories(ModRecord record)
        {
            foreach (MelonBase melon in record.Melons)
            {
                try
                {
                    string directory = _paths.GetModUserDataDirectory(melon.ID);
                    Directory.CreateDirectory(directory);
                    _log.Write("lifecycle", AdapterLogLevel.Debug, "User data directory for '" + melon.ID + "': " + directory);
                }
                catch (Exception exception)
                {
                    _log.Write("lifecycle", AdapterLogLevel.Warning, "Cannot create the user data directory for '" + melon.ID + "': " + exception.Message, exception);
                }
            }
        }

        // ---------------------------------------------------------------- callbacks

        private void DispatchAll(string callback, Action<MelonBase> action)
        {
            foreach (ModRecord record in _loadOrder)
            {
                if (record.State != ModState.Loaded)
                {
                    continue;
                }

                Dispatch(record, callback, action);
            }
        }

        private void Dispatch(ModRecord record, string callback, Action<MelonBase> action)
        {
            bool threw = false;
            foreach (MelonBase melon in record.Melons.ToArray())
            {
                if (record.State != ModState.Loaded)
                {
                    return;
                }

                try
                {
                    action(melon);
                }
                catch (Exception exception)
                {
                    threw = true;
                    HandleCallbackException(record, callback, melon, exception);
                }
            }

            if (!threw)
            {
                ResetFailureCount(record, callback);
            }
        }

        /// <summary>
        /// Counts consecutive exceptions per callback: a mod that only breaks in <c>OnUpdate</c> must
        /// still reach the limit even though every other callback succeeds each frame.
        /// </summary>
        private int IncrementFailureCount(ModRecord record, string callback)
        {
            Dictionary<string, int> perCallback;
            if (!_consecutiveExceptions.TryGetValue(record, out perCallback))
            {
                perCallback = new Dictionary<string, int>(StringComparer.Ordinal);
                _consecutiveExceptions[record] = perCallback;
            }

            int count;
            perCallback.TryGetValue(callback, out count);
            count++;
            perCallback[callback] = count;
            return count;
        }

        private void ResetFailureCount(ModRecord record, string callback)
        {
            Dictionary<string, int> perCallback;
            if (!_consecutiveExceptions.TryGetValue(record, out perCallback))
            {
                return;
            }

            perCallback.Remove(callback);
            if (perCallback.Count == 0)
            {
                _consecutiveExceptions.Remove(record);
            }
        }

        private void HandleCallbackException(ModRecord record, string callback, MelonBase melon, Exception exception)
        {
            int count = IncrementFailureCount(record, callback);
            record.ConsecutiveFailures.Add(callback + ": " + exception.GetType().Name + ": " + exception.Message);
            if (record.ConsecutiveFailures.Count > 20)
            {
                record.ConsecutiveFailures.RemoveAt(0);
            }

            _log.Write("callbacks", AdapterLogLevel.Error, "'" + record.DisplayName + "' threw in " + callback + " (" + melon.GetType().FullName + ", " + count + "/" + _config.MaxConsecutiveExceptions + "): " + exception, exception);

            if (_config.MaxConsecutiveExceptions > 0 && count >= _config.MaxConsecutiveExceptions)
            {
                DisableRecord(record, "Disabled after " + count + " consecutive callback exceptions (last in " + callback + ").");
            }
        }

        private void DisableRecord(ModRecord record, string reason)
        {
            if (record.State == ModState.Disabled)
            {
                return;
            }

            record.State = ModState.Disabled;
            record.FailureReason = reason;
            _log.Write("lifecycle", AdapterLogLevel.Error, "'" + record.DisplayName + "' " + reason);

            try
            {
                if (record.Assembly != null)
                {
                    _events.UnsubscribeAssembly(record.Assembly);
                }

                _harmony.UnpatchOwner(record.ModId);
            }
            catch (Exception exception)
            {
                _log.Write("lifecycle", AdapterLogLevel.Warning, "Cleanup for '" + record.DisplayName + "' failed: " + exception.Message, exception);
            }

            foreach (MelonBase melon in record.Melons.ToArray())
            {
                try
                {
                    melon.Unregister(reason, true);
                }
                catch (Exception exception)
                {
                    _log.Write("lifecycle", AdapterLogLevel.Warning, "Unregistering '" + melon.ID + "' failed: " + exception.Message, exception);
                }

                record.Melons.Remove(melon);
            }

            WriteReport();
        }

        private void InvokeEvent(string name, MelonEvent melonEvent)
        {
            try
            {
                melonEvent.Invoke(action => action());
            }
            catch (Exception exception)
            {
                _log.Write("events", AdapterLogLevel.Error, "A subscriber of MelonEvents." + name + " threw: " + exception, exception);
            }
        }

        private void InvokeSceneEvent(string name, MelonEvent<int, string> melonEvent, int index, string sceneName)
        {
            try
            {
                melonEvent.Invoke(action => action(index, sceneName));
            }
            catch (Exception exception)
            {
                _log.Write("events", AdapterLogLevel.Error, "A subscriber of MelonEvents." + name + " threw: " + exception, exception);
            }
        }

        // ---------------------------------------------------------------- frame loop

        /// <summary>Per-frame entry point. On the first frame the delayed application callbacks fire.</summary>
        public void Tick(float deltaTime)
        {
            if (!_initialized || !_enabled || _shuttingDown)
            {
                return;
            }

            if (!_applicationStarted)
            {
                StartApplication();
            }

            _coroutines.Tick();
            InvokeEvent("OnUpdate", MelonEvents.OnUpdate);
            DispatchAll("OnUpdate", melon => melon.OnUpdate());
        }

        public void FixedUpdate(float fixedDeltaTime)
        {
            if (!_initialized || !_enabled || _shuttingDown)
            {
                return;
            }

            InvokeEvent("OnFixedUpdate", MelonEvents.OnFixedUpdate);
            DispatchAll("OnFixedUpdate", melon => melon.OnFixedUpdate());
        }

        public void LateUpdate(float deltaTime)
        {
            if (!_initialized || !_enabled || _shuttingDown)
            {
                return;
            }

            InvokeEvent("OnLateUpdate", MelonEvents.OnLateUpdate);
            DispatchAll("OnLateUpdate", melon => melon.OnLateUpdate());
        }

        public void OnGUI()
        {
            if (!_initialized || !_enabled || _shuttingDown)
            {
                return;
            }

            InvokeEvent("OnGUI", MelonEvents.OnGUI);
            DispatchAll("OnGUI", melon => melon.OnGUI());
        }

        private void StartApplication()
        {
            _applicationStarted = true;
            _log.Write("lifecycle", AdapterLogLevel.Info, "First frame reached; dispatching application start callbacks.");

            InvokeEvent("OnApplicationEarlyStart", MelonEvents.OnApplicationEarlyStart);
            DispatchAll("OnApplicationEarlyStart", melon => melon.OnApplicationEarlyStart());

            InvokeEvent("OnApplicationStart", MelonEvents.OnApplicationStart);
            DispatchAll("OnApplicationStart", melon => melon.OnApplicationStart());

            InvokeEvent("OnApplicationLateStart", MelonEvents.OnApplicationLateStart);
            DispatchAll("OnApplicationLateStart", melon => melon.OnApplicationLateStart());

            DispatchAll("OnModSettingsApplied", melon => melon.OnModSettingsApplied());
        }

        public void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (!_initialized || !_enabled || _shuttingDown)
            {
                return;
            }

            InvokeSceneEvent("OnSceneWasLoaded", MelonEvents.OnSceneWasLoaded, buildIndex, sceneName);
            DispatchAll("OnSceneWasLoaded", melon => (melon as MelonMod)?.OnSceneWasLoaded(buildIndex, sceneName));
            DispatchAll("OnLevelWasLoaded", melon => (melon as MelonMod)?.OnLevelWasLoaded(buildIndex));

            InvokeSceneEvent("OnSceneWasInitialized", MelonEvents.OnSceneWasInitialized, buildIndex, sceneName);
            DispatchAll("OnSceneWasInitialized", melon => (melon as MelonMod)?.OnSceneWasInitialized(buildIndex, sceneName));
            DispatchAll("OnLevelWasInitialized", melon => (melon as MelonMod)?.OnLevelWasInitialized(buildIndex));
        }

        public void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (!_initialized || !_enabled || _shuttingDown)
            {
                return;
            }

            InvokeSceneEvent("OnSceneWasUnloaded", MelonEvents.OnSceneWasUnloaded, buildIndex, sceneName);
            DispatchAll("OnSceneWasUnloaded", melon => (melon as MelonMod)?.OnSceneWasUnloaded(buildIndex, sceneName));
        }

        // ---------------------------------------------------------------- registry

        public void NotifyRegistered(MelonBase melon)
        {
            if (melon?.Assembly == null)
            {
                return;
            }

            if (_assemblyOwners.TryGetValue(melon.Assembly, out ModRecord record))
            {
                if (!record.Melons.Contains(melon))
                {
                    record.Melons.Add(melon);
                }

                return;
            }

            if (!_initialized)
            {
                // Melons registered while their record was being loaded. The loader owns that bookkeeping.
                return;
            }

            _log.Write("registry", AdapterLogLevel.Warning, "Melon '" + melon.ID + "' registered from an assembly that MelonAdapter did not load (" + melon.Assembly.GetName().Name + "). It will receive lifecycle callbacks but is not listed in the compatibility report.");
        }

        public void NotifyUnregistered(MelonBase melon)
        {
            if (melon == null)
            {
                return;
            }

            foreach (ModRecord record in _loadOrder)
            {
                if (record.Melons.Remove(melon) && record.Melons.Count == 0 && record.State == ModState.Loaded)
                {
                    record.State = ModState.Disabled;
                    record.FailureReason = "All melons unregistered themselves.";
                    _log.Write("registry", AdapterLogLevel.Warning, "'" + record.DisplayName + "' unregistered all of its melons and will not receive further callbacks.");
                }
            }
        }

        // ---------------------------------------------------------------- preferences and shutdown

        public void SavePreferences()
        {
            try
            {
                MelonPreferences.Save();
            }
            catch (Exception exception)
            {
                _log.Write("preferences", AdapterLogLevel.Error, "Saving MelonPreferences failed: " + exception.Message, exception);
            }
        }

        /// <summary>Toggles the adapter. Assemblies cannot be unloaded, so enabling again needs a restart.</summary>
        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                if (_enabled)
                {
                    return;
                }

                _log.Write("lifecycle", AdapterLogLevel.Warning, "Re-enabling MelonAdapter is not possible at runtime: mod assemblies cannot be unloaded. Restart the game to load mods again.");
                return;
            }

            if (!_enabled)
            {
                return;
            }

            _enabled = false;
            _log.Write("lifecycle", AdapterLogLevel.Warning, "MelonAdapter was disabled. Callbacks stop, preferences are saved and Harmony patches are removed. A game restart is required to load mods again.");

            SavePreferences();
            _coroutines.StopAll();

            foreach (ModRecord record in _loadOrder)
            {
                try
                {
                    if (record.Assembly != null)
                    {
                        _events.UnsubscribeAssembly(record.Assembly);
                    }

                    _harmony.UnpatchOwner(record.ModId);
                }
                catch (Exception exception)
                {
                    _log.Write("lifecycle", AdapterLogLevel.Warning, "Cleanup for '" + record.DisplayName + "' failed: " + exception.Message, exception);
                }
            }

            WriteReport();
        }

        /// <summary>Clean shutdown: quit callbacks, preferences, de-initialization, unpatch, resolver teardown.</summary>
        public void Shutdown()
        {
            if (_shuttingDown || _disposed)
            {
                return;
            }

            _shuttingDown = true;
            _log.Write("lifecycle", AdapterLogLevel.Info, "Shutting down; dispatching quit callbacks.");

            InvokeEvent("OnApplicationQuit", MelonEvents.OnApplicationQuit);
            DispatchAll("OnApplicationQuit", melon => melon.OnApplicationQuit());

            SavePreferences();

            InvokeEvent("OnApplicationDefiniteQuit", MelonEvents.OnApplicationDefiniteQuit);
            DispatchAll("OnApplicationDefiniteQuit", melon => melon.OnApplicationDefiniteQuit());

            foreach (ModRecord record in _loadOrder)
            {
                foreach (MelonBase melon in record.Melons.ToArray())
                {
                    try
                    {
                        melon.Unregister("MelonAdapter shutdown", false);
                    }
                    catch (Exception exception)
                    {
                        _log.Write("lifecycle", AdapterLogLevel.Warning, "Unregistering '" + melon.ID + "' during shutdown failed: " + exception.Message, exception);
                    }
                }

                record.Melons.Clear();

                if (record.State == ModState.Loaded)
                {
                    record.State = ModState.Disabled;
                    record.Notes.Add("Unregistered during shutdown.");
                }
            }

            _harmony.UnpatchAllOwners();
            _coroutines.StopAll();
            _resolver.Uninstall();
            WriteReport();
            _log.Write("lifecycle", AdapterLogLevel.Info, "Shutdown complete: " + StatusSummary);
        }

        public void WriteReport()
        {
            try
            {
                _report.PreferencesOrigin = PreferencesOrigin;
                _report.Write(_paths.ReportPath, _mods, _resolver.Attempts, _enabled);
            }
            catch (Exception exception)
            {
                _log.Write("diagnostics", AdapterLogLevel.Warning, "Writing the compatibility report failed: " + exception.Message, exception);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // Shutdown refuses to run on a disposed runtime, so the flag must be set afterwards.
                Shutdown();
            }
            catch (Exception)
            {
                // Shutdown already logs what it can.
            }

            _disposed = true;
            _resolver.Uninstall();
            AdapterBridge.Reset();
            _log.Dispose();
        }
    }
}
