using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace GroupPressure
{
    /// <summary>
    /// Submit to group pressure: if enough people say a specific thing in the last X messages,
    /// join in on the fray!
    /// </summary>
    public class GroupPressure : ModuleV1
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Queue<BacklogMessage> _backlog;
        private PressureConfig _config;

        public GroupPressure(ChatboxConnector connector, JObject cfg)
            : base(connector)
        {
            _backlog = new Queue<BacklogMessage>();
            _config = new PressureConfig(cfg);
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (isBanned)
            {
                return;
            }

            var body = message.BodyBBCode;
            if (body.Length == 0)
            {
                return;
            }

            // clean out the backlog
            while (_backlog.Count > _config.BacklogSize)
            {
                _backlog.Dequeue();
            }

            if (isEdited)
            {
                // find the message in the backlog and modify it
                var newBacklog = new Queue<BacklogMessage>();
                foreach (var backMessage in _backlog)
                {
                    if (backMessage.MessageID == message.ID)
                    {
                        // store the updated version
                        newBacklog.Enqueue(new BacklogMessage
                        {
                            MessageID = message.ID,
                            Sender = message.UserName,
                            Body = message.BodyBBCode
                        });
                    }
                    else
                    {
                        newBacklog.Enqueue(backMessage);
                    }
                }
                _backlog = newBacklog;
            }
            else
            {
                // simply append the message
                _backlog.Enqueue(new BacklogMessage
                {
                    MessageID = message.ID,
                    Sender = message.UserName,
                    Body = message.BodyBBCode
                });
            }

            if (isPartOfInitialSalvo)
            {
                // don't post anything just yet
                return;
            }

            // perform accounting
            var messageToSenders = new Dictionary<string, HashSet<string>>();
            foreach (var backMessage in _backlog)
            {
                if (backMessage.Sender == Connector.ForumConfig.Username)
                {
                    // this is my message -- start counting from zero, so to speak
                    messageToSenders[backMessage.Body] = new HashSet<string>();
                }
                else
                {
                    if (!messageToSenders.ContainsKey(backMessage.Body))
                    {
                        messageToSenders[backMessage.Body] = new HashSet<string>();
                    }
                    messageToSenders[backMessage.Body].Add(backMessage.Sender);
                }
            }

            foreach (var messageAndSenders in messageToSenders)
            {
                var msg = messageAndSenders.Key;
                var senders = messageAndSenders.Value;
                if (senders.Count < _config.TriggerCount)
                {
                    continue;
                }

                Logger.DebugFormat(
                    "bowing to the group pressure of ({0}) sending {1}",
                    string.Join(", ", senders.Select(s => Util.LiteralString(s))),
                    Util.LiteralString(msg)
                );

                // submit to group pressure
                Connector.SendMessage(msg);

                // fake this message into the backlog to prevent duplicates
                _backlog.Enqueue(new BacklogMessage
                {
                    MessageID = -1,
                    Sender = Connector.ForumConfig.Username,
                    Body = msg
                });
            }
        }
    }
}
