using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MelonAdapter.Runtime.Localization
{
    /// <summary>
    /// Minimal JSON reader for the language file. It is deliberately small: only the subset the adapter
    /// needs (objects, arrays, strings, numbers, booleans and null) and it never serialises anything, so
    /// the runtime does not have to ship a JSON dependency into the game.
    /// </summary>
    internal static class JsonLite
    {
        /// <summary>Parses <paramref name="text"/> and returns the value as a plain object tree.</summary>
        /// <exception cref="FormatException">The text is not valid JSON.</exception>
        public static object Parse(string text)
        {
            if (text == null)
            {
                throw new FormatException("The JSON text is null.");
            }

            Reader reader = new Reader(text);
            object value = reader.ReadValue();
            reader.SkipWhitespace();
            if (!reader.AtEnd)
            {
                throw reader.Error("Unexpected text after the document");
            }

            return value;
        }

        /// <summary>Parses <paramref name="text"/> and requires the document to be an object.</summary>
        public static Dictionary<string, object> ParseObject(string text)
        {
            Dictionary<string, object> root = Parse(text) as Dictionary<string, object>;
            if (root == null)
            {
                throw new FormatException("The JSON document must be an object.");
            }

            return root;
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _index;

            public Reader(string text)
            {
                _text = text;
            }

            public bool AtEnd => _index >= _text.Length;

            public FormatException Error(string message) =>
                new FormatException(message + " (at offset " + _index.ToString(CultureInfo.InvariantCulture) + ").");

            public void SkipWhitespace()
            {
                while (_index < _text.Length)
                {
                    char current = _text[_index];
                    if (current == ' ' || current == '\t' || current == '\r' || current == '\n' || current == '\uFEFF')
                    {
                        _index++;
                        continue;
                    }

                    break;
                }
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (AtEnd)
                {
                    throw Error("Unexpected end of the document");
                }

                char current = _text[_index];
                switch (current)
                {
                    case '{':
                        return ReadObject();
                    case '[':
                        return ReadArray();
                    case '"':
                        return ReadString();
                    case 't':
                        Expect("true");
                        return true;
                    case 'f':
                        Expect("false");
                        return false;
                    case 'n':
                        Expect("null");
                        return null;
                    default:
                        if (current == '-' || (current >= '0' && current <= '9'))
                        {
                            return ReadNumber();
                        }

                        throw Error("Unexpected character '" + current + "'");
                }
            }

            private Dictionary<string, object> ReadObject()
            {
                Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
                _index++; // '{'
                SkipWhitespace();
                if (!AtEnd && _text[_index] == '}')
                {
                    _index++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _text[_index] != '"')
                    {
                        throw Error("Expected a property name");
                    }

                    string name = ReadString();
                    SkipWhitespace();
                    if (AtEnd || _text[_index] != ':')
                    {
                        throw Error("Expected ':' after the property name '" + name + "'");
                    }

                    _index++;
                    result[name] = ReadValue();
                    SkipWhitespace();
                    if (AtEnd)
                    {
                        throw Error("Unterminated object");
                    }

                    if (_text[_index] == ',')
                    {
                        _index++;
                        continue;
                    }

                    if (_text[_index] == '}')
                    {
                        _index++;
                        return result;
                    }

                    throw Error("Expected ',' or '}'");
                }
            }

            private List<object> ReadArray()
            {
                List<object> result = new List<object>();
                _index++; // '['
                SkipWhitespace();
                if (!AtEnd && _text[_index] == ']')
                {
                    _index++;
                    return result;
                }

                while (true)
                {
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (AtEnd)
                    {
                        throw Error("Unterminated array");
                    }

                    if (_text[_index] == ',')
                    {
                        _index++;
                        continue;
                    }

                    if (_text[_index] == ']')
                    {
                        _index++;
                        return result;
                    }

                    throw Error("Expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                _index++; // opening quote
                StringBuilder builder = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                    {
                        throw Error("Unterminated string");
                    }

                    char current = _text[_index++];
                    if (current == '"')
                    {
                        return builder.ToString();
                    }

                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }

                    if (AtEnd)
                    {
                        throw Error("Unterminated escape sequence");
                    }

                    char escape = _text[_index++];
                    switch (escape)
                    {
                        case '"':
                            builder.Append('"');
                            break;
                        case '\\':
                            builder.Append('\\');
                            break;
                        case '/':
                            builder.Append('/');
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            if (_index + 4 > _text.Length)
                            {
                                throw Error("Truncated \\u escape sequence");
                            }

                            string hex = _text.Substring(_index, 4);
                            int code;
                            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            {
                                throw Error("Invalid \\u escape sequence '" + hex + "'");
                            }

                            builder.Append((char)code);
                            _index += 4;
                            break;
                        default:
                            throw Error("Unknown escape sequence '\\" + escape + "'");
                    }
                }
            }

            private double ReadNumber()
            {
                int start = _index;
                if (!AtEnd && _text[_index] == '-')
                {
                    _index++;
                }

                while (!AtEnd && _text[_index] >= '0' && _text[_index] <= '9')
                {
                    _index++;
                }

                if (!AtEnd && _text[_index] == '.')
                {
                    _index++;
                    while (!AtEnd && _text[_index] >= '0' && _text[_index] <= '9')
                    {
                        _index++;
                    }
                }

                if (!AtEnd && (_text[_index] == 'e' || _text[_index] == 'E'))
                {
                    _index++;
                    if (!AtEnd && (_text[_index] == '+' || _text[_index] == '-'))
                    {
                        _index++;
                    }

                    while (!AtEnd && _text[_index] >= '0' && _text[_index] <= '9')
                    {
                        _index++;
                    }
                }

                string number = _text.Substring(start, _index - start);
                double value;
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    throw Error("Invalid number '" + number + "'");
                }

                return value;
            }

            private void Expect(string literal)
            {
                if (_index + literal.Length > _text.Length ||
                    string.CompareOrdinal(_text, _index, literal, 0, literal.Length) != 0)
                {
                    throw Error("Expected '" + literal + "'");
                }

                _index += literal.Length;
            }
        }
    }
}
