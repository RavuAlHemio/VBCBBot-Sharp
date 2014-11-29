using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VBCBBot
{
    public class HtmlDecompiler
    {
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

                ret.Add(new BBCodeDom.Element(elementTag, null, new[] { new BBCodeDom.TextNode(match.Value) }));

                lastUnmatchedStartIndex = match.Index + match.Length;
            }

            lastUnmatchedString = str.Substring(lastUnmatchedStartIndex);
            if (lastUnmatchedString.Length > 0)
            {
                ret.Add(new BBCodeDom.TextNode(lastUnmatchedString));
            }

            return ret.ToArray();
        }

        public HtmlDecompiler()
        {
        }
    }
}
