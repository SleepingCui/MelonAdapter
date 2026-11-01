using System;
using System.IO;
using System.Text;
using MelonLoader;

namespace MelonAdapter.Runtime
{
    /// <summary>
    /// Writes adapter diagnostics to the console and to <c>MelonAdapter/Logs/MelonAdapter.log</c>.
    /// </summary>
    public sealed class AdapterLog : IAdapterLogSink, IDisposable
    {
        private const long MaxLogBytes = 4 * 1024 * 1024;

        private readonly object _gate = new object();
        private readonly string _filePath;
        private StreamWriter _writer;

        public AdapterLog(string filePath, bool debugEnabled)
        {
            _filePath = filePath;
            DebugEnabled = debugEnabled;
        }

        public bool DebugEnabled { get; set; }

        public string FilePath => _filePath;

        public bool IsDebugEnabled => DebugEnabled;

        /// <summary>Optional mirror into the hosting mod loader's console (UnityModManager).</summary>
        public Action<AdapterLogLevel, string> Echo { get; set; }

        public void Write(string source, AdapterLogLevel level, string message, Exception exception = null)
        {
            if (level == AdapterLogLevel.Debug && !DebugEnabled)
            {
                return;
            }

            string line = FormatLine(source, level, message, exception);
            Action<AdapterLogLevel, string> echo = Echo;
            if (echo != null)
            {
                try
                {
                    echo(level, message);
                }
                catch (Exception)
                {
                    // The host console must not break logging.
                }
            }

            lock (_gate)
            {
                try
                {
                    Console.Error.WriteLine(line);
                }
                catch (Exception)
                {
                    // A Unity player without a console must not break logging.
                }

                WriteToFile(line);
            }
        }

        private void WriteToFile(string line)
        {
            if (string.IsNullOrEmpty(_filePath))
            {
                return;
            }

            try
            {
                if (_writer == null)
                {
                    string directory = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    RollOverIfNeeded();
                    _writer = new StreamWriter(new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false));
                    _writer.AutoFlush = true;
                }

                _writer.WriteLine(line);
            }
            catch (Exception)
            {
                // Logging must never throw into mod code.
            }
        }

        private void RollOverIfNeeded()
        {
            FileInfo info = new FileInfo(_filePath);
            if (!info.Exists || info.Length < MaxLogBytes)
            {
                return;
            }

            string previous = _filePath + ".old";
            if (File.Exists(previous))
            {
                File.Delete(previous);
            }

            File.Move(_filePath, previous);
        }

        private static string FormatLine(string source, AdapterLogLevel level, string message, Exception exception)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('[').Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append("] ");
            builder.Append('[').Append(LevelName(level)).Append("] ");
            if (!string.IsNullOrEmpty(source))
            {
                builder.Append('[').Append(source).Append("] ");
            }

            builder.Append(message ?? string.Empty);
            if (exception != null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            return builder.ToString();
        }

        private static string LevelName(AdapterLogLevel level)
        {
            switch (level)
            {
                case AdapterLogLevel.Debug: return "DEBUG";
                case AdapterLogLevel.Warning: return "WARNING";
                case AdapterLogLevel.Error: return "ERROR";
                case AdapterLogLevel.Critical: return "CRITICAL";
                default: return "INFO";
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_writer == null)
                {
                    return;
                }

                try
                {
                    _writer.Dispose();
                }
                catch (Exception)
                {
                    // ignored
                }

                _writer = null;
            }
        }
    }
}
