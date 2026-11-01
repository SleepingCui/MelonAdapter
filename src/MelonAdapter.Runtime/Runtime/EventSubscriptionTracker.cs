using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;

namespace MelonAdapter.Runtime
{
    /// <summary>
    /// Removes <see cref="MelonEvents"/> subscriptions that belong to a mod assembly, so a disabled
    /// mod cannot keep receiving lifecycle callbacks.
    /// </summary>
    public sealed class EventSubscriptionTracker
    {
        private static readonly string[] EventFields =
        {
            "OnPreInitialization",
            "OnApplicationEarlyStart",
            "OnPreSupportModule",
            "OnApplicationStart",
            "OnApplicationLateStart",
            "OnApplicationDefiniteQuit",
            "OnApplicationQuit",
            "OnUpdate",
            "OnFixedUpdate",
            "OnLateUpdate",
            "OnGUI",
            "OnSceneWasLoaded",
            "OnSceneWasInitialized",
            "OnSceneWasUnloaded",
            "OnPreModsLoaded"
        };

        private readonly IAdapterLogSink _log;

        public EventSubscriptionTracker(IAdapterLogSink log)
        {
            _log = log;
        }

        public int UnsubscribeAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (string fieldName in EventFields)
            {
                FieldInfo field = typeof(MelonEvents).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
                if (field == null)
                {
                    continue;
                }

                object eventInstance = field.GetValue(null);
                if (eventInstance == null)
                {
                    continue;
                }

                try
                {
                    MethodInfo getSubscribers = eventInstance.GetType().GetMethod("GetSubscribers", Type.EmptyTypes);
                    MethodInfo unsubscribe = eventInstance.GetType().GetMethod("Unsubscribe", new[] { typeof(MethodInfo), typeof(object) });
                    if (getSubscribers == null || unsubscribe == null)
                    {
                        continue;
                    }

                    // The subscriber type is nested inside MelonEventBase<T>, so it is read through
                    // reflection instead of a compile-time reference (the runtime must not depend on
                    // the facade's generic shape).
                    Array subscribers = getSubscribers.Invoke(eventInstance, null) as Array;
                    if (subscribers == null)
                    {
                        continue;
                    }

                    foreach (object subscriber in subscribers)
                    {
                        Delegate handler = ReadSubscriberDelegate(subscriber);
                        if (handler == null)
                        {
                            continue;
                        }

                        MethodInfo method = handler.Method;
                        if (method == null || method.DeclaringType == null || method.DeclaringType.Assembly != assembly)
                        {
                            continue;
                        }

                        unsubscribe.Invoke(eventInstance, new object[] { method, handler.Target });
                        removed++;
                    }
                }
                catch (Exception exception)
                {
                    _log?.Write("events", AdapterLogLevel.Warning, "Failed to prune subscriptions in MelonEvents." + fieldName + ": " + exception.Message, exception);
                }
            }

            if (removed > 0)
            {
                _log?.Write("events", AdapterLogLevel.Debug, "Removed " + removed + " event subscription(s) owned by '" + assembly.GetName().Name + "'.");
            }

            return removed;
        }

        private static Delegate ReadSubscriberDelegate(object subscriber)
        {
            if (subscriber == null)
            {
                return null;
            }

            Type type = subscriber.GetType();
            FieldInfo field = type.GetField("del", BindingFlags.Public | BindingFlags.Instance) ?? type.GetField("Subscriber", BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(subscriber) as Delegate;
            }

            PropertyInfo property = type.GetProperty("Subscriber", BindingFlags.Public | BindingFlags.Instance);
            return property == null ? null : property.GetValue(subscriber, null) as Delegate;
        }

        public int UnsubscribeAll(IEnumerable<Assembly> assemblies)
        {
            int removed = 0;
            foreach (Assembly assembly in assemblies)
            {
                removed += UnsubscribeAssembly(assembly);
            }

            return removed;
        }
    }
}
