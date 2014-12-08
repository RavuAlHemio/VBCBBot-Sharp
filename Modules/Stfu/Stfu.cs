using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using log4net;
using Newtonsoft.Json.Linq;
using Stfu.ORM;
using VBCBBot;

namespace Stfu
{
    public class Stfu : ModuleV1
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Regex TimeRegex = new Regex(
            "^" +
            "(?:([1-9][0-9]*)w)?" +
            "(?:([1-9][0-9]*)d)?" +
            "(?:([1-9][0-9]*)h)?" +
            "(?:([1-9][0-9]*)min)?" +
            "(?:([1-9][0-9]*)s)?" +
            "$"
        );
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

        private StfuConfig _config;
        private string _whoShutMeUpLast;

        /// <summary>
        /// Returns the number of seconds described by the duration string.
        /// </summary>
        /// <returns>The number of seconds described by the duration string.</returns>
        /// <param name="durationString">The duration string to parse.</param>
        public static long DurationStringToSeconds(string durationString)
        {
            if (durationString == "forever")
            {
                return -1;
            }

            var match = TimeRegex.Match(durationString);
            long seconds = 0;

            if (!match.Success)
            {
                return seconds;
            }

            if (match.Groups[1].Success)
            {
                seconds += long.Parse(match.Groups[1].Value) * (60 * 60 * 24 * 7);
            }
            if (match.Groups[2].Success)
            {
                seconds += long.Parse(match.Groups[2].Value) * (60 * 60 * 24);
            }
            if (match.Groups[3].Success)
            {
                seconds += long.Parse(match.Groups[3].Value) * (60 * 60);
            }
            if (match.Groups[4].Success)
            {
                seconds += long.Parse(match.Groups[4].Value) * 60;
            }
            if (match.Groups[5].Success)
            {
                seconds += long.Parse(match.Groups[5].Value);
            }

            return seconds;
        }

        /// <summary>
        /// Output a snarky message.
        /// </summary>
        /// <param name="username">The username of the recipient of this snarky message.</param>
        public void SendSnark(string username)
        {
            if (_config.Snarks.Count == 0)
            {
                return;
            }

            var random = new Random();
            var index = random.Next(_config.Snarks.Count);
            var snarkyMessage = _config.Snarks[index];
            var formattedSnarkyMessage = snarkyMessage.Replace("&&USERNAME&&", username);
            Connector.SendMessage(formattedSnarkyMessage);
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (isEdited || isPartOfInitialSalvo || isBanned)
            {
                // do nothing
                return;
            }

            if (message.UserName == Connector.ForumConfig.Username)
            {
                // ignore my own messages
                return;
            }

            var body = message.BodyBBCode;
            if (body == "!stfu")
            {
                // check for ban
                using (var context = GetNewContext())
                {
                    var ban = context.Bans.Find(message.UserName);
                    if (ban != null)
                    {
                        if (!ban.Deadline.HasValue)
                        {
                            Logger.DebugFormat("{0} wants to shut me up but they're permabanned", message.UserName);
                            SendSnark(message.UserName);
                            return;
                        }
                        else if (ban.Deadline.Value > DateTime.Now)
                        {
                            Logger.DebugFormat("{0} wants to shut me up but they're banned until {1}", message.UserName, ban.Deadline.Value);
                            SendSnark(message.UserName);
                            return;
                        }
                    }

                    // no ban -- STFU
                    Logger.InfoFormat("{0} shut me up for {1} minutes", message.UserName, _config.Duration / 60);
                    _whoShutMeUpLast = message.UserName;
                    Connector.StfuDeadline = DateTime.Now.AddSeconds(_config.Duration);
                }
            }
            else if (body == "!unstfu")
            {
                if (!_config.Admins.Contains(message.UserName))
                {
                    Logger.DebugFormat("{0} wants to un-stfu me, but they aren't an admin", message.UserName);
                    return;
                }

                Logger.InfoFormat("{0} un-stfu-ed me", message.UserName);
                Connector.StfuDeadline = null;
                Connector.SendMessage("I can speak again!");
            }
            else if (body.StartsWith("!stfuban "))
            {
                if (!_config.Admins.Contains(message.UserName))
                {
                    Logger.DebugFormat("{0} wants to ban someone but they aren't an admin", message.UserName);
                    SendSnark(message.UserName);
                    return;
                }

                var bodyRest = body.Substring(("!stfuban ").Length);
                var nextSpace = bodyRest.IndexOf(' ');
                if (nextSpace == -1)
                {
                    Connector.SendMessage("Usage: !stfuban timespec username");
                    return;
                }

                var timeSpec = bodyRest.Substring(0, nextSpace);
                var banThisUser = bodyRest.Substring(nextSpace + 1);

                var seconds = DurationStringToSeconds(timeSpec);
                DateTime? deadline;
                if (seconds == 0)
                {
                    Connector.SendMessage("Invalid timespec!");
                    return;
                }
                else if (seconds == -1)
                {
                    deadline = null;
                }
                else
                {
                    deadline = DateTime.Now.AddSeconds(seconds);
                }

                // insert the ban into the database
                using (var context = GetNewContext())
                {
                    var runningBan = context.Bans.Find(banThisUser);
                    if (runningBan != null)
                    {
                        runningBan.Deadline = deadline;
                        runningBan.Banner = message.UserName;
                    }
                    else
                    {
                        runningBan = new Ban
                        {
                            BannedUser = banThisUser,
                            Deadline = deadline,
                            Banner = message.UserName
                        };
                        context.Bans.Add(runningBan);
                    }
                    context.SaveChanges();
                }

                if (_whoShutMeUpLast == banThisUser)
                {
                    // un-STFU immediately
                    Connector.StfuDeadline = null;
                }

                Logger.InfoFormat("{0} banned {1} from using !stfu for {2}", message.UserName, banThisUser, timeSpec);
                if (!deadline.HasValue)
                {
                    Connector.SendMessage(string.Format("Alright! Banning {0} from using the !stfu function.", banThisUser), bypassStfu: true);
                }
                else
                {
                    Connector.SendMessage(string.Format("Alright! Banning {0} from using the !stfu function until {1}.", banThisUser, deadline.Value), bypassStfu: true);
                }
            }
            else if (body.StartsWith("!stfuunban "))
            {
                if (!_config.Admins.Contains(message.UserName))
                {
                    Logger.DebugFormat("{0} wants to unban someone but they aren't an admin", message.UserName);
                    SendSnark(message.UserName);
                    return;
                }

                var unbanThisUser = body.Substring(("!stfuunban ").Length);

                using (var context = GetNewContext())
                {
                    var runningBan = context.Bans.Find(unbanThisUser);
                    if (runningBan == null)
                    {
                        Connector.SendMessage(string.Format("{0} isn't even banned...?", unbanThisUser));
                    }
                    else
                    {
                        context.Bans.Remove(runningBan);
                        context.SaveChanges();
                        Connector.SendMessage(string.Format("Alright, {0} may use !stfu again.", unbanThisUser));
                    }
                }
            }
        }

        private StfuContext GetNewContext()
        {
            var conn = Util.GetDatabaseConnection(_config);
            return new StfuContext(conn);
        }

        public Stfu(ChatboxConnector connector, JObject cfg)
            : base(connector)
        {
            _config = new StfuConfig(cfg);

            // clear out old bans
            using (var context = GetNewContext())
            {
                foreach (var expiredBan in context.Bans.Where(b => b.Deadline.HasValue && b.Deadline.Value < DateTime.Now))
                {
                    context.Bans.Remove(expiredBan);
                }
            }
        }
    }
}

