using System.Text.Json.Serialization;

namespace MacroUI
{
    public class AppSettings
    {
        [JsonPropertyName("centerTitle")]
        public string CenterTitle { get; set; } = "AULA";

        [JsonPropertyName("centerImagePath")]
        public string CenterImagePath { get; set; } = null;

        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "Cyberpunk Cyan";

        [JsonPropertyName("animationEasing")]
        public string AnimationEasing { get; set; } = "Elastic";


        [JsonPropertyName("selectKey")]
        public string SelectKey { get; set; } = "Volume_Mute";

        [JsonPropertyName("backKey")]
        public string BackKey { get; set; } = "RButton";
    }
}
