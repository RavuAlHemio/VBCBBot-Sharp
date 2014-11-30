using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using log4net;

namespace VBCBBot
{
    public class ChatboxConnector
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static readonly ISet<char> UrlSafeChars = new HashSet<char>("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.");
        public static readonly Regex TimestampPattern = new Regex("[[]([0-9][0-9]-[0-9][0-9]-[0-9][0-9], [0-9][0-9]:[0-9][0-9])[]]");
        public static readonly Regex XmlCharEscapePattern = new Regex("[&][#]([0-9]+|x[0-9a-fA-F]+)[;]");
        public static readonly Regex DstSettingPattern = new Regex("var tzOffset = ([0-9]+) [+] ([0-9]+)[;]");

        public Config.ForumConfig ForumConfig;
        public HtmlDecompiler Decompiler;
        public int Timeout;

        public Encoding ServerEncoding;
        public int TimeBetweenReads;
        public int DSTUpdateMinute;
        public string MessageIDPiece;
        public string UserIDPiece;

        public Uri LoginUrl;
        public Uri CheapPageUrl;
        public Uri PostEditUrl;
        public Uri MessagesUrl;
        public Uri SmiliesUrl;
        public Uri AjaxUrl;
        public Uri DstUrl;

        private object _cookieJarLid = new object();
        private CookieWebClient _webClient = new CookieWebClient();
        private Thread _readingThread;

        private ISet<string> _bannedNicknames = new HashSet<string>();
        //private ISet<IChatboxSubscriber> _subscribers = new HashSet<IChatboxSubscriber>();
        private IDictionary<long, string> _oldMessageIDsToBodies = new Dictionary<long, string>();
        private IDictionary<string, Tuple<long, string>> _lowercaseUsernamesToUserIDNamePairs = new Dictionary<string, Tuple<long, string>>();
        private IDictionary<string, string> _forumSmileyCodesToURLs = new Dictionary<string, string>();
        private IDictionary<string, string> _forumSmileyURLsToCodes = new Dictionary<string, string>();
        private IDictionary<string, string> _customSmileyCodesToURLs = new Dictionary<string, string>();
        private IDictionary<string, string> _customSmileyURLsToCodes = new Dictionary<string, string>();
        private bool _initialSalvo = true;
        private string _securityToken = null;
        private long _lastMessageReleased = -1;
        private bool _stopReading = false;
        private DateTime? _stfuDeadline = null;
        private short _lastDSTUpdateHourUTC = -1;

