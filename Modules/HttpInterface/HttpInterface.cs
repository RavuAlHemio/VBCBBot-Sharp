using System;
using System.Collections.Generic;
using log4net;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace HttpInterface
{
    public class HttpInterface : ModuleV1
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Responder _responder;

        internal InterfaceConfig Config;
        internal LinkedList<ChatboxMessage> MessageList;

        public HttpInterface(ChatboxConnector connector, JObject config)
            : base(connector)
        {
            Config = new InterfaceConfig(config);
            MessageList = new LinkedList<ChatboxMessage>();

            _responder = new Responder(this);
            _responder.Start();
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (isEdited)
            {
                // find and change!
                lock (MessageList)
                {
                    var node = MessageList.Find(m => m.ID == message.ID);
                    if (node == null)
                    {
                        // I guess I don't remember this one anymore
                        return;
                    }
                    node.Value = message;
                }
            }
            else
            {
                lock (MessageList)
                {
                    MessageList.AddFirst(message);
                    while (MessageList.Count > Config.Backlog)
                    {
                        MessageList.RemoveLast();
                    }
                }
            }
        }

        internal ChatboxConnector CBConnector
        {
            get
            {
                return Connector;
            }
        }
    }
}

