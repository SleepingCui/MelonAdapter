using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;

namespace MelonAdapter.Runtime
{
    /// <summary>
    /// The single Harmony provider of the adapter. Every melon gets exactly one
    /// <see cref="Harmony"/> instance keyed by its stable owner id, so patches can be tracked and
    /// removed owner by owner.
    /// </summary>
    public sealed class HarmonyProvider : IAdapterHarmonyProvider
    {
        private const string MissingProviderHint =
            "No Harmony provider is available. Place a Harmony 2.x build (0Harmony.dll) into '<adapter>/Dependencies/' and restart the game; mods that use Harmony cannot be loaded without it.";

        private readonly object _gate = new object();
        private readonly Dictionary<string, Harmony> _instances = new Dictionary<string, Harmony>(StringComparer.Ordinal);
        private readonly IAdapterLogSink _log;
        private bool _availabilityChecked;
        private bool _available;
        private string _providerVersion = "unavailable";
        private string _providerLocation = "unavailable";

        public HarmonyProvider(IAdapterLogSink log)
        {
            _log = log;
        }

        public string ProviderVersion
        {
            get
            {
                EnsureAvailable();
                return _providerVersion;
            }
        }

        public string ProviderAssemblyLocation
        {
            get
            {
                EnsureAvailable();
                return _providerLocation;
            }
        }

        /// <summary>
        /// Resolves the Harmony assembly on first use instead of in the constructor: a game without
        /// <c>0Harmony.dll</c> must still start the adapter and report a readable diagnostic rather
        /// than fail while touching a type reference.
        /// </summary>
        private bool EnsureAvailable()
        {
            lock (_gate)
            {
                if (_availabilityChecked)
                {
                    return _available;
                }

                _availabilityChecked = true;
                try
                {
                    System.Reflection.Assembly assembly = typeof(Harmony).Assembly;
                    _providerVersion = assembly.GetName().Version?.ToString() ?? "unknown";
                    _providerLocation = string.IsNullOrEmpty(assembly.Location) ? "unknown" : assembly.Location;
                    _available = true;
                }
                catch (Exception exception)
                {
                    _providerVersion = "unavailable";
                    _providerLocation = "unavailable";
                    _log?.Write("harmony", AdapterLogLevel.Error, MissingProviderHint + " (" + exception.GetType().Name + ": " + exception.Message + ")", exception);
                }

                return _available;
            }
        }

        public Harmony GetInstance(string ownerId)
        {
            if (!EnsureAvailable())
            {
                return null;
            }

            if (string.IsNullOrEmpty(ownerId))
            {
                ownerId = "MelonAdapter.Anonymous";
            }

            lock (_gate)
            {
                Harmony instance;
                if (_instances.TryGetValue(ownerId, out instance))
                {
                    return instance;
                }

                instance = new Harmony(ownerId);
                _instances[ownerId] = instance;
                return instance;
            }
        }

        public int PatchAllFor(string ownerId)
        {
            Harmony instance = GetInstance(ownerId);
            if (instance == null)
            {
                return 0;
            }

            instance.PatchAll();
            return instance.GetPatchedMethods() != null ? 1 : 0;
        }

        public void UnpatchOwner(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                return;
            }

            lock (_gate)
            {
                Harmony instance;
                if (!_instances.TryGetValue(ownerId, out instance))
                {
                    return;
                }

                try
                {
                    instance.UnpatchAll(ownerId);
                }
                catch (Exception exception)
                {
                    _log?.Write("harmony", AdapterLogLevel.Error, "Failed to unpatch harmony owner '" + ownerId + "': " + exception.Message, exception);
                }
                finally
                {
                    _instances.Remove(ownerId);
                }
            }
        }

        public void UnpatchAllOwners()
        {
            List<string> owners;
            lock (_gate)
            {
                owners = new List<string>(_instances.Keys);
            }

            foreach (string owner in owners)
            {
                UnpatchOwner(owner);
            }
        }
    }
}
