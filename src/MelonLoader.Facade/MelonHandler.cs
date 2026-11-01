using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader.Utils;
using System.Reflection;

namespace MelonLoader
{
    /// <summary>
    /// Entry points that mods occasionally use to inspect (or try to trigger) mod loading.
    /// MelonAdapter performs discovery exactly once at startup; runtime loading is not supported.
    /// The signatures (void returns, List properties) follow the MelonLoader 0.7.3 baseline exactly:
    /// a mismatched return type would surface as a MissingMethodException inside the mod.
    /// </summary>
    public static class MelonHandler
    {
        public static List<MelonMod> Mods => MelonBase.RegisteredMelons.OfType<MelonMod>().ToList();

        public static List<MelonPlugin> Plugins => MelonBase.RegisteredMelons.OfType<MelonPlugin>().ToList();

        public static List<MelonAssembly> Assemblies => MelonAssembly.LoadedAssemblies.ToList();

        public static void LoadFromFile(string filepath, bool loadOnly = false)
        {
            MelonLogger.Publish(
                AdapterLogLevel.Error,
                "MelonHandler.LoadFromFile('" + filepath + "') is not supported by MelonAdapter v1. Mods are discovered from the configured Mods directory during startup only; place the assembly there and restart the game.",
                null,
                null);
        }

        public static void LoadFromFile(string filepath, string name)
        {
            MelonLogger.Publish(
                AdapterLogLevel.Error,
                "MelonHandler.LoadFromFile('" + filepath + "', '" + name + "') is not supported by MelonAdapter v1. Mods are discovered from the configured Mods directory during startup only; place the assembly there and restart the game.",
                null,
                null);
        }

        public static void LoadFromAssembly(Assembly assembly, string filepath)
        {
            MelonLogger.Publish(
                AdapterLogLevel.Error,
                "MelonHandler.LoadFromAssembly('" + (assembly == null ? "<null>" : assembly.GetName().Name) + "', '" + filepath + "') is not supported by MelonAdapter v1. MelonAdapter loads assemblies from the configured Mods directory during startup only.",
                null,
                null);
        }

        public static void LoadFromAssembly(Assembly assembly, string filepath, bool isBundle)
        {
            MelonLogger.Publish(
                AdapterLogLevel.Error,
                "MelonHandler.LoadFromAssembly('" + (assembly == null ? "<null>" : assembly.GetName().Name) + "', '" + filepath + "', isBundle: " + isBundle + ") is not supported by MelonAdapter v1. MelonAdapter loads assemblies from the configured Mods directory during startup only.",
                null,
                null);
        }

        public static void LoadFromByteArray(byte[] assemblyData, string filepath)
        {
            MelonLogger.Publish(
                AdapterLogLevel.Error,
                "MelonHandler.LoadFromByteArray('" + filepath + "') is not supported by MelonAdapter v1. MelonAdapter never loads assemblies from memory; place the assembly in the configured Mods directory and restart the game.",
                null,
                null);
        }

        public static void LoadFromByteArray(byte[] assemblyData, byte[] symbolsData, string filepath)
        {
            MelonLogger.Publish(
                AdapterLogLevel.Error,
                "MelonHandler.LoadFromByteArray('" + filepath + "', symbols: " + (symbolsData == null ? "no" : "yes") + ") is not supported by MelonAdapter v1. MelonAdapter never loads assemblies from memory; place the assembly in the configured Mods directory and restart the game.",
                null,
                null);
        }

        public static void LoadFromByteArray(byte[] assemblyData, string filepath, bool isBundle)
        {
            MelonLogger.Publish(
                AdapterLogLevel.Error,
                "MelonHandler.LoadFromByteArray('" + filepath + "', isBundle: " + isBundle + ") is not supported by MelonAdapter v1. MelonAdapter never loads assemblies from memory; place the assembly in the configured Mods directory and restart the game.",
                null,
                null);
        }

        public static bool IsMelonAlreadyLoaded(string filepath) => IsModAlreadyLoaded(filepath);

        public static bool IsPluginAlreadyLoaded(string filepath) => IsModAlreadyLoaded(filepath);

        public static string GetMelonHash(MelonBase melon) => melon == null ? string.Empty : (melon.Hash ?? string.Empty);

        /// <summary>Directory this adapter scans for mods; matches the Mods directory reported at start-up.</summary>
        public static string ModsDirectory => MelonEnvironment.ModsDirectory;

        /// <summary>MelonAdapter has no separate plugin directory; plugins live next to mods.</summary>
        public static string PluginsDirectory => MelonEnvironment.PluginsDirectory;

        public static bool IsModAlreadyLoaded(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return false;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(filepath);
            }
            catch (Exception)
            {
                return false;
            }

            return MelonAssembly.LoadedAssemblies.Any(a => string.Equals(SafeLocation(a), fullPath, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetMelonHash(string filepath)
        {
            if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath))
            {
                return string.Empty;
            }

            return MelonUtils.ComputeHash(filepath);
        }

        private static string SafeLocation(MelonAssembly assembly)
        {
            try
            {
                return assembly.Location;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }

    public static class Main
    {
        public static string GetUnityVersion() => MelonUtils.GetUnityVersion();

        public static string GetUserPath() => MelonUtils.UserDataDirectory;

        public static List<MelonMod> Mods => MelonHandler.Mods;
    }
}