        /// <summary>
        /// Fishes out an ID following the URL piece from a link containing a given URL piece.
        /// </summary>
        /// <param name="element">The element at which to root the search for the ID.</param>
        /// <param name="urlPiece">The URL piece to search for; it is succeeded directly by the ID.</param>
        /// <returns>The ID fished out of the message.</returns>
        public static long? FishOutID(HtmlNode element, string urlPiece)
        {
            foreach (var linkElement in element.SelectNodes(".//a[@href]"))
            {
                var href = linkElement.GetAttributeValue("href", null);
                var pieceIndex = href.IndexOf(urlPiece);
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
        /// Converts a string into Unicode code points, handling surrogate pairs gracefully.
        /// </summary>
        /// <returns>The code points.</returns>
        /// <param name="str">The string to convert to code points.</param>
        public IEnumerable<string> StringToCodePointStrings(string str)
        {
            char precedingLeadSurrogate = (char)0;
            bool awaitingTrailSurrogate = false;

            foreach (char c in str)
            {
                if (awaitingTrailSurrogate)
                {
                    if (c >= 0xDC00 && c <= 0xDFFF)
                    {
                        // SMP code point
                        yield return new string(new [] { precedingLeadSurrogate, c });
                    }
                    else
                    {
                        // lead surrogate without trail surrogate
                        // return both independently
                        yield return precedingLeadSurrogate.ToString();
                        yield return c.ToString();
                    }
                    
                    awaitingTrailSurrogate = false;
                }
                else if (c >= 0xD800 && c <= 0xDBFF)
                {
                    precedingLeadSurrogate = c;
                    awaitingTrailSurrogate = true;
                }
                else
                {
                    yield return c.ToString();
                }
            }
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
                if (UrlSafeChars.Contains(c))
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

        /// <summary>
        /// Connect to a vBulletin chatbox.
        /// </summary>
        /// <param name="forumConfig">Forum configuration.</param>
        /// <param name="decompiler">A correctly configured HTML decompiler, or null.</param>
        /// <param name="timeout">The timeout for HTTP connections.</param>
        public ChatboxConnector(Config.ForumConfig forumConfig, HtmlDecompiler decompiler = null, int timeout = 30)
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
            MessagesUrl = new Uri(ForumConfig.Url, "misc.php?do=ccbmessages");
            SmiliesUrl = new Uri(ForumConfig.Url, "misc.php?do=showsmilies");
            AjaxUrl = new Uri(ForumConfig.Url, "ajax.php");
            DstUrl = new Uri(ForumConfig.Url, "profile.php?do=dst");

            // prepare the reading thread
            //_readingThread = new Thread(PerformReading);
            _readingThread.Name = "ChatboxConnector reading";
        }

        private IDictionary<string, string> _smileyCodesToURLs
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

        private IDictionary<string, string> _smileyURLsToCodes
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
            Login();
            _readingThread.Start();
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
                _webClient.UploadValues(LoginUrl, postValues);
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
            string cheapPageString;

            Logger.Info("fetching new security token");
            lock (_cookieJarLid)
            {
                var cheapPageData = _webClient.DownloadData(CheapPageUrl);
                cheapPageString = ServerEncoding.GetString(cheapPageData);
            }

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
            string smiliesPageString;

            Logger.Info("updating smilies");
            lock (_cookieJarLid)
            {
                var smiliesPageData = _webClient.DownloadData(SmiliesUrl);
                smiliesPageString = ServerEncoding.GetString(smiliesPageData);
            }

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
            Decompiler.SmileyUrlToSymbol = _smileyURLsToCodes;
        }

        /// <summary>
        /// Encodes the outgoing message as it can be understood by the server.
        /// </summary>
        /// <returns>The string representing the message in a format understood by the chatbox.</returns>
        /// <param name="outgoingMessage">The message that will be sent.</param>
        protected string EncodeOutgoingMessage(string outgoingMessage)
        {
            var ret = new StringBuilder();
            foreach (string ps in StringToCodePointStrings(outgoingMessage))
            {
                if (ps.Length == 1 && UrlSafeChars.Contains(ps[0]))
                {
                    // URL-safe character
                    ret.Append(ps[0]);
                }
                else
                {
                    // character in the server's encoding?
                    try
                    {
                        // URL-encode
                        foreach (var b in ServerEncoding.GetBytes(ps))
                        {
                            ret.AppendFormat("%{X2}", (int)b);
                        }
                    }
                    catch (EncoderFallbackException)
                    {
                        // unsupported natively by the encoding; perform a URL-encoded HTML escape
                        ret.AppendFormat("%26%23{0}%3B", Char.ConvertToUtf32(ps, 0));
                    }
                }
            }

            return ret.ToString();
        }

        public string EscapeOutogingText(string text)
        {
            var ret = new StringBuilder(text);
            ret.Replace("[", "[noparse][[/noparse]");

            var smiliesByLength = _forumSmileyCodesToURLs.Keys.ToList();
            smiliesByLength.Sort((r, l) => {
                var lc = l.Length.CompareTo(r.Length);
                if (lc != 0)
                {
                    return lc;
                }
                return l.CompareTo(r);
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
                catch
                {
                }
            }
            else if (retryCount == 1)
            {
                try
                {
                    Login();
                }
                catch
                {
                }
            }
            else
            {
                throw new TransferException();
            }

            actionToRetry.Invoke();
        }

        protected bool ShouldSTFU
        {
            get
            {
                return _stfuDeadline.HasValue && DateTime.Now < _stfuDeadline.Value;
            }
        }
    }
}

