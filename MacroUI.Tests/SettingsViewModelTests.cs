using System.ComponentModel;
using MacroUI;
using MacroUI.ViewModels;
using Xunit;

namespace MacroUI.Tests
{
    public class SettingsViewModelTests
    {
        [Fact]
        public void PropertyChange_UpdatesUnderlyingSettings()
        {
            // Arrange
            var config = new AppSettings();
            var settingsVm = new SettingsViewModel(config);

            // Act
            settingsVm.CenterTitle = "New Center";
            settingsVm.EnableGameMode = true;
            settingsVm.TickSoundVolume = 0.5;
            settingsVm.TickSoundPath = "C:\\sound.wav";

            // Assert
            Assert.Equal("New Center", config.CenterTitle);
            Assert.True(config.EnableGameMode);
            Assert.Equal(0.5, config.TickSoundVolume);
            Assert.Equal("C:\\sound.wav", config.TickSoundPath);
        }

        [Fact]
        public void PropertyChange_RaisesPropertyChangedEvent()
        {
            // Arrange
            var config = new AppSettings();
            var settingsVm = new SettingsViewModel(config);
            string? changedProperty = null;
            settingsVm.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            // Act
            settingsVm.Theme = "Dark";

            // Assert
            Assert.Equal("Theme", changedProperty);
        }

        [Fact]
        public void ClearCenterImageCommand_SetsImagePathToNull()
        {
            // Arrange
            var config = new AppSettings { CenterImagePath = "C:\\image.png" };
            var settingsVm = new SettingsViewModel(config);

            // Act
            settingsVm.ClearCenterImageCommand.Execute(null);

            // Assert
            Assert.Null(settingsVm.CenterImagePath);
            Assert.Null(config.CenterImagePath);
        }

        [Fact]
        public void ClearTickSoundCommand_ClearsTickSoundPath()
        {
            // Arrange
            var config = new AppSettings { TickSoundPath = "C:\\sound.wav" };
            var settingsVm = new SettingsViewModel(config);

            // Act
            settingsVm.ClearTickSoundCommand.Execute(null);

            // Assert
            Assert.Equal("", settingsVm.TickSoundPath);
            Assert.Equal("", config.TickSoundPath);
        }
    }
}
