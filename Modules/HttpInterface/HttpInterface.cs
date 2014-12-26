using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace HttpInterface
{
    public class HttpInterface : ModuleV1
    {
        private Responder _responder;

        public InterfaceConfig Config;
        public List<ChatboxMessage> MessageList;

        public HttpInterface(ChatboxConnector connector, JObject config)
            : base(connector)
        {
            Config = new InterfaceConfig(config);
            MessageList = new List<ChatboxMessage>();

            _responder = new Responder(this);
            _responder.Start();
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            throw new NotImplementedException();
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

