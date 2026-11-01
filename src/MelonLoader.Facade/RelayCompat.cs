using System;
using MelonLoader.Logging;

namespace MelonLoader.Pastel
{
    /// <summary>
    /// MelonLoader's console colouring helpers. MelonAdapter writes plain text to its log, so the
    /// Pastel helpers return the input unchanged (or wrapped in ANSI escapes when the host console
    /// honours them, which it ignores inside Unity).
    /// </summary>
    public static class ConsoleExtensions
    {
        private static bool _enabled;

        public static void Enable() => _enabled = true;

        public static void Disable() => _enabled = false;

        public static string Pastel(string text, ColorARGB color) => _enabled ? Ansi(text, color, false) : text;

        public static string Pastel(string text, string color) => text;

        public static string PastelBg(string text, ColorARGB color) => _enabled ? Ansi(text, color, true) : text;

        public static string PastelBg(string text, string color) => text;

        private static string Ansi(string text, ColorARGB color, bool background)
        {
            if (text == null)
            {
                return null;
            }

            int prefix = background ? 48 : 38;
            return "\u001b[" + prefix + ";2;" + color.R + ";" + color.G + ";" + color.B + "m" + text + "\u001b[0m";
        }
    }
}

namespace MelonLoader.Properties
{
    /// <summary>Build information of the MelonLoader API that MelonAdapter implements.</summary>
    public static class BuildInfo
    {
        public static string Version => "0.7.3";

        public static Semver.SemVersion VersionNumber => new Semver.SemVersion(0, 7, 3);
    }
}

namespace MelonLoader
{
    /// <summary>
    /// Marker attribute that MelonLoader uses to declare a type safe against its assembly shield.
    /// MelonAdapter never rewrites mod assemblies, so the attribute only carries its metadata.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
    public class PatchShield : Attribute
    {
        public override bool Match(object obj) => obj is PatchShield;
    }
}
