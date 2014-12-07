using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace BinAdmin
{
    [JsonObject(MemberSerialization.OptOut)]
    public class BinAdminConfig : IDatabaseModuleConfig
    {
        public string DatabaseProvider { get; set; }
        public string DatabaseConnectionString { get; set; }
        public HashSet<string> Banned { get; set; }

        public BinAdminConfig(JObject obj)
        {
            var ser = new JsonSerializer();
            ser.Populate(obj.CreateReader(), this);
        }

        [JsonIgnore]
        public string ContextString
        {
            get
            {
                return Util.EntityConnectionString(this);
            }
        }
    }
}
