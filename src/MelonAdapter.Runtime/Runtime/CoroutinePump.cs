using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;

namespace MelonAdapter.Runtime
{
    /// <summary>
    /// Drives <see cref="MelonCoroutines"/> routines on the host's main thread. The host may install
    /// <see cref="YieldComplete"/> to translate Unity yield instructions; without it every unknown
    /// instruction simply waits one frame.
    /// </summary>
    public sealed class CoroutinePump : IAdapterCoroutineRunner
    {
        private readonly List<Handle> _handles = new List<Handle>();
        private readonly IAdapterLogSink _log;

        public CoroutinePump(IAdapterLogSink log)
        {
            _log = log;
        }

        /// <summary>Returns true once the instruction may be replaced by the next coroutine step.</summary>
        public Func<object, bool> YieldComplete { get; set; }

        public int ActiveCount => _handles.Count;

        public object Start(IEnumerator routine)
        {
            if (routine == null)
            {
                return null;
            }

            Handle handle = new Handle(routine);
            _handles.Add(handle);
            return handle;
        }

        public void Stop(object coroutineHandle)
        {
            Handle handle = coroutineHandle as Handle;
            if (handle == null)
            {
                return;
            }

            handle.Complete = true;
            _handles.Remove(handle);
        }

        public void StopAll()
        {
            foreach (Handle handle in _handles)
            {
                handle.Complete = true;
            }

            _handles.Clear();
        }

        public void Tick()
        {
            if (_handles.Count == 0)
            {
                return;
            }

            for (int i = _handles.Count - 1; i >= 0; i--)
            {
                Handle handle = _handles[i];
                if (handle.Complete)
                {
                    _handles.RemoveAt(i);
                    continue;
                }

                if (handle.Current != null)
                {
                    bool ready = YieldComplete != null && YieldComplete(handle.Current);
                    if (!ready && handle.Current is IEnumerator nested)
                    {
                        handle.Stack.Push(nested);
                        handle.Current = null;
                        continue;
                    }

                    if (!ready)
                    {
                        continue;
                    }

                    handle.Current = null;
                }

                Advance(handle);
            }
        }

        private void Advance(Handle handle)
        {
            while (true)
            {
                if (handle.Stack.Count == 0)
                {
                    handle.Complete = true;
                    return;
                }

                IEnumerator current = handle.Stack.Peek();
                bool moved;
                object produced;
                try
                {
                    moved = current.MoveNext();
                    produced = moved ? current.Current : null;
                }
                catch (Exception exception)
                {
                    _log?.Write("coroutines", AdapterLogLevel.Error, "Coroutine threw an exception and was stopped: " + exception.Message, exception);
                    handle.Complete = true;
                    return;
                }

                if (!moved)
                {
                    handle.Stack.Pop();
                    continue;
                }

                if (produced is IEnumerator nested)
                {
                    handle.Stack.Push(nested);
                    continue;
                }

                handle.Current = produced;
                return;
            }
        }

        private sealed class Handle
        {
            public Handle(IEnumerator routine)
            {
                Stack.Push(routine);
            }

            public Stack<IEnumerator> Stack { get; } = new Stack<IEnumerator>();

            public object Current { get; set; }

            public bool Complete { get; set; }
        }
    }
}
