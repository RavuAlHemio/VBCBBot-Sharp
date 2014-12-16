using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using log4net;

namespace VBCBBot
{
    public class ChatboxConnector
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static readonly Regex TimestampPattern = new Regex("[[]([0-9][0-9]-[0-9][0-9]-[0-9][0-9], [0-9][0-9]:[0-9][0-9])[]]");
        public static readonly Regex XmlCharEscapePattern = new Regex("[&][#]([0-9]+|x[0-9a-fA-F]+)[;]");
        public static readonly Regex DSTSettingPattern = new Regex("var tzOffset = ([0-9]+) [+] ([0-9]+)[;]");

        public event EventHandler<MessageUpdatedEventArgs> MessageUpdated;

        public readonly Config.ForumConfig ForumConfig;
        public readonly HtmlDecompiler Decompiler;
        public readonly int Timeout;

        public readonly Encoding ServerEncoding;
        public readonly int TimeBetweenReads;
        public readonly int DSTUpdateMinute;
        public readonly string MessageIDPiece;
        public readonly string UserIDPiece;

        public readonly Uri LoginUrl;
        public readonly Uri CheapPageUrl;
        public readonly Uri PostEditUrl;
        public readonly Uri MessagesUrl;
        public readonly Uri SmiliesUrl;
        public readonly Uri AjaxUrl;
        public readonly Uri DSTUrl;

        private readonly object _cookieJarLid = new object();
        private readonly CookieWebClient _webClient = new CookieWebClient();
        private readonly Thread _readingThread;

        private IDictionary<long, string> _oldMessageIDsToBodies = new Dictionary<long, string>();
        private readonly IDictionary<string, UserIDAndNickname> _lowercaseUsernamesToUserIDNamePairs = new Dictionary<string, UserIDAndNickname>();
        private IDictionary<string, string> _forumSmileyCodesToURLs = new Dictionary<string, string>();
        private IDictionary<string, string> _forumSmileyURLsToCodes = new Dictionary<string, string>();
        private readonly IDictionary<string, string> _customSmileyCodesToURLs = new Dictionary<string, string>();
        private readonly IDictionary<string, string> _customSmileyURLsToCodes = new Dictionary<string, string>();
        private bool _initialSalvo = true;
        private string _securityToken = null;
        private long _lastMessageReceived = -1;
        private volatile bool _stopReading = false;
        private int _lastDSTUpdateHourUTC = -1;

        public DateTime? StfuDeadline = null;

        /// <summary>
        /// Fishes out an ID following the URL piece from a link containing a given URL piece.
        /// </summary>
        /// <param name="element">The element at which to root the search for the ID.</param>
        /// <param name="urlPiece">The URL piece to search for; it is succeeded directly by the ID.</param>
        /// <returns>The ID fished out of the message.</returns>
        public static long? FishOutID(HtmlNode element, string urlPiece)
        {
            foreach (var linkElement in element.SelectNodesOrEmpty(".//a[@href]"))
            {
                var href = linkElement.GetAttributeValue("href", null);
                var pieceIndex = href.IndexOf(urlPiece, StringComparison.InvariantCulture);
                if (pieceIndex >= 0)
                {
                    return long.Parse(href.Substring(pieceIndex + urlPiece.Length));
                }
            }
            return null;
        }

