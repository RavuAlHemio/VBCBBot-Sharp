using System.Linq;

namespace VBCBBot
{
    /// <summary>
    /// Classes for the Bulletin Board Code (BBCode) Document Object Model (DOM).
    /// </summary>
    public static class BBCodeDom
    {
        public const string EscapedOpeningSquareBracket = "[noparse][[/noparse]";

        /// <summary>
        /// A node in the BBCode DOM.
        /// </summary>
        public abstract class Node
        {
            /// <summary>
            /// Whether this node is an element.
            /// </summary>
            public virtual bool IsElement
            {
                get { return false; }
            }

            /// <summary>
            /// Whether this node is a text node.
            /// </summary>
            public virtual bool IsText
            {
                get { return false; }
            }

            /// <summary>
            /// Whether this node has children.
            /// </summary>
            public virtual bool HasChildren
            {
                get { return false; }
            }

            /// <summary>
            /// A BBCode string representing this node, but escaped to force interpretation
            /// as a verbatim string and not as BBCode.
            /// </summary>
            public abstract string EscapedBBCodeString
            {
                get;
            }

            /// <summary>
            /// A BBCode string representing this node.
            /// </summary>
            public abstract string BBCodeString
            {
                get;
            }
        }

        public abstract class NodeWithChildren : Node
        {
            protected readonly Node[] InternalChildren;

            protected NodeWithChildren(Node[] children)
            {
                InternalChildren = (Node[])children.Clone();
            }

            public Node[] Children
            {
                get { return (Node[])InternalChildren.Clone(); }
            }

            public override bool HasChildren
            {
                get { return true; }
            }
        }

        /// <summary>
        /// A BBCode element -- a start tag and an end tag with other nodes in between.
        /// </summary>
        public class Element : NodeWithChildren
        {
            public string Name { get; protected set; }
            public string AttributeValue { get; protected set; }

            public Element(string name, Node[] children, string attributeValue = null)
                : base(children)
            {
                Name = name;
                AttributeValue = attributeValue;
            }

            public override bool IsElement
            {
                get { return true; }
            }

            public override string EscapedBBCodeString
            {
                get
                {
                    var av = (AttributeValue == null) ? "" : ("=" + AttributeValue.Replace("[", EscapedOpeningSquareBracket));
                    return string.Format(
                        "{0}{1}{2}]{3}{0}/{1}]",
                        EscapedOpeningSquareBracket,
                        Name,
                        av,
                        string.Join("", InternalChildren.Select(c => c.EscapedBBCodeString))
                    );
                }
            }

            public override string BBCodeString
            {
                get
                {
                    var av = (AttributeValue == null) ? "" : ("=" + AttributeValue);
                    return string.Format(
                        "[{0}{1}]{2}[/{0}]",
                        Name,
                        av,
                        string.Join("", InternalChildren.Select(c => c.BBCodeString))
                    );
                }
            }
        }

        public class ListItem : NodeWithChildren
        {
            public ListItem(Node[] children)
                : base(children)
            {
            }

            public override string EscapedBBCodeString
            {
                get
                {
                    return "[noparse][*][/noparse]" + string.Join("", InternalChildren.Select(c => c.EscapedBBCodeString));
                }
            }

            public override string BBCodeString
            {
                get
                {
                    return "[*]" + string.Join("", InternalChildren.Select(c => c.BBCodeString));
                }
            }
        }

        public class TextNode : Node
        {
            public string Text { get; protected set; }

            public TextNode(string text)
            {
                Text = text;
            }

            public override bool IsText
            {
                get { return true; }
            }

            public override string EscapedBBCodeString
            {
                get
                {
                    return Text.Replace("[", EscapedOpeningSquareBracket);
                }
            }

            public override string BBCodeString
            {
                get
                {
                    return Text;
                }
            }
        }

        public class SmileyTextNode : TextNode
        {
            public string SmileyUrl { get; protected set; }

            public SmileyTextNode(string smileyText, string smileyUrl)
                : base(smileyText)
            {
                SmileyUrl = smileyUrl;
            }

            public override string EscapedBBCodeString
            {
                get
                {
                    return string.Format("[noparse]{0}[/noparse]", Text);
                }
            }
        }
    }
}
