using System.Runtime.Remoting.Messaging;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace Allograph
{
    public class Allograph : ModuleV1
    {
        private AllographConfig _config;

        public Allograph(ChatboxConnector connector, JObject config)
            : base(connector)
        {
            _config = new AllographConfig(config);
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false,
            bool isBanned = false)
        {
            if (isPartOfInitialSalvo || isEdited || isBanned)
            {
                return;
            }

            var originalBody = message.BodyBBCode;
            var newBody = originalBody;

            foreach (var repl in _config.Replacements)
            {
                newBody = repl.Regex.Replace(newBody, repl.ReplacementString);
            }

            if (newBody != originalBody)
            {
                Connector.SendMessage(newBody);
            }
        }
    }
}