        /// <summary>
        /// Reduces the number of combining marks on a single character to a specific value.
        /// </summary>
        /// <returns>The filtered string.</returns>
        /// <param name="str">The string to filter.</param>
        /// <param name="maximumMarks">Maximum number of combining marks on a character.</param>
        public static string FilterCombiningMarkClusters(string str, int maximumMarks = 4)
        {
            var ret = new StringBuilder();
            int markCount = 0;

            // necessary to handle SMP characters correctly
            for (int i = 0; i < str.Length; ++i)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(str, i);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    ++markCount;
                    if (markCount <= maximumMarks)
                    {
                        ret.Append(str[i]);
                    }
                }
                else if (category == UnicodeCategory.Format)
                {
                    // these characters don't have a width
                    // add them, but don't reset the mark counter
                    ret.Append(str[i]);
                }
                else
                {
                    markCount = 0;
                    ret.Append(str[i]);
                }
            }

            return ret.ToString();
        }

        /// <summary>
        /// Encode the string in the escape method used by vB AJAX.
        /// </summary>
        /// <returns>The string escaped correctly for vB AJAX.</returns>
        /// <param name="str">The string to send.</param>
        public string AjaxUrlEncodeString(string str)
        {
            var ret = new StringBuilder();
            foreach (char c in str)
            {
                if (Util.UrlSafeChars.Contains(c))
                {
                    ret.Append(c);
                }
                else if (c <= 0x7f)
                {
                    ret.AppendFormat("%{0:X2}", (int)c);
                }
                else
                {
                    // escape it as UTF-16 with %u
                    ret.AppendFormat("%u{0:X4}", (int)c);
                }
            }
            return ret.ToString();
        }

        static ChatboxConnector()
        {
            // form overlapping support: form's children become siblings
            // get rid of this behavior
            HtmlNode.ElementsFlags.Remove("form");
        }

        /// <summary>
        /// Connect to a vBulletin chatbox.
        /// </summary>
        /// <param name="forumConfig">Forum configuration.</param>
        /// <param name="decompiler">A correctly configured HTML decompiler, or null.</param>
        /// <param name="timeout">The timeout for HTTP connections.</param>
        public ChatboxConnector(Config.ForumConfig forumConfig, HtmlDecompiler decompiler = null, int timeout = 10000)
        {
            ForumConfig = forumConfig;
            Decompiler = decompiler;
            Timeout = timeout;

            // assume a good default for these
            ServerEncoding = Encoding.GetEncoding("windows-1252", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            TimeBetweenReads = 5;
            DSTUpdateMinute = 3;
            MessageIDPiece = "misc.php?ccbloc=";
            UserIDPiece = "member.php?u=";

            // precompute the relevant URLs
            LoginUrl = new Uri(ForumConfig.Url, "login.php?do=login");
            CheapPageUrl = new Uri(ForumConfig.Url, "faq.php");
            PostEditUrl = new Uri(ForumConfig.Url, "misc.php");
            MessagesUrl = new Uri(ForumConfig.Url, "misc.php?show=ccbmessages");
            SmiliesUrl = new Uri(ForumConfig.Url, "misc.php?do=showsmilies");
            AjaxUrl = new Uri(ForumConfig.Url, "ajax.php");
            DSTUrl = new Uri(ForumConfig.Url, "profile.php?do=dst");

            // prepare the reading thread
            _readingThread = new Thread(PerformReading)
            {
                Name = "ChatboxConnector reading"
            };
        }

        private IDictionary<string, string> SmileyCodesToURLs
        {
            get
            {
                var ret = new Dictionary<string, string>();
                foreach (var s in _forumSmileyCodesToURLs)
                {
                    ret[s.Key] = s.Value;
                }
                foreach (var s in _customSmileyCodesToURLs)
                {
                    ret[s.Key] = s.Value;
                }
                return ret;
            }
        }

        private IDictionary<string, string> SmileyURLsToCodes
        {
            get
            {
                var ret = new Dictionary<string, string>();
                foreach (var s in _forumSmileyURLsToCodes)
                {
                    ret[s.Key] = s.Value;
                }
                foreach (var s in _customSmileyURLsToCodes)
                {
                    ret[s.Key] = s.Value;
                }
                return ret;
            }
        }

        public void Start()
        {
            _stopReading = false;
            Login();
            _readingThread.Start();
        }

        public void Stop()
        {
            _stopReading = true;
        }

        /// <summary>
        /// Login to the vBulletin chatbox using the credentials contained in this object.
        /// </summary>
        protected void Login()
        {
            Logger.InfoFormat("logging in as {0}", ForumConfig.Username);
            var postValues = new NameValueCollection
            {
                { "vb_login_username", ForumConfig.Username },
                { "vb_login_password", ForumConfig.Password },
                { "cookieuser", "1" },
                { "s", "" },
                { "do", "login" },
                { "vb_login_md5password", "" },
                { "vb_login_md5password_utf", "" }
            };

            lock (_cookieJarLid)
            {
                // empty the cookie jar
                _webClient.ClearCookieJar();

                // log in
                WebPostForm(LoginUrl, postValues);
            }

            // fetch the security token too
            FetchSecurityToken();

            // update smilies
            UpdateSmilies();

            Logger.Info("ready");
        }

        /// <summary>
        /// Fetch and update the security token required for most operations from the forum.
        /// </summary>
        protected void FetchSecurityToken()
        {
            Logger.Info("fetching new security token");
            var cheapPageString = WebGetPage(CheapPageUrl);

            var cheapPage = new HtmlDocument();
            cheapPage.LoadHtml(cheapPageString);
            var tokenField = cheapPage.DocumentNode.SelectSingleNode(".//input[@name='securitytoken']");
            _securityToken = tokenField.GetAttributeValue("value", null);
            Logger.DebugFormat("new security token: {0}", _securityToken);
        }

        /// <summary>
        /// Fetches an up-to-date list of available smilies.
        /// </summary>
        protected void UpdateSmilies()
        {
            Logger.Info("updating smilies");
            var smiliesPageString = WebGetPage(SmiliesUrl);

            var smiliesPage = new HtmlDocument();
            smiliesPage.LoadHtml(smiliesPageString);

            var codeToUrl = new Dictionary<string, string>();
            var urlToCode = new Dictionary<string, string>();

            foreach (var smileyBit in smiliesPage.DocumentNode.QuerySelectorAll("li.smiliebit"))
            {
                var code = smileyBit.QuerySelector("div.smilietext").InnerText;
                var url = smileyBit.QuerySelector("div.smilieimage img").GetAttributeValue("src", null);

                codeToUrl[code] = url;
                urlToCode[url] = code;
            }

            if (codeToUrl.Count == 0 || urlToCode.Count == 0)
            {
                return;
            }

            _forumSmileyCodesToURLs = codeToUrl;
            _forumSmileyURLsToCodes = urlToCode;

            // update this one too (to the combination)
            Decompiler.SmileyUrlToSymbol = SmileyURLsToCodes;
        }

        /// <summary>
        /// Encodes the outgoing message as it can be understood by the server.
        /// </summary>
        /// <returns>The string representing the message in a format understood by the chatbox.</returns>
        /// <param name="outgoingMessage">The message that will be sent.</param>
        protected string EncodeOutgoingMessage(string outgoingMessage)
        {
            return Util.UrlEncode(outgoingMessage, ServerEncoding);
        }

        public string EscapeOutogingText(string text)
        {
            var ret = new StringBuilder(text);
            ret.Replace("[", "[noparse][[/noparse]");

            var smiliesByLength = _forumSmileyCodesToURLs.Keys.ToList();
            smiliesByLength.Sort((r, l) => {
                var lc = l.Length.CompareTo(r.Length);
                return (lc != 0)
                    ? lc
                    : string.Compare(l, r, StringComparison.InvariantCulture);
            });

            foreach (var smiley in smiliesByLength)
            {
                ret.Replace(smiley, string.Format("[noparse]{0}[/noparse]", smiley));
            }

            return ret.ToString();
        }

        /// <summary>
        /// Renews some information and tries invoking an action again.
        /// </summary>
        /// <param name="retryCount">How many times have we retried already?</param>
        /// <param name="actionToRetry">The action to retry.</param>
        protected void Retry(int retryCount, Action actionToRetry)
        {
            if (retryCount == 0)
            {
                try
                {
                    FetchSecurityToken();
                }
                catch (Exception ex)
                {
                    Logger.Warn("fetching security token", ex);
                }
            }
            else if (retryCount == 1)
            {
                try
                {
                    Login();
                }
                catch (Exception ex)
                {
                    Logger.Warn("logging in", ex);
                }
            }
            else
            {
                throw new TransferException();
            }

            actionToRetry.Invoke();
        }

        /// <summary>
        /// Renews some information and tries invoking a function again.
        /// </summary>
        /// <param name="retryCount">How many times have we retried already?</param>
        /// <param name="functionToRetry">The function to retry.</param>
        protected T RetryFunc<T>(int retryCount, Func<T> functionToRetry)
        {
            if (retryCount == 0)
            {
                try
                {
                    FetchSecurityToken();
                }
                catch (Exception ex)
                {
                    Logger.Warn("fetching security token", ex);
                }
            }
            else if (retryCount == 1)
            {
                try
                {
                    Login();
                }
                catch (Exception ex)
                {
                    Logger.Warn("logging in", ex);
                }
            }
            else
            {
                throw new TransferException();
            }

            return functionToRetry.Invoke();
        }

        protected bool ShouldSTFU
        {
            get
            {
                return StfuDeadline.HasValue && DateTime.Now < StfuDeadline.Value;
            }
        }

        /// <summary>
        /// Perform an AJAX request.
        /// </summary>
        /// <returns>The result XML DOM.</returns>
        /// <param name="operation">The name of the operation to perform.</param>
        /// <param name="parameters">The parameters to supply.</param>
        /// <param name="retry">How many times this operation has been tried already.</param>
        public XmlDocument Ajax(string operation, IDictionary<string, string> parameters = null, int retry = 0)
        {
            var postValues = new NameValueCollection
            {
                { "securitytoken", _securityToken },
                { "do", operation }
            };
            if (parameters != null)
            {
                foreach (var pair in parameters)
                {
                    postValues[pair.Key] = pair.Value;
                }
            }

            byte[] response = null;
            var fail = false;
            try
            {
                response = WebPostForm(AjaxUrl, postValues);
            }
            catch (WebException)
            {
                fail = true;
            }

            if (fail || response.Length == 0)
            {
                // something failed
                return RetryFunc(retry, () => Ajax(operation, parameters, retry + 1));
            }

            try
            {
                var doc = new XmlDocument();
                doc.Load(new MemoryStream(response));
                return doc;
            }
            catch (XmlException ex)
            {
                Logger.Warn("AJAX response parse", ex);
                return RetryFunc(retry, () => Ajax(operation, parameters, retry + 1));
            }
        }

        /// <summary>
        /// Send the given emssage to the server.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="bypassStfu">Should an active STFU period be bypassed?</param>
        /// <param name="bypassFilters">Should filters be bypassed?</param>
        /// <param name="customSmileys">Should custom smileys be substituted?</param>
        /// <param name="retry">Level of desperation to post the new message.</param>
        public void SendMessage(string message, bool bypassStfu = false, bool bypassFilters = false, bool customSmileys = false, int retry = 0)
        {
            if (!bypassStfu && ShouldSTFU)
            {
                Logger.DebugFormat("I've been shut up; not posting message {0}", Util.LiteralString(message));
                return;
            }

            if (customSmileys)
            {
                message = SubstituteCustomSmilies(message);
            }

            if (!bypassFilters)
            {
                message = FilterCombiningMarkClusters(message);
            }

            Logger.DebugFormat("posting message {0} (retry {1})", Util.LiteralString(message), retry);
            var requestString = string.Format(
                "do=cb_postnew&securitytoken={0}&vsacb_newmessage={1}",
                _securityToken, EncodeOutgoingMessage(message)
            );

            byte[] postResponse;
            try
            {
                postResponse = WebPostFormData(PostEditUrl, requestString);
            }
            catch (WebException ex)
            {
                Logger.Warn("sending message", ex);
                // don't send the message -- fixing this might take longer
                return;
            }

            if (postResponse.Length != 0)
            {
                Retry(retry, () => SendMessage(message, bypassStfu, bypassFilters, customSmileys, retry + 1));
                return;
            }
        }

        /// <summary>
        /// Edits a previously posted chatbox message.
        /// </summary>
        /// <param name="messageID">The ID of the message to modify.</param>
        /// <param name="message">The new body of the message.</param>
        /// <param name="bypassStfu">Should an active STFU period be bypassed?</param>
        /// <param name="bypassFilters">Should filters be bypassed?</param>
        /// <param name="customSmileys">Should custom smileys be substituted?</param>
        /// <param name="retry">Level of desperation to post the new message.</param>
        public void EditMessage(long messageID, string message, bool bypassStfu = true, bool bypassFilters = false, bool customSmileys = false, int retry = 0)
        {
            if (!bypassStfu && ShouldSTFU)
            {
                Logger.DebugFormat("I've been shut up; not editing message {0} to {1}", messageID, Util.LiteralString(message));
                return;
            }

            if (customSmileys)
            {
                message = SubstituteCustomSmilies(message);
            }

            if (!bypassFilters)
            {
                message = FilterCombiningMarkClusters(message);
            }

            Logger.DebugFormat("editing message {0} to {1} (retry {2})", messageID, Util.LiteralString(message), retry);
            var requestString = string.Format(
                "do=vsacb_editmessage&s=&securitytoken={0}&id={1}&vsacb_editmessage={2}",
                _securityToken, messageID, EncodeOutgoingMessage(message)
            );

            byte[] editResponse;
            try
            {
                editResponse = WebPostFormData(PostEditUrl, requestString);
            }
            catch (WebException ex)
            {
                Logger.Warn("editing message", ex);
                // don't edit the message -- fixing this might take longer
                return;
            }

            if (editResponse.Length != 0)
            {
                Retry(retry, () => EditMessage(messageID, message, bypassStfu, bypassFilters, customSmileys, retry + 1));
                return;
            }
        }

        /// <summary>
        /// Fetches new messages from the chatbox.
        /// </summary>
        /// <param name="retry">Level of desperation fetching the new messages.</param>
        protected void FetchNewMessages(int retry = 0)
        {
            string messagesResponse;
            try
            {
                messagesResponse = WebGetPage(MessagesUrl);
            }
            catch (WebException ex)
            {
                Logger.WarnFormat("fetching new messages failed, retry {0}\n{1}", retry, ex);

                // try harder
                Retry(retry, () => FetchNewMessages(retry + 1));
                return;
            }

            var messages = new HtmlDocument();
            messages.LoadHtml(messagesResponse);
            if (messages.DocumentNode == null)
            {
                // dang
                Retry(retry, () => FetchNewMessages(retry + 1));
                return;
            }

            var allTrs = messages.DocumentNode.SelectNodesOrEmpty("/tr");
            if (allTrs.Count == 0)
            {
                // aw crap
                Retry(retry, () => FetchNewMessages(retry + 1));
                return;
            }

            var newLastMessage = _lastMessageReceived;
            var visibleMessageIDs = new HashSet<long>();
            var newAndEditedMessages = new Stack<MessageToDistribute>();

            // for each message
            foreach (var tr in allTrs)
            {
                // pick out the TDs
                var tds = tr.SelectNodesOrEmpty("./td");
                if (tds.Count != 2)
                {
                    continue;
                }

                // pick out the first (metadata)
                var metaTd = tds[0];
                var bodyTd = tds[1];

                // find the link to the message and to the user
                var messageID = FishOutID(metaTd, MessageIDPiece);
                var userID = FishOutID(metaTd, UserIDPiece);

                if (!messageID.HasValue || !userID.HasValue)
                {
                    // bah, humbug
                    continue;
                }

                if (newLastMessage < messageID.Value)
                {
                    newLastMessage = messageID.Value;
                }

                visibleMessageIDs.Add(messageID.Value);

                // fetch the timestamp
                var timestamp = DateTime.Now;
                var timestampMatch = TimestampPattern.Match(metaTd.InnerHtml);
                if (timestampMatch.Success)
                {
                    var timeString = timestampMatch.Groups[1].Value;
                    DateTime parsed;
                    if (DateTime.TryParseExact(
                        timeString,
                        "dd-MM-yy, HH:mm",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal,
                        out parsed
                    ))
                    {
                        timestamp = parsed;
                    }
                }

                // get the nickname
                HtmlNode nickElement = null;
                foreach (var linkElement in metaTd.SelectNodesOrEmpty(".//a[@href]"))
                {
                    if (linkElement.GetAttributeValue("href", null).Contains(UserIDPiece))
                    {
                        nickElement = linkElement;
                    }
                }

                if (nickElement == null)
                {
                    // bah, humbug
                    continue;
                }

                var nick = nickElement.InnerText;

                var isBanned = ForumConfig.LowercaseBannedUsers.Contains(nick.ToLowerInvariant());

                // cache the nickname
                _lowercaseUsernamesToUserIDNamePairs[nick.ToLowerInvariant()] = new UserIDAndNickname(userID.Value, nick);

                var message = new ChatboxMessage(
                    messageID.Value,
                    userID.Value,
                    nickElement,
                    bodyTd,
                    timestamp,
                    Decompiler
                );

                var body = bodyTd.InnerHtml;
                if (_oldMessageIDsToBodies.ContainsKey(messageID.Value))
                {
                    var oldBody = _oldMessageIDsToBodies[messageID.Value];
                    if (oldBody != body)
                    {
                        _oldMessageIDsToBodies[messageID.Value] = body;
                        newAndEditedMessages.Push(new MessageToDistribute(_initialSalvo, true, isBanned, message));
                    }
                }
                else
                {
                    _oldMessageIDsToBodies[messageID.Value] = body;
                    newAndEditedMessages.Push(new MessageToDistribute(_initialSalvo, false, isBanned, message));
                }
            }

            // port the bodies of messages that are still visible
            var messageIDsToBodies = new Dictionary<long, string>();
            foreach (var pair in _oldMessageIDsToBodies)
            {
                if (visibleMessageIDs.Contains(pair.Key))
                {
                    messageIDsToBodies[pair.Key] = pair.Value;
                }
            }
            _oldMessageIDsToBodies = messageIDsToBodies;

            // distribute the news and modifications
            while (newAndEditedMessages.Count > 0)
            {
                var msg = newAndEditedMessages.Pop();
                OnMessageUpdated(new MessageUpdatedEventArgs { Message = msg });
            }

            _initialSalvo = false;
            _lastMessageReceived = newLastMessage;
        }

        protected virtual void OnMessageUpdated(MessageUpdatedEventArgs e)
        {
            var handler = MessageUpdated;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        /// <summary>
        /// Processes incoming messages.
        /// </summary>
        protected void PerformReading()
        {
            int penaltyCoefficient = 1;
            while (!_stopReading)
            {
                try
                {
                    FetchNewMessages();
                    penaltyCoefficient = 1;
                }
                catch (Exception ex)
                {
                    Logger.WarnFormat("exception fetching messages; penalty coefficient is {0}\n{1}", penaltyCoefficient, ex);
                }
                try
                {
                    PotentialDSTFix();
                }
                catch (Exception ex)
                {
                    Logger.WarnFormat("potential DST fix failed\n{0}", ex);
                }

                ++penaltyCoefficient;
                    Thread.Sleep(TimeSpan.FromSeconds(TimeBetweenReads * penaltyCoefficient));
            }
        }

        /// <summary>
        /// Returns the user ID of the user with the given name.
        /// </summary>
        /// <returns>The user ID of the user with the given name, or -1 if the user does not exist.</returns>
        /// <param name="username">The username to search for.</param>
        public long UserIDForName(string username)
        {
            var result = UserIDAndNicknameForUncasedName(username);
            if (result.HasValue)
            {
                return result.Value.UserID;
            }
            return -1;
        }

        /// <summary>
        /// Returns the user ID and real nickname of the user with the given case-insensitive name.
        /// </summary>
        /// <returns>The information about the user with the given name, or null if the username does not exist.</returns>
        /// <param name="username">The username to search for.</param>
        public UserIDAndNickname? UserIDAndNicknameForUncasedName(string username)
        {
            var lowerUsername = username.ToLowerInvariant();
            if (_lowercaseUsernamesToUserIDNamePairs.ContainsKey(lowerUsername))
            {
                return _lowercaseUsernamesToUserIDNamePairs[lowerUsername];
            }

            if (username.Length < 3)
            {
                // vB doesn't allow usernames shorter than three characters
                return null;
            }

            var result = Ajax("usersearch", new Dictionary<string, string> {{ "fragment", username }});
            foreach (XmlNode child in result.SelectNodes("/users/user[@userid]"))
            {
                var userID = long.Parse(child.Attributes["userid"].Value);
                var usernameText = child.InnerText;

                if (lowerUsername == usernameText.ToLowerInvariant())
                {
                    var info = new UserIDAndNickname(userID, usernameText);

                    // cache!
                    _lowercaseUsernamesToUserIDNamePairs[lowerUsername] = info;
                    return info;
                }
            }

            return null;
        }

        public string SubstituteCustomSmilies(string message)
        {
            var ret = new StringBuilder(message);
            foreach (var smiley in _customSmileyCodesToURLs)
            {
                ret.Replace(smiley.Key, string.Format("[icon]{0}[/icon]", smiley.Value));
            }
            return ret.ToString();
        }

        /// <summary>
        /// Update Daylight Savings Time settings if necessary (to make the forum shut up).
        /// </summary>
        public void PotentialDSTFix()
        {
            var utcNow = DateTime.UtcNow;
            if (_lastDSTUpdateHourUTC == utcNow.Hour)
            {
                // we already checked this hour
                return;
            }

            if (utcNow.Minute < DSTUpdateMinute)
            {
                // too early to check
                return;
            }

            // update hour to this one
            _lastDSTUpdateHourUTC = utcNow.Hour;

            Logger.Debug("checking for DST update");

            // fetch a (computationally cheap) page from the server
            string cheapPageString = WebGetPage(CheapPageUrl);

            // load it
            var cheapPage = new HtmlDocument();
            cheapPage.LoadHtml(cheapPageString);
            var dstForm = cheapPage.DocumentNode.SelectSingleNode(".//form[@name='dstform']");
            if (dstForm == null)
            {
                return;
            }

            Logger.Info("performing DST update");

            // find the forum's DST settings (they're hidden in JavaScript)
            int? firstValue = null, secondValue = null;
            foreach (Match match in DSTSettingPattern.Matches(cheapPageString))
            {
                firstValue = int.Parse(match.Groups[1].Value);
                secondValue = int.Parse(match.Groups[2].Value);
                break;
            }

            if (!firstValue.HasValue || !secondValue.HasValue)
            {
                Logger.Error("can't perform DST update: timezone calculation not found");
                return;
            }

            var forumOffset = firstValue.Value + secondValue.Value;
            var localOffset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).TotalHours;
            var offsetDifference = Math.Abs(forumOffset - localOffset);

            if (Math.Abs(offsetDifference - 1) < 0.01)
            {
                // DST hasn't changed
                Logger.Info("DST already correct");
                return;
            }

            var sNode = dstForm.SelectSingleNode(".//input[@name='s']");
            if (sNode == null)
            {
                Logger.Error("can't perform DST update: input \"s\" not found");
                return;
            }

            // fish out all the necessary fields
            var postFields = new NameValueCollection
            {
                {"s", sNode.GetAttributeValue("value", null)},
                {"securitytoken", dstForm.SelectSingleNode(".//input[@name='securitytoken']").GetAttributeValue("value", null)},
                {"do", "dst"}
            };

            // call the update page
            WebPostForm(DSTUrl, postFields);

            Logger.Info("DST updated");
        }

        protected byte[] WebPostForm(Uri url, NameValueCollection postValues)
        {
            lock (_cookieJarLid)
            {
                _webClient.Headers["Content-Type"] = "application/x-www-form-urlencoded";
                _webClient.Timeout = Timeout;
                return _webClient.UploadValues(url, "POST", postValues);
            }
        }

        protected byte[] WebPostFormData(Uri url, string formData)
        {
            lock (_cookieJarLid)
            {
                _webClient.Headers["Content-Type"] = "application/x-www-form-urlencoded";
                _webClient.Timeout = Timeout;
                return _webClient.UploadData(url, "POST", ServerEncoding.GetBytes(formData));
            }
        }

        protected string WebGetPage(Uri url)
        {
            return ServerEncoding.GetString(WebGetPageBytes(url));
        }

        protected byte[] WebGetPageBytes(Uri url)
        {
            lock (_cookieJarLid)
            {
                _webClient.Timeout = Timeout;
                return _webClient.DownloadData(url);
            }
        }
    }
}

