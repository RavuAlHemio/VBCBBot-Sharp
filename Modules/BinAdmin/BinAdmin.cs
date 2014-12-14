using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BinAdmin.ORM;
using log4net;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace BinAdmin
{
    /// <summary>
    /// Remembers "something -> somethingTonneSomething".
    /// </summary>
    public class BinAdmin : ModuleV1
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const string SpecialRightArrows =
            "\u2192\u219d\u21a0\u21a3\u21a6\u21aa\u21ac\u21b1\u21b3\u21b7\u21c0" +
            "\u21c1\u21c9\u21d2\u21db\u21dd\u21e2\u21e5\u21e8\u21f4\u21f6\u21f8\u21fb\u21fe\u27f4" +
            "\u27f6\u27f9\u27fc\u27fe\u27ff\u2900\u2901\u2903\u2905\u2907\u290d\u290f\u2910\u2911" +
            "\u2914\u2915\u2916\u2917\u2918\u291a\u291c\u291e\u2920\u2933\u2937\u2939\u293f\u2942" +
            "\u2945\u2953\u2957\u295b\u295f\u2964\u296c\u296d\u2971\u2972\u2974\u2975\u2978\u2b43" +
            "\u2979\u2b44\u297c\u27a1\u2b0e\u2b0f\u2b46\u2b47\u2b48\u2b4c"
        ;
        private const string ArrowRegexString = "((?:[-=~>]*[>" + SpecialRightArrows + "]+[-=~>]*)+)";
        private static readonly Regex ArrowRegex = new Regex(ArrowRegexString);
        private static readonly Regex ArrowWasteBinRegex = new Regex(
            "^" + // beginning of the line
            "(.+?)" + // what to throw out
            ArrowRegexString + // arrows
            "(.*[tT][oO][nN][nN][eE].*)" + // where to throw it out
            "$" // end of the line
        );

        private BinAdminConfig _config;

        public BinAdmin(ChatboxConnector connector, JObject cfg)
            : base(connector)
        {
            _config = new BinAdminConfig(cfg);
        }

        private BinAdminContext GetNewContext()
        {
            var conn = Util.GetDatabaseConnection(_config);
            return new BinAdminContext(conn);
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (isEdited)
            {
                // don't react to edited messages
                return;
            }

            if (message.UserName.Equals(Connector.ForumConfig.Username, StringComparison.InvariantCulture))
            {
                // the bot itself may not throw things into the bin
                return;
            }

            if (isBanned || _config.Banned.Contains(message.UserName))
            {
                return;
            }

            var body = message.BodyBBCode;

            if (!body.StartsWith("!", StringComparison.InvariantCulture))
            {
                // not a bot trigger; process a possible toss
                HandlePossibleToss(message, body);
            }
            else if (isPartOfInitialSalvo)
            {
                // don't process commands that are part of the initial salvo
            }
            else if (body == "!tonnen")
            {
                Logger.DebugFormat("bin overview request from {0}", message.UserName);

                using (var context = GetNewContext())
                {
                    var bins = context.Bins.Select(b => b.BinName).ToList();

                    if (bins.Count == 0)
                    {
                        Connector.SendMessage("Ich kenne keine Tonnen.");
                    }
                    else if (bins.Count == 1)
                    {
                        Connector.SendMessage("Ich kenne folgende Tonne: '" + bins[0] + "'");
                    }
                    else
                    {
                        bins.Sort();
                        var names = string.Join(", ", bins.Select(x => "'" + x + "'"));
                        Connector.SendMessage("Ich kenne folgende Tonnen: " + names);
                    }
                }
            }
            else if (body.StartsWith("!tonneninhalt "))
            {
                var binName = body.Substring(("!tonneninhalt ").Length);
                Logger.DebugFormat("bin {0} contents request from {1}", binName, message.UserName);

                using (var context = GetNewContext())
                {
                    var bin = context.Bins.FirstOrDefault(b => b.BinName == binName);
                    if (bin == null)
                    {
                        Connector.SendMessage("Diese Tonne kenne ich nicht.");
                        return;
                    }

                    var items = context.BinItems.Where(i => i.Bin.BinName == binName).Select(i => i.Item).ToList();
                    if (items.Count == 0)
                    {
                        Connector.SendMessage("In dieser Tonne befindet sich nichts.");
                    }
                    else if (items.Count == 1)
                    {
                        Connector.SendMessage("In dieser Tonne befindet sich: " + items[0]);
                    }
                    else
                    {
                        items.Sort();
                        var itemString = string.Join(", ", items);
                        Connector.SendMessage("In dieser Tonne befinden sich: " + itemString);
                    }
                }
            }
            else if (body.StartsWith("!entleere "))
            {
                var binName = body.Substring(("!entleere ").Length);
                Logger.DebugFormat("bin {0} emptying request from {1}", binName, message.UserName);

                using (var context = GetNewContext())
                {
                    var bin = context.Bins.FirstOrDefault(b => b.BinName == binName);
                    if (bin == null)
                    {
                        Connector.SendMessage("Diese Tonne kenne ich nicht.");
                        return;
                    }

                    bin.Items.Clear();
                    context.SaveChanges();
                }
            }
            else if (body == "!m\u00fcllabfuhr")
            {
                Logger.DebugFormat("bin removal request from {0}", message.UserName);
                using (var context = GetNewContext())
                {
                    context.DeleteAll<BinItem>();
                    context.DeleteAll<Bin>();
                    context.SaveChanges();
                }
                Connector.SendMessage("Tonnen abgesammelt.");
            }
        }

        protected void HandlePossibleToss(ChatboxMessage message, string body)
        {
            var match = ArrowWasteBinRegex.Match(body);
            if (!match.Success)
            {
                return;
            }

            // a waste bin toss has been found
            var what = match.Groups[1].Value.Trim();
            var arrow = match.Groups[2].Value;
            var where = match.Groups[3].Value.Trim().ToLowerInvariant();

            if (ArrowRegex.IsMatch(what) || ArrowRegex.IsMatch(where))
            {
                Logger.DebugFormat(
                    "{0} is trying to trick us by throwing {1} into {2}",
                    message.UserName, Util.LiteralString(what), Util.LiteralString(where)
                );
                return;
            }

            var timestamp = DateTime.Now;

            Logger.DebugFormat(
                "{0} tossed {1} into {2} using {3}",
                message.UserName, Util.LiteralString(what), Util.LiteralString(where), Util.LiteralString(arrow)
            );

            using (var context = GetNewContext())
            {
                var bin = context.Bins.Where(x => x.BinName == where).FirstOrDefault();
                if (bin == null)
                {
                    // add the bin
                    bin = new Bin
                    {
                        BinName = where
                    };
                    context.Bins.Add(bin);
                    context.SaveChanges();
                }

                var item = context.BinItems.Where(x => x.Bin.BinName == where && x.Item == what).FirstOrDefault();
                if (item == null)
                {
                    item = new BinItem
                    {
                        Bin = bin,
                        Thrower = message.UserName,
                        Arrow = arrow,
                        Item = what,
                        Timestamp = timestamp
                    };
                    context.BinItems.Add(item);
                    context.SaveChanges();
                }
            }
        }
    }
}
