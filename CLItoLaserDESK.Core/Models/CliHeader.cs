using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CLItoLaserDESK.Core.Models {
    public class CliHeader {
        [JsonPropertyName("binary")]
        public bool Binary { get; set; }

        [JsonPropertyName("units")]
        public double Units { get; set; } // JSON shows 1.0, so double or float is fine

        [JsonPropertyName("version")]
        public float Version { get; set; }

        [JsonPropertyName("aligned")]
        public bool Aligned { get; set; }

        [JsonPropertyName("layers")]
        public int? Layers { get; set; } // Nullable int for Option<usize>
    }
}
