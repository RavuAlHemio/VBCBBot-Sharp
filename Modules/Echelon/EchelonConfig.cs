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
        public HashSet<string> Terrorists { get; set; }
        public HashSet<string> WordLists { get; set; }
        public Dictionary<string, string> UsernamesToSpecialCountFormats { get; set; }
        public int RankCount { get; set; }

        public EchelonConfig(JObject obj)
        {
            Spymasters = new HashSet<string>();
            Terrorists = new HashSet<string>();
            WordLists = new HashSet<string>();
            UsernamesToSpecialCountFormats = new Dictionary<string, string>();
            RankCount = 5;

            var ser = new JsonSerializer();
            ser.Populate(obj.CreateReader(), this);
        }
    }
}
