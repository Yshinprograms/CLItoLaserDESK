using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CLItoLaserDESK.Core.Models {
    public class CliHatches {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("points")]
        public List<float> Points { get; set; }
    }
}
