using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MacroUI
{
    public class MacroNode
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("action")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Action { get; set; }

        [JsonPropertyName("imagePath")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ImagePath { get; set; }

        [JsonPropertyName("children")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, MacroNode> Children { get; set; }
    }
}
