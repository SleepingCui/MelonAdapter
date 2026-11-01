using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace Semver
{
    /// <summary>
    /// Minimal Semantic Versioning 2.0 implementation matching the Semver package API surface that
    /// MelonLoader 0.7.3 embeds (and re-exports through MelonInfoAttribute.SemanticVersion and
    /// VerifyLoaderVersionAttribute.SemVer).
    /// </summary>
    [Serializable]
    public class SemVersion : IComparable<SemVersion>, IComparable, ISerializable
    {
        private static readonly Regex StrictPattern =
            new Regex(@"^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);

        public SemVersion(int major, int minor = 0, int patch = 0, string prerelease = "", string build = "")
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = prerelease ?? string.Empty;
            Build = build ?? string.Empty;
        }

        public SemVersion(Version version)
            : this(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build)
        {
        }

        protected SemVersion(SerializationInfo info, StreamingContext context)
        {
            Major = info.GetInt32("major");
            Minor = info.GetInt32("minor");
            Patch = info.GetInt32("patch");
            Prerelease = info.GetString("prerelease") ?? string.Empty;
            Build = info.GetString("build") ?? string.Empty;
        }

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string Prerelease { get; }
        public string Build { get; }

        public static SemVersion Parse(string version, bool strict = false)
        {
            SemVersion parsed;
            if (!TryParse(version, out parsed, strict))
            {
                throw new ArgumentException("'" + version + "' is not a valid semantic version.", "version");
            }

            return parsed;
        }

        public static bool TryParse(string version, out SemVersion semver, bool strict = false)
        {
            semver = null;
            if (string.IsNullOrEmpty(version))
            {
                return false;
            }

            string text = version.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase) && !strict)
            {
                text = text.Substring(1);
            }

            if (strict && !StrictPattern.IsMatch(text))
            {
                return false;
            }

            string build = string.Empty;
            int buildIndex = text.IndexOf('+');
            if (buildIndex >= 0)
            {
                build = text.Substring(buildIndex + 1);
                text = text.Substring(0, buildIndex);
            }

            string prerelease = string.Empty;
            int preIndex = text.IndexOf('-');
            if (preIndex >= 0)
            {
                prerelease = text.Substring(preIndex + 1);
                text = text.Substring(0, preIndex);
            }

            string[] numbers = text.Split('.');
            if (numbers.Length < 1 || numbers.Length > 4)
            {
                return false;
            }

            int[] parts = new int[3];
            for (int i = 0; i < 3; i++)
            {
                if (i >= numbers.Length)
                {
                    parts[i] = 0;
                    continue;
                }

                int value;
                if (!int.TryParse(numbers[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0)
                {
                    return false;
                }

                parts[i] = value;
            }

            semver = new SemVersion(parts[0], parts[1], parts[2], prerelease, build);
            return true;
        }

        public static bool Equals(SemVersion versionA, SemVersion versionB)
        {
            if (ReferenceEquals(versionA, versionB))
            {
                return true;
            }

            if (ReferenceEquals(versionA, null) || ReferenceEquals(versionB, null))
            {
                return false;
            }

            return versionA.CompareTo(versionB) == 0;
        }

        public static int Compare(SemVersion versionA, SemVersion versionB)
        {
            if (ReferenceEquals(versionA, versionB))
            {
                return 0;
            }

            if (ReferenceEquals(versionA, null))
            {
                return -1;
            }

            return versionA.CompareTo(versionB);
        }

        public SemVersion Change(int? major = null, int? minor = null, int? patch = null, string prerelease = null, string build = null) =>
            new SemVersion(major ?? Major, minor ?? Minor, patch ?? Patch, prerelease ?? Prerelease, build ?? Build);

        public bool PrecedenceMatches(SemVersion other) => CompareTo(other) == 0;

        public int CompareByPrecedence(SemVersion other) => CompareTo(other);

        public int CompareTo(SemVersion other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int result = Major.CompareTo(other.Major);
            if (result != 0)
            {
                return result;
            }

            result = Minor.CompareTo(other.Minor);
            if (result != 0)
            {
                return result;
            }

            result = Patch.CompareTo(other.Patch);
            if (result != 0)
            {
                return result;
            }

            return ComparePrerelease(Prerelease, other.Prerelease);
        }

        public int CompareTo(object obj) => CompareTo(obj as SemVersion);

        private static int ComparePrerelease(string left, string right)
        {
            bool leftEmpty = string.IsNullOrEmpty(left);
            bool rightEmpty = string.IsNullOrEmpty(right);
            if (leftEmpty && rightEmpty)
            {
                return 0;
            }

            if (leftEmpty)
            {
                return 1;
            }

            if (rightEmpty)
            {
                return -1;
            }

            string[] leftParts = left.Split('.');
            string[] rightParts = right.Split('.');
            int length = Math.Max(leftParts.Length, rightParts.Length);
            for (int i = 0; i < length; i++)
            {
                if (i >= leftParts.Length)
                {
                    return -1;
                }

                if (i >= rightParts.Length)
                {
                    return 1;
                }

                int leftNumber;
                int rightNumber;
                bool leftNumeric = int.TryParse(leftParts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out leftNumber);
                bool rightNumeric = int.TryParse(rightParts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out rightNumber);

                int result;
                if (leftNumeric && rightNumeric)
                {
                    result = leftNumber.CompareTo(rightNumber);
                }
                else if (leftNumeric)
                {
                    result = -1;
                }
                else if (rightNumeric)
                {
                    result = 1;
                }
                else
                {
                    result = string.Compare(leftParts[i], rightParts[i], StringComparison.Ordinal);
                }

                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        public override string ToString()
        {
            string text = Major + "." + Minor + "." + Patch;
            if (!string.IsNullOrEmpty(Prerelease))
            {
                text += "-" + Prerelease;
            }

            if (!string.IsNullOrEmpty(Build))
            {
                text += "+" + Build;
            }

            return text;
        }

        public override bool Equals(object obj) => Equals(this, obj as SemVersion);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Major * 397 ^ Minor;
                hash = hash * 397 ^ Patch;
                hash = hash * 397 ^ (Prerelease ?? string.Empty).GetHashCode();
                hash = hash * 397 ^ (Build ?? string.Empty).GetHashCode();
                return hash;
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            info.AddValue("major", Major);
            info.AddValue("minor", Minor);
            info.AddValue("patch", Patch);
            info.AddValue("prerelease", Prerelease);
            info.AddValue("build", Build);
        }

        public static implicit operator SemVersion(string version) => Parse(version);

        public static bool operator ==(SemVersion left, SemVersion right) => Equals(left, right);

        public static bool operator !=(SemVersion left, SemVersion right) => !Equals(left, right);

        public static bool operator >(SemVersion left, SemVersion right) => Compare(left, right) > 0;

        public static bool operator >=(SemVersion left, SemVersion right) => Compare(left, right) >= 0;

        public static bool operator <(SemVersion left, SemVersion right) => Compare(left, right) < 0;

        public static bool operator <=(SemVersion left, SemVersion right) => Compare(left, right) <= 0;
    }
}
