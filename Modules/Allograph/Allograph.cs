using System;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace Allograph
{
    public class Allograph : ModuleV1
    {
        private readonly AllographConfig _config;
        private readonly Random _random;

        public Allograph(ChatboxConnector connector, JObject config)
            : base(connector)
        {
            _config = new AllographConfig(config);
            _random = new Random();
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false,
            bool isBanned = false)
        {
            if (isPartOfInitialSalvo || isEdited || isBanned)
            {
                return;
            }

            if (message.UserName == Connector.ForumConfig.Username)
            {
                // prevent loops
                return;
            }

            var originalBody = Util.RemoveControlCharactersAndStrip(message.BodyBBCode);
            var newBody = originalBody;

            foreach (var repl in _config.Replacements)
            {
                newBody = repl.Regex.Replace(newBody, repl.ReplacementString);
            }

            if (newBody == originalBody)
            {
                return;
            }

            var thisProbabilityValue = _random.NextDouble() * 100.0;
            if (thisProbabilityValue < _config.ProbabilityPercent)
            {
                Connector.SendMessage(newBody);
            }
        }
    }
}
