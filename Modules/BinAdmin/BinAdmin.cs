using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using VBCBBot;
using log4net;

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

        private ISet<string> BinBanned;

        public BinAdmin(ChatboxConnector connector)
            : base(connector)
        {
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (message.UserName.Equals(Connector.ForumConfig.Username, StringComparison.InvariantCulture))
            {
                // the bot itself may not throw things into the bin
                return;
            }

            if (isBanned || BinBanned.Contains(message.UserName))
            {
                return;
            }

            var body = message.BodyBBCode;

            if (body.StartsWith("!", StringComparison.InvariantCulture))
            {
                // bot trigger; ignore
                return;
            }

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

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            Logger.DebugFormat(
                "{0} tossed {1} into {2} using {3}",
                message.UserName, Util.LiteralString(what), Util.LiteralString(where), Util.LiteralString(arrow)
            );
        }
    }
}

