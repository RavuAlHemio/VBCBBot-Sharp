using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace VBCBBot
{
    [JsonObject]
    public class Config
    {
        [JsonObject]
        public class ForumConfig
        {
            public string Url;
            public string Username;
            public string Password;
            public List<string> BannedUsers;
            public double RefreshTime;
            public string TeXPrefix;

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

        public Config(string configString)
        {
        }
    }
}
