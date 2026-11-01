using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace MelonLoader
{
    public static class MelonUtils
    {
        private static IAdapterContext Context => AdapterBridge.Context;

        public static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

        public static string GameDirectory => Context?.GameDirectory ?? BaseDirectory;

        public static string MelonLoaderDirectory => Context?.MelonLoaderDirectory ?? Path.Combine(GameDirectory, "MelonLoader");

        public static string UserDataDirectory => Context?.UserDataDirectory ?? Path.Combine(MelonLoaderDirectory, "UserData");

        public static string UserLibsDirectory => Path.Combine(MelonLoaderDirectory, "UserLibs");

        public static string GameDeveloper => Context?.GameDeveloper ?? string.Empty;

        public static string GameName => Context?.GameName ?? string.Empty;

        public static string GameVersion => Context?.GameVersion ?? string.Empty;

        public static MelonGameAttribute CurrentGameAttribute => new MelonGameAttribute(GameDeveloper, GameName);

        public static MelonPlatformAttribute.CompatiblePlatforms CurrentPlatform
        {
            get
            {
                if (IsWindows)
                {
                    return Environment.Is64BitProcess ? MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64 : MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X86;
                }

                if (IsMac)
                {
                    return MelonPlatformAttribute.CompatiblePlatforms.MAC;
                }

                return MelonPlatformAttribute.CompatiblePlatforms.LINUX;
            }
        }

        public static MelonPlatformDomainAttribute.CompatibleDomains CurrentDomain => MelonPlatformDomainAttribute.CompatibleDomains.MONO;

        public static bool IsUnix => Environment.OSVersion.Platform == PlatformID.Unix;

        public static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;

        public static bool IsMac => Environment.OSVersion.Platform == PlatformID.MacOSX;

        public static string GetApplicationPath() => Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? BaseDirectory);

        public static string GetGameDataDirectory() => Path.Combine(GameDirectory, GameName + "_Data");

        public static string GetManagedDirectory() => Path.Combine(GetGameDataDirectory(), "Managed");

        public static string GetUnityVersion()
        {
            IAdapterContext context = Context;
            return context?.UnityVersion ?? "unknown";
        }

        public static bool IsGameIl2Cpp() => false;

        public static bool IsOldMono() => false;

        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0)
            {
                return min;
            }

            return value.CompareTo(max) > 0 ? max : value;
        }

        public static string ComputeHash(Assembly assembly)
        {
            try
            {
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(File.ReadAllBytes(assembly.Location));
                    return string.Concat(hash.Select(b => b.ToString("x2")));
                }
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Warning, "Cannot compute the hash of assembly '" + assembly?.GetName().Name + "': " + exception.Message, null, null);
                return string.Empty;
            }
        }

        public static string ComputeHash(string filePath)
        {
            try
            {
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(File.ReadAllBytes(filePath));
                    return string.Concat(hash.Select(b => b.ToString("x2")));
                }
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Warning, "Cannot compute the hash of file '" + filePath + "': " + exception.Message, null, null);
                return string.Empty;
            }
        }
    }

    public static class MelonLoaderBase
    {
        public static string UserDataPath => MelonUtils.UserDataDirectory;

        public static string UnityVersion => MelonUtils.GetUnityVersion();
    }

    public class MelonConsole
    {
        public static void SetTitle(string title)
        {
            try
            {
                Console.Title = title;
            }
            catch (Exception)
            {
                // Console titles are unavailable inside a Unity player; ignore.
            }
        }
    }
}

namespace MelonLoader.Utils
{
    /// <summary>
    /// MelonLoader's directory layout. MelonAdapter maps it onto its own adapter directory; the
    /// IL2CPP and support-module paths exist for API compatibility but no IL2CPP runtime is present.
    /// </summary>
    public static class MelonEnvironment
    {
        public static bool IsDotnetRuntime => Type.GetType("Mono.Runtime") == null && Type.GetType("System.Private.CoreLib, System.Private.CoreLib") != null;

        public static bool IsMonoRuntime => Type.GetType("Mono.Runtime") != null;

        public static string GameRootDirectory => MelonLoader.MelonUtils.GameDirectory;

        public static string MelonBaseDirectory => MelonLoader.MelonUtils.MelonLoaderDirectory;

        public static string MelonLoaderDirectory => MelonLoader.MelonUtils.MelonLoaderDirectory;

        public static string OurRuntimeDirectory => MelonLoader.MelonUtils.MelonLoaderDirectory;

        public static string MelonManagedDirectory => Path.Combine(MelonLoader.MelonUtils.MelonLoaderDirectory, "Managed");

        public static string DependenciesDirectory => Path.Combine(MelonLoader.MelonUtils.MelonLoaderDirectory, "Dependencies");

        public static string SupportModuleDirectory => Path.Combine(MelonLoader.MelonUtils.MelonLoaderDirectory, "SupportModules");

        public static string CompatibilityLayerDirectory => Path.Combine(MelonLoader.MelonUtils.MelonLoaderDirectory, "CompatibilityLayer");

        public static string Il2CppAssemblyGeneratorDirectory => Path.Combine(MelonLoader.MelonUtils.MelonLoaderDirectory, "Il2CppAssemblyGenerator");

        public static string Il2CppDataDirectory => Path.Combine(MelonLoader.MelonUtils.MelonLoaderDirectory, "Il2CppData");

        public static string Il2CppAssembliesDirectory => Path.Combine(Il2CppDataDirectory, "Assembly");

        public static string ModsDirectory
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("MELONADAPTER_MODS_DIRECTORY");
                return string.IsNullOrEmpty(configured) ? Path.Combine(MelonLoader.MelonUtils.GameDirectory, "Mods") : configured;
            }
        }

        public static string PluginsDirectory => Path.Combine(MelonLoader.MelonUtils.GameDirectory, "Plugins");

        public static string UserLibsDirectory => MelonLoader.MelonUtils.UserLibsDirectory;

        public static string UserDataDirectory => MelonLoader.MelonUtils.UserDataDirectory;

        public static string MelonLoaderLogsDirectory => Path.Combine(MelonLoader.MelonUtils.GameDirectory, "Logs");

        public static string GameExecutablePath => Assembly.GetEntryAssembly()?.Location ?? string.Empty;

        public static string GameExecutableName => Path.GetFileNameWithoutExtension(GameExecutablePath);

        public static string UnityPlayerPath => Path.Combine(MelonLoader.MelonUtils.GameDirectory, "UnityPlayer.dll");

        public static string UnityGameDataDirectory => MelonLoader.MelonUtils.GetGameDataDirectory();

        public static string UnityGameManagedDirectory => MelonLoader.MelonUtils.GetManagedDirectory();
    }
}
