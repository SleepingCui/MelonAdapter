using System;
using System.IO;
using MelonAdapter.Runtime;
using MelonLoader;
using UnityModManagerNet;
using UnityEngine;

namespace MelonAdapter.Host
{
    /// <summary>
    /// UnityModManager entry point. UMM loads this mod when the game starts; the adapter then scans
    /// its own mods directory and emulates the MelonLoader lifecycle for every discovered MelonMod.
    /// </summary>
    public static class AdapterHost
    {
        private static AdapterRuntime _runtime;
        private static AdapterTicker _ticker;
        private static UnityModManager.ModEntry _entry;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            _entry = modEntry;
            UnityModManager.ModEntry.ModLogger logger = modEntry.Logger;

            try
            {
                string ummModPath = modEntry.Path;
                string gameRoot = AdapterInstall.ResolveGameRoot(ummModPath);
                string dataDirectory = AdapterInstall.ResolveDataDirectory(gameRoot, ummModPath);

                logger.Log("MelonAdapter starting. Game root: " + gameRoot);
                logger.Log("MelonAdapter data directory: " + dataDirectory);

                string conflict = ConflictDetector.Describe(gameRoot);
                if (conflict != null)
                {
                    logger.Error(conflict);
                    return false;
                }

                string configPath = Path.Combine(dataDirectory, "Config.toml");
                AdapterConfig config = AdapterConfig.Load(configPath, out bool createdDefault);
                if (createdDefault)
                {
                    logger.Log("Created the default configuration at '" + configPath + "'.");
                }

                _runtime = new AdapterRuntime(gameRoot, dataDirectory, config);
                _runtime.Environment.UnityVersion = DescribeUnityVersion();
                DescribeGameIdentity(_runtime.Environment);
                GameIdentity.ApplyDefaults(_runtime.Environment);
                _runtime.Environment.HostName = "UnityModManager";
                _runtime.Environment.HostVersion = DescribeHostVersion(modEntry);
                _runtime.Log.Echo = (level, message) => Echo(logger, level, message);

                _runtime.Initialize();

                _ticker = AdapterTicker.Create(_runtime);
                _ticker.Runtime = _runtime;

                modEntry.OnToggle = OnToggle;
                modEntry.OnUnload = OnUnload;
                modEntry.OnGUI = OnDrawSettingsUI;

                logger.Log("MelonAdapter loaded: " + _runtime.StatusSummary);
                logger.Log("Mods directory: " + _runtime.Paths.ModsDirectory);
                logger.Log("Compatibility report: " + _runtime.Paths.ReportPath);
                return true;
            }
            catch (Exception exception)
            {
                logger.LogException("MelonAdapter failed to start", exception);
                SafeShutdown();
                return false;
            }
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            AdapterRuntime runtime = _runtime;
            if (runtime == null)
            {
                return false;
            }

            if (value)
            {
                modEntry.Logger.Warning("MelonAdapter cannot reload mod assemblies at runtime. Restart the game to load them again.");
                return true;
            }

            runtime.SetEnabled(false);
            modEntry.Logger.Log("MelonAdapter disabled: callbacks stopped, preferences saved, Harmony patches removed. Restart the game to load mods again.");
            return true;
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            SafeShutdown();
            return true;
        }

        /// <summary>Draws the adapter page inside the UnityModManager mod window.</summary>
        private static void OnDrawSettingsUI(UnityModManager.ModEntry modEntry)
        {
            AdapterUI.Draw(_runtime);
        }

        private static void SafeShutdown()
        {
            try
            {
                _ticker?.gameObject.SetActive(false);
            }
            catch (Exception)
            {
                // The ticker may already be destroyed during shutdown.
            }

            try
            {
                _runtime?.Shutdown();
            }
            catch (Exception exception)
            {
                _entry?.Logger.LogException("MelonAdapter shutdown failed", exception);
            }

            try
            {
                _runtime?.Dispose();
            }
            catch (Exception)
            {
                // Nothing else can be done while the process is going away.
            }

            _runtime = null;
            _ticker = null;
        }

        private static void Echo(UnityModManager.ModEntry.ModLogger logger, AdapterLogLevel level, string message)
        {
            switch (level)
            {
                case AdapterLogLevel.Error:
                case AdapterLogLevel.Critical:
                    logger.Error(message);
                    break;
                case AdapterLogLevel.Warning:
                    logger.Warning(message);
                    break;
                case AdapterLogLevel.Debug:
                    logger.Log(message);
                    break;
                default:
                    logger.Log(message);
                    break;
            }
        }

        private static string DescribeUnityVersion()
        {
            try
            {
                return UnityModManager.unityVersion?.ToString() ?? Application.unityVersion;
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        private static string DescribeHostVersion(UnityModManager.ModEntry modEntry)
        {
            try
            {
                if (modEntry != null && modEntry.ManagerVersion != null)
                {
                    return modEntry.ManagerVersion.ToString();
                }

                Version version = UnityModManager.GetVersion();
                return version != null ? version.ToString() : "unknown";
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        /// <summary>
        /// Unity exposes the project's player settings at runtime. MelonLoader mods are matched
        /// against these values (see <see cref="MelonGameAttribute"/>), so the folder name is not
        /// good enough: an install under a renamed directory would reject every mod.
        /// </summary>
        private static void DescribeGameIdentity(AdapterEnvironment environment)
        {
            try
            {
                string company = Application.companyName;
                if (!string.IsNullOrEmpty(company))
                {
                    environment.GameDeveloper = company;
                }
            }
            catch (Exception)
            {
                // Unity is not initialised yet; app.info covers this case.
            }

            try
            {
                string product = Application.productName;
                if (!string.IsNullOrEmpty(product))
                {
                    environment.GameName = product;
                }
            }
            catch (Exception)
            {
                // Same as above.
            }
        }
    }
}
