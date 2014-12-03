using System;
using System.Linq;
using HtmlAgilityPack;

namespace VBCBBot
{
    public class ChatboxMessage
    {
        public long ID { get; protected set; }
        public long UserID { get; protected set; }
        public HtmlNode UserNameNode { get; protected set; }
        public HtmlNode BodyNode { get; protected set; }
        public DateTime Timestamp { get; protected set; }
        protected HtmlDecompiler Decompiler { get; set; }

        private string _cachedUsername = null;
        private string _cachedBody = null;

        /// <summary>
        /// Initializes a new chatbox message.
        /// </summary>
        /// <param name="messageID">The ID of the message.</param>
        /// <param name="userID">The ID of the user who posted this message.</param>
        /// <param name="userNameNode">The name of the user who posted this message.</param>
        /// <param name="bodyNode">The body of the message.</param>
        /// <param name="timestamp">The time at which the message was posted..</param>
        /// <param name="decompiler">The HTML decompiler to use.</param>
        public ChatboxMessage(long messageID, long userID, HtmlNode userNameNode, HtmlNode bodyNode, DateTime? timestamp = null, HtmlDecompiler decompiler = null)
        {
            ID = messageID;
            UserID = userID;
            UserNameNode = userNameNode;
            BodyNode = bodyNode;
            Timestamp = timestamp ?? DateTime.Now;
            Decompiler = decompiler ?? new HtmlDecompiler();
        }

        /// <summary>
        /// The username of the user who posted this message.
        /// </summary>
        public string UserName
        {
            get { return _cachedUsername ?? (_cachedUsername = UserNameNode.InnerText); }
        }

        /// <summary>
        /// The BBCode DOM representation of the username of the user who posted this message.
        /// </summary>
        public BBCodeDom.Node[] UserNameDom
        {
            get
            {
                return Decompiler.DecompileHtmlNode(UserNameNode);
            }
        }

        /// <summary>
        /// The BBCode representation of the username of the user who posted this message.
        /// </summary>
        public string UserNameBBCode
        {
            get
            {
                return string.Join("", UserNameDom.Select(x => x.ToString()));
            }
        }

        /// <summary>
        /// The body text of this message.
        /// </summary>
        public string Body
        {
            get { return _cachedBody ?? (_cachedBody = BodyNode.InnerText); }
        }

        /// <summary>
        /// The BBCode DOM representation of the body of this message.
        /// </summary>
        public BBCodeDom.Node[] BodyDom
        {
            get
            {
                return Decompiler.DecompileHtmlNode(BodyNode);
            }
        }

        /// <summary>
        /// The BBCode representation of the body of this message.
        /// </summary>
        public string BodyBBCode
        {
            get
            {
                return string.Join("", BodyDom.Select(x => x.ToString()));
            }
        }
    }
}

