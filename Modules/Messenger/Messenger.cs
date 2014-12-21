using System;
using System.Collections.Generic;
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
        private static readonly Regex ReplayTrigger = new Regex("^!replay(?:msg|mail) 0*([0-9]+)$");

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
                    Timestamp = message.Timestamp.ToUniversalTime(),
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
            var lowerSenderName = message.UserName.ToLowerInvariant();

            List<MessageOnRetainer> messages;
            int messagesLeft;
            using (var ctx = GetNewContext())
            {
                // get the messages
                messages = ctx.MessagesOnRetainer
                    .Where(m => m.RecipientFolded == lowerSenderName)
                    .OrderBy(m => m.ID)
                    .Take(fetchCount)
                    .ToList()
                ;

                // delete them
                ctx.MessagesOnRetainer.RemoveRange(messages);
                ctx.SaveChanges();

                // check how many are left
                messagesLeft = ctx.MessagesOnRetainer
                    .Count(m => m.RecipientFolded == lowerSenderName)
                ;
            }

            // deliver them
            if (messages.Count > 0)
            {
                Connector.SendMessage(string.Format(
                    "Delivering {0} {1} for [noparse]{2}[/noparse]!",
                    messages.Count,
                    messages.Count == 1 ? "message" : "messages",
                    message.UserName
                ));
                foreach (var msg in messages)
                {
                    Logger.DebugFormat(
                        "delivering {0}'s retained message {1} to {2} as part of a chunk",
                        Util.LiteralString(msg.SenderOriginal),
                        Util.LiteralString(msg.Body),
                        Util.LiteralString(message.UserName)
                    );
                    Connector.SendMessage(string.Format(
                        "{0} <[noparse]{1}[/noparse]> {2}",
                        FormatAssumedUtcTimestamp(msg.ID, msg.Timestamp),
                        msg.SenderOriginal,
                        msg.Body
                    ));
                }
            }

            // output remaining messages count
            if (messagesLeft == 0)
            {
                if (messages.Count > 0)
                {
                    Connector.SendMessage(string.Format(
                        "[noparse]{0}[/noparse] has no more messages left to deliver!",
                        message.UserName
                    ));
                }
                else
                {
                    Connector.SendMessage(string.Format(
                        "[noparse]{0}[/noparse] has no messages to deliver!",
                        message.UserName
                    ));
                }
            }
            else
            {
                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse] has {1} {2} left to deliver!",
                    message.UserName,
                    messagesLeft,
                    (messagesLeft == 1) ? "message" : "messages"
                ));
            }
        }

        protected void PotentialReplayRequest(ChatboxMessage message)
        {
            var body = message.BodyBBCode;
            var match = ReplayTrigger.Match(body);
            if (!match.Success)
            {
                return;
            }

            if (match.Groups[1].Length > 3)
            {
                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: I am absolutely not replaying that many messages at once.",
                    message.UserName
                ));
                return;
            }

            var replayCount = int.Parse(match.Groups[1].Value);
            if (replayCount > _config.MaxMessagesToReplay)
            {
                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: I only remember a backlog of up to {1} messages.",
                    message.UserName,
                    _config.MaxMessagesToReplay
                ));
                return;
            }
            else if (replayCount == 0)
            {
                return;
            }

            var lowerSenderName = message.UserName.ToLowerInvariant();

            List<ReplayableMessage> messages;
            using (var ctx = GetNewContext())
            {
                // get the messages
                messages = ctx.ReplayableMessages
                    .Where(m => m.RecipientFolded == lowerSenderName)
                    .OrderByDescending(m => m.ID)
                    .Take(replayCount)
                    .ToList()
                ;
                messages.Reverse();
            }

            if (messages.Count == 0)
            {
                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: You have no messages to replay!",
                    message.UserName
                ));
                return;
            }
            if (messages.Count == 1)
            {
                Logger.DebugFormat("replaying a message for {0}", Util.LiteralString(message.UserName));
                Connector.SendMessage(string.Format(
                    "Replaying message for [noparse]{0}[/noparse]! {1} <[noparse]{2}[/noparse]> {3}",
                    message.UserName,
                    FormatAssumedUtcTimestamp(messages[0].ID, messages[0].Timestamp),
                    messages[0].SenderOriginal,
                    messages[0].Body
                ));
                return;
            }

            Connector.SendMessage(string.Format(
                "[noparse]{0}[/noparse]: Replaying {1} messages!",
                message.UserName,
                messages.Count
            ));
            Logger.DebugFormat(
                "replaying {0} messages for {1}",
                messages.Count,
                Util.LiteralString(message.UserName)
            );
            foreach (var msg in messages)
            {
                Connector.SendMessage(string.Format(
                    "{0} <[noparse]{1}[/noparse]> {2}!",
                    FormatAssumedUtcTimestamp(msg.ID, msg.Timestamp),
                    msg.SenderOriginal,
                    msg.Body
                ));
            }
            Connector.SendMessage(string.Format(
                "[noparse]{0}[/noparse]: Take care!",
                message.UserName
            ));
        }

        protected void PotentialIgnoreListRequest(ChatboxMessage message)
        {
            var body = message.BodyBBCode;
            var match = IgnoreTrigger.Match(body);
            if (!match.Success)
            {
                return;
            }

            var command = match.Groups[1].Value;
            var blockSender = match.Groups[2].Value.Trim();
            var blockSenderLower = blockSender.ToLowerInvariant();
            var blockRecipientLower = message.UserName.ToLowerInvariant();

            bool isIgnored;
            using (var ctx = GetNewContext())
            {
                isIgnored = ctx.IgnoreList
                    .Any(ie => ie.SenderFolded == blockSenderLower && ie.RecipientFolded == blockRecipientLower);
            }

            if (command == "ignore")
            {
                if (isIgnored)
                {
                    Connector.SendMessage(string.Format(
                        "[noparse]{0}[/noparse]: You are already ignoring [i][noparse]{1}[/noparse][/i].",
                        message.UserName,
                        blockSender
                    ));
                    return;
                }

                using (var ctx = GetNewContext())
                {
                    var entry = new IgnoreEntry
                    {
                        SenderFolded = blockSenderLower,
                        RecipientFolded = blockRecipientLower
                    };
                    ctx.IgnoreList.Add(entry);
                    ctx.SaveChanges();
                }
                Logger.DebugFormat(
                    "{0} is now ignoring {1}",
                    Util.LiteralString(message.UserName),
                    Util.LiteralString(blockSender)
                );

                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: You are now ignoring [i][noparse]{1}[/noparse][/i].",
                    message.UserName,
                    blockSender
                ));
            }
            else if (command == "unignore")
            {
                if (isIgnored)
                {
                    Connector.SendMessage(string.Format(
                        "[noparse]{0}[/noparse]: You have not been ignoring [i][noparse]{1}[/noparse][/i].",
                        message.UserName,
                        blockSender
                    ));
                    return;
                }

                using (var ctx = GetNewContext())
                {
                    var entry = ctx.IgnoreList
                        .FirstOrDefault(ie => ie.SenderFolded == blockSenderLower && ie.RecipientFolded == blockRecipientLower);
                    ctx.IgnoreList.Remove(entry);
                    ctx.SaveChanges();
                }
                Logger.DebugFormat(
                    "{0} is not ignoring {1} anymore",
                    Util.LiteralString(message.UserName),
                    Util.LiteralString(blockSender)
                );

                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: You are not ignoring [i][noparse]{1}[/noparse][/i] anymore.",
                    message.UserName,
                    blockSender
                ));
            }
        }

        private MessengerContext GetNewContext()
        {
            var conn = Util.GetDatabaseConnection(_config);
            return new MessengerContext(conn);
        }

        protected string FormatAssumedUtcTimestamp(long messageID, DateTime timestamp)
        {
            var localTime = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc).ToLocalTime();
            var timestampFormat = (_config.ArchiveLinkTemplate == null) ? "[{0}]" : "[{0}] ([url={1}]archive[/url])";
            var timestampLinkUrl = (_config.ArchiveLinkTemplate == null) ? "" : string.Format(_config.ArchiveLinkTemplate, messageID);
            return string.Format(
                timestampFormat,
                localTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                timestampLinkUrl
            );
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (isPartOfInitialSalvo || isEdited || message.UserName == Connector.ForumConfig.Username)
            {
                return;
            }

            var body = Util.RemoveControlCharactersAndStrip(message.BodyBBCode);
            var lowerNickname = message.UserName.ToLowerInvariant();

            if (!isBanned)
            {
                PotentialMessageSend(message);
                PotentialDeliverRequest(message);
                PotentialIgnoreListRequest(message);
                PotentialReplayRequest(message);
            }

            // even banned users get messages; they just can't respond to them

            if (Connector.StfuDeadline.HasValue && Connector.StfuDeadline.Value > DateTime.Now)
            {
                // don't bother just yet
                return;
            }

            // check if the sender should get any messages
            List<Message> messages;
            using (var ctx = GetNewContext())
            {
                messages = ctx.Messages
                    .Where(m => m.RecipientFolded == lowerNickname)
                    .OrderBy(m => m.ID)
                    .ToList()
                ;
                var numberMessagesOnRetainer = ctx.MessagesOnRetainer
                    .Count(m => m.RecipientFolded == lowerNickname);
                var messagesToDisplay = messages
                    .Where(m => message.ID - m.ID < 1 || message.ID - m.ID > 2)
                    .ToList()
                ;

                var retainerText = (numberMessagesOnRetainer > 0)
                    ? string.Format(" (and {0} pending !delivermsg)", numberMessagesOnRetainer)
                    : ""
                ;

                var moveToReplay = true;
                if (messagesToDisplay.Count == 0)
                {
                    // meh
                    // (don't return yet; delete the skipped "responded directly to" messages)
                }
                else if (messagesToDisplay.Count == 1)
                {
                    // one message
                    Logger.DebugFormat(
                        "delivering {0}'s message #{1} {2} to {3}",
                        Util.LiteralString(messagesToDisplay[0].SenderOriginal),
                        messagesToDisplay[0].ID,
                        Util.LiteralString(messagesToDisplay[0].Body),
                        Util.LiteralString(message.UserName)
                    );
                    Connector.SendMessage(string.Format(
                        "Message for [noparse]{0}[/noparse]{1}! {2} <[noparse]{3}[/noparse]> {4}",
                        message.UserName,
                        retainerText,
                        FormatAssumedUtcTimestamp(messagesToDisplay[0].ID, messagesToDisplay[0].Timestamp),
                        messagesToDisplay[0].SenderOriginal,
                        messagesToDisplay[0].Body
                    ));
                }
                else if (messagesToDisplay.Count >= _config.TooManyMessages)
                {
                    // use messages instead of messagesToDisplay to put all of them on retainer
                    Logger.DebugFormat(
                        "{0} got {1} messages; putting on retainer",
                        Util.LiteralString(message.UserName),
                        messages.Count
                    );
                    Connector.SendMessage(string.Format(
                        "{0} new messages for [noparse]{1}[/noparse]{2}! Use \u201c!delivermsg [i]maxnumber[/i]\u201d to get them!",
                        messages.Count,
                        message.UserName,
                        retainerText
                    ));

                    // put messages on retainer
                    ctx.MessagesOnRetainer.AddRange(messages.Select(m => new MessageOnRetainer(m)));

                    // don't replay!
                    moveToReplay = false;

                    // the content of messages will be cleaned out from ctx.Messages below
                }
                else
                {
                    // multiple but not too many messages
                    Connector.SendMessage(string.Format(
                        "{0} new messages for [noparse]{1}[/noparse]{2}!",
                        messagesToDisplay.Count,
                        message.UserName,
                        retainerText
                    ));
                    foreach (var msg in messagesToDisplay)
                    {
                        Logger.DebugFormat(
                            "delivering {0}'s message #{1} {2} to {3} as part of a chunk",
                            Util.LiteralString(msg.SenderOriginal),
                            msg.ID,
                            Util.LiteralString(msg.Body),
                            Util.LiteralString(message.UserName)
                        );
                        Connector.SendMessage(string.Format(
                            "{0} <[noparse]{1}[/noparse]> {2}",
                            FormatAssumedUtcTimestamp(msg.ID, msg.Timestamp),
                            msg.SenderOriginal,
                            msg.Body
                        ));
                    }
                    Connector.SendMessage(string.Format(
                        "[noparse]{0}[/noparse]: Have a nice day!",
                        message.UserName
                    ));
                }

                if (moveToReplay)
                {
                    // place the messages on the repeat heap
                    ctx.ReplayableMessages.AddRange(messages.Select(m => new ReplayableMessage(m)));
                }

                // purge the repeat heap if necessary
                var currentReplayables = ctx.ReplayableMessages
                    .Where(rm => rm.RecipientFolded == lowerNickname)
                    .OrderBy(rm => rm.ID)
                    .ToList()
                ;
                if (currentReplayables.Count > _config.MaxMessagesToReplay)
                {
                    var deleteCount = currentReplayables.Count - _config.MaxMessagesToReplay;
                    foreach (var oldReplayable in currentReplayables.Take(deleteCount))
                    {
                        ctx.ReplayableMessages.Remove(oldReplayable);
                    }
                }

                // remove the messages from the delivery queue
                ctx.Messages.RemoveRange(messages);

                // commit
                ctx.SaveChanges();
            }
        }
    }
}
