using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HtmlAgilityPack;

namespace VBCBBot
{
    public static class Util
    {
        public static readonly ISet<char> UrlSafeChars = new HashSet<char>("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.");
        public static readonly Encoding Utf8NoBom = new UTF8Encoding(false, true);

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
                        yield return new string(precedingLeadSurrogate, 1);
                        yield return new string(c, 1);
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
                    yield return new string(c, 1);
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
                switch (p)
                {
                    case '\0':
                        ret.Append("\\0");
                        break;
                    case '\\':
                        ret.Append("\\\\");
                        break;
                    case '"':
                        ret.Append("\\\"");
                        break;
                    case '\a':
                        ret.Append("\\a");
                        break;
                    case '\b':
                        ret.Append("\\b");
                        break;
                    case '\f':
                        ret.Append("\\f");
                        break;
                    case '\n':
                        ret.Append("\\n");
                        break;
                    case '\r':
                        ret.Append("\\r");
                        break;
                    case '\t':
                        ret.Append("\\t");
                        break;
                    case '\v':
                        ret.Append("\\v");
                        break;
                    default:
                        if (p < ' ' || (p > '~' && p <= 0xFFFF))
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
                        break;
                }
            }
            ret.Append('"');
            return ret.ToString();
        }

        public static DbConnection GetDatabaseConnection(IDatabaseModuleConfig config)
        {
            var conn = DbProviderFactories.GetFactory(config.DatabaseProvider).CreateConnection();
            conn.ConnectionString = config.DatabaseConnectionString;
            return conn;
        }

        /// <summary>
        /// URL-encodes the string.
        /// </summary>
        /// <returns>The URL-encoded string.</returns>
        /// <param name="data">The string to URL-encode.</param>
        /// <param name="charset">The charset being used.</param>
        /// <param name="spaceAsPlus">If true, encodes spaces (U+0020) as pluses (U+002B).
        /// If false, encodes spaces as the hex escape "%20".</param>
        public static string UrlEncode(string data, Encoding charset, bool spaceAsPlus = false)
        {
            var ret = new StringBuilder();
            foreach (string ps in StringToCodePointStrings(data))
            {
                if (ps.Length == 1 && UrlSafeChars.Contains(ps[0]))
                {
                    // URL-safe character
                    ret.Append(ps[0]);
                }
                else if (spaceAsPlus && ps.Length == 1 && ps[0] == ' ')
                {
                    ret.Append('+');
                }
                else
                {
                    // character in the server's encoding?
                    try
                    {
                        // URL-encode
                        foreach (var b in charset.GetBytes(ps))
                        {
                            ret.AppendFormat("%{0:X2}", (int)b);
                        }
                    }
                    catch (EncoderFallbackException)
                    {
                        // unsupported natively by the encoding; perform a URL-encoded HTML escape
                        ret.AppendFormat("%26%23{0}%3B", Char.ConvertToUtf32(ps, 0));
                    }
                }
            }

            return ret.ToString();
        }

        public static byte? DecodeHexNybble(byte c)
        {
            if (c >= '0' && c <= '9')
            {
                return (byte)(c - '0');
            }
            if (c >= 'A' && c <= 'F')
            {
                return (byte)(c - 'A' + 10);
            }
            if (c >= 'a' && c <= 'f')
            {
                return (byte)(c - 'a' + 10);
            }
            return null;
        }

        public static byte[] UrlDecode(byte[] urlEncodedBytes, bool plusIsSpace = false)
        {
            var ret = new List<byte>();
            var percentDecodingState = 0;
            byte topNybbleByte = 0;

            foreach (var b in urlEncodedBytes)
            {
                if (percentDecodingState == 1)
                {
                    // percent escape, top nybble
                    topNybbleByte = b;
                    percentDecodingState = 2;
                }
                else if (percentDecodingState == 2)
                {
                    // percent escape, bottom nybble
                    byte bottomNybbleByte = b;
                    byte? topNybble = DecodeHexNybble(topNybbleByte);
                    byte? bottomNybble = DecodeHexNybble(bottomNybbleByte);

                    if (!topNybble.HasValue || !bottomNybble.HasValue)
                    {
                        // add this "escape" verbatim
                        ret.Add((byte)'%');
                        ret.Add(topNybbleByte);
                        ret.Add(bottomNybbleByte);
                    }
                    else
                    {
                        ret.Add((byte)((topNybble.Value << 4) | bottomNybble.Value));
                    }
                    percentDecodingState = 0;
                }
                else if (b == '%')
                {
                    // start of a percent escape!
                    percentDecodingState = 1;
                }
                else if (plusIsSpace && b == '+')
                {
                    ret.Add((byte)' ');
                }
                else
                {
                    ret.Add(b);
                }
            }

            if (percentDecodingState == 1)
            {
                // string ends with "%"
                // append verbatim
                ret.Add((byte)'%');
            }
            else if (percentDecodingState == 2)
            {
                // string ends with "%x"
                // append verbatim
                ret.Add((byte)'%');
                ret.Add((byte)topNybbleByte);
            }

            return ret.ToArray();
        }

        public static string UrlDecodeToString(byte[] urlEncodedBytes, Encoding charset, bool plusIsSpace = false)
        {
            return charset.GetString(UrlDecode(urlEncodedBytes, plusIsSpace));
        }

        public static DateTime? UnixTimestampStringToLocalDateTime(string unixTimestampString)
        {
            // try parsing timestamp
            double? unixTime = Util.MaybeParseDouble(unixTimestampString);
            if (!unixTime.HasValue)
            {
                return null;
            }

            // calculate date/time
            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, CultureInfo.InvariantCulture.Calendar, DateTimeKind.Utc);
            var myTimeUTC = unixEpoch.AddSeconds(unixTime.Value);
            var myTimeLocal = myTimeUTC.ToLocalTime();
            return myTimeLocal;
        }

        public static int? MaybeParseInt(string str)
        {
            int ret;
            if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out ret))
            {
                return ret;
            }
            return null;
        }

        public static long? MaybeParseLong(string str)
        {
            long ret;
            if (long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out ret))
            {
                return ret;
            }
            return null;
        }

        public static double? MaybeParseDouble(string str)
        {
            double ret;
            if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out ret))
            {
                return ret;
            }
            return null;
        }

        public static HtmlNodeCollection SelectNodesOrEmpty(this HtmlNode node, string xpath)
        {
            return node.SelectNodes(xpath) ?? new HtmlNodeCollection(node);
        }

        /// <summary>
        /// Removes [noparse] tags from the string repeatedly until a fixed point is reached.
        /// </summary>
        /// <param name="str">The string from which to remove [noparse] tags.</param>
        /// <returns>The string without [noparse] tags.</returns>
        public static string ExpungeNoparse(string str)
        {
            string previous = null;
            var changeMe = new StringBuilder(str);
            while (previous != changeMe.ToString())
            {
                previous = changeMe.ToString();
                changeMe.Replace("[noparse]", "").Replace("[/noparse]", "");
            }
            return changeMe.ToString();
        }

        public static string RemoveControlCharactersAndStrip(string text)
        {
            var ret = new StringBuilder();
            foreach (var cp in StringToCodePointStrings(text))
            {
                if (char.GetUnicodeCategory(cp, 0) == UnicodeCategory.Control)
                {
                    continue;
                }
                ret.Append(cp);
            }
            return ret.ToString().Trim();
        }

        public static DateTime ToUniversalTimeForDatabase(this DateTime dt)
        {
            return DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Unspecified);
        }

        public static DateTime ToLocalTimeFromDatabase(this DateTime dt)
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime();
        }

        public static LinkedListNode<T> Find<T>(this LinkedList<T> list, Predicate<T> pred)
        {
            for (var node = list.First; node != null; node = node.Next)
            {
                if (pred(node.Value))
                {
                    return node;
                }
            }
            return null;
        }

        public static bool StartsWith<T>(this IList<T> haystack, IList<T> needle)
        {
            if (needle.Count > haystack.Count)
            {
                return false;
            }

            for (int i = 0; i < needle.Count; ++i)
            {
                if (!object.Equals(haystack[i], needle[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool EndsWith<T>(this IList<T> haystack, IList<T> needle)
        {
            var haystackOffset = haystack.Count - needle.Count;
            if (haystackOffset < 0)
            {
                return false;
            }

            for (int i = 0; i < needle.Count; ++i)
            {
                if (!object.Equals(haystack[haystackOffset + i], needle[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static IEnumerable<T[]> Split<T>(this IList<T> arr, IList<T> separator, int maxCount = -1)
        {
            if (maxCount == 0)
            {
                yield break;
            }

            var current = new List<T>();
            int currentCount = 1;
            bool clearance = false;

            foreach (var elem in arr)
            {
                current.Add(elem);

                if (!clearance && current.EndsWith(separator))
                {
                    current.RemoveRange(current.Count - separator.Count, separator.Count);
                    yield return current.ToArray();

                    ++currentCount;
                    if (maxCount >= 0 && currentCount >= maxCount)
                    {
                        // put all the remaining elements into one array
                        clearance = true;
                    }

                    current.Clear();
                }
            }

            // if the separator has a length, this causes an empty element to appear at the end
            if (separator.Count > 0)
            {
                yield return current.ToArray();
            }
        }
    }
}
