using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using MacroUI.Services;

namespace MacroUI.ViewModels
{
    public class ConfigWindowViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly string _macrosPath;
        private readonly string _hotstringsPath;
        private readonly string _settingsPath;

        public MacroTreeViewModel TreeVM { get; set; }
        public ObservableCollection<HotstringEntry> Hotstrings { get; set; }
        public SettingsViewModel SettingsVM { get; set; }

        public ICommand SaveAllCommand { get; }
        public ICommand AddHotstringCommand { get; }
        public ICommand DeleteHotstringCommand { get; }

        public ConfigWindowViewModel(ISettingsService settingsService, string macrosPath, string hotstringsPath, string settingsPath)
        {
            _settingsService = settingsService;
            _macrosPath = macrosPath;
            _hotstringsPath = hotstringsPath;
            _settingsPath = settingsPath;

            TreeVM = new MacroTreeViewModel();
            Hotstrings = new ObservableCollection<HotstringEntry>();

            SaveAllCommand = new RelayCommand(_ => SaveData());
            AddHotstringCommand = new RelayCommand(_ => AddHotstring());
            DeleteHotstringCommand = new RelayCommand(DeleteHotstring);

            LoadData();
        }

        public void LoadData()
        {
            // Load Macros
            var macros = _settingsService.LoadMacros(_macrosPath);
            TreeVM.RootNodes.Clear();
            foreach (var kvp in macros)
            {
                TreeVM.RootNodes.Add(new TreeNodeViewModel(kvp.Key, kvp.Value, null));
            }

            // Load Hotstrings
            var hotstringsList = _settingsService.LoadHotstrings(_hotstringsPath);
            Hotstrings.Clear();
            foreach (var entry in hotstringsList)
            {
                Hotstrings.Add(entry);
            }

            // Load Settings
            var settings = _settingsService.LoadSettings(_settingsPath);
            SettingsVM = new SettingsViewModel(settings);
            
            // Re-bind import completed to reload data
            SettingsVM.ImportCompleted += (s, e) => LoadData();
        }

        private void AddHotstring()
        {
            Hotstrings.Add(new HotstringEntry { Trigger = "", Replacement = "" });
        }

        private void DeleteHotstring(object parameter)
        {
            if (parameter is HotstringEntry entry)
            {
                Hotstrings.Remove(entry);
            }
        }

        private void SaveData()
        {
            // Save Macros
            var macrosDict = new System.Collections.Generic.Dictionary<string, MacroNode>();
            foreach (var node in TreeVM.RootNodes)
            {
                macrosDict[node.Key] = node.ToMacroNode();
            }
            _settingsService.SaveMacros(_macrosPath, macrosDict);

            // Save Hotstrings
            var validHotstrings = new System.Collections.Generic.List<HotstringEntry>();
            foreach (var entry in Hotstrings)
            {
                if (!string.IsNullOrWhiteSpace(entry.Trigger) && !string.IsNullOrWhiteSpace(entry.Replacement))
                {
                    validHotstrings.Add(entry);
                }
            }
            _settingsService.SaveHotstrings(_hotstringsPath, validHotstrings);

            // Save Settings
            _settingsService.SaveSettings(_settingsPath, SettingsVM.Settings);

            // Generate AHK integration files
            string basePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            
            // Generate settings.ini
            string iniPath = System.IO.Path.Combine(basePath, "settings.ini");
            System.IO.File.WriteAllText(iniPath, $"[Hotkeys]\nSelect={SettingsVM.Settings.SelectKey}\nBack={SettingsVM.Settings.BackKey}\n");

            // Generate hotstrings.ahk
            string ahkPath = System.IO.Path.Combine(basePath, "hotstrings.ahk");
            var ahkContent = new System.Text.StringBuilder();
            ahkContent.AppendLine("; Auto-generated hotstrings");
            foreach (var entry in validHotstrings)
            {
                string trigger = entry.Trigger.Replace(":", "`:");
                string ahkOptions = entry.MatchTypedCase ? "" : "C";
                ahkContent.AppendLine($":{ahkOptions}:{trigger}:: {{");
                if (!string.IsNullOrWhiteSpace(entry.Replacement))
                {
                    string replacement = entry.Replacement.Replace("`", "``").Replace("\r", "`r").Replace("\n", "`n").Replace("\"", "\"\"");
                    ahkContent.AppendLine($"    SendAsPaste(\"{replacement}\")");
                }
                if (!string.IsNullOrWhiteSpace(entry.ImagePath))
                {
                    ahkContent.AppendLine($"    PasteImage(\"{entry.ImagePath.Replace("\\", "\\\\")}\")");
                }
                ahkContent.AppendLine("}");
            }
            System.IO.File.WriteAllText(ahkPath, ahkContent.ToString(), System.Text.Encoding.UTF8);

            // Generate custom_hotkeys.ahk
            string customHotkeysPath = System.IO.Path.Combine(basePath, "custom_hotkeys.ahk");
            var customHotkeysContent = new System.Text.StringBuilder();
            customHotkeysContent.AppendLine("; Auto-generated direct hotkeys");
            
            System.Action<System.Collections.Generic.IEnumerable<TreeNodeViewModel>> scanHotkeys = null;
            scanHotkeys = (nodes) => {
                foreach (var node in nodes)
                {
                    if (node.MacroType != "Category" && !string.IsNullOrWhiteSpace(node.TriggerHotkey) && !string.IsNullOrWhiteSpace(node.Action))
                    {
                        customHotkeysContent.AppendLine($"{node.TriggerHotkey}:: {{");
                        if (node.Action.StartsWith("send:"))
                            customHotkeysContent.AppendLine($"    Send(\"{node.Action.Substring(5)}\")");
                        else if (node.Action.StartsWith("run:"))
                            customHotkeysContent.AppendLine($"    Run(\"{node.Action.Substring(4)}\")");
                        else if (node.Action.StartsWith("media:"))
                            customHotkeysContent.AppendLine($"    Send(\"{{{node.Action.Substring(6)}}}\")");
                        customHotkeysContent.AppendLine("}\n");
                    }
                    if (node.Children != null) scanHotkeys(node.Children);
                }
            };
            scanHotkeys(TreeVM.RootNodes);
            System.IO.File.WriteAllText(customHotkeysPath, customHotkeysContent.ToString(), System.Text.Encoding.UTF8);

            // Notify AHK to reload
            try
            {
                var allProcs = System.Diagnostics.Process.GetProcesses();
                foreach (var proc in allProcs)
                {
                    if (proc.ProcessName.Contains("AutoHotkey", System.StringComparison.OrdinalIgnoreCase))
                    {
                        try { proc.Kill(); } catch { }
                    }
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = System.IO.Path.Combine(basePath, "MacroEngine.ahk"), UseShellExecute = true });
            }
            catch { }

            // Reload config in current process
            (System.Windows.Application.Current as App)?.LoadConfig();

            System.Windows.MessageBox.Show("Configuration saved successfully!\nThe AHK engine has been reloaded.", "Save Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }
}
