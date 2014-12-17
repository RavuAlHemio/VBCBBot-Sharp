using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
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
        private static readonly Regex DeliverTrigger = new Regex("^!deliver(?:msg|mail) ([0-9]+)$");
        private static readonly Regex IgnoreTrigger = new Regex("^!(?:msg|mail)(ignore|unignore) (.+)$");
        private static readonly Regex ReplayTrigger = new Regex("^!replaymsg ([0-9]+)$");

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

            // TODO
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
