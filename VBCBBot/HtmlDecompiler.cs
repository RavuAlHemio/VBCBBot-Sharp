using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using HtmlAgilityPack;

namespace VBCBBot
{
    public class HtmlDecompiler
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly Regex YoutubeEmbedRegex = new Regex("^//www\\.youtube\\.com/([a-zA-Z0-9]+)\\?wmode=opaque$");

        protected Dictionary<string, string> InternalSmileyUrlToSymbol;
        protected readonly string TeXPrefix;
        protected Regex RegexForNoparse;

        public static BBCodeDom.Node[] JoinAdjacentTextNodes(IEnumerable<BBCodeDom.Node> nodeList)
        {
            var textNodesToJoin = new List<BBCodeDom.TextNode>();
            var ret = new List<BBCodeDom.Node>();
            string texts;

            foreach (var node in nodeList)
            {
                if (node is BBCodeDom.TextNode)
                {
                    textNodesToJoin.Add((BBCodeDom.TextNode)node);
                }
                else
                {
                    texts = string.Join("", textNodesToJoin.Select(n => n.Text));
                    if (texts.Length > 0)
                    {
                        ret.Add(new BBCodeDom.TextNode(texts));
                    }
                    ret.Add(node);
                    textNodesToJoin.Clear();
                }
            }

            if (textNodesToJoin.Count > 0)
            {
                texts = string.Join("", textNodesToJoin.Select(n => n.Text));
                if (texts.Length > 0)
                {
                    ret.Add(new BBCodeDom.TextNode(texts));
                }
            }

            return ret.ToArray();
        }

        public static void TrimOuterTextNodes(IList<BBCodeDom.Node> nodeList)
        {
            if (nodeList.Count > 0)
            {
                var tn = nodeList[0] as BBCodeDom.TextNode;
                if (tn != null)
                {
                    nodeList[0] = new BBCodeDom.TextNode(tn.Text.TrimStart());
                }

                var last = nodeList.Count - 1;
                var ln = nodeList[last] as BBCodeDom.TextNode;
                if (ln != null)
                {
                    nodeList[last] = new BBCodeDom.TextNode(ln.Text.TrimEnd());
                }
            }

        }

        public static BBCodeDom.Node[] IntercalateTextAndMatchesAsElement(Regex regex, string str, string elementTag = "noparse")
        {
            var ret = new List<BBCodeDom.Node>();
            int lastUnmatchedStartIndex = 0;
            string lastUnmatchedString;

            foreach (Match match in regex.Matches(str))
            {
                lastUnmatchedString = str.Substring(lastUnmatchedStartIndex, match.Index - lastUnmatchedStartIndex);
                if (lastUnmatchedString.Length > 0)
                {
                    ret.Add(new BBCodeDom.TextNode(lastUnmatchedString));
                }

                ret.Add(new BBCodeDom.Element(elementTag, new BBCodeDom.Node[] { new BBCodeDom.TextNode(match.Value) }));

                lastUnmatchedStartIndex = match.Index + match.Length;
            }

            lastUnmatchedString = str.Substring(lastUnmatchedStartIndex);
            if (lastUnmatchedString.Length > 0)
            {
                ret.Add(new BBCodeDom.TextNode(lastUnmatchedString));
            }

            return ret.ToArray();
        }

        public HtmlDecompiler(IDictionary<string, string> smileyUrlToSymbol = null, string teXPrefix = null)
        {
            SmileyUrlToSymbol = smileyUrlToSymbol == null ? new Dictionary<string, string>() : new Dictionary<string, string>(smileyUrlToSymbol);
            TeXPrefix = teXPrefix;
        }

        public IDictionary<string, string> SmileyUrlToSymbol
        {
            get
            {
                return InternalSmileyUrlToSymbol;
            }
            set
            {
                InternalSmileyUrlToSymbol = new Dictionary<string, string>(value);

                // update noparse string regex
                var regexForNoparseString = new StringBuilder();
                regexForNoparseString.Append("\\[+");
                var smileyStrings = InternalSmileyUrlToSymbol.Values.ToList();
                // warning: swapped parameters for descending sort!
                smileyStrings.Sort((r, l) =>
                    (l.Length != r.Length)
                        ? l.Length.CompareTo(r.Length)
                        : string.Compare(l, r, StringComparison.InvariantCulture)
                );
                foreach (var smileyString in smileyStrings)
                {
                    regexForNoparseString.AppendFormat("|{0}", Regex.Escape(smileyString));
                }
                RegexForNoparse = new Regex(regexForNoparseString.ToString());
            }
        }

