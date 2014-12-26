using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HttpInterface
{
    [JsonObject(MemberSerialization.OptOut)]
    public class InterfaceConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public int HttpPort { get; set; }
        public int Backlog { get; set; }
        public List<string> QuickMessages { get; set; }
        public string TemplateDirectory { get; set; }
        public string StaticDirectory { get; set; }

        public InterfaceConfig(JObject obj)
        {
            Backlog = 50;
            QuickMessages = new List<string>();
            TemplateDirectory = "HttpTemplates";
            StaticDirectory = "HttpStatic";

            var ser = new JsonSerializer();
            ser.Populate(obj.CreateReader(), this);
        }
    }
}
