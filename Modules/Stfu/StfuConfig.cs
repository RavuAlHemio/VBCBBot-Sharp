using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace Stfu
{
    [JsonObject(MemberSerialization.OptOut)]
    public class StfuConfig : IDatabaseModuleConfig
    {
        public string DatabaseProvider { get; set; }
        public string DatabaseConnectionString { get; set; }
        public long Duration;
        public List<string> Snarks { get; set; }
        public HashSet<string> Admins { get; set; }

        public StfuConfig(JObject obj)
        {
            Duration = 30 * 60;
            Snarks = new List<string>();
            Admins = new HashSet<string>();

            var ser = new JsonSerializer();
            ser.Populate(obj.CreateReader(), this);
        }
    }
}
