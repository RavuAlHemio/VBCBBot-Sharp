using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace LastSeenApi
{
    [JsonObject(MemberSerialization.OptOut)]
    public class SeenConfig
    {
        public string ApiUrlTemplate { get; set; }
        public string ApiUsername { get; set; }
        public string ApiPassword { get; set; }
        public string ArchiveLinkTemplate { get; set; }

        public SeenConfig(JObject obj)
        {
            var ser = new JsonSerializer();
            ser.Populate(obj.CreateReader(), this);
        }
    }
}
