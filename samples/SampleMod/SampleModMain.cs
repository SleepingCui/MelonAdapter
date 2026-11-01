using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using MelonLoader;
using SupportLib;

[assembly: MelonInfo(typeof(SampleMod.SampleModMain), "Sample Mod", "1.0.0", "MelonAdapter", "https://example.invalid/melonadapter/sample-mod")]
[assembly: MelonGame]

namespace SampleMod
{
    /// <summary>
    /// Append-only lifecycle journal. The self test reads this file to prove that callbacks really ran,
    /// in the documented order, on the main thread.
    /// </summary>
    public static class LifecycleLog
    {
        public static string FilePath
        {
            get { return Path.Combine(MelonUtils.UserDataDirectory, "SampleMod", "lifecycle.log"); }
        }

        public static void Note(string message)
        {
            try
            {
                string path = FilePath;
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(path, message + Environment.NewLine);
            }
            catch (Exception)
            {
                // Diagnostics must never break the sample mod.
            }
        }
    }

    public class SampleTarget
    {
        public virtual int Value()
        {
            return 21;
        }
    }

    [HarmonyPatch(typeof(SampleTarget), nameof(SampleTarget.Value))]
    public static class SampleTargetPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref int __result)
        {
            __result *= 2;
        }
    }

    public class SampleModMain : MelonMod
    {
        private MelonPreferences_Entry<bool> _enabled;
        private MelonPreferences_Entry<int> _frameBudget;
        private MelonPreferences_Entry<string> _greeting;

        private int _frames;
        private int _eventUpdates;
        private bool _guiReported;
        private object _coroutine;

        public override void OnEarlyInitializeMelon()
        {
            LifecycleLog.Note("OnEarlyInitializeMelon");
        }

        public override void OnInitializeMelon()
        {
            LifecycleLog.Note("OnInitializeMelon");

            MelonPreferences_Category category = MelonPreferences.CreateCategory("SampleMod", "Sample Mod");
            _enabled = category.CreateEntry("Enabled", true, "Enable the sample mod");
            _frameBudget = category.CreateEntry("FrameBudget", 3, "Frames the sample coroutine runs for");
            _greeting = category.CreateEntry("Greeting", "hello from MelonAdapter", "Greeting logged on application start");

            MelonEvents.OnUpdate.Subscribe(OnAdapterUpdate);
            MelonEvents.OnSceneWasInitialized.Subscribe(OnAdapterSceneInitialized);

            HarmonyInstance.PatchAll(typeof(SampleModMain).Assembly);
            LifecycleLog.Note("harmony=" + new SampleTarget().Value());
            LifecycleLog.Note("support=" + SupportLibBootstrap.Ping());
        }

        public override void OnLateInitializeMelon()
        {
            LifecycleLog.Note("OnLateInitializeMelon");
        }

        public override void OnApplicationEarlyStart()
        {
            LifecycleLog.Note("OnApplicationEarlyStart");
        }

        public override void OnApplicationStart()
        {
            LifecycleLog.Note("OnApplicationStart greeting=" + _greeting.Value + " enabled=" + _enabled.Value);
            _coroutine = MelonCoroutines.Start(RunRoutine());
        }

        public override void OnApplicationLateStart()
        {
            LifecycleLog.Note("OnApplicationLateStart");
        }

        public override void OnUpdate()
        {
            _frames++;
            if (_frames == 1)
            {
                LifecycleLog.Note("OnUpdate");
            }
        }

        public override void OnFixedUpdate()
        {
            LifecycleLog.Note("OnFixedUpdate");
        }

        public override void OnLateUpdate()
        {
            LifecycleLog.Note("OnLateUpdate");
        }

        public override void OnGUI()
        {
            if (_guiReported)
            {
                return;
            }

            _guiReported = true;
            LifecycleLog.Note("OnGUI");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            LifecycleLog.Note("OnSceneWasInitialized:" + buildIndex + ":" + sceneName);
        }

        public override void OnPreferencesSaved()
        {
            LifecycleLog.Note("OnPreferencesSaved");
        }

        public override void OnApplicationQuit()
        {
            LifecycleLog.Note("OnApplicationQuit");
            if (_coroutine != null)
            {
                MelonCoroutines.Stop(_coroutine);
                _coroutine = null;
            }

            MelonEvents.OnUpdate.Unsubscribe(OnAdapterUpdate);
            MelonEvents.OnSceneWasInitialized.Unsubscribe(OnAdapterSceneInitialized);
            MelonPreferences.Save();
        }

        public override void OnDeinitializeMelon()
        {
            LifecycleLog.Note("OnDeinitializeMelon");
        }

        private void OnAdapterUpdate()
        {
            _eventUpdates++;
            if (_eventUpdates == 1)
            {
                LifecycleLog.Note("event:OnUpdate");
            }
        }

        private void OnAdapterSceneInitialized(int buildIndex, string sceneName)
        {
            LifecycleLog.Note("event:OnSceneWasInitialized:" + sceneName);
        }

        private IEnumerator RunRoutine()
        {
            for (int i = 0; i < _frameBudget.Value; i++)
            {
                yield return null;
            }

            LifecycleLog.Note("coroutine-done");
        }
    }
}
