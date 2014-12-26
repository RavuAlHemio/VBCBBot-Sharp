using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using log4net;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace LinkInfo
{
    public class LinkInfo : ModuleV1
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly Uri GoogleHomepageUrl = new Uri("http://www.google.com/");
        public static readonly Uri GoogleImageSearchUrl = new Uri("http://www.google.com/imghp?hl=en&tab=wi");
        public const string GoogleImageSearchByImageUrlPattern = "https://www.google.com/searchbyimage?hl=en&image_url={0}";
        public const string FakeUserAgent = "Mozilla/5.0 (X11; Linux x86_64; rv:31.0) Gecko/20100101 Firefox/31.0";

        private Uri _lastLink = null;
        private Uri _lastIcon = null;

        public LinkInfo(ChatboxConnector connector, JObject config)
            : base(connector)
        {
        }

        public static IList<Uri> FindLinks(IEnumerable<BBCodeDom.Node> nodeList)
        {
            var ret = new List<Uri>();
            foreach (var node in nodeList)
            {
                var element = node as BBCodeDom.Element;
                if (element != null && element.Name == "url")
                {
                    ret.Add(new Uri(element.AttributeValue));
                }

                if (node.HasChildren)
                {
                    ret.AddRange(FindLinks(node.Children));
                }
            }
            return ret;
        }

        public static IList<Uri> FindIcons(IEnumerable<BBCodeDom.Node> nodeList)
        {
            var ret = new List<Uri>();
            foreach (var node in nodeList)
            {
                var element = node as BBCodeDom.Element;
                if (element != null && element.Name == "icon")
                {
                    ret.Add(new Uri(string.Concat(element.Children.Select(n => n.BBCodeString))));
                }

                if (node.HasChildren)
                {
                    ret.AddRange(FindIcons(node.Children));
                }
            }
            return ret;
        }

        public static string RealObtainLinkInfo(Uri link)
        {
            var lowerUrl = link.ToString().ToLowerInvariant();
            if (!lowerUrl.StartsWith("http://") && !lowerUrl.StartsWith("https://"))
            {
                return "(I only access HTTP and HTTPS URLs)";
            }

            // check URL blacklist
            var addresses = Dns.GetHostAddresses(link.Host);
            if (addresses.Length == 0)
            {
                return "(cannot resolve)";
            }
            if (addresses.Any(IPAddressBlacklist.IsIPAddressBlacklisted))
            {
                return "(I refuse to access this IP address)";
            }

            var request = WebRequest.Create(link);
            using (var respStore = new MemoryStream())
            {
                var contentType = "application/octet-stream";
                string contentTypeHeader = null;
                string responseCharacterSet = null;
                request.Timeout = 5000;
                try
                {
                    var resp = request.GetResponse();

                    // find the content-type
                    contentTypeHeader = resp.Headers[HttpResponseHeader.ContentType];
                    if (contentTypeHeader != null)
                    {
                        contentType = contentTypeHeader.Split(';')[0];
                    }
                    var webResp = resp as HttpWebResponse;
                    responseCharacterSet = (webResp != null) ? webResp.CharacterSet : null;

                    // copy
                    resp.GetResponseStream().CopyTo(respStore);
                }
                catch (WebException we)
                {
                    var httpResponse = we.Response as HttpWebResponse;
                    return string.Format("(HTTP {0})", httpResponse != null ? httpResponse.StatusCode.ToString() : "error");
                }

                switch (contentType)
                {
                    case "application/octet-stream":
                        return "(can't figure out the content type, sorry)";
                    case "text/html":
                    case "application/xhtml+xml":
                    // HTML? parse it and get the title
                        var respStr = EncodingGuesser.GuessEncodingAndDecode(respStore.ToArray(), responseCharacterSet,
                                      contentTypeHeader);

                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(respStr);
                        var titleElement = htmlDoc.DocumentNode.SelectSingleNode(".//title");
                        if (titleElement != null)
                        {
                            return titleElement.InnerText;
                        }
                        var h1Element = htmlDoc.DocumentNode.SelectSingleNode(".//h1");
                        if (h1Element != null)
                        {
                            return h1Element.InnerText;
                        }
                        return "(HTML without a title O_o)";
                    case "image/png":
                        return ObtainImageInfo(link, "PNG image");
                    case "image/jpeg":
                        return ObtainImageInfo(link, "JPEG image");
                    case "image/gif":
                        return ObtainImageInfo(link, "GIF image");
                    case "application/json":
                        return "JSON";
                    case "text/xml":
                    case "application/xml":
                        return "XML";
                    default:
                        return string.Format("file of type {0}", contentType);
                }
            }
        }

        public static string ObtainImageInfo(Uri url, string text)
        {
            try
            {
                var client = new CookieWebClient();
                client.Headers[HttpRequestHeader.UserAgent] = FakeUserAgent;

                // alibi-visit the image search page to get the cookies
                client.Headers[HttpRequestHeader.Referer] = GoogleHomepageUrl.ToString();
                client.DownloadData(GoogleImageSearchUrl);

                // fetch the actual info
                var searchUrl = new Uri(string.Format(
                    GoogleImageSearchByImageUrlPattern,
                    Util.UrlEncode(url.ToString(), Util.Utf8NoBom, true)
                ));
                client.Headers[HttpRequestHeader.Referer] = GoogleImageSearchUrl.ToString();
                var responseBytes = client.DownloadData(searchUrl);
                var parseMe = EncodingGuesser.GuessEncodingAndDecode(responseBytes, null, null);
                
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(parseMe);
                var foundHints = htmlDoc.DocumentNode.QuerySelectorAll(".qb-bmqc .qb-b");
                foreach (var hint in foundHints)
                {
                    return string.Format("{0} ({1})", text, hint.InnerText);
                }
                return text;
            }
            catch (Exception ex)
            {
                Logger.Warn("image info", ex);
                return text;
            }
        }

        public static string ObtainLinkInfo(Uri link)
        {
            try
            {
                return RealObtainLinkInfo(link);
            }
            catch (Exception ex)
            {
                Logger.Warn("link info", ex);
                return "(an error occurred)";
            }
        }

        protected void PostLinkInfo(IEnumerable<Uri> links)
        {
            foreach (var linkAndInfo in links.Select(l => new LinkAndInfo(l, ObtainLinkInfo(l))))
            {
                var linkInfo = Util.ExpungeNoparse(linkAndInfo.Info);
                Connector.SendMessage(string.Format(
                    "[url]{0}[/url]: [noparse]{1}[/noparse]",
                    linkAndInfo.Link,
                    linkInfo
                ));
            }
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false,
            bool isBanned = false)
        {
            if (isPartOfInitialSalvo || isEdited || isBanned)
            {
                return;
            }

            var body = message.BodyBBCode;
            if (body == "!lastlink")
            {
                if (_lastLink == null)
                {
                    Connector.SendMessage("No last link!");
                }
                else
                {
                    PostLinkInfo(new [] {_lastLink});
                }
                return;
            }
            if (body == "!lasticon")
            {
                if (_lastIcon == null)
                {
                    Connector.SendMessage("No last icon!");
                }
                else
                {
                    PostLinkInfo(new [] {_lastIcon});
                }
            }

            // find all the links
            var dom = message.BodyDom;
            var links = FindLinks(dom);
            var icons = FindIcons(dom);

            // store the new "last link"
            if (links.Count > 0)
            {
                _lastLink = links[links.Count - 1];
            }
            if (icons.Count > 0)
            {
                _lastIcon = icons[icons.Count - 1];
            }

            // respond?
            if (body.StartsWith("!link "))
            {
                PostLinkInfo(links);
            }
            else if (body.StartsWith("!icon "))
            {
                PostLinkInfo(icons);
            }
        }
    }
}
