using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DotLiquid;
using DotLiquid.FileSystems;
using log4net;
using VBCBBot;

namespace HttpInterface
{
    public class Responder
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Regex EditMessageRegex = new Regex("^/edit-message/(0|[1-9][0-9]*)$");
        private static readonly Regex AllowedStaticPathFormat = new Regex("^[a-zA-Z0-9-.]+$");

        private HttpInterface _interface;

        private HttpListener _listener;
        private Thread _acceptorThread;
        private CancellationTokenSource _canceller;
        private Guid? _authGuid;
        private string _staticPath;
        private string _templatePath;

        public Responder(HttpInterface iface)
        {
            _interface = iface;

            _listener = new HttpListener();
            _listener.Prefixes.Add(string.Format("http://+:{0}/", _interface.Config.HttpPort));
            _acceptorThread = new Thread(AcceptorProc)
            {
                Name = "HttpInterface acceptor"
            };
            _canceller = new CancellationTokenSource();
            _authGuid = null;
            _staticPath = Path.Combine(Util.ProgramDirectory, _interface.Config.StaticDirectory);
            _templatePath = Path.Combine(Util.ProgramDirectory, _interface.Config.TemplateDirectory);

            // set up DotLiquid
            Template.FileSystem = new LocalFileSystem(_templatePath);
        }

        public void Start()
        {
            _listener.Start();
            _acceptorThread.Start();
        }

        public void Stop()
        {
            _listener.Stop();
            _canceller.Cancel();
            _acceptorThread.Join();
        }

        protected virtual void AcceptorProc()
        {
            var cancelToken = _canceller.Token;
            while (!cancelToken.IsCancellationRequested)
            {
                var context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(st8 =>
                {
                    var ctx = (HttpListenerContext)st8;
                    try
                    {
                        HandleRequest(ctx);
                    }
                    catch (WebException exc)
                    {
                        Logger.Error("handling request failed", exc);
                    }
                }, context);
            }
        }

        protected void Send404(HttpListenerResponse response, string message = "Not found.")
        {
            var messageBytes = Util.Utf8NoBom.GetBytes(message);

            response.ContentType = "text/plain; charset=utf-8";
            response.ContentLength64 = messageBytes.LongLength;
            response.StatusCode = 404;
            response.StatusDescription = "Not Found";
            response.Close(messageBytes, true);
        }

        protected void SendOkHtml(HttpListenerResponse response, string htmlText)
        {
            var htmlBytes = Util.Utf8NoBom.GetBytes(htmlText);

            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = htmlBytes.LongLength;
            response.StatusCode = 200;
            response.StatusDescription = "OK";
            response.Close(htmlBytes, true);
        }

        protected void SendOkPlainText(HttpListenerResponse response, string text)
        {
            var textBytes = Util.Utf8NoBom.GetBytes(text);

            response.ContentType = "text/plain; charset=utf-8";
            response.ContentLength64 = textBytes.LongLength;
            response.StatusCode = 200;
            response.StatusDescription = "OK";
            response.Close(textBytes, true);
        }

        protected Template LoadTemplate(string templateName)
        {
            using (var sr = new StreamReader(Path.Combine(_templatePath, templateName + ".liquid")))
            {
                return Template.Parse(sr.ReadToEnd());
            }
        }

        protected Dictionary<string, string> GetPostValues(Stream stream)
        {
            using (var buf = new MemoryStream())
            {
                stream.CopyTo(buf);
                return DomToHtml.DecodeUrlEncodedFormData(buf.ToArray(), Util.Utf8NoBom);
            }
        }

        protected Hash NewPageHash()
        {
            return new Hash
            {
                { "myNickname", _interface.CBConnector.ForumConfig.Username }
            };
        }

        protected virtual void ActUponSuccessfulLogin(HttpListenerContext context)
        {
            if (!_authGuid.HasValue)
            {
                _authGuid = Guid.NewGuid();
            }
            var newAuthCookie = new Cookie
            {
                Name = "VBCBBotAuth",
                Value = _authGuid.Value.ToString("D"),
                Expires = DateTime.Now.AddDays(365)
            };
            context.Response.SetCookie(newAuthCookie);
            context.Response.Redirect(new Uri(context.Request.Url, "/").ToString());
            context.Response.Close();
        }

