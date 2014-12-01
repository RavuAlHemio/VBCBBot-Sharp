using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VBCBBot
{
    [JsonObject]
    public class Config
    {
        [JsonObject]
        public class ForumConfig
        {
            public string Username;
            public string Password;
            public List<string> BannedUsers;
            public double RefreshTime;
            public string TeXPrefix;

            [JsonProperty("Url")]
            public string UrlString
            {
                get
                {
                    return Url.ToString();
                }
                set
                {
                    Url = new Uri(value);
                }
            }

            [JsonIgnore]
            public Uri Url;

            [JsonProperty("CustomSmileys")]
            public Dictionary<string, string> CustomSmileysToUrls;

            [JsonIgnore]
            public Dictionary<string, string> CustomUrlsToSmileys
            {
                get
                {
                    var ret = new Dictionary<string, string>();
                    foreach (var pair in CustomSmileysToUrls)
                    {
                        ret[pair.Value] = pair.Key;
                    }
                    return ret;
                }
            }
        }

        [JsonObject]
        public class HtmlDecompilerConfig
        {
            public Dictionary<string, string> SmileyUrlToSymbol;
        }

        public Config(string configString)
        {
            // TODO: write me
        }
    }
}
