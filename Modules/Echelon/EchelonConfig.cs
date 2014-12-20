using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace Echelon
{
    [JsonObject(MemberSerialization.OptOut)]
    public class EchelonConfig : IDatabaseModuleConfig
    {
        public string DatabaseProvider { get; set; }
        public string DatabaseConnectionString { get; set; }
        public HashSet<string> Spymasters { get; set; }
        public Dictionary<string, string> UsernamesToSpecialCounts { get; set; }

        public EchelonConfig(JObject obj)
        {
            Spymasters = new HashSet<string>();
            UsernamesToSpecialCounts = new Dictionary<string, string>();

            var ser = new JsonSerializer();
            ser.Populate(obj.CreateReader(), this);
        }
    }
}
