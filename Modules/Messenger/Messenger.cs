using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using Messenger.ORM;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace Messenger
{
    /// <summary>
    /// Delivers messages to users when they return.
    /// </summary>
    public class Messenger : ModuleV1
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Regex MsgTrigger = new Regex("^!(s?)(?:msg|mail) (.+)$");
        private static readonly Regex DeliverTrigger = new Regex("^!deliver(?:msg|mail) 0*([0-9]+)$");
        private static readonly Regex IgnoreTrigger = new Regex("^!(?:msg|mail)(ignore|unignore) (.+)$");
        private static readonly Regex ReplayTrigger = new Regex("^!replaymsg 0*([0-9]+)$");

        private MessengerConfig _config;

        public Messenger(ChatboxConnector connector, JObject config)
            : base(connector)
        {
            _config = new MessengerConfig(config);
        }

        /// <summary>
        /// Split recipient and message on a colon boundary. Allow escaping of colons using backslashes
        /// as well as escaping backslashes by doubling.
        /// </summary>
        /// <returns>The recipient and body text of the message, split up.</returns>
        /// <param name="text">The text to escape.</param>
        public static RecipientAndMessage SplitRecipientAndMessage(string text)
        {
            var recipient = new StringBuilder();
            var body = new StringBuilder();
            var escaping = false;
            var recipientDone = false;
            foreach (var c in text)
            {
                if (recipientDone)
                {
                    body.Append(c);
                    continue;
                }

                if (escaping)
                {
                    if (":\\".IndexOf(c) == -1)
                    {
                        throw new FormatException("Invalid escape sequence: \\" + c);
                    }
                    body.Append(c);
                    escaping = false;
                    continue;
                }

                switch (c)
                {
                    case '\\':
                        escaping = true;
                        break;
                    case ':':
                        recipientDone = true;
                        break;
                    default:
                        recipient.Append(c);
                        break;
                }
            }

            if (recipientDone)
            {
                return new RecipientAndMessage
                {
                    Recipient = recipient.ToString(),
                    Message = body.ToString()
                };
            }
            // fell out of loop without a colon
            throw new FormatException("You need to put a colon between the nickname and the message!");
        }

        protected void PotentialMessageSend(ChatboxMessage message)
        {
            var body = message.BodyBBCode;
            var match = MsgTrigger.Match(body);
            if (!match.Success)
            {
                return;
            }

            var lowerSenderName = message.UserName.ToLowerInvariant();
            var recipientAndMessage = match.Groups[2].Value;
            RecipientAndMessage ram;
            try
            {
                ram = SplitRecipientAndMessage(recipientAndMessage);
            }
            catch (FormatException exc)
            {
                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: {1}",
                    message.UserName,
                    exc.Message
                ));
                return;
            }

            var targetName = Util.RemoveControlCharactersAndStrip(ram.Recipient);
            var lowerTargetName = targetName.ToLowerInvariant();
            var sendBody = Util.RemoveControlCharactersAndStrip(ram.Message);
            //var lowerSenderName = message.UserName.ToLowerInvariant();

            if (lowerTargetName.Length == 0)
            {
                Connector.SendMessage(string.Format("[noparse]{0}[/noparse]: You must specify a name to deliver to!", message.UserName));
                return;
            }
            if (sendBody.Length == 0)
            {
                Connector.SendMessage(string.Format("[noparse]{0}[/noparse]: You must specify a message to deliver!", message.UserName));
                return;
            }
            if (lowerTargetName == Connector.ForumConfig.Username.ToLowerInvariant())
            {
                Connector.SendMessage(string.Format("[noparse]{0}[/noparse]: Sorry, I don\u2019t deliver to myself!", message.UserName));
                return;
            }

            UserIDAndNickname? userInfo;
            try
            {
                userInfo = Connector.UserIDAndNicknameForUncasedName(targetName);
            }
            catch (TransferException)
            {
                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: Sorry, I couldn\u2019t verify if \u201c[noparse]{1}[/noparse] exists because the forum isn\u2019t being cooperative. Please try again later!",
                    message.UserName, targetName
                ));
                return;
            }

            if (!userInfo.HasValue)
            {
                var colonInfo = "";
                if (sendBody.Contains(":"))
                {
                    colonInfo = " (You may escape colons in usernames using a backslash.)";
                }
                else if (targetName.Length > 32)
                {
                    colonInfo = " (You must place a colon between the username and the message.)";
                }
                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: Sorry, I don\u2019t know \u201c[noparse]{1}[/noparse]\u201d.{2}",
                    message.UserName, targetName, colonInfo
                ));
                return;
            }

            // check ignore list
            bool isIgnored;
            using (var ctx = GetNewContext())
            {
                isIgnored = ctx.IgnoreList.Any(il => il.SenderFolded == lowerSenderName && il.RecipientFolded == lowerTargetName);
            }

            if (isIgnored)
            {
                Logger.DebugFormat(
                    "{0} wants to send message {1} to {2}, but the recipient is ignoring the sender",
                    Util.LiteralString(message.UserName),
                    Util.LiteralString(sendBody),
                    Util.LiteralString(targetName)
                );
                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: Can\u2019t send a message to [i][noparse]{1}[/noparse][/i]\u2014they\u2019re ignoring you.",
                    message.UserName,
                        userInfo.Value.Nickname
                ));
                return;
            }

            Logger.DebugFormat(
                "{0} sending message {1} to {2}",
                Util.LiteralString(message.UserName),
                Util.LiteralString(sendBody),
                Util.LiteralString(targetName)
            );

            using (var ctx = GetNewContext())
            {
                var msg = new Message
                {
                    ID = message.ID,
                    Timestamp = message.Timestamp,
                    SenderOriginal = message.UserName,
                    RecipientFolded = lowerTargetName,
                    Body = sendBody
                };
                ctx.Messages.Add(msg);
                ctx.SaveChanges();
            }

            if (match.Groups[1].Value == "s")
            {
                // silent msg
                return;
            }

            if (lowerTargetName == lowerSenderName)
            {
                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: Talking to ourselves? Well, no skin off my back. I\u2019ll deliver your message to you right away. ;)",
                    message.UserName
                ));
            }
            else
            {
                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: Aye-aye! I\u2019ll deliver your message to [i][noparse]{1}[/noparse][/i] next time I see \u2019em!",
                    message.UserName,
                    userInfo.Value.Nickname
                ));
            }
        }

        protected void PotentialDeliverRequest(ChatboxMessage message)
        {
            var body = message.BodyBBCode;
            var match = DeliverTrigger.Match(body);
            if (!match.Success)
            {
                return;
            }

            // overflow avoidance
            if (match.Groups[1].Length > 3)
            {
                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: I am absolutely not delivering that many messages at once.",
                    message.UserName
                ));
                return;
            }
            var fetchCount = int.Parse(match.Groups[1].Value);

            // TODO
        }

        private MessengerContext GetNewContext()
        {
            var conn = Util.GetDatabaseConnection(_config);
            return new MessengerContext(conn);
        }

        protected string FormatTimestamp(long messageID, DateTime timestamp)
        {
            var timestampFormat = (_config.ArchiveLinkTemplate == null) ? "[{0}]" : "[{0}] ([url={1}]archive[/url])";
            var timestampLinkUrl = (_config.ArchiveLinkTemplate == null) ? "" : string.Format(_config.ArchiveLinkTemplate, messageID);
            return string.Format(
                timestampFormat,
                timestamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                timestampLinkUrl
            );
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (isPartOfInitialSalvo || isEdited)
            {
                return;
            }

            // TODO
        }
    }
}