        protected virtual void HandleRequest(HttpListenerContext context)
        {
            Match match;
            var path = context.Request.Url.AbsolutePath;
            var method = context.Request.HttpMethod;
            var authCookie = context.Request.Cookies["VBCBBotAuth"];

            if (path == "/login")
            {
                if (method == "POST")
                {
                    var loginValues = GetPostValues(context.Request.InputStream);
                    if (loginValues.ContainsKey("username") && loginValues.ContainsKey("password"))
                    {
                        if (loginValues["username"] == _interface.Config.Username && loginValues["password"] == _interface.Config.Password)
                        {
                            ActUponSuccessfulLogin(context);
                            return;
                        }
                    }

                    // unset or wrong username/password; fall through and output form again
                }
                else if (method != "GET")
                {
                    Send404(context.Response);
                }
                var vals = NewPageHash();
                var ret = LoadTemplate("login").Render(vals);
                SendOkHtml(context.Response, ret);
                return;
            }
            else if (path.StartsWith("/devicelogin/"))
            {
                // (don't mind about HTTP method here)

                var deviceAccessCode = path.Substring(("/devicelogin/").Length);
                if (_interface.Config.DeviceAccessCodes.Contains(deviceAccessCode))
                {
                    ActUponSuccessfulLogin(context);
                    return;
                }

                // redirect to login page
                context.Response.Redirect(new Uri(context.Request.Url, "/login").ToString());
                context.Response.Close();
                return;
            }
            else if (path.StartsWith("/static/"))
            {
                var filePath = path.Substring(("/static/").Length);
                if (!AllowedStaticPathFormat.IsMatch(filePath))
                {
                    Send404(context.Response);
                    return;
                }
                var fullPath = Path.Combine(_staticPath, filePath);
                if (!File.Exists(Path.Combine(_staticPath, filePath)))
                {
                    Send404(context.Response);
                    return;
                }

                using (var mule = new MemoryStream())
                {
                    using (var inFile = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        inFile.CopyTo(mule);
                    }

                    mule.Seek(0, SeekOrigin.Begin);

                    context.Response.ContentLength64 = mule.Length;
                    context.Response.StatusCode = 200;
                    context.Response.StatusDescription = "OK";
                    mule.CopyTo(context.Response.OutputStream);
                }
                context.Response.Close();
                return;
            }
            // everything else requires authentication
            else if (!_authGuid.HasValue || authCookie == null || authCookie.Expired || authCookie.Value != _authGuid.Value.ToString("D"))
            {
                // redirect to login page
                context.Response.Redirect(new Uri(context.Request.Url, "/login").ToString());
                context.Response.Close();
                return;
            }
            else if (path == "/logout")
            {
                // (don't mind about HTTP method here)

                // log everyone else out too
                _authGuid = null;

                // clear the cookie
                authCookie.Expires = new DateTime(1970, 1, 1);
                context.Response.SetCookie(authCookie);
                context.Response.Redirect(new Uri(context.Request.Url, "/login").ToString());
                context.Response.Close();
                return;
            }
            else if (path == "/" && method == "GET")
            {
                // assemble the quick-messages
                var quickMessageString = new StringBuilder("<span class=\"quickmessagelist\">");
                foreach (var quickMessage in _interface.Config.QuickMessages)
                {
                    quickMessageString.AppendFormat(
                        " <button type=\"button\" onclick=\"sendQuick('{0}')\">{1}</button>",
                        DomToHtml.HtmlEscape(DomToHtml.JsEscapeString(quickMessage)),
                        DomToHtml.HtmlEscape(quickMessage)
                    );
                }
                quickMessageString.Append("</span>");

                var vars = NewPageHash();
                vars["quickMessages"] = quickMessageString.ToString();
                var ret = LoadTemplate("messages").Render(vars);
                SendOkHtml(context.Response, ret);
                return;
            }
            else if (path == "/smilies" && method == "GET")
            {
                var smileyString = new StringBuilder("<span class=\"smileylist\">");
                smileyString.Append("<button type=\"button\" onclick=\"hideSmilies()\">Hide!</button>");

                var smileyCodesToUrls = new SortedDictionary<string, string>(_interface.CBConnector.SmileyCodesToURLs);
                foreach (var smileyCodeAndUrl in smileyCodesToUrls)
                {
                    smileyString.Append(' ');
                    smileyString.Append("<span class=\"smiley\">");
                    smileyString.AppendFormat(
                        "<a class=\"jsclick\" onclick=\"smileyClicked('{0}')\">",
                        DomToHtml.HtmlEscape(
                            DomToHtml.JsEscapeString(smileyCodeAndUrl.Key, escapeQuotes: false, escapeApostrophes: true),
                            escapeQuotes: true,
                            escapeApostrophes: false
                        )
                    );
                    smileyString.AppendFormat(
                        "<img class=\"smiley picksmiley\" src=\"{0}\" title=\"{1}\"/>",
                        DomToHtml.HtmlEscape(new Uri(_interface.CBConnector.ForumConfig.Url, smileyCodeAndUrl.Value)),
                        DomToHtml.HtmlEscape(smileyCodeAndUrl.Key)
                    );
                    smileyString.Append("</a>");
                    smileyString.Append("</span>");
                }
                smileyString.Append("</span>");

                SendOkHtml(context.Response, smileyString.ToString());
                return;
            }
            else if (path == "/messages" && method == "GET")
            {
                var tpl = LoadTemplate("post");
                var messageStrings = new List<string>();
                lock (_interface.MessageList)
                {
                    foreach (var message in _interface.MessageList)
                    {
                        var senderInfoUrl = new Uri(_interface.CBConnector.ForumConfig.Url, "member.php?u=" + message.UserID.ToString());
                        var senderName = DomToHtml.Convert(message.UserNameDom, _interface.CBConnector.ForumConfig.Url);
                        var body = DomToHtml.Convert(message.BodyDom, _interface.CBConnector.ForumConfig.Url);
                        if (message.UserName == _interface.CBConnector.ForumConfig.Username)
                        {
                            // it's me
                            senderName = string.Format("<span class=\"myself\">{0}</span>", senderName);
                        }
                        var msgHash = new Hash
                        {
                            { "time", message.Timestamp.ToString("yyyy-MM-dd HH:mm") },
                            { "messageID", message.ID.ToString() },
                            { "senderInfoURL", senderInfoUrl.ToString() },
                            { "senderNameHTML", senderName },
                            { "body", body }
                        };
                        messageStrings.Add(tpl.Render(msgHash));
                    }
                }

                string allMessagesString = string.Join("\n", messageStrings);
                SendOkHtml(context.Response, allMessagesString);
                return;
            }
            else if ((match = EditMessageRegex.Match(path)).Success)
            {
                long messageID;
                if (!long.TryParse(match.Groups[1].Value, out messageID))
                {
                    Send404(context.Response, "Message ID out of range.");
                    return;
                }

                if (method == "GET")
                {
                    ChatboxMessage messageToEdit;
                    lock (_interface.MessageList)
                    {
                        var nodeToEdit = _interface.MessageList.Find(m => m.ID == messageID);
                        messageToEdit = (nodeToEdit == null) ? null : nodeToEdit.Value;
                    }

                    if (messageToEdit == null)
                    {
                        Send404(context.Response, "Message not found.");
                        return;
                    }

                    var currentBody = messageToEdit.BodyBBCode;
                    var vars = NewPageHash();
                    vars["messageID"] = messageID.ToString();
                    vars["messageBody"] = currentBody;
                    var ret = LoadTemplate("editmessage").Render(vars);
                    SendOkHtml(context.Response, ret);
                    return;
                }
                else if (method == "POST")
                {
                    var postValues = GetPostValues(context.Request.InputStream);
                    if (!postValues.ContainsKey("newbody"))
                    {
                        SendOkPlainText(context.Response, "You must supply a new body to edit in.");
                        return;
                    }

                    // send
                    _interface.CBConnector.EditMessage(messageID, postValues["newbody"], customSmileys: true);

                    // redirect back to list
                    context.Response.Redirect(new Uri(context.Request.Url, "/").ToString());
                    context.Response.Close();
                    return;
                }
            }
            else if (path == "/post" && method == "POST")
            {
                var postValues = GetPostValues(context.Request.InputStream);
                if (!postValues.ContainsKey("message") || string.IsNullOrWhiteSpace(postValues["message"]))
                {
                    SendOkPlainText(context.Response, "You must supply a message to send.");
                    return;
                }

                // send
                _interface.CBConnector.SendMessage(postValues["message"], customSmileys: true);

                // redirect back to list
                context.Response.Redirect(new Uri(context.Request.Url, "/").ToString());
                context.Response.Close();
                return;
            }

            Send404(context.Response);
            return;
        }
    }
}
