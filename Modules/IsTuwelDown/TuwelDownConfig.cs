using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace IsTuwelDown
{
    [JsonObject(MemberSerialization.OptOut)]
    public class TuwelDownConfig
    {
        public Uri ApiUrl { get; set; }
        public List<string> DownMessages { get; set; }
        public List<string> UpMessages { get; set; }
        public List<string> UnknownMessages { get; set; }

        public TuwelDownConfig(JObject obj)
        {
            DownMessages = new List<string>
            {
                "[noparse]{0}[/noparse]: TUWEL is down since {1}. (Last checked {2}.)"
            };
            UpMessages = new List<string>
            {
                "[noparse]{0}[/noparse]: TUWEL is up since {1}. (Last checked {2}.)"
            };
            UnknownMessages = new List<string>
            {
                "[noparse]{0}[/noparse]: I don\u2019 know either..."
            };

            var ser = new JsonSerializer();
            ser.Populate(obj.CreateReader(), this);
        }
    }
}
