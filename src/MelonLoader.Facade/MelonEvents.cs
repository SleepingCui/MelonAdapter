using System;
using System.Collections.Generic;
using System.Reflection;

namespace MelonLoader
{
    public delegate void LemonAction();
    public delegate void LemonAction<in T1>(T1 arg1);
    public delegate void LemonAction<in T1, in T2>(T1 arg1, T2 arg2);
    public delegate void LemonAction<in T1, in T2, in T3>(T1 arg1, T2 arg2, T3 arg3);
    public delegate void LemonAction<in T1, in T2, in T3, in T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate void LemonAction<in T1, in T2, in T3, in T4, in T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    public delegate void LemonAction<in T1, in T2, in T3, in T4, in T5, in T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    public delegate void LemonAction<in T1, in T2, in T3, in T4, in T5, in T6, in T7>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
    public delegate void LemonAction<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);

    public delegate TResult LemonFunc<out TResult>();
    public delegate TResult LemonFunc<in T1, out TResult>(T1 arg1);
    public delegate TResult LemonFunc<in T1, in T2, out TResult>(T1 arg1, T2 arg2);
    public delegate TResult LemonFunc<in T1, in T2, in T3, out TResult>(T1 arg1, T2 arg2, T3 arg3);
    public delegate TResult LemonFunc<in T1, in T2, in T3, in T4, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate TResult LemonFunc<in T1, in T2, in T3, in T4, in T5, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    public delegate TResult LemonFunc<in T1, in T2, in T3, in T4, in T5, in T6, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    public delegate TResult LemonFunc<in T1, in T2, in T3, in T4, in T5, in T6, in T7, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
    public delegate TResult LemonFunc<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);

    /// <summary>
    /// Ordered subscriber list used by <see cref="MelonEvents"/> and by mods that need trackable,
    /// priority ordered callbacks. Exceptions thrown by one subscriber never stop the others.
    /// </summary>
    public abstract class MelonEventBase<T> where T : class
    {
        /// <summary>
        /// One subscriber entry. MelonLoader 0.7.3 declares this as the nested generic class
        /// MelonLoader.MelonEventBase`1/MelonEventSubscriber`1 with public fields named del,
        /// unsubscribeOnFirstInvocation, priority and melonAssembly; mods reflect on those names,
        /// so they are reproduced verbatim here.
        /// </summary>
        public class MelonEventSubscriber
        {
            public T del;
            public bool unsubscribeOnFirstInvocation;
            public int priority;
            public MelonAssembly melonAssembly;

            public MelonEventSubscriber()
            {
            }
        }

        private readonly object _sync = new object();
        private readonly List<MelonEventSubscriber> _subscribers = new List<MelonEventSubscriber>();

        protected MelonEventBase(bool oneTimeUse = false)
        {
            this.oneTimeUse = oneTimeUse;
        }

        public bool oneTimeUse;

        public bool Disposed { get; private set; }

        public void Subscribe(T del, int priority = 0, bool unsubscribeOnFirstInvocation = false)
        {
            if (del == null)
            {
                return;
            }

            Delegate callback = del as Delegate;
            MelonAssembly assembly = null;
            if (callback != null)
            {
                try
                {
                    assembly = MelonAssembly.GetMelonAssemblyOfMember(callback.Method, callback.Target);
                }
                catch (Exception)
                {
                    assembly = null;
                }
            }

            lock (_sync)
            {
                if (Disposed)
                {
                    return;
                }

                int index = _subscribers.Count;
                for (int i = 0; i < _subscribers.Count; i++)
                {
                    if (_subscribers[i].priority < priority)
                    {
                        index = i;
                        break;
                    }
                }

                _subscribers.Insert(index, new MelonEventSubscriber
                {
                    del = del,
                    priority = priority,
                    unsubscribeOnFirstInvocation = unsubscribeOnFirstInvocation,
                    melonAssembly = assembly
                });
            }
        }

        public void Unsubscribe(T del)
        {
            if (del == null)
            {
                return;
            }

            Delegate callback = del as Delegate;
            lock (_sync)
            {
                for (int i = _subscribers.Count - 1; i >= 0; i--)
                {
                    Delegate existing = _subscribers[i].del as Delegate;
                    if (callback == null ? ReferenceEquals(_subscribers[i].del, del) : Equals(existing, callback))
                    {
                        _subscribers.RemoveAt(i);
                    }
                }
            }
        }

        public void Unsubscribe(MethodInfo method, object target = null)
        {
            if (method == null)
            {
                return;
            }

            lock (_sync)
            {
                for (int i = _subscribers.Count - 1; i >= 0; i--)
                {
                    Delegate d = _subscribers[i].del as Delegate;
                    if (d == null)
                    {
                        continue;
                    }

                    if (d.Method == method && (target == null || ReferenceEquals(d.Target, target)))
                    {
                        _subscribers.RemoveAt(i);
                    }
                }
            }
        }

        public void UnsubscribeAll()
        {
            lock (_sync)
            {
                _subscribers.Clear();
            }
        }

        public bool CheckIfSubscribed(MethodInfo method, object target = null)
        {
            if (method == null)
            {
                return false;
            }

            lock (_sync)
            {
                foreach (MelonEventSubscriber subscriber in _subscribers)
                {
                    Delegate d = subscriber.del as Delegate;
                    if (d == null)
                    {
                        continue;
                    }

                    if (d.Method == method && (target == null || ReferenceEquals(d.Target, target)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public MelonEventSubscriber[] GetSubscribers()
        {
            lock (_sync)
            {
                return _subscribers.ToArray();
            }
        }

        public void Invoke(Action<T> invoke)
        {
            if (invoke == null)
            {
                return;
            }

            MelonEventSubscriber[] snapshot;
            lock (_sync)
            {
                if (Disposed || _subscribers.Count == 0)
                {
                    return;
                }

                snapshot = _subscribers.ToArray();
            }

            List<MelonEventSubscriber> remove = null;
            foreach (MelonEventSubscriber subscriber in snapshot)
            {
                Delegate d = subscriber.del as Delegate;
                try
                {
                    invoke(subscriber.del);
                }
                catch (Exception exception)
                {
                    MelonLogger.Publish(
                        AdapterLogLevel.Error,
                        "Exception while invoking event subscriber '" + (d?.Method?.DeclaringType?.FullName ?? "?") + "." + (d?.Method?.Name ?? "?") + "': " + exception.Message,
                        null,
                        exception);
                    remove = remove ?? new List<MelonEventSubscriber>();
                    remove.Add(subscriber);
                    continue;
                }

                if (subscriber.unsubscribeOnFirstInvocation || oneTimeUse)
                {
                    remove = remove ?? new List<MelonEventSubscriber>();
                    remove.Add(subscriber);
                }
            }

            if (remove != null)
            {
                lock (_sync)
                {
                    foreach (MelonEventSubscriber subscriber in remove)
                    {
                        _subscribers.Remove(subscriber);
                    }
                }
            }

            if (oneTimeUse)
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                Disposed = true;
                _subscribers.Clear();
            }
        }
    }

    public class MelonEvent : MelonEventBase<LemonAction>
    {
        public MelonEvent(bool oneTimeUse = false) : base(oneTimeUse)
        {
        }

        public void Invoke() => Invoke(action => action());
    }

    public class MelonEvent<T1> : MelonEventBase<LemonAction<T1>>
    {
        public MelonEvent(bool oneTimeUse = false) : base(oneTimeUse)
        {
        }

        public void Invoke(T1 arg1) => Invoke(action => action(arg1));
    }

    public class MelonEvent<T1, T2> : MelonEventBase<LemonAction<T1, T2>>
    {
        public MelonEvent(bool oneTimeUse = false) : base(oneTimeUse)
        {
        }

        public void Invoke(T1 arg1, T2 arg2) => Invoke(action => action(arg1, arg2));
    }

    public class MelonEvent<T1, T2, T3> : MelonEventBase<LemonAction<T1, T2, T3>>
    {
        public MelonEvent(bool oneTimeUse = false) : base(oneTimeUse)
        {
        }

        public void Invoke(T1 arg1, T2 arg2, T3 arg3) => Invoke(action => action(arg1, arg2, arg3));
    }

    public class MelonEvent<T1, T2, T3, T4> : MelonEventBase<LemonAction<T1, T2, T3, T4>>
    {
        public MelonEvent(bool oneTimeUse = false) : base(oneTimeUse)
        {
        }

        public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4) => Invoke(action => action(arg1, arg2, arg3, arg4));
    }

    public class MelonEvent<T1, T2, T3, T4, T5> : MelonEventBase<LemonAction<T1, T2, T3, T4, T5>>
    {
        public MelonEvent(bool oneTimeUse = false) : base(oneTimeUse)
        {
        }

        public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) => Invoke(action => action(arg1, arg2, arg3, arg4, arg5));
    }

    public class MelonEvent<T1, T2, T3, T4, T5, T6> : MelonEventBase<LemonAction<T1, T2, T3, T4, T5, T6>>
    {
        public MelonEvent(bool oneTimeUse = false) : base(oneTimeUse)
        {
        }

        public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6) => Invoke(action => action(arg1, arg2, arg3, arg4, arg5, arg6));
    }

    public class MelonEvent<T1, T2, T3, T4, T5, T6, T7> : MelonEventBase<LemonAction<T1, T2, T3, T4, T5, T6, T7>>
    {
        public MelonEvent(bool oneTimeUse = false) : base(oneTimeUse)
        {
        }

        public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7) => Invoke(action => action(arg1, arg2, arg3, arg4, arg5, arg6, arg7));
    }

    public class MelonEvent<T1, T2, T3, T4, T5, T6, T7, T8> : MelonEventBase<LemonAction<T1, T2, T3, T4, T5, T6, T7, T8>>
    {
        public MelonEvent(bool oneTimeUse = false) : base(oneTimeUse)
        {
        }

        public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8) => Invoke(action => action(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
    }

    /// <summary>
    /// Global loader events. MelonAdapter dispatches these on the Unity main thread before the
    /// corresponding per-melon overrides, matching the lifecycle documented in spec/LIFECYCLE.md.
    /// </summary>
    public static class MelonEvents
    {
        public static MelonEvent OnPreInitialization = new MelonEvent();
        public static MelonEvent OnApplicationEarlyStart = new MelonEvent();
        public static MelonEvent OnPreSupportModule = new MelonEvent();
        public static MelonEvent OnApplicationStart = new MelonEvent();
        public static MelonEvent OnApplicationLateStart = new MelonEvent();
        public static MelonEvent OnApplicationDefiniteQuit = new MelonEvent();
        public static MelonEvent OnApplicationQuit = new MelonEvent();
        public static MelonEvent OnUpdate = new MelonEvent();
        public static MelonEvent OnFixedUpdate = new MelonEvent();
        public static MelonEvent OnLateUpdate = new MelonEvent();
        public static MelonEvent OnGUI = new MelonEvent();
        public static MelonEvent<int, string> OnSceneWasLoaded = new MelonEvent<int, string>();
        public static MelonEvent<int, string> OnSceneWasInitialized = new MelonEvent<int, string>();
        public static MelonEvent<int, string> OnSceneWasUnloaded = new MelonEvent<int, string>();
        public static MelonEvent OnPreModsLoaded = new MelonEvent();

        internal static void Reset()
        {
            OnPreInitialization = new MelonEvent();
            OnApplicationEarlyStart = new MelonEvent();
            OnPreSupportModule = new MelonEvent();
            OnApplicationStart = new MelonEvent();
            OnApplicationLateStart = new MelonEvent();
            OnApplicationDefiniteQuit = new MelonEvent();
            OnApplicationQuit = new MelonEvent();
            OnUpdate = new MelonEvent();
            OnFixedUpdate = new MelonEvent();
            OnLateUpdate = new MelonEvent();
            OnGUI = new MelonEvent();
            OnSceneWasLoaded = new MelonEvent<int, string>();
            OnSceneWasInitialized = new MelonEvent<int, string>();
            OnSceneWasUnloaded = new MelonEvent<int, string>();
            OnPreModsLoaded = new MelonEvent();
        }
    }
}
