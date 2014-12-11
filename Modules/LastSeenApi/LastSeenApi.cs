using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace LastSeenApi
{
    /// <summary>
    /// Checks when a user has most recently posted a message to the chatbox by consulting an API.
    /// </summary>
    public class LastSeenApi : ModuleV1
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Regex SeenRegex = new Regex("^!(?:last)?seen (.+)$");

        private readonly SeenConfig _config;

        public LastSeenApi(ChatboxConnector connector, JObject cfg)
            : base(connector)
        {
            _config = new SeenConfig(cfg);
        }

        public static string FormatDateTime(DateTime? dt)
        {
            if (!dt.HasValue)
            {
                return "???";
            }
            return dt.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            var body = message.BodyBBCode;
            var match = SeenRegex.Match(body);
            if (!match.Success)
            {
                return;
            }

            var nicknames = match.Groups[1].Value.Split(';').Select(n => n.Trim().Replace("[/noparse]", "")).Where(n => n.Length > 0).ToList();
            if (nicknames.Count == 0)
            {
                return;
            }

            var nicknamesInfos = new Dictionary<string, SeenInfo>();
            foreach (var nickname in nicknames)
            {
                var urlEncodedNickname = Util.UrlEncode(nickname, Util.Utf8NoBom, spaceAsPlus: true);
                var callUrl = new Uri(string.Format(_config.ApiUrlTemplate, urlEncodedNickname));

                var client = new WebClient();
                if (_config.ApiUsername != null || _config.ApiPassword != null)
                {
                    client.Credentials = new NetworkCredential(_config.ApiUsername ?? "", _config.ApiPassword ?? "");
                }
                var response = client.DownloadString(callUrl);

                if (response == "NULL")
                {
                    nicknamesInfos[nickname] = null;
                }
                var pieces = response.Split(' ');
                if (pieces.Length != 3)
                {
                    Logger.WarnFormat("unexpected server answer {0} for nickname {1}", Util.LiteralString(response), Util.LiteralString(nickname));
                    continue;
                }

                nicknamesInfos[nickname] = new SeenInfo
                {
                    Timestamp = Util.UnixTimestampStringToLocalDateTime(pieces[0]),
                    MessageID = Util.MaybeParseLong(pieces[1]),
                    Epoch = Util.MaybeParseLong(pieces[2])
                };
            }

            if (nicknames.Count == 1)
            {
                // single-user request
                var nickname = nicknames[0];
                var info = nicknamesInfos[nickname];

                if (info == null)
                {
                    Connector.SendMessage(string.Format(
                            "{0}: The great and powerful [i]signanz[/i] doesn't remember seeing [i]{1}[/i].",
                            Connector.EscapeOutogingText(message.UserName),
                            Connector.EscapeOutogingText(nickname)
                        ));
                }
                else if (!info.Timestamp.HasValue)
                {
                    Connector.SendMessage(string.Format(
                            "{0}: The great and powerful [i]signanz[/i]'s answer confused me\u2014sorry!",
                            Connector.EscapeOutogingText(message.UserName)
                        ));
                }
                else
                {
                    var timestampString = FormatDateTime(info.Timestamp.Value);
                    if (_config.ArchiveLinkTemplate != null && info.MessageID.HasValue && info.Epoch.HasValue)
                    {
                        timestampString += string.Format(
                            " ([url={0}]\u2192 archive[/url])",
                            string.Format(
                                _config.ArchiveLinkTemplate,
                                info.MessageID.Value,
                                info.Epoch.Value
                            )
                        );
                    }
                    Connector.SendMessage(string.Format(
                            "{0}: The last time the great and powerful [i]signanz[/i] saw [i]{1}[/i] was {2}.",
                            Connector.EscapeOutogingText(message.UserName),
                            Connector.EscapeOutogingText(nickname),
                            timestampString
                        ));
                }
            }
            else
            {
                // multi-nick request
                var responseBits = new List<string>();
                foreach (var nickname in nicknames)
                {
                    string text;
                    var info = nicknamesInfos[nickname];
                    if (info == null)
                    {
                        text = "never";
                    }
                    else if (!info.Timestamp.HasValue)
                    {
                        text = "o_O";
                    }
                    else
                    {
                        text = FormatDateTime(info.Timestamp.Value);
                        if (_config.ArchiveLinkTemplate != null && info.MessageID.HasValue && info.Epoch.HasValue)
                        {
                            text += string.Format(
                                " ([url={0}]\u2192[/url])",
                                string.Format(
                                    _config.ArchiveLinkTemplate,
                                    info.MessageID.Value,
                                    info.Epoch.Value
                                )
                            );
                        }
                    }

                    responseBits.Add(string.Format(
                        "[i]{0}[/i]: {1}",
                        Connector.EscapeOutogingText(nickname),
                        text
                    ));
                }

                Connector.SendMessage(string.Format(
                    "{0}: The great and powerful [i]signanz[/i] saw: {1}",
                    message.UserName,
                    string.Join(", ", responseBits)
                ));
            }
        }
    }
}
