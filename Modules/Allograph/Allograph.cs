using System;
using System.Reflection;
using log4net;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace Allograph
{
    public class Allograph : ModuleV1
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
                Logger.DebugFormat("{0:F2} < {1:F2}; posting {2}", thisProbabilityValue, _config.ProbabilityPercent, newBody);
                Connector.SendMessage(newBody);
            }
            else
            {
                Logger.DebugFormat("{0:F2} >= {1:F2}; not posting {2}", thisProbabilityValue, _config.ProbabilityPercent, newBody);
            }
        }
    }
}
