using System;
using MelonLoader.Logging;

namespace MelonLoader
{
    /// <summary>
    /// Static logger of the MelonLoader API baseline. Output is forwarded to the MelonAdapter log sink
    /// and mirrored to the console when no sink is attached (for example in unit tests).
    /// </summary>
    public class MelonLogger
    {
        public static ColorARGB DefaultMelonColor = ColorARGB.FromArgb(255, 100, 220, 255);
        public static ColorARGB DefaultTextColor = ColorARGB.FromArgb(255, 255, 255, 255);

        public class Instance
        {
            public Instance(string name) : this(name, DefaultMelonColor)
            {
            }

            public Instance(string name, ConsoleColor color) : this(name, ConsoleColorToColorARGB(color))
            {
            }

            public Instance(string name, ColorARGB color)
            {
                Name = name ?? "MelonLoader";
                Color = color;
            }

            public string Name { get; }
            public ColorARGB Color { get; set; }

            public void Msg(string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
            public void Msg(object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
            public void Msg(string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);
            public void Msg(ConsoleColor color, string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
            public void Msg(ConsoleColor color, object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
            public void Msg(ConsoleColor color, string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);
            public void Msg(ColorARGB color, string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
            public void Msg(ColorARGB color, object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
            public void Msg(ColorARGB color, string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);

            public void MsgPastel(string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
            public void MsgPastel(object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
            public void MsgPastel(string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);
            public void MsgPastel(ConsoleColor color, string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
            public void MsgPastel(ConsoleColor color, object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
            public void MsgPastel(ConsoleColor color, string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);
            public void MsgPastel(ColorARGB color, string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
            public void MsgPastel(ColorARGB color, object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
            public void MsgPastel(ColorARGB color, string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);

            public void Warning(string msg) => Publish(AdapterLogLevel.Warning, msg, null, null);
            public void Warning(object obj) => Publish(AdapterLogLevel.Warning, obj?.ToString(), null, null);
            public void Warning(string msg, params object[] args) => Publish(AdapterLogLevel.Warning, msg, args, null);

            public void Error(string msg) => Publish(AdapterLogLevel.Error, msg, null, null);
            public void Error(object obj) => Publish(AdapterLogLevel.Error, obj?.ToString(), null, null);
            public void Error(string msg, params object[] args) => Publish(AdapterLogLevel.Error, msg, args, null);
            public void Error(string msg, Exception exception) => Publish(AdapterLogLevel.Error, msg, null, exception);

            public void BigError(string msg) => Publish(AdapterLogLevel.Critical, msg, null, null);

            public void WriteLine(int count = 1)
            {
                for (int i = 0; i < count; i++)
                {
                    Publish(AdapterLogLevel.Info, string.Empty, null, null);
                }
            }

            public void WriteLine(ColorARGB color, int count = 1) => WriteLine(count);

            public void WriteSpacer() => Publish(AdapterLogLevel.Info, string.Empty, null, null);

            internal void Publish(AdapterLogLevel level, string message, object[] args, Exception exception)
            {
                string text = MelonLogger.Format(message, args);
                MelonLogger.RaiseCallbacks(Name, level, text);
                IAdapterLogSink sink = AdapterBridge.LogSink;
                if (sink != null)
                {
                    sink.Write(Name, level, text, exception);
                    return;
                }

                MelonLogger.WriteToConsole(Name, level, text, exception);
            }
        }

        internal static string Format(string message, object[] args)
        {
            if (message == null)
            {
                return string.Empty;
            }

            if (args == null || args.Length == 0)
            {
                return message;
            }

            try
            {
                return string.Format(message, args);
            }
            catch (FormatException)
            {
                return message + " " + string.Join(", ", Array.ConvertAll(args, a => a?.ToString() ?? "null"));
            }
        }

        // ---- log callbacks (MelonLoader 0.7.3 surface) -----------------------
        // Mods use these to mirror or filter the loader output. The signatures matter: they are the exact
        // delegate types of the API baseline, so a mod compiled against 0.7.3 binds to them.

        public static event Action<ConsoleColor, ConsoleColor, string, string> MsgCallbackHandler;
        public static event Action<ColorARGB, ColorARGB, string, string> MsgDrawingCallbackHandler;
        public static event Action<string, string> WarningCallbackHandler;
        public static event Action<string, string> ErrorCallbackHandler;

        [ThreadStatic]
        private static bool _raisingCallbacks;

        internal static void RaiseCallbacks(string source, AdapterLogLevel level, string text)
        {
            if (_raisingCallbacks)
            {
                return;
            }

            _raisingCallbacks = true;
            try
            {
                switch (level)
                {
                    case AdapterLogLevel.Warning:
                        WarningCallbackHandler?.Invoke(source, text);
                        break;
                    case AdapterLogLevel.Error:
                    case AdapterLogLevel.Critical:
                        ErrorCallbackHandler?.Invoke(source, text);
                        break;
                    default:
                        MsgCallbackHandler?.Invoke(ConsoleColor.White, ConsoleColor.White, source, text);
                        MsgDrawingCallbackHandler?.Invoke(DefaultMelonColor, DefaultTextColor, source, text);
                        break;
                }
            }
            catch (Exception exception)
            {
                WriteToConsole("MelonLoader", AdapterLogLevel.Warning, "A log callback threw: " + exception.Message + "; the callback was ignored.", null);
            }
            finally
            {
                _raisingCallbacks = false;
            }
        }

        internal static void WriteToConsole(string source, AdapterLogLevel level, string message, Exception exception)
        {
            string prefix = level == AdapterLogLevel.Critical ? "[!]" : "[" + source + "]";
            Console.WriteLine(prefix + " " + message);
            if (exception != null)
            {
                Console.WriteLine(exception);
            }
        }

        internal static void Publish(AdapterLogLevel level, string message, object[] args, Exception exception) =>
            new Instance("MelonLoader").Publish(level, message, args, exception);

        private static ColorARGB ConsoleColorToColorARGB(ConsoleColor color)
        {
            switch (color)
            {
                case ConsoleColor.Black: return ColorARGB.Black;
                case ConsoleColor.DarkBlue: return ColorARGB.DarkBlue;
                case ConsoleColor.DarkGreen: return ColorARGB.DarkGreen;
                case ConsoleColor.DarkCyan: return ColorARGB.DarkCyan;
                case ConsoleColor.DarkRed: return ColorARGB.DarkRed;
                case ConsoleColor.DarkMagenta: return ColorARGB.DarkMagenta;
                case ConsoleColor.DarkYellow: return ColorARGB.DarkGoldenrod;
                case ConsoleColor.Gray: return ColorARGB.Gray;
                case ConsoleColor.DarkGray: return ColorARGB.DarkGray;
                case ConsoleColor.Blue: return ColorARGB.Blue;
                case ConsoleColor.Green: return ColorARGB.Lime;
                case ConsoleColor.Cyan: return ColorARGB.Cyan;
                case ConsoleColor.Red: return ColorARGB.Red;
                case ConsoleColor.Magenta: return ColorARGB.Magenta;
                case ConsoleColor.Yellow: return ColorARGB.Yellow;
                default: return ColorARGB.White;
            }
        }

        // ---- static surface -------------------------------------------------

        public static void Log(object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
        public static void Log(string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
        public static void Log(string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);
        public static void Log(ConsoleColor color, string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
        public static void Log(ConsoleColor color, object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
        public static void Log(ConsoleColor color, string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);

        public static void Msg(string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
        public static void Msg(object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
        public static void Msg(string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);
        public static void Msg(ConsoleColor color, string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
        public static void Msg(ConsoleColor color, object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
        public static void Msg(ConsoleColor color, string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);
        public static void Msg(ColorARGB color, string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
        public static void Msg(ColorARGB color, object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
        public static void Msg(ColorARGB color, string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);

        public static void MsgPastel(string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
        public static void MsgPastel(object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
        public static void MsgPastel(string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);
        public static void MsgPastel(ConsoleColor color, string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
        public static void MsgPastel(ConsoleColor color, object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
        public static void MsgPastel(ConsoleColor color, string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);
        public static void MsgPastel(ColorARGB color, string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
        public static void MsgPastel(ColorARGB color, object obj) => Publish(AdapterLogLevel.Info, obj?.ToString(), null, null);
        public static void MsgPastel(ColorARGB color, string msg, params object[] args) => Publish(AdapterLogLevel.Info, msg, args, null);

        public static void MsgDirect(ColorARGB color, string msg) => Publish(AdapterLogLevel.Info, msg, null, null);
        public static void MsgPastelDirect(ColorARGB color, string msg) => Publish(AdapterLogLevel.Info, msg, null, null);

        public static void Warning(string msg) => Publish(AdapterLogLevel.Warning, msg, null, null);
        public static void Warning(object obj) => Publish(AdapterLogLevel.Warning, obj?.ToString(), null, null);
        public static void Warning(string msg, params object[] args) => Publish(AdapterLogLevel.Warning, msg, args, null);
        public static void LogWarning(string msg) => Publish(AdapterLogLevel.Warning, msg, null, null);
        public static void LogWarning(string msg, params object[] args) => Publish(AdapterLogLevel.Warning, msg, args, null);

        public static void Error(string msg) => Publish(AdapterLogLevel.Error, msg, null, null);
        public static void Error(object obj) => Publish(AdapterLogLevel.Error, obj?.ToString(), null, null);
        public static void Error(string msg, params object[] args) => Publish(AdapterLogLevel.Error, msg, args, null);
        public static void Error(string msg, Exception exception) => Publish(AdapterLogLevel.Error, msg, null, exception);
        public static void LogError(string msg) => Publish(AdapterLogLevel.Error, msg, null, null);
        public static void LogError(string msg, params object[] args) => Publish(AdapterLogLevel.Error, msg, args, null);

        public static void BigError(string author, string message) =>
            Publish(AdapterLogLevel.Critical, "[" + author + "] " + message, null, null);

        public static void WriteLine(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                Publish(AdapterLogLevel.Info, string.Empty, null, null);
            }
        }

        public static void WriteLine(ColorARGB color, int count = 1) => WriteLine(count);
    }

    /// <summary>Logger type kept for binary compatibility with mods that reference it directly.</summary>
    public class MelonModLogger : MelonLogger
    {
    }

    public static class MelonDebug
    {
        public static event Action<ConsoleColor, string> MsgCallbackHandler;

        public static event Action<string> ErrorCallbackHandler;

        public static bool IsEnabled()
        {
            IAdapterContext context = AdapterBridge.Context;
            return context != null && context.DebugLogging;
        }

        public static void Msg(string msg)
        {
            if (!IsEnabled())
            {
                return;
            }

            MelonLogger.Publish(AdapterLogLevel.Debug, "[DEBUG] " + msg, null, null);
            Raise(MsgCallbackHandler, ConsoleColor.Gray, msg);
        }

        public static void Msg(object obj) => Msg(obj?.ToString());

        public static void Msg(string msg, params object[] args) => Msg(MelonLogger.Format(msg, args));

        public static void Error(string msg)
        {
            MelonLogger.Publish(AdapterLogLevel.Error, "[DEBUG] " + msg, null, null);
            Raise(ErrorCallbackHandler, msg);
        }

        private static void Raise(Action<ConsoleColor, string> handler, ConsoleColor color, string msg)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(color, msg);
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "A MelonDebug message callback threw: " + exception.Message + "; the callback was ignored.", null, exception);
            }
        }

        private static void Raise(Action<string> handler, string msg)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(msg);
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "A MelonDebug error callback threw: " + exception.Message + "; the callback was ignored.", null, exception);
            }
        }
    }
}
