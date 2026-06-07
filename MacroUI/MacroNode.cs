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

        [JsonPropertyName("targetProcess")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string TargetProcess { get; set; }

        [JsonPropertyName("triggerHotkey")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string TriggerHotkey { get; set; }

        [JsonPropertyName("iconUnicode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string IconUnicode { get; set; }

        [JsonPropertyName("isSecure")]
        public bool IsSecure { get; set; }

        [JsonPropertyName("children")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, MacroNode> Children { get; set; }
    }
}
