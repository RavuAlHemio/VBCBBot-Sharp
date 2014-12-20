using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Echelon.ORM;
using VBCBBot;
using Newtonsoft.Json.Linq;

namespace Echelon
{
    /// <summary>
    /// Not part of the NSA's ECHELON program.
    /// </summary>
    public class Echelon : ModuleV1
    {
        private static readonly Regex SpyTrigger = new Regex("^!echelon trigger(?:all | ([^;]+)[;])(.+)$");
        private static readonly Regex StatsTrigger = new Regex("^!echelon incidents (.+)$");

        private EchelonConfig _config;
        private Dictionary<string, Regex> _regexCache;

        public Echelon(ChatboxConnector connector, JObject config)
            : base(connector)
        {
            _config = new EchelonConfig(config);
            _regexCache = new Dictionary<string, Regex>();
        }

        private EchelonContext GetNewContext()
        {
            var conn = Util.GetDatabaseConnection(_config);
            return new EchelonContext(conn);
        }

        protected void PotentialStats(ChatboxMessage message)
        {
            var statsMatch = StatsTrigger.Match(message.BodyBBCode);
            if (!statsMatch.Success)
            {
                return;
            }

            var target = statsMatch.Groups[1].Value;
            var targetLower = statsMatch.Groups[1].Value.ToLowerInvariant();
            var salutation = _config.Spymasters.Contains(message.UserName) ? "Spymaster" : "Agent";

            long incidentCount;
            using (var ctx = GetNewContext())
            {
                incidentCount = ctx.Incidents.Where(i => i.PerpetratorName.ToLower() == targetLower).LongCount();
            }

            if (_config.UsernamesToSpecialCountFormats.ContainsKey(targetLower))
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: {2}",
                    salutation,
                    message.UserName,
                    string.Format(
                        _config.UsernamesToSpecialCountFormats[targetLower],
                        target,
                        incidentCount
                    )
                ));
            }
            else if (incidentCount == 0)
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: Subject [noparse]{2}[/noparse] may or may not have caused any incident.",
                    salutation,
                    message.UserName,
                    statsMatch.Groups[1].Value
                ));
            }
            else
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: Subject [noparse]{2}[/noparse] may or may not have caused {3} {4}.",
                    salutation,
                    message.UserName,
                    statsMatch.Groups[1].Value,
                    incidentCount,
                    incidentCount == 1 ? "incident" : "incidents"
                ));
            }
        }

        protected void PotentialSpy(ChatboxMessage message)
        {
            var spyMatch = SpyTrigger.Match(message.BodyBBCode);
            if (!spyMatch.Success)
            {
                return;
            }

            if (!_config.Spymasters.Contains(message.UserName))
            {
                Connector.SendMessage(string.Format(
                    "Agent [noparse]{0}[/noparse]: Your rank is insufficient for this operation.",
                    message.UserName
                ));
                return;
            }
                    
            var username = spyMatch.Groups[1].Success ? spyMatch.Groups[1].Value.Trim() : null;
            var regex = spyMatch.Groups[2].Value.Trim();

            using (var ctx = GetNewContext())
            {
                var trig = new Trigger
                {
                    TargetNameLower = username,
                    Regex = regex,
                    SpymasterName = message.UserName
                };
                ctx.Triggers.Add(trig);
                ctx.SaveChanges();
            }

            Connector.SendMessage(string.Format(
                "Spymaster [noparse]{0}[/noparse]: Done.",
                message.UserName
            ));
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (isEdited || isPartOfInitialSalvo)
            {
                return;
            }

            if (!isBanned)
            {
                PotentialStats(message);
                PotentialSpy(message);
            }

            // spy on messages from banned users too

            var lowerSenderName = message.UserName.ToLowerInvariant();
            using (var ctx = GetNewContext())
            {
                var relevantTriggers = ctx.Triggers.Where(t => t.TargetNameLower == null || t.TargetNameLower == lowerSenderName);
                foreach (var trigger in relevantTriggers)
                {
                    var re = trigger.Regex;
                    if (!_regexCache.ContainsKey(re))
                    {
                        _regexCache[re] = new Regex(re);
                    }

                    if (!_regexCache[re].IsMatch(message.BodyBBCode))
                    {
                        continue;
                    }

                    // incident!
                    var inc = new Incident
                    {
                        TriggerId = trigger.Id,
                        MessageId = message.ID,
                        Timestamp = DateTime.Now,
                        PerpetratorName = message.UserName
                    };
                    ctx.Incidents.Add(inc);
                }
                ctx.SaveChanges();
            }
        }
    }
}
