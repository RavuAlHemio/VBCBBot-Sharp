using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace VBCBBot
{
    public static class Util
    {
        public static string ProgramDirectory
        {
            get
            {
                var localPath = (new Uri(Assembly.GetExecutingAssembly().CodeBase)).LocalPath;
                return Path.GetDirectoryName(localPath);
            }
        }

        /// <summary>
        /// Converts a string into Unicode code points, handling surrogate pairs gracefully.
        /// </summary>
        /// <returns>The code points.</returns>
        /// <param name="str">The string to convert to code points.</param>
        public static IEnumerable<string> StringToCodePointStrings(string str)
        {
            char precedingLeadSurrogate = (char)0;
            bool awaitingTrailSurrogate = false;

            foreach (char c in str)
            {
                if (awaitingTrailSurrogate)
                {
                    if (c >= 0xDC00 && c <= 0xDFFF)
                    {
                        // SMP code point
                        yield return new string(new [] { precedingLeadSurrogate, c });
                    }
                    else
                    {
                        // lead surrogate without trail surrogate
                        // return both independently
                        yield return precedingLeadSurrogate.ToString();
                        yield return c.ToString();
                    }

                    awaitingTrailSurrogate = false;
                }
                else if (c >= 0xD800 && c <= 0xDBFF)
                {
                    precedingLeadSurrogate = c;
                    awaitingTrailSurrogate = true;
                }
                else
                {
                    yield return c.ToString();
                }
            }
        }

        /// <summary>
        /// Returns the string as a C# string literal.
        /// </summary>
        /// <returns>The C# string literal.</returns>
        /// <param name="str">The string to return as a C# literal.</param>
        public static string LiteralString(string str)
        {
            var ret = new StringBuilder("\"");
            foreach (var pStr in StringToCodePointStrings(str))
            {
                var p = Char.ConvertToUtf32(pStr, 0);
                if (p == '\0')
                {
                    ret.Append("\\0");
                }
                else if (p == '\\')
                {
                    ret.Append("\\\\");
                }
                else if (p == '"')
                {
                    ret.Append("\\\"");
                }
                else if (p == '\a')
                {
                    ret.Append("\\a");
                }
                else if (p == '\b')
                {
                    ret.Append("\\b");
                }
                else if (p == '\f')
                {
                    ret.Append("\\f");
                }
                else if (p == '\n')
                {
                    ret.Append("\\n");
                }
                else if (p == '\r')
                {
                    ret.Append("\\r");
                }
                else if (p == '\t')
                {
                    ret.Append("\\t");
                }
                else if (p == '\v')
                {
                    ret.Append("\\v");
                }
                else if (p < ' ' || (p > '~' && p <= 0xFFFF))
                {
                    ret.AppendFormat("\\u{0:X4}", p);
                }
                else if (p > 0xFFFF)
                {
                    ret.AppendFormat("\\U{0:X8}", p);
                }
                else
                {
                    ret.Append((char)p);
                }
            }
            ret.Append('"');
            return ret.ToString();
        }
    }
}

