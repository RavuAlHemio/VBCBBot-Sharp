using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace Motivator
{
    class Motivator : ModuleV1
    {
        private MotivatorConfig _config;
        private Random _random;

        public Motivator(ChatboxConnector connector, JObject config)
            : base(connector)
        {
            _config = new MotivatorConfig(config);
            _random = new Random();
        }

        protected void SendRandomMotivatorFromList(IList<string> motivatorList, ChatboxMessage requestMessage)
        {
            // pick a motivator
            var index = _random.Next(motivatorList.Count);
            var motivator = motivatorList[index];

            // personalize
            motivator = string.Format(motivator, requestMessage.UserName);

            // send
            Connector.SendMessage(string.Format("[noparse]{0}[/noparse]: {1}", requestMessage.UserName, motivator));
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (isPartOfInitialSalvo || isEdited || isBanned)
            {
                return;
            }

            var body = message.BodyBBCode.ToLowerInvariant();
            foreach (var verbCategories in _config.VerbsCategoriesMotivators)
            {
                var verbMeUsing = string.Format("!{0} me using ", verbCategories.Key);
                if (body == string.Format("!{0} me", verbCategories.Key))
                {
                    // unite the motivators from all categories
                    var motivatorList = new List<string>();
                    foreach (var motivators in verbCategories.Value.Values)
                    {
                        motivatorList.AddRange(motivators);
                    }
                    SendRandomMotivatorFromList(motivatorList, message);
                    return;
                }
                else if (body.StartsWith(verbMeUsing))
                {
                    var category = body.Substring(verbMeUsing.Length);
                    if (!verbCategories.Value.ContainsKey(category))
                    {
                        Connector.SendMessage(string.Format(
                            "[noparse]{0}[/noparse]: I don\u2019t know that category.",
                            message.UserName
                        ));
                        return;
                    }
                    var motivatorList = verbCategories.Value[category];
                    SendRandomMotivatorFromList(motivatorList, message);
                    return;
                }
                else if (body == string.Format("!how can you {0} me", verbCategories.Key))
                {
                    var categories = verbCategories.Value.Keys.ToList();
                    categories.Sort();
                    var categoriesString = string.Join(", ", categories);
                    Connector.SendMessage(string.Format(
                        "[noparse]{0}[/noparse]: I can {1} you using {2}.",
                        message.UserName, verbCategories.Key, categoriesString
                    ));
                    return;
                }
            }
        }
    }
}
