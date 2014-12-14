using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Motivator
{
    [JsonObject]
    public class MotivatorConfig
    {
        public Dictionary<string, Dictionary<string, List<string>>> VerbsCategoriesMotivators { get; set; }

        public MotivatorConfig(JObject obj)
        {
            var ser = new JsonSerializer();
            ser.Populate(obj.CreateReader(), this);
        }
    }
}
