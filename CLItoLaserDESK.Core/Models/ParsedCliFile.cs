using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CLItoLaserDESK.Core.Models {
    public class ParsedCliFile {
        [JsonPropertyName("header")]
        public CliHeader Header { get; set; }

        [JsonPropertyName("layers")]
        public List<CliLayer> Layers { get; set; }
    }
}
