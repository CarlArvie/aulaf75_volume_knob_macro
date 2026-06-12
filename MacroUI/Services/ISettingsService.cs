using System.Collections.Generic;
using MacroUI;
using MacroUI.ViewModels;

namespace MacroUI.Services
{
    public interface ISettingsService
    {
        Dictionary<string, MacroNode> LoadMacros(string jsonPath);
        void SaveMacros(string jsonPath, Dictionary<string, MacroNode> macros);

        List<HotstringEntry> LoadHotstrings(string jsonPath);
        void SaveHotstrings(string jsonPath, List<HotstringEntry> hotstrings);

        AppSettings LoadSettings(string jsonPath);
        void SaveSettings(string jsonPath, AppSettings settings);
    }
}
