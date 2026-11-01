using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MelonLoader
{
    /// <summary>
    /// Convenience base class that stores the single active instance of a melon type.
    /// </summary>
    public abstract class MelonTypeBase<T> : MelonBase where T : MelonTypeBase<T>
    {
        private static readonly List<T> RegisteredMelonsInternal = new List<T>();
        private static readonly ReadOnlyCollection<T> RegisteredMelonsView = RegisteredMelonsInternal.AsReadOnly();

        public static T Instance { get; internal set; }

        public static MelonLogger.Instance Logger { get; internal set; }

        /// <summary>Display name of this melon kind; the type name unless a derived type changes it.</summary>
        public static string TypeName { get; set; } = typeof(T).Name;

        public static new ReadOnlyCollection<T> RegisteredMelons => RegisteredMelonsView;

        public new string MelonTypeName => TypeName;

        public static void ExecuteAll(LemonAction<T> func, bool unregisterOnFail = false, string unregistrationReason = null)
        {
            ExecuteList(func, RegisteredMelonsInternal.ToList(), unregisterOnFail, unregistrationReason);
        }

        internal override void OnRegisteredInternals()
        {
            Instance = this as T;
            Logger = LoggerInstance;
            if (!RegisteredMelonsInternal.Contains(this as T))
            {
                RegisteredMelonsInternal.Add(this as T);
            }
        }

        internal override void OnUnregisteredInternals()
        {
            RegisteredMelonsInternal.Remove(this as T);
            if (ReferenceEquals(Instance, this))
            {
                Instance = RegisteredMelonsInternal.FirstOrDefault();
                Logger = Instance?.LoggerInstance;
            }
        }
    }

    /// <summary>
    /// Convenience accessor for the single active instance of an arbitrary melon type.
    /// </summary>
    public class Melon<T> where T : MelonBase
    {
        public static T Instance => MelonAssembly.RegisteredMelonsView.OfType<T>().FirstOrDefault();

        public static MelonLogger.Instance Logger => Instance?.LoggerInstance;
    }

    /// <summary>
    /// The classic MelonLoader mod type. MelonAdapter detects every subclass inside a mod assembly.
    /// </summary>
    public class MelonMod : MelonTypeBase<MelonMod>
    {
        public MelonModInfoAttribute InfoAttribute => Info as MelonModInfoAttribute;

        public MelonModGameAttribute[] GameAttributes
        {
            get
            {
                MelonGameAttribute[] games = Games;
                if (games == null || games.Length == 0)
                {
                    return new MelonModGameAttribute[0];
                }

                return games.OfType<MelonModGameAttribute>().ToArray();
            }
        }

        public virtual void OnLevelWasInitialized(int level)
        {
        }

        public virtual void OnLevelWasLoaded(int level)
        {
        }

        public virtual void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
        }

        public virtual void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
        }

        public virtual void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
        }
    }
}
