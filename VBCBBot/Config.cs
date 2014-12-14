using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            public double RefreshTime;

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
            public HashSet<string> LowercaseBannedUsers;

            [JsonIgnore]
            private HashSet<string> _bannedUsers;

            public HashSet<string> BannedUsers
            {
                get { return _bannedUsers; }
                set
                {
                    _bannedUsers = value;
                    LowercaseBannedUsers = new HashSet<string>(_bannedUsers.Select(u => u.ToLowerInvariant()));
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
            public string TeXPrefix;
        }

        [JsonObject]
        public class ModuleConfig
        {
            public string Assembly { get; set; }
            public string ModuleClass { get; set; }
            public JObject Config { get; set; }
        }

        public ForumConfig Forum;
        public HtmlDecompilerConfig HtmlDecompiler;
        public List<ModuleConfig> Modules;

        public Config(string configString)
        {
            JsonConvert.PopulateObject(configString, this);
        }
    }
}
