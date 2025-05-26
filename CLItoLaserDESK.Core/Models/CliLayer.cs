using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CLItoLaserDESK.Core.Models {
    public class CliLayer {
        [JsonPropertyName("height")]
        public float Height { get; set; }

        [JsonPropertyName("loops")]
        public List<CliLoop> Loops { get; set; }

        [JsonPropertyName("hatches")]
        public List<CliHatches> Hatches { get; set; }
    }
}
