using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CLItoLaserDESK.Core.Models {
    public class CliLoop {
        [JsonPropertyName("id")] // Matches "id" in JSON
        public int Id { get; set; }

        [JsonPropertyName("dir")] // Matches "dir" in JSON
        public int Dir { get; set; }

        [JsonPropertyName("points")] // Matches "points" in JSON
        public List<float> Points { get; set; }
    }
}
