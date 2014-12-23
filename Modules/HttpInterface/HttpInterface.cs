using System;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace HttpInterface
{
    public class HttpInterface : ModuleV1
    {
        public HttpInterface(ChatboxConnector connector, JObject config)
            : base(connector)
        {
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            throw new NotImplementedException();
        }
    }
}

