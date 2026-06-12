using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace MacroUI.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void Serialize_MacroNode_IgnoresNullValues()
        {
            // Arrange
            var node = new MacroNode
            {
                Name = "TestNode",
                IsSecure = false
            };

            // Act
            string json = JsonSerializer.Serialize(node);

            // Assert
            Assert.Contains("\"name\":\"TestNode\"", json);
            Assert.Contains("\"isSecure\":false", json);
            Assert.DoesNotContain("\"action\"", json);
            Assert.DoesNotContain("\"imagePath\"", json);
            Assert.DoesNotContain("\"children\"", json);
            Assert.DoesNotContain("\"targetProcess\"", json);
            Assert.DoesNotContain("\"triggerHotkey\"", json);
        }

        [Fact]
        public void Deserialize_Hotstring_LoadsCorrectly()
        {
            // Arrange
            string json = "{\"trigger\":\"brb\",\"replacement\":\"be right back\",\"imagePath\":\"C:\\\\test.png\"}";

            // Act
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var hotstring = JsonSerializer.Deserialize<HotstringEntry>(json, options);

            // Assert
            Assert.NotNull(hotstring);
            Assert.Equal("brb", hotstring.Trigger);
            Assert.Equal("be right back", hotstring.Replacement);
            Assert.Equal("C:\\test.png", hotstring.ImagePath);
        }

        [Fact]
        public void Serialize_Settings_UpdatesAllFields()
        {
            // Arrange
            var settings = new AppSettings
            {
                CenterTitle = "TestTitle",
                Theme = "Dark",
                TickSoundPath = "tick.wav",
                EnableGameMode = true
            };

            // Act
            string json = JsonSerializer.Serialize(settings);

            // Assert
            Assert.Contains("\"centerTitle\":\"TestTitle\"", json);
            Assert.Contains("\"theme\":\"Dark\"", json);
            Assert.Contains("\"tickSoundPath\":\"tick.wav\"", json);
        }
    }
}
