using System;
using System.Collections;

namespace MelonLoader
{
    public enum AdapterLogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    /// <summary>Logging backend supplied by MelonAdapter.Runtime.</summary>
    public interface IAdapterLogSink
    {
        bool IsDebugEnabled { get; }
        void Write(string source, AdapterLogLevel level, string message, Exception exception = null);
    }

    /// <summary>Static facts about the hosting environment supplied by MelonAdapter.Runtime.</summary>
    public interface IAdapterContext
    {
        string GameDirectory { get; }
        string MelonLoaderDirectory { get; }
        string UserDataDirectory { get; }
        string GameDeveloper { get; }
        string GameName { get; }
        string GameVersion { get; }
        string UnityVersion { get; }
        string AdapterVersion { get; }
        string ApiBaselineVersion { get; }
        bool DebugLogging { get; }
    }

    /// <summary>Persistence backend for <see cref="MelonPreferences"/> supplied by MelonAdapter.Runtime.</summary>
    public interface IAdapterPreferencesStorage
    {
        string FilePath { get; }
        bool TryGetValue(string categoryIdentifier, string entryIdentifier, out string rawValue);
        void SaveAll();
    }

    /// <summary>Main-thread coroutine host supplied by MelonAdapter.Host (a single hidden MonoBehaviour).</summary>
    public interface IAdapterCoroutineRunner
    {
        object Start(IEnumerator routine);
        void Stop(object handle);
    }

    /// <summary>Single Harmony provider resolved by MelonAdapter.Runtime.</summary>
    public interface IAdapterHarmonyProvider
    {
        string ProviderVersion { get; }
        HarmonyLib.Harmony GetInstance(string ownerId);
        void UnpatchOwner(string ownerId);
        void UnpatchAllOwners();
    }

    /// <summary>Registration hook so the runtime can track melons as they register.</summary>
    public interface IAdapterMelonRegistry
    {
        void NotifyRegistered(MelonBase melon);
        void NotifyUnregistered(MelonBase melon);
    }

    /// <summary>
    /// Bridge between the MelonLoader facade assembly and MelonAdapter.Runtime.
    /// The facade never references the runtime (that would be circular); the runtime assigns these
    /// services during startup. When nothing is assigned the facade degrades gracefully:
    /// logging goes to the console, preferences are memory-only and coroutines are refused with a diagnostic.
    /// </summary>
    public static class AdapterBridge
    {
        private static readonly object Sync = new object();

        public static IAdapterLogSink LogSink { get; set; }
        public static IAdapterContext Context { get; set; }
        public static IAdapterPreferencesStorage PreferencesStorage { get; set; }
        public static IAdapterCoroutineRunner CoroutineRunner { get; set; }
        public static IAdapterHarmonyProvider HarmonyProvider { get; set; }
        public static IAdapterMelonRegistry Registry { get; set; }

        /// <summary>Diagnostic message used when a facade feature has no host implementation.</summary>
        public static string DescribeMissingService(string service) =>
            "MelonAdapter: the '" + service + "' service is not available. " +
            "This MelonLoader API surface is not implemented by the current MelonAdapter runtime.";

        public static void Reset()
        {
            lock (Sync)
            {
                LogSink = null;
                Context = null;
                PreferencesStorage = null;
                CoroutineRunner = null;
                HarmonyProvider = null;
                Registry = null;
            }
        }

        public static bool IsAttached
        {
            get
            {
                lock (Sync)
                {
                    return LogSink != null || Context != null;
                }
            }
        }
    }
}
