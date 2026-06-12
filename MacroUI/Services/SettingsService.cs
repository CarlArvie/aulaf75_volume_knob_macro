using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MacroUI;
using MacroUI.ViewModels;

namespace MacroUI.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public Dictionary<string, MacroNode> LoadMacros(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                return new Dictionary<string, MacroNode>();

            try
            {
                string json = File.ReadAllText(jsonPath);
                var macros = JsonSerializer.Deserialize<Dictionary<string, MacroNode>>(json);
                return macros ?? new Dictionary<string, MacroNode>();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading macros: " + ex.Message);
                return new Dictionary<string, MacroNode>();
            }
        }

        public void SaveMacros(string jsonPath, Dictionary<string, MacroNode> macros)
        {
            try
            {
                string json = JsonSerializer.Serialize(macros, _jsonOptions);
                File.WriteAllText(jsonPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving macros: " + ex.Message);
            }
        }

        public List<HotstringEntry> LoadHotstrings(string jsonPath)
        {
            var list = new List<HotstringEntry>();
            if (!File.Exists(jsonPath))
                return list;

            try
            {
                string json = File.ReadAllText(jsonPath);
                var hotstringsList = JsonSerializer.Deserialize<List<HotstringEntry>>(json);
                if (hotstringsList != null)
                {
                    foreach (var entry in hotstringsList)
                    {
                        if (entry.IsSecure && !string.IsNullOrEmpty(entry.Replacement))
                        {
                            entry.Replacement = CryptoHelper.Decrypt(entry.Replacement);
                        }
                        list.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading hotstrings: " + ex.Message);
            }
            return list;
        }

        public void SaveHotstrings(string jsonPath, List<HotstringEntry> hotstrings)
        {
            try
            {
                var secureCopies = new List<HotstringEntry>();
                foreach (var entry in hotstrings)
                {
                    var copy = new HotstringEntry 
                    { 
                        Trigger = entry.Trigger, 
                        Replacement = entry.Replacement, 
                        ImagePath = entry.ImagePath, 
                        MatchTypedCase = entry.MatchTypedCase, 
                        IsSecure = entry.IsSecure 
                    };
                    
                    if (copy.IsSecure && !string.IsNullOrEmpty(copy.Replacement))
                    {
                        copy.Replacement = CryptoHelper.Encrypt(copy.Replacement);
                    }
                    secureCopies.Add(copy);
                }

                string json = JsonSerializer.Serialize(secureCopies, _jsonOptions);
                File.WriteAllText(jsonPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving hotstrings: " + ex.Message);
            }
        }

        public AppSettings LoadSettings(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                return new AppSettings();

            try
            {
                string settingsJson = File.ReadAllText(jsonPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(settingsJson);
                return settings ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading settings: " + ex.Message);
                return new AppSettings();
            }
        }

        public void SaveSettings(string jsonPath, AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText(jsonPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving settings: " + ex.Message);
            }
        }
    }
}
