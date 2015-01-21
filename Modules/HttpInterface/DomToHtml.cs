using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VBCBBot;

namespace HttpInterface
{
    public static class DomToHtml
    {
        public static string HtmlEscape(object o, bool escapeQuotes = true, bool escapeApostrophes = false, bool hardBreaks = true)
        {
            var ret = new StringBuilder();
            var s = o.ToString();
            foreach (var cps in Util.StringToCodePointStrings(s))
            {
                var cp = Char.ConvertToUtf32(cps, 0);
                if (cp == '<')
                {
                    ret.Append("&lt;");
                }
                else if (cp == '>')
                {
                    ret.Append("&gt;");
                }
                else if (cp == '&')
                {
                    ret.Append("&amp;");
                }
                else if (escapeQuotes && cp == '"')
                {
                    ret.Append("&quot;");
                }
                else if (escapeApostrophes && cp == '\'')
                {
                    ret.Append("&apos;");
                }
                else if (cp > 0x7E)
                {
                    ret.AppendFormat("&#{0};", cp);
                }
                else if (hardBreaks && cp == '\n')
                {
                    ret.Append("<br/>\n");
                }
                else
                {
                    ret.Append(cps);
                }
            }
            return ret.ToString();
        }

        public static string JsEscapeString(object o, bool escapeQuotes = true, bool escapeApostrophes = false)
        {
            var ret = new StringBuilder();
            var s = o.ToString();
            foreach (var c in s)
            {
                if (c == '\\')
                {
                    ret.Append("\\\\");
                }
                else if (escapeQuotes && c == '"')
                {
                    ret.Append("\\\"");
                }
                else if (escapeApostrophes && c == '\'')
                {
                    ret.Append("\\'");
                }
                else
                {
                    ret.Append(c);
                }
            }
            return ret.ToString();
        }

        public static Dictionary<string, string> DecodeUrlEncodedFormData(byte[] postBytes, Encoding encoding)
        {
            var ret = new Dictionary<string, string>();

            var keyValueByteArrays = postBytes.Split(new [] {(byte)'&'});
            foreach (var keyValueByteArray in keyValueByteArrays)
            {
                var keyAndValue = keyValueByteArray.Split(new [] { (byte)'=' }, 2).ToList();
                if (keyAndValue.Count != 2)
                {
                    continue;
                }

                var key = Util.UrlDecodeToString(keyAndValue[0], Util.Utf8NoBom);
                var value = Util.UrlDecodeToString(keyAndValue[1], Util.Utf8NoBom, plusIsSpace: true);
                ret[key] = value;
            }

            return ret;
        }

        public static string Convert(IEnumerable<BBCodeDom.Node> bodyDom, Uri baseUrl)
        {
            var ret = new StringBuilder();
            foreach (var node in bodyDom)
            {
                var elem = node as BBCodeDom.Element;
                var smt = node as BBCodeDom.SmileyTextNode;
                var nwt = node as BBCodeDom.NodeWithText;

                if (elem != null)
                {
                    if (elem.Name == "url")
                    {
                        ret.AppendFormat(
                            "<a class=\"tag-url\" href=\"{0}\">{1}</a>",
                            HtmlEscape(Util.RobustUriJoin(baseUrl, elem.AttributeValue)),
                            Convert(elem.Children, baseUrl)
                        );
                    }
                    else if (elem.Name == "icon")
                    {
                        var iconUrl = string.Concat(elem.Children.Select(n =>
                            n is BBCodeDom.NodeWithText ? ((BBCodeDom.NodeWithText)n).Text : ""
                        ));
                        ret.AppendFormat(
                            "<a class=\"tag-icon iconlink\" href=\"{0}\"><img class=\"tag-icon icon\" src=\"{0}\" /></a>",
                            HtmlEscape(Util.RobustUriJoin(baseUrl, iconUrl))
                        );
                    }
                    else if (elem.Name == "b" || elem.Name == "i" || elem.Name == "u")
                    {
                        ret.AppendFormat(
                            "<{0} class=\"tag-{0}\">{1}</{0}>",
                            elem.Name,
                            Convert(elem.Children, baseUrl)
                        );
                    }
                    else if (elem.Name == "h")
                    {
                        ret.AppendFormat(
                            "<sup class=\"tag-h\">{0}</sup>",
                            Convert(elem.Children, baseUrl)
                        );
                    }
                    else if (elem.Name == "t")
                    {
                        ret.AppendFormat(
                            "<sub class=\"tag-t\">{0}</sub>",
                            Convert(elem.Children, baseUrl)
                        );
                    }
                    else if (elem.Name == "strike")
                    {
                        ret.AppendFormat(
                            "<s class=\"tag-strike\">{0}</s>",
                            Convert(elem.Children, baseUrl)
                        );
                    }
                    else if (elem.Name == "color")
                    {
                        ret.AppendFormat(
                            "<span class=\"tag-color color\" style=\"color:{0}\">{1}</span>",
                            elem.AttributeValue,
                            Convert(elem.Children, baseUrl)
                        );
                    }
                    else if (elem.Name == "spoiler")
                    {
                        ret.AppendFormat(
                            "<span class=\"tag-spoiler spoiler\">{0}</span>",
                            Convert(elem.Children, baseUrl)
                        );
                    }
                    else if (elem.Name == "noparse")
                    {
                        ret.Append(HtmlEscape(string.Concat(elem.Children.Select(c => c.BBCodeString))));
                    }
                    else if (elem.Name == "tex")
                    {
                        ret.AppendFormat(
                            "<script type=\"math/tex\">{0}</script>",
                            string.Concat(elem.Children.Select(c => c.BBCodeString))
                        );
                    }
                }
                else if (smt != null)
                {
                    ret.AppendFormat(
                        "<img class=\"smiley\" src=\"{0}\" alt=\"{1}\" title=\"{1}\" />",
                        HtmlEscape(Util.RobustUriJoin(baseUrl, smt.SmileyUrl)),
                        HtmlEscape(smt.Text)
                    );
                }
                else if (nwt != null)
                {
                    ret.Append(HtmlEscape(nwt.Text));
                }
                else
                {
                    ret.Append(HtmlEscape(node));
                }
            }
            return ret.ToString();
        }
    }
}
