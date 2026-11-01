using System;
using MelonAdapter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MelonAdapter.Host
{
    /// <summary>
    /// Single hidden main-thread scheduler. Every MelonLoader callback is dispatched from here, so
    /// mods never run on UMM's or Unity's background threads.
    /// </summary>
    public sealed class AdapterTicker : MonoBehaviour
    {
        internal AdapterRuntime Runtime;
        private bool _initialSceneReported;

        internal static AdapterTicker Create(AdapterRuntime runtime)
        {
            GameObject host = new GameObject("MelonAdapter.Ticker");
            host.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(host);
            AdapterTicker ticker = host.AddComponent<AdapterTicker>();
            ticker.Runtime = runtime;
            return ticker;
        }

        private void Start()
        {
            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
            }
            catch (Exception exception)
            {
                Runtime?.Log.Write("unity", MelonLoader.AdapterLogLevel.Warning, "Scene callbacks are unavailable in this Unity version: " + exception.Message, exception);
            }
        }

        private void Update()
        {
            AdapterRuntime runtime = Runtime;
            if (runtime == null)
            {
                return;
            }

            try
            {
                runtime.Tick(Time.deltaTime);

                if (!_initialSceneReported)
                {
                    _initialSceneReported = true;
                    Scene active = SceneManager.GetActiveScene();
                    if (active.IsValid())
                    {
                        runtime.OnSceneWasLoaded(active.buildIndex, active.name);
                    }
                }
            }
            catch (Exception exception)
            {
                runtime.Log.Write("unity", MelonLoader.AdapterLogLevel.Error, "Update dispatch failed: " + exception, exception);
            }
        }

        private void FixedUpdate()
        {
            AdapterRuntime runtime = Runtime;
            if (runtime == null)
            {
                return;
            }

            try
            {
                runtime.FixedUpdate(Time.fixedDeltaTime);
            }
            catch (Exception exception)
            {
                runtime.Log.Write("unity", MelonLoader.AdapterLogLevel.Error, "FixedUpdate dispatch failed: " + exception, exception);
            }
        }

        private void LateUpdate()
        {
            AdapterRuntime runtime = Runtime;
            if (runtime == null)
            {
                return;
            }

            try
            {
                runtime.LateUpdate(Time.deltaTime);
            }
            catch (Exception exception)
            {
                runtime.Log.Write("unity", MelonLoader.AdapterLogLevel.Error, "LateUpdate dispatch failed: " + exception, exception);
            }
        }

        private void OnGUI()
        {
            AdapterRuntime runtime = Runtime;
            if (runtime == null)
            {
                return;
            }

            try
            {
                runtime.OnGUI();
            }
            catch (Exception exception)
            {
                runtime.Log.Write("unity", MelonLoader.AdapterLogLevel.Error, "OnGUI dispatch failed: " + exception, exception);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Runtime?.OnSceneWasLoaded(scene.buildIndex, scene.name);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Runtime?.OnSceneWasUnloaded(scene.buildIndex, scene.name);
        }

        private void OnDestroy()
        {
            try
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
            }
            catch (Exception)
            {
                // The scene manager may already be shutting down.
            }
        }
    }
}