        public BBCodeDom.Node[] DecompileHtmlNode(HtmlNode node)
        {
            if (node == null)
            {
                return new BBCodeDom.Node[0];
            }

            var ret = new List<BBCodeDom.Node>();

            foreach (var childNode in node.ChildNodes)
            {
                if (childNode.NodeType == HtmlNodeType.Element)
                {
                    if (childNode.Name == "img" && childNode.Attributes.Contains("src"))
                    {
                        var imageSource = childNode.GetAttributeValue("src", null);
                        if (SmileyUrlToSymbol.ContainsKey(imageSource))
                        {
                            // it's a smiley
                            ret.Add(new BBCodeDom.SmileyTextNode(SmileyUrlToSymbol[imageSource], imageSource));
                        }
                        else if (TeXPrefix != null && imageSource.StartsWith(TeXPrefix))
                        {
                            var texCode = imageSource.Substring(TeXPrefix.Length);
                            ret.Add(new BBCodeDom.Element("tex", new BBCodeDom.Node[] { new BBCodeDom.TextNode(texCode) }));
                        }
                        else
                        {
                            // icon?
                            ret.Add(new BBCodeDom.Element("icon", new BBCodeDom.Node[] { new BBCodeDom.TextNode(imageSource) }));
                        }
                    }
                    else if (childNode.Name == "a" && childNode.Attributes.Contains("href"))
                    {
                        var href = childNode.GetAttributeValue("href", null);
                        if (href.StartsWith("mailto:"))
                        {
                            // e-mail link
                            var address = href.Substring(("mailto:").Length);
                            ret.Add(new BBCodeDom.Element("email", DecompileHtmlNode(childNode), address));
                        }
                        else
                        {
                            var grandchildren = childNode.ChildNodes;
                            if (grandchildren.Count == 1 && grandchildren[0].NodeType == HtmlNodeType.Element && grandchildren[0].Name == "img" && grandchildren[0].GetAttributeValue("src", null) == href)
                            {
                                // this is the link around an icon -- let the img handler take care of it
                                ret.AddRange(DecompileHtmlNode(childNode));
                            }
                            else
                            {
                                // some other link -- do it manually
                                // this also catches [thread], [post], [rtfaq], [stfw] -- I decided not to special-case them
                                ret.Add(new BBCodeDom.Element("url", DecompileHtmlNode(childNode), href));
                            }
                        }
                    }
                    else if (childNode.Name == "b" || childNode.Name == "i" || childNode.Name == "u" || childNode.Name == "strike")
                    {
                        ret.Add(new BBCodeDom.Element(childNode.Name, DecompileHtmlNode(childNode)));
                    }
                    else if (childNode.Name == "sub")
                    {
                        ret.Add(new BBCodeDom.Element("t", DecompileHtmlNode(childNode)));
                    }
                    else if (childNode.Name == "sup")
                    {
                        ret.Add(new BBCodeDom.Element("h", DecompileHtmlNode(childNode)));
                    }
                    else if (childNode.Name == "font" && childNode.Attributes.Contains("color"))
                    {
                        ret.Add(new BBCodeDom.Element("color", DecompileHtmlNode(childNode), childNode.GetAttributeValue("color", null)));
                    }
                    else if (childNode.Name == "span")
                    {
                        if (childNode.Attributes.Contains("style"))
                        {
                            var style = childNode.GetAttributeValue("style", null);
                            if (style == "direction: rtl; unicode-bidi: bidi-override;")
                            {
                                ret.Add(new BBCodeDom.Element("flip", DecompileHtmlNode(childNode)));
                            }
                            else if (style.StartsWith("font-family: "))
                            {
                                var fontFamily = style.Substring(("font-family: ").Length);
                                ret.Add(new BBCodeDom.Element("font", DecompileHtmlNode(childNode), fontFamily));
                            }
                            else
                            {
                                Logger.WarnFormat("skipping span with unknown style {0}", style);
                            }
                        }
                        else if (childNode.Attributes.Contains("class"))
                        {
                            var cls = childNode.GetAttributeValue("class", null);
                            if (cls == "highlight")
                            {
                                ret.Add(new BBCodeDom.Element("highlight", DecompileHtmlNode(childNode)));
                            }
                            else if (cls == "IRONY")
                            {
                                ret.Add(new BBCodeDom.Element("irony", DecompileHtmlNode(childNode)));
                            }
                            else
                            {
                                Logger.WarnFormat("skipping span with unknown class {0}", cls);
                            }
                        }
                    }
                    else if (childNode.Name == "div")
                    {
                        if (childNode.Attributes.Contains("style"))
                        {
                            var style = childNode.GetAttributeValue("style", null);
                            if (style == "margin-left:40px")
                            {
                                ret.Add(new BBCodeDom.Element("indent", DecompileHtmlNode(childNode)));
                            }
                            else if (style == "text-align: left;")
                            {
                                ret.Add(new BBCodeDom.Element("left", DecompileHtmlNode(childNode)));
                            }
                            else if (style == "text-align: center;")
                            {
                                ret.Add(new BBCodeDom.Element("center", DecompileHtmlNode(childNode)));
                            }
                            else if (style == "text-align: right;")
                            {
                                ret.Add(new BBCodeDom.Element("right", DecompileHtmlNode(childNode)));
                            }
                            else if (style == "margin:5px; margin-top:5px;width:auto")
                            {
                                // why don't spoilers have a rational CSS class? -.-
                                var spoilerMarkerElement = childNode.SelectSingleNode("./div[@class='smallfont']");
                                if (spoilerMarkerElement != null && spoilerMarkerElement.InnerText.Contains("Spoiler"))
                                {
                                    var spoilerPreElement = childNode.SelectSingleNode("./pre[@class='alt2']");
                                    if (spoilerPreElement != null)
                                    {
                                        ret.Add(new BBCodeDom.Element("spoiler", new BBCodeDom.Node[] { new BBCodeDom.TextNode(spoilerPreElement.InnerText) }));
                                    }
                                }
                            }
                            else
                            {
                                Logger.WarnFormat("skipping div with unknown style {0}", style);
                            }
                        }
                        else if (childNode.Attributes.Contains("class"))
                        {
                            var cls = childNode.GetAttributeValue("class", null);
                            if (cls == "bbcode_container")
                            {
                                var codePre = childNode.SelectSingleNode("./pre[@class='bbcode_code']");
                                var quoteDiv = childNode.SelectSingleNode("./div[@class='bbcode_quote']");
                                if (codePre != null)
                                {
                                    // [code]
                                    // hopefully this is correct enough
                                    var codeString = string.Join("", childNode.SelectSingleNode(".//pre").InnerText);
                                    ret.Add(new BBCodeDom.Element("code", new BBCodeDom.Node[] { new BBCodeDom.TextNode(codeString) }));
                                }
                                else if (quoteDiv != null)
                                {
                                    long? postNumber = null;
                                    string posterName = null;
                                    var postedByDiv = quoteDiv.SelectSingleNode(".//div[@class='bbcode_postedby']");
                                    if (postedByDiv != null)
                                    {
                                        posterName = postedByDiv.SelectSingleNode(".//strong").InnerText;
                                        var posterLinkA = postedByDiv.SelectSingleNode(".//a[@href]");
                                        if (posterLinkA != null && posterLinkA.GetAttributeValue("href", null).StartsWith("showthread.php?p="))
                                        {
                                            var postHrefRest = posterLinkA.GetAttributeValue("href", null).Substring(("showthread.php?p=").Length);
                                            postNumber = long.Parse(postHrefRest.Substring(0, postHrefRest.IndexOf('#')));
                                        }
                                    }

                                    string quoteAttrib = null;
                                    if (posterName != null)
                                    {
                                        quoteAttrib = posterName;
                                        if (postNumber.HasValue)
                                        {
                                            quoteAttrib += ";" + postNumber;
                                        }
                                    }

                                    var messageDiv = quoteDiv.SelectSingleNode(".//div[@class='message']");
                                    ret.Add(new BBCodeDom.Element("quote", DecompileHtmlNode(messageDiv), quoteAttrib));
                                }
                                else
                                {
                                    Logger.Warn("skipping div.bbcode_container of unknown kind");
                                }
                            }
                            else
                            {
                                Logger.WarnFormat("skipping div with unknown class {0}", cls);
                            }
                        }
                        else if (childNode.Name == "ul")
                        {
                            ret.Add(new BBCodeDom.Element("list", DecompileHtmlNode(childNode)));
                        }
                        else if (childNode.Name == "ol" && childNode.GetAttributeValue("class", null) == "decimal")
                        {
                            ret.Add(new BBCodeDom.Element("list", DecompileHtmlNode(childNode), "1"));
                        }
                        else if (childNode.Name == "li" && childNode.GetAttributeValue("style", null) == "")
                        {
                            ret.Add(new BBCodeDom.ListItem(DecompileHtmlNode(childNode)));
                        }
                        else if (childNode.Name == "iframe" && childNode.Attributes.Contains("src"))
                        {
                            var match = YoutubeEmbedRegex.Match(childNode.GetAttributeValue("src", null));
                            if (match.Success)
                            {
                                // YouTube embed
                                var videoSelector = "youtube;" + match.Groups[1].Value;
                                ret.Add(new BBCodeDom.Element("video", new BBCodeDom.Node[] { new BBCodeDom.TextNode("a video") }, videoSelector));
                            }
                            else
                            {
                                Logger.WarnFormat("skipping iframe with unknown source {0}", childNode.GetAttributeValue("src", null));
                            }
                        }
                        else if (childNode.Name == "br")
                        {
                            ret.Add(new BBCodeDom.TextNode("\n"));
                        }
                        else
                        {
                            Logger.WarnFormat("skipping unknown HTML element {0}", childNode.Name);
                        }
                    }
                }
                else if (childNode.NodeType == HtmlNodeType.Text)
                {
                    // put evil stuff (opening brackets and smiley triggers) into noparse tags
                    var escapedChildren = IntercalateTextAndMatchesAsElement(RegexForNoparse, HtmlEntity.DeEntitize(childNode.InnerText), "noparse");
                    ret.AddRange(escapedChildren);
                }
            }

            var trimmedRet = JoinAdjacentTextNodes(ret);
            TrimOuterTextNodes(trimmedRet);
            return trimmedRet;
        }
    }
}
