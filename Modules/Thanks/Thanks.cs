using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using Newtonsoft.Json.Linq;
using Thanks.ORM;
using VBCBBot;

namespace Thanks
{
    public class Thanks : ModuleV1
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Regex ThankRegex = new Regex("^!(?:thank|thanks|thx) (.+)$");

        private ThanksConfig _config;

        public Thanks(ChatboxConnector connector, JObject config)
            : base (connector)
        {
            _config = new ThanksConfig(config);
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (isBanned || isEdited || isPartOfInitialSalvo)
            {
                return;
            }

            if (message.UserName == Connector.ForumConfig.Username)
            {
                return;
            }

            var body = message.BodyBBCode;
            var thanksMatch = ThankRegex.Match(body);
            if (thanksMatch.Success)
            {
                var nickname = thanksMatch.Groups[1].Value.Trim();
                var lowerNickname = nickname.ToLowerInvariant();
                if (lowerNickname == message.UserName.ToLowerInvariant())
                {
                    Connector.SendMessage(string.Format(
                            "You are so full of yourself, [noparse]{0}[/noparse].",
                            message.UserName
                        ));
                    return;
                }


                UserIDAndNickname? userInfo = null;
                try
                {
                    userInfo = Connector.UserIDAndNicknameForUncasedName(nickname);
                }
                catch (TransferException)
                {
                    // never mind
                }

                if (!userInfo.HasValue)
                {
                    Connector.SendMessage(string.Format(
                            "I don't know '[noparse]{0}[/noparse]'!",
                            nickname
                        ));
                    return;
                }

                Logger.DebugFormat("{0} thanks {1}", message.UserName, nickname);

                long thankedCount = -1;
                using (var ctx = GetNewContext())
                {
                    var entry = ctx.ThanksEntries.Where(te => te.Thanker == message.UserName && te.ThankeeFolded == lowerNickname).FirstOrDefault();
                    if (entry == null)
                    {
                        entry = new ThanksEntry
                        {
                            Thanker = message.UserName,
                            ThankeeFolded = lowerNickname,
                            ThankCount = 1
                        };
                        ctx.ThanksEntries.Add(entry);
                    }
                    else
                    {
                        ++entry.ThankCount;
                    }
                    ctx.SaveChanges();

                    thankedCount = ctx.ThanksEntries.Where(te => te.ThankeeFolded == lowerNickname).Sum(te => te.ThankCount);
                }

                Connector.SendMessage(string.Format(
                        "[noparse]{0}[/noparse]: Alright! By the way, [noparse]{1}[/noparse] has been thanked {2} until now.",
                        message.UserName,
                        userInfo.Value.Nickname,
                        (thankedCount == 1) ? "once" : (thankedCount + " times")
                    ));
            }
            else if (body.StartsWith("!thanked "))
            {
                var nickname = body.Substring(("!thanked ").Length).Trim();
                var lowerNickname = nickname.ToLowerInvariant();

                UserIDAndNickname? userInfo = null;
                try
                {
                    userInfo = Connector.UserIDAndNicknameForUncasedName(nickname);
                }
                catch (TransferException)
                {
                    // never mind
                }

                if (!userInfo.HasValue)
                {
                    Connector.SendMessage(string.Format(
                            "I don't know '[noparse]{0}[/noparse]'!",
                            nickname
                        ));
                    return;
                }

                long thankedCount = -1;
                using (var ctx = GetNewContext())
                {
                    thankedCount = ctx.ThanksEntries.Where(te => te.ThankeeFolded == lowerNickname).Sum(te => te.ThankCount);
                }

                string countPhrase = null;
                bool showStats = (thankedCount != 0);

                if (thankedCount == 0)
                {
                    countPhrase = "not been thanked";
                }
                else if (thankedCount == 1)
                {
                    countPhrase = "been thanked once";
                }
                else
                {
                    countPhrase = string.Format("been thanked {0} times", thankedCount);
                }

                var statsString = "";
                if (showStats)
                {
                    List<string> mostGratefulStrings;
                    using (var ctx = GetNewContext())
                    {
                        mostGratefulStrings = ctx.ThanksEntries
                            .Where(te => te.ThankeeFolded == lowerNickname)
                            .OrderByDescending(te => te.ThankCount)
                            .Take(_config.MostGratefulCount + 1)
                            .Select(te => string.Format("[noparse]{0}[/noparse]: {1}\u00D7", te.Thanker, te.ThankCount))
                            .ToList();
                    }

                    // mention that the list is truncated if there are more than MostGratefulCount entries
                    var countString = (mostGratefulStrings.Count <= _config.MostGratefulCount) ? "" : (" " + _config.MostGratefulCountText);
                    statsString = string.Format(
                        " (Most grateful{0}: {1})",
                        countString,
                        string.Join(", ", mostGratefulStrings)
                    );
                }

                Connector.SendMessage(string.Format(
                    "[noparse]{0}: {1}[/noparse] has {2} until now.{3}",
                    message.UserName,
                    userInfo.Value.Nickname,
                    countPhrase,
                    statsString
                ));
            }
            else if (body == "!topthanked")
            {
                List<ThankeeAndCount> top;
                using (var ctx = GetNewContext())
                {
                    top = ctx.ThanksEntries
                        .GroupBy(te => te.ThankeeFolded)
                        .Select(teg => new ThankeeAndCount {
                            ThankeeFolded = teg.FirstOrDefault().ThankeeFolded,
                            ThankCount = teg.Sum(te => te.ThankCount)
                        })
                        .ToList()
                    ;
                }

                var topStrings = new List<string>();
                foreach (var thankeeAndCount in top)
                {
                    var actualUsername = thankeeAndCount.ThankeeFolded;
                    try
                    {
                        var info = Connector.UserIDAndNicknameForUncasedName(thankeeAndCount.ThankeeFolded);
                        if (info.HasValue)
                        {
                            actualUsername = info.Value.Nickname;
                        }
                    }
                    catch (TransferException)
                    {
                    }
                    topStrings.Add(string.Format("{0}: {1}", actualUsername, thankeeAndCount.ThankCount));
                }

                Connector.SendMessage(string.Format(
                    "[noparse]{0}[/noparse]: {1}",
                    message.UserName,
                    string.Join(", ", topStrings)
                ));
            }
        }

        private ThanksContext GetNewContext()
        {
            var conn = Util.GetDatabaseConnection(_config);
            return new ThanksContext(conn);
        }
    }
}

