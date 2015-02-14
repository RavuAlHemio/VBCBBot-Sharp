using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Corrigendum
{
    [JsonObject(MemberSerialization.OptOut)]
    public class CorrigConfig
    {
        [JsonObject(MemberSerialization.OptOut)]
        public class CorrigItem
        {
            public string WordListFilename;
            public string From;
            public string To;
        }

        public List<CorrigItem> Items;
        public string CorrectedWordFormat;
        public string Separator;

        public CorrigConfig(JObject obj)
        {
            Items = new List<CorrigItem>();
            CorrectedWordFormat = "{0}*";
            Separator = " ";

            var ser = new JsonSerializer();
            ser.Populate(obj.CreateReader(), this);
        }
    }
}
