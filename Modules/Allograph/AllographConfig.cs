﻿using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Allograph
{
    [JsonObject(MemberSerialization.OptOut)]
    class AllographConfig
    {
        [JsonObject(MemberSerialization.OptOut)]
        public class Replacement
        {
            public string RegexString
            {
                get
                {
                    return Regex == null ? null : Regex.ToString();
                }

                set
                {
                    Regex = new Regex(value);
                }
            }

            [JsonIgnore]
            public Regex Regex { get; private set; }

            public string ReplacementString { get; set; }

            public string Comment { get; set; }
        }

        public List<Replacement> Replacements { get; set; }

        public double ProbabilityPercent { get; set; }

        public AllographConfig(JObject obj)
        {
            Replacements = new List<Replacement>();
            ProbabilityPercent = 100.0;

            var ser = new JsonSerializer();
            ser.Populate(obj.CreateReader(), this);
        }
    }
}
