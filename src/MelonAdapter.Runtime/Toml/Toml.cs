using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MelonAdapter.Runtime.Toml
{
    /// <summary>
    /// Small, dependency-free TOML reader/writer covering the subset MelonAdapter needs:
    /// tables, dotted table headers, basic/literal strings, integers, floats, booleans and arrays.
    /// </summary>
    public static class Toml
    {
        public static TomlTable Parse(string text, out List<string> errors)
        {
            errors = new List<string>();
            TomlTable root = new TomlTable();
            TomlTable current = root;

            if (string.IsNullOrEmpty(text))
            {
                return root;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripComment(lines[i]).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '[')
                {
                    if (line[line.Length - 1] != ']')
                    {
                        errors.Add("Line " + (i + 1) + ": unterminated table header '" + line + "'.");
                        continue;
                    }

                    string header = line.Substring(1, line.Length - 2).Trim();
                    if (header.Length == 0)
                    {
                        errors.Add("Line " + (i + 1) + ": empty table header.");
                        continue;
                    }

                    current = root;
                    foreach (string part in header.Split('.'))
                    {
                        current = current.GetTable(part.Trim());
                    }

                    continue;
                }

                int separator = IndexOfUnquoted(line, '=');
                if (separator <= 0)
                {
                    errors.Add("Line " + (i + 1) + ": expected 'key = value', got '" + line + "'.");
                    continue;
                }

                string key = line.Substring(0, separator).Trim().Trim('"');
                string rawValue = line.Substring(separator + 1).Trim();
                if (key.Length == 0)
                {
                    errors.Add("Line " + (i + 1) + ": empty key.");
                    continue;
                }

                object value;
                if (!TryParseValue(rawValue, out value))
                {
                    errors.Add("Line " + (i + 1) + ": cannot parse value '" + rawValue + "' for key '" + key + "'.");
                    continue;
                }

                current.Set(key, value);
            }

            return root;
        }

        public static string FormatValue(object value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            if (value is string)
            {
                return Quote((string)value);
            }

            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }

            if (value is long || value is int)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (value is double)
            {
                double number = (double)value;
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    return "0.0";
                }

                return number.ToString("R", CultureInfo.InvariantCulture);
            }

            if (value is IEnumerable)
            {
                StringBuilder builder = new StringBuilder("[");
                bool first = true;
                foreach (object item in (IEnumerable)value)
                {
                    if (!first)
                    {
                        builder.Append(", ");
                    }

                    first = false;
                    builder.Append(FormatValue(item));
                }

                builder.Append(']');
                return builder.ToString();
            }

            return Quote(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        public static string Quote(string value)
        {
            StringBuilder builder = new StringBuilder("\"");
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default: builder.Append(c); break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        private static string StripComment(string line)
        {
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\\' && inString)
                {
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (c == '#' && !inString)
                {
                    return line.Substring(0, i);
                }
            }

            return line;
        }

        private static int IndexOfUnquoted(string line, char target)
        {
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\\' && inString)
                {
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (c == target && !inString)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryParseValue(string raw, out object value)
        {
            value = null;
            raw = raw.Trim();
            if (raw.Length == 0)
            {
                return false;
            }

            if (raw[0] == '"')
            {
                int end;
                string parsed;
                if (!TryReadString(raw, 0, out parsed, out end))
                {
                    return false;
                }

                value = parsed;
                return true;
            }

            if (raw[0] == '\'')
            {
                int end = raw.IndexOf('\'', 1);
                if (end < 0)
                {
                    return false;
                }

                value = raw.Substring(1, end - 1);
                return true;
            }

            if (raw[0] == '[')
            {
                if (raw[raw.Length - 1] != ']')
                {
                    return false;
                }

                string inner = raw.Substring(1, raw.Length - 2).Trim();
                List<object> items = new List<object>();
                if (inner.Length > 0)
                {
                    foreach (string part in SplitTopLevel(inner))
                    {
                        object item;
                        if (!TryParseValue(part, out item))
                        {
                            return false;
                        }

                        items.Add(item);
                    }
                }

                value = items;
                return true;
            }

            if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            long integer;
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
            {
                value = integer;
                return true;
            }

            double number;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                value = number;
                return true;
            }

            return false;
        }

        private static bool TryReadString(string raw, int start, out string value, out int end)
        {
            value = null;
            end = -1;
            StringBuilder builder = new StringBuilder();
            for (int i = start + 1; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '\\' && i + 1 < raw.Length)
                {
                    i++;
                    switch (raw[i])
                    {
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        default: builder.Append(raw[i]); break;
                    }

                    continue;
                }

                if (c == '"')
                {
                    value = builder.ToString();
                    end = i;
                    return true;
                }

                builder.Append(c);
            }

            return false;
        }

        private static IEnumerable<string> SplitTopLevel(string text)
        {
            int depth = 0;
            bool inString = false;
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (c == '\\')
                    {
                        i++;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        break;
                    case '[':
                        depth++;
                        break;
                    case ']':
                        depth--;
                        break;
                    case ',':
                        if (depth == 0)
                        {
                            yield return text.Substring(start, i - start).Trim();
                            start = i + 1;
                        }

                        break;
                }
            }

            yield return text.Substring(start).Trim();
        }
    }
}
