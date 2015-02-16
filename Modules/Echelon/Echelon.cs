using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        private static readonly Regex SpyDictTrigger = new Regex("^!echelon dicttrigger ([^;]+)[;]([^;]+)[;](.+)$");
        private static readonly Regex ModifySpyDictTrigger = new Regex("^!echelon moddicttrigger (0|[1-9][0-9]*)[;]([^;]+)[;]([^;]+)[;](.+)$");
        private static readonly Regex DeleteSpyDictTrigger = new Regex("^!echelon (un)?deldicttrigger (0|[1-9][0-9]*)$");

        private static readonly Regex StatsTrigger = new Regex("^!echelon incidents (.+)$");
        private static readonly Regex StatsRankTrigger = new Regex("^!echelon topincidents (.+)$");

        private readonly EchelonConfig _config;
        private readonly Dictionary<string, Regex> _regexCache;
        private readonly Dictionary<string, HashSet<string>> _wordLists;

        public Echelon(ChatboxConnector connector, JObject config)
            : base(connector)
        {
            _config = new EchelonConfig(config);
            _regexCache = new Dictionary<string, Regex>();
            _wordLists = new Dictionary<string, HashSet<string>>();

            // read the dictionaries
            foreach (var wordList in _config.WordLists)
            {
                using (var dictFile = new StreamReader(wordList, Encoding.UTF8))
                {
                    var wordSet = new HashSet<string>();

                    string line;
                    while ((line = dictFile.ReadLine()) != null)
                    {
                        var trimmedLine = line.Trim().ToLower();
                        if (trimmedLine.Length > 0)
                        {
                            wordSet.Add(trimmedLine);
                        }
                    }

                    _wordLists[wordList] = wordSet;
                }
            }
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

        protected string RemoveNonWord(string str)
        {
            var ret = new StringBuilder();
            foreach (var c in str)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (ret.Length > 0 && ret[ret.Length - 1] != ' ')
                    {
                        ret.Append(' ');
                    }
                }
                else if (char.IsLetterOrDigit(c))
                {
                    ret.Append(c);
                }
            }
            if (ret[ret.Length - 1] == ' ')
            {
                ret.Remove(ret.Length - 1, 1);
            }
            return ret.ToString();
        }

        protected void SendInsufficientRankMessage(string actualRank, string username)
        {
            Connector.SendMessage(string.Format(
                "{0} [noparse]{1}[/noparse]: Your rank is insufficient for this operation.",
                actualRank,
                username
            ));
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
            var targetLower = target.ToLowerInvariant();
            var userLevel = GetUserLevel(message.UserName);
            var salutation = userLevel.ToString();

            if (userLevel == UserLevel.Terrorist)
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: ECHELON does not provide such information to known terrorists.",
                    salutation,
                    message.UserName
                ));
                return;
            }

            long incidentCount;
            using (var ctx = GetNewContext())
            {
                incidentCount = ctx.Incidents.Where(i => !i.Expunged && i.PerpetratorName == targetLower).LongCount();
                incidentCount += ctx.DictionaryIncidents.Where(di => !di.Expunged && di.PerpetratorName == targetLower).LongCount();
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
                    target
                ));
            }
            else
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: Subject [noparse]{2}[/noparse] may or may not have caused {3} {4}.",
                    salutation,
                    message.UserName,
                    target,
                    incidentCount,
                    incidentCount == 1 ? "incident" : "incidents"
                ));
            }
        }

        /// <summary>
        /// Checks whether top-trigger statistics for a user were requested and potentially displays them.
        /// </summary>
        protected void PotentialStatsRank(ChatboxMessage message)
        {
            var rankStatsMatch = StatsRankTrigger.Match(message.BodyBBCode);
            if (!rankStatsMatch.Success)
            {
                return;
            }

            var target = rankStatsMatch.Groups[1].Value;
            var targetLower = target.ToLowerInvariant();
            var userLevel = GetUserLevel(message.UserName);
            var salutation = userLevel.ToString();

            if (userLevel == UserLevel.Terrorist)
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: ECHELON does not provide such information to known terrorists.",
                    salutation,
                    message.UserName
                ));
                return;
            }

            var triggersAndCounts = new List<TriggerAndCount>();
            using (var ctx = GetNewContext())
            {
                var triggerCounts = ctx.Incidents
                    .Where(i => !i.Expunged && i.PerpetratorName == targetLower)
                    .GroupBy(i => i.TriggerId)
                    .ToList()
                    .Select(it => new TriggerAndCount
                        {
                            TriggerString = "R " + ctx.Triggers.FirstOrDefault(t => t.Id == it.Key).Regex,
                            Count = it.LongCount()
                        })
                    .OrderByDescending(tac => tac.Count)
                    .Take(_config.RankCount);

                var dictTriggerCounts = ctx.DictionaryIncidents
                    .Where(di => !di.Expunged && di.PerpetratorName == targetLower)
                    .GroupBy(di => di.TriggerID)
                    .ToList()
                    .Select(dit => new TriggerAndCount
                        {
                            TriggerString = string.Format(
                                "D {0}->{1}",
                                ctx.DictionaryTriggers.FirstOrDefault(dt => dt.ID == dit.Key).OriginalString,
                                ctx.DictionaryTriggers.FirstOrDefault(dt => dt.ID == dit.Key).ReplacementString
                            ),
                            Count = dit.LongCount()
                        })
                    .OrderByDescending(tac => tac.Count)
                    .Take(_config.RankCount);

                triggersAndCounts.AddRange(triggerCounts);
                triggersAndCounts.AddRange(dictTriggerCounts);
            }

            if (triggersAndCounts.Count == 0)
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: No known incidents for subject [noparse]{2}[/noparse]!",
                    salutation,
                    message.UserName,
                    target
                ));
                return;
            }

            triggersAndCounts.Sort();
            triggersAndCounts.Reverse();

            var statsString = string.Join(" || ", triggersAndCounts.Take(_config.RankCount).Select(tac => string.Format("{0}\u00D7{1}", tac.Count, tac.TriggerString)));

            Connector.SendMessage(string.Format(
                "{0} [noparse]{1}[/noparse]: Top \u2264{2} incidents for subject [noparse]{3} || {4}[/noparse]",
                salutation,
                message.UserName,
                _config.RankCount,
                target,
                Util.ExpungeNoparse(statsString)
            ));
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
                SendInsufficientRankMessage(salutation, message.UserName);
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
                SendInsufficientRankMessage(salutation, message.UserName);
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
                SendInsufficientRankMessage(salutation, message.UserName);
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

        protected void PotentialDictSpy(ChatboxMessage message)
        {
            var spyDictMatch = SpyDictTrigger.Match(message.BodyBBCode);
            if (!spyDictMatch.Success)
            {
                return;
            }

            var userLevel = GetUserLevel(message.UserName);
            var salutation = userLevel.ToString();
            if (userLevel != UserLevel.Spymaster)
            {
                SendInsufficientRankMessage(salutation, message.UserName);
                return;
            }

            var wordList = spyDictMatch.Groups[1].Value.Trim();
            var origString = spyDictMatch.Groups[2].Value.Trim();
            var replString = spyDictMatch.Groups[3].Value.Trim();

            if (!_wordLists.ContainsKey(wordList))
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: Unknown word list [noparse]{2}[/noparse].",
                    salutation,
                    message.UserName,
                    Util.ExpungeNoparse(wordList)
                ));
                return;
            }

            long newDictTriggerID;
            using (var ctx = GetNewContext())
            {
                var dictTrig = new DictionaryTrigger
                {
                    OriginalString = origString,
                    ReplacementString = replString,
                    WordList = wordList,
                    SpymasterName = message.UserName
                };
                ctx.DictionaryTriggers.Add(dictTrig);
                ctx.SaveChanges();
                newDictTriggerID = dictTrig.ID;
            }

            Connector.SendMessage(string.Format(
                "{0} [noparse]{1}[/noparse]: Done (#{2}).",
                salutation,
                message.UserName,
                newDictTriggerID
            ));
        }

        protected void PotentialModifyDictSpy(ChatboxMessage message)
        {
            var modSpyDictMatch = ModifySpyDictTrigger.Match(message.BodyBBCode);
            if (!modSpyDictMatch.Success)
            {
                return;
            }

            var userLevel = GetUserLevel(message.UserName);
            var salutation = userLevel.ToString();
            if (userLevel != UserLevel.Spymaster)
            {
                SendInsufficientRankMessage(salutation, message.UserName);
                return;
            }

            var dictTriggerID = long.Parse(modSpyDictMatch.Groups[1].Value);
            var wordList = modSpyDictMatch.Groups[2].Value.Trim();
            var origString = modSpyDictMatch.Groups[3].Value.Trim();
            var replString = modSpyDictMatch.Groups[4].Value.Trim();

            if (!_wordLists.ContainsKey(wordList))
            {
                Connector.SendMessage(string.Format(
                    "{0} [noparse]{1}[/noparse]: Unknown word list [noparse]{2}[/noparse].",
                    salutation,
                    message.UserName,
                    Util.ExpungeNoparse(wordList)
                ));
                return;
            }

            using (var ctx = GetNewContext())
            {
                var dictTrig = ctx.DictionaryTriggers.FirstOrDefault(t => t.ID == dictTriggerID);
                if (dictTrig == null)
                {
                    Connector.SendMessage(string.Format(
                        "{0} [noparse]{1}[/noparse]: The dictionary trigger with this ID does not exist.",
                        salutation,
                        message.UserName
                    ));
                    return;
                }

                dictTrig.WordList = wordList;
                dictTrig.OriginalString = origString;
                dictTrig.ReplacementString = replString;
                dictTrig.SpymasterName = message.UserName;

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
        protected void PotentialDeleteDictSpy(ChatboxMessage message)
        {
            var delDictMatch = DeleteSpyDictTrigger.Match(message.BodyBBCode);
            if (!delDictMatch.Success)
            {
                return;
            }

            var userLevel = GetUserLevel(message.UserName);
            var salutation = userLevel.ToString();

            if (userLevel != UserLevel.Spymaster)
            {
                SendInsufficientRankMessage(salutation, message.UserName);
                return;
            }

            var undelete = delDictMatch.Groups[1].Success;
            var dictTriggerID = long.Parse(delDictMatch.Groups[2].Value);

            using (var ctx = GetNewContext())
            {
                var dictTrig = ctx.DictionaryTriggers.FirstOrDefault(t => t.ID == dictTriggerID);
                if (dictTrig == null)
                {
                    Connector.SendMessage(string.Format(
                        "{0} [noparse]{1}[/noparse]: The dictionary trigger with this ID does not exist.",
                        salutation,
                        message.UserName
                    ));
                    return;
                }

                if (dictTrig.Deactivated && !undelete)
                {
                    Connector.SendMessage(string.Format(
                        "{0} [noparse]{1}[/noparse]: The dictionary trigger with this ID is already deleted.",
                        salutation,
                        message.UserName
                    ));
                    return;
                }
                else if (!dictTrig.Deactivated && undelete)
                {
                    Connector.SendMessage(string.Format(
                        "{0} [noparse]{1}[/noparse]: The dictionary trigger with this ID is still active.",
                        salutation,
                        message.UserName
                    ));
                    return;
                }

                dictTrig.Deactivated = !undelete;
                dictTrig.SpymasterName = message.UserName;

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
            if (isEdited || isPartOfInitialSalvo || message.UserName == Connector.ForumConfig.Username)
            {
                return;
            }

            if (!isBanned)
            {
                PotentialStats(message);
                PotentialStatsRank(message);

                PotentialSpy(message);
                PotentialModifySpy(message);
                PotentialDeleteSpy(message);

                PotentialDictSpy(message);
                PotentialModifyDictSpy(message);
                PotentialDeleteDictSpy(message);
            }

            // spy on messages from banned users too

            var lowerSenderName = message.UserName.ToLowerInvariant();
            var bodyWords = RemoveNonWord(message.Body).Split(' ');
            using (var ctx = GetNewContext())
            {
                // standard triggers
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
                        PerpetratorName = message.UserName.ToLowerInvariant(),
                        Expunged = false
                    };
                    ctx.Incidents.Add(inc);
                }
                ctx.SaveChanges();

                // dictionary triggers
                var relevantDictTriggers = ctx.DictionaryTriggers.Where(t => !t.Deactivated);
                foreach (var dictTrigger in relevantDictTriggers)
                {
                    var wordSet = _wordLists[dictTrigger.WordList];

                    foreach (var word in bodyWords)
                    {
                        var lowercaseWord = word.ToLower();

                        // word must contain the "from" substring
                        if (lowercaseWord.IndexOf(dictTrigger.OriginalString) == -1)
                        {
                            // doesn't
                            continue;
                        }

                        // is the word in the dictionary?
                        if (wordSet.Contains(lowercaseWord))
                        {
                            // it is
                            continue;
                        }

                        // correct the word
                        var corrected = word.Replace(dictTrigger.OriginalString, dictTrigger.ReplacementString);
                        var correctedLower = corrected.ToLower();

                        // is the corrected word in the dictionary?
                        if (!wordSet.Contains(correctedLower))
                        {
                            // no
                            continue;
                        }

                        // count it as an incident
                        var dictIncident = new DictionaryIncident
                        {
                            TriggerID = dictTrigger.ID,
                            MessageID = message.ID,
                            Timestamp = DateTime.UtcNow.ToUniversalTimeForDatabase(),
                            PerpetratorName = message.UserName.ToLowerInvariant(),
                            OriginalWord = word,
                            CorrectedWord = corrected,
                            Expunged = true
                        };
                        ctx.DictionaryIncidents.Add(dictIncident);
                    }
                }
                ctx.SaveChanges();
            }
        }
    }
}
