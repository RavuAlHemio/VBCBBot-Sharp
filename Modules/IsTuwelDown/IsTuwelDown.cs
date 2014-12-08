using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace IsTuwelDown
{
    public class IsTuwelDown : ModuleV1
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Regex IsDownRegex = new Regex("^!ist?tuwel(up|down)$");

        private TuwelDownConfig _config;

        public static string FormatDateTime(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        public static string UnixTimestampStringToLocalTimeString(string unixTimestampString)
        {
            // try parsing timestamp
            double unixTime;
            if (!double.TryParse(unixTimestampString, out unixTime))
            {
                return "???";
            }

            // calculate date/time
            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, CultureInfo.InvariantCulture.Calendar, DateTimeKind.Utc);
            var myTimeUTC = unixEpoch.AddSeconds(unixTime);
            var myTimeLocal = myTimeUTC.ToLocalTime();
            return FormatDateTime(myTimeLocal);
        }

        public static string PickRandom(IList<string> list)
        {
            var rnd = new Random();
            var n = rnd.Next(list.Count);
            return list[n];
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (isPartOfInitialSalvo || isEdited || isBanned)
            {
                return;
            }

            var body = message.BodyBBCode;
            if (!IsDownRegex.Match(body).Success)
            {
                return;
            }

            var client = new WebClient
            {
                Encoding = Encoding.UTF8
            };
            var response = client.DownloadString(_config.ApiUrl);
            var pieces = response.Split(' ');
            if (pieces.Length != 3)
            {
                Logger.DebugFormat(
                    "unexpected server answer {0} for nickname {1}",
                    Util.LiteralString(response),
                    message.UserName
                );
                Connector.SendMessage(string.Format(PickRandom(_config.UnknownMessages), message.UserName));
                return;
            }

            var status = pieces[0];
            var sinceTimeString = pieces[1];
            var lastUpdateTimeString = pieces[2];

            var since = UnixTimestampStringToLocalTimeString(sinceTimeString);
            var lastUpdate = UnixTimestampStringToLocalTimeString(lastUpdateTimeString);

            IList<string> pickOne = _config.UnknownMessages;
            if (status == "0")
            {
                pickOne = _config.UpMessages;
            }
            else if (status == "1")
            {
                pickOne = _config.DownMessages;
            }

            var outgoing = PickRandom(pickOne);
            Connector.SendMessage(string.Format(
                outgoing,
                message.UserName,
                since,
                lastUpdate
            ));
        }

        public IsTuwelDown(ChatboxConnector connector, JObject cfg)
            : base(connector)
        {
            _config = new TuwelDownConfig(cfg);
        }
    }
}

