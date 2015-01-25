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
        private static readonly Regex ModifySpyTrigger = new Regex("^!echelon modtrigger (0|[1-9][0-9]*)[;]([^;]+)[;](.+)$");
        private static readonly Regex ModifySpyAllTrigger = new Regex("^!echelon modtriggerall (0|[1-9][0-9]*)[;](.+)$");
        private static readonly Regex DeleteSpyTrigger = new Regex("^!echelon (un)?deltrigger (0|[1-9][0-9]*)$");
        private static readonly Regex StatsTrigger = new Regex("^!echelon incidents (.+)$");

        private readonly EchelonConfig _config;
        private readonly Dictionary<string, Regex> _regexCache;

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

        private UserLevel GetUserLevel(string userName)
        {
            if (_config.Spymasters.Contains(userName))
            {
                return UserLevel.Spymaster;
            }
            else if (_config.Terrorists.Contains(userName))
            {
                return UserLevel.Terrorist;
            }
            else
            {
                return UserLevel.Agent;
            }
        }

        /// <summary>
        /// Checks whether ECHELON statistics for a user were requested and potentially displays them.
        /// </summary>
        protected void PotentialStats(ChatboxMessage message)
        {
            var statsMatch = StatsTrigger.Match(message.BodyBBCode);
            if (!statsMatch.Success)
            {
                return;
            }

            var target = statsMatch.Groups[1].Value;
            var targetLower = statsMatch.Groups[1].Value.ToLowerInvariant();
            var userLevel = GetUserLevel(message.UserName);
            var salutation = userLevel.ToString();

            long incidentCount;
            using (var ctx = GetNewContext())
            {
                incidentCount = ctx.Incidents.Where(i => i.PerpetratorName == targetLower).LongCount();
            }

            if (userLevel == UserLevel.Terrorist)
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: ECHELON does not provide such information to known terrorists.",
                    salutation,
                    message.UserName
                ));
            }
            else if (_config.UsernamesToSpecialCountFormats.ContainsKey(targetLower))
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

        /// <summary>
        /// Checks whether a new ECHELON trigger is to be added and potentially does so.
        /// </summary>
        protected void PotentialSpy(ChatboxMessage message)
        {
            var spyMatch = SpyTrigger.Match(message.BodyBBCode);
            if (!spyMatch.Success)
            {
                return;
            }

            var userLevel = GetUserLevel(message.UserName);
            var salutation = userLevel.ToString();

            if (userLevel != UserLevel.Spymaster)
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: Your rank is insufficient for this operation.",
                    salutation,
                    message.UserName
                ));
                return;
            }
                    
            var username = spyMatch.Groups[1].Success ? spyMatch.Groups[1].Value.Trim().ToLowerInvariant() : null;
            var regex = spyMatch.Groups[2].Value.Trim();

            long newTriggerID;
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
                newTriggerID = trig.Id;
            }

            Connector.SendMessage(string.Format(
                "{0} [noparse]{1}[/noparse]: Done (#{2}).",
                salutation,
                message.UserName,
                newTriggerID
            ));
        }

        /// <summary>
        /// Checks whether an existing ECHELON trigger is to be modified and potentially does so.
        /// </summary>
        protected void PotentialModifySpy(ChatboxMessage message)
        {
            var modMatch = ModifySpyTrigger.Match(message.BodyBBCode);
            var modMatchAll = ModifySpyAllTrigger.Match(message.BodyBBCode);

            long triggerID;
            string triggerUsernameLower;
            string triggerRegex;

            if (modMatch.Success)
            {
                triggerID = long.Parse(modMatch.Groups[1].Value);
                triggerUsernameLower = modMatch.Groups[2].Value.Trim().ToLowerInvariant();
                triggerRegex = modMatch.Groups[3].Value.Trim();
            }
            else if (modMatchAll.Success)
            {
                triggerID = long.Parse(modMatchAll.Groups[1].Value);
                triggerUsernameLower = null;
                triggerRegex = modMatchAll.Groups[2].Value.Trim();
            }
            else
            {
                return;
            }

            var userLevel = GetUserLevel(message.UserName);
            var salutation = userLevel.ToString();

            if (userLevel != UserLevel.Spymaster)
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: Your rank is insufficient for this operation.",
                    salutation,
                    message.UserName
                ));
                return;
            }

            using (var ctx = GetNewContext())
            {
                var trig = ctx.Triggers.FirstOrDefault(t => t.Id == triggerID);
                if (trig == null)
                {
                    Connector.SendMessage(string.Format(
                        "{0} [noparse]{1}[/noparse]: The trigger with this ID does not exist.",
                        salutation,
                        message.UserName
                    ));
                    return;
                }

                trig.TargetNameLower = triggerUsernameLower;
                trig.Regex = triggerRegex;
                trig.SpymasterName = message.UserName;

                ctx.SaveChanges();
            }

            Connector.SendMessage(string.Format(
                "{0} [noparse]{1}[/noparse]: Updated.",
                salutation,
                message.UserName
            ));
        }

        /// <summary>
        /// Checks whether an existing ECHELON trigger is to be deleted or undeleted and potentially does so.
        /// </summary>
        protected void PotentialDeleteSpy(ChatboxMessage message)
        {
            var delMatch = DeleteSpyTrigger.Match(message.BodyBBCode);
            if (!delMatch.Success)
            {
                return;
            }

            var userLevel = GetUserLevel(message.UserName);
            var salutation = userLevel.ToString();

            if (userLevel != UserLevel.Spymaster)
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: Your rank is insufficient for this operation.",
                    salutation,
                    message.UserName
                ));
                return;
            }

            var undelete = delMatch.Groups[1].Success;
            var triggerID = long.Parse(delMatch.Groups[2].Value);

            using (var ctx = GetNewContext())
            {
                var trig = ctx.Triggers.FirstOrDefault(t => t.Id == triggerID);
                if (trig == null)
                {
                    Connector.SendMessage(string.Format(
                        "{0} [noparse]{1}[/noparse]: The trigger with this ID does not exist.",
                        salutation,
                        message.UserName
                    ));
                    return;
                }

                if (trig.Deactivated && !undelete)
                {
                    Connector.SendMessage(string.Format(
                        "{0} [noparse]{1}[/noparse]: The trigger with this ID is already deleted.",
                        salutation,
                        message.UserName
                    ));
                    return;
                }
                else if (!trig.Deactivated && undelete)
                {
                    Connector.SendMessage(string.Format(
                        "{0} [noparse]{1}[/noparse]: The trigger with this ID is still active.",
                        salutation,
                        message.UserName
                    ));
                    return;
                }

                trig.Deactivated = !undelete;
                trig.SpymasterName = message.UserName;

                ctx.SaveChanges();
            }

            Connector.SendMessage(string.Format(
                "{0} [noparse]{1}[/noparse]: {2}.",
                salutation,
                message.UserName,
                undelete ? "Undeleted" : "Deleted"
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
                PotentialModifySpy(message);
                PotentialDeleteSpy(message);
            }

            // spy on messages from banned users too

            var lowerSenderName = message.UserName.ToLowerInvariant();
            using (var ctx = GetNewContext())
            {
                var relevantTriggers = ctx.Triggers.Where(t => !t.Deactivated && (t.TargetNameLower == null || t.TargetNameLower == lowerSenderName));
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
                        Timestamp = DateTime.UtcNow.ToUniversalTimeForDatabase(),
                        PerpetratorName = message.UserName.ToLowerInvariant()
                    };
                    ctx.Incidents.Add(inc);
                }
                ctx.SaveChanges();
            }
        }
    }
}
