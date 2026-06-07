using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Text;

namespace MacroUI
{
    public partial class ConfigWindow : Window
    {
        public ObservableCollection<TreeNodeViewModel> RootNodes { get; set; } = new ObservableCollection<TreeNodeViewModel>();
        public ObservableCollection<HotstringEntry> Hotstrings { get; set; } = new ObservableCollection<HotstringEntry>();
        
        private TreeNodeViewModel _selectedNode;
        private bool _isUpdatingUI = false;

        public ConfigWindow()
        {
            InitializeComponent();
            LoadData();
            MacroTreeView.ItemsSource = RootNodes;
        }

        private void LoadData()
        {
            // Load Macros
            string jsonPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "macros.json"));

            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                var rootDict = JsonSerializer.Deserialize<Dictionary<string, MacroNode>>(json);
                RootNodes.Clear();
                foreach (var kvp in rootDict)
                {
                    RootNodes.Add(new TreeNodeViewModel(kvp.Key, kvp.Value, null));
                }
            }
            
            // Load Hotstrings
            string hotstringsPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "hotstrings.json"));
            
            Hotstrings.Clear();
            if (File.Exists(hotstringsPath))
            {
                try
                {
                    string json = File.ReadAllText(hotstringsPath);
                    var hotstringsList = JsonSerializer.Deserialize<List<HotstringEntry>>(json);
                    if (hotstringsList != null)
                    {
                        foreach (var entry in hotstringsList)
                        {
                            Hotstrings.Add(entry);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error loading hotstrings: " + ex.Message);
                }
            }
            
            // Load Settings
            string settingsPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "settings.json"));
            if (File.Exists(settingsPath))
            {
                try
                {
                    string settingsJson = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(settingsJson);
                    if (settings != null)
                    {
                        if (settings.ContainsKey("CenterTitle"))
                            CenterTitleTextBox.Text = settings["CenterTitle"];
                        
                        if (settings.ContainsKey("SelectKey"))
                            SelectKeyComboBox.Text = settings["SelectKey"];
                        else
                            SelectKeyComboBox.Text = "Volume_Mute";

                        if (settings.ContainsKey("BackKey"))
                            BackKeyComboBox.Text = settings["BackKey"];
                        else
                            BackKeyComboBox.Text = "RButton";
                    }
                }
                catch { }
            }
        }

        private void MacroTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _selectedNode = e.NewValue as TreeNodeViewModel;
            UpdateEditorUI();
        }

        private void UpdateEditorUI()
        {
            if (_selectedNode != null)
            {
                _isUpdatingUI = true;
                
                EditorPanel.Visibility = Visibility.Visible;
                EmptyStatePanel.Visibility = Visibility.Hidden;
                
                NameTextBox.Text = _selectedNode.Name;

                // Select the correct combobox item
                foreach (ComboBoxItem item in TypeComboBox.Items)
                {
                    if (item.Tag.ToString() == _selectedNode.MacroType)
                    {
                        TypeComboBox.SelectedItem = item;
                        break;
                    }
                }

                // Update visibility of input panels
                KeystrokePanel.Visibility = Visibility.Collapsed;
                ProgramPanel.Visibility = Visibility.Collapsed;
                TextPanel.Visibility = Visibility.Collapsed;

                if (_selectedNode.MacroType == "Send")
                {
                    KeystrokePanel.Visibility = Visibility.Visible;
                    KeystrokeTextBox.Text = _selectedNode.RawActionValue;
                }
                else if (_selectedNode.MacroType == "Run")
                {
                    ProgramPanel.Visibility = Visibility.Visible;
                    ProgramTextBox.Text = _selectedNode.RawActionValue;
                }
                else if (_selectedNode.MacroType == "SendText")
                {
                    TextPanel.Visibility = Visibility.Visible;
                    TextTextBox.Text = _selectedNode.RawActionValue;
                }
                
                _isUpdatingUI = false;
            }
            else
            {
                EditorPanel.Visibility = Visibility.Hidden;
                EmptyStatePanel.Visibility = Visibility.Visible;
            }
        }

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            
            if (TypeComboBox.SelectedItem is ComboBoxItem item)
            {
                string newType = item.Tag.ToString();
                if (_selectedNode.MacroType != newType)
                {
                    _selectedNode.MacroType = newType;
                    // Auto-focus the corresponding box and refresh UI
                    UpdateEditorUI();
                }
            }
        }

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            if (NameTextBox.IsFocused)
            {
                _selectedNode.Name = NameTextBox.Text;
            }
        }

        private void RawField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            
            if (_selectedNode.MacroType == "Send" && KeystrokeTextBox.IsFocused)
                _selectedNode.RawActionValue = KeystrokeTextBox.Text;
            else if (_selectedNode.MacroType == "Run" && ProgramTextBox.IsFocused)
                _selectedNode.RawActionValue = ProgramTextBox.Text;
            else if (_selectedNode.MacroType == "SendText" && TextTextBox.IsFocused)
                _selectedNode.RawActionValue = TextTextBox.Text;
        }

        private void CenterTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Do nothing, just capturing so xaml compiles. We save on Save_Click.
        }

        private void EditNode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.DataContext is TreeNodeViewModel ctxNode)
            {
                _selectedNode = ctxNode;
                _selectedNode.IsSelected = true;
                UpdateEditorUI();
                NameTextBox.Focus();
            }
        }

        private void LeftPanel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(source);
                while (parent != null)
                {
                    if (parent is TreeViewItem || parent is System.Windows.Controls.Button || parent is System.Windows.Controls.TextBox) return;
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
            }

            if (_selectedNode != null)
            {
                _selectedNode.IsSelected = false;
                _selectedNode = null;
                UpdateEditorUI();
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            TreeNodeViewModel targetNode = _selectedNode;
            if (sender is System.Windows.Controls.MenuItem mi && mi.DataContext is TreeNodeViewModel ctxNode)
                targetNode = ctxNode;

            var newNode = new TreeNodeViewModel($"category_{Guid.NewGuid().ToString().Substring(0, 4)}", new MacroNode { Name = "New Category", Children = new Dictionary<string, MacroNode>() }, targetNode);
            if (targetNode != null)
                targetNode.Children.Add(newNode);
            else
                RootNodes.Add(newNode);
        }

        private void AddMacro_Click(object sender, RoutedEventArgs e)
        {
            TreeNodeViewModel targetNode = _selectedNode;
            if (sender is System.Windows.Controls.MenuItem mi && mi.DataContext is TreeNodeViewModel ctxNode)
                targetNode = ctxNode;

            var newNode = new TreeNodeViewModel($"macro_{Guid.NewGuid().ToString().Substring(0, 4)}", new MacroNode { Name = "New Macro", Action = "send:" }, targetNode);
            if (targetNode != null)
                targetNode.Children.Add(newNode);
            else
                RootNodes.Add(newNode);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            TreeNodeViewModel targetNode = _selectedNode;
            if (sender is System.Windows.Controls.MenuItem mi && mi.DataContext is TreeNodeViewModel ctxNode)
                targetNode = ctxNode;

            if (targetNode != null)
            {
                if (targetNode.Parent != null)
                    targetNode.Parent.Children.Remove(targetNode);
                else
                    RootNodes.Remove(targetNode);
                
                if (_selectedNode == targetNode)
                {
                    _selectedNode = null;
                    UpdateEditorUI();
                }
            }
        }

        private void DeleteHotstring_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is HotstringEntry entry)
            {
                Hotstrings.Remove(entry);
            }
        }

        private void AddHotstring_Click(object sender, RoutedEventArgs e)
        {
            Hotstrings.Add(new HotstringEntry { Trigger = "", Replacement = "" });
        }

        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            // --- 1. Save Macros ---
            var rootDict = new Dictionary<string, MacroNode>();
            foreach (var node in RootNodes)
            {
                rootDict[node.Key] = node.ToMacroNode();
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string macrosJson = JsonSerializer.Serialize(rootDict, options);
            string macrosPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "macros.json"));
            File.WriteAllText(macrosPath, macrosJson);

            // --- 2. Save Settings ---
            var settings = new Dictionary<string, string>
            {
                { "CenterTitle", CenterTitleTextBox.Text },
                { "SelectKey", SelectKeyComboBox.Text },
                { "BackKey", BackKeyComboBox.Text }
            };
            string settingsPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "settings.json"));
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, options));

            // Also save as settings.ini for AHK to easily read
            string iniPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "settings.ini"));
            string iniContent = $"[Hotkeys]\nSelect={SelectKeyComboBox.Text}\nBack={BackKeyComboBox.Text}\n";
            File.WriteAllText(iniPath, iniContent);

            // --- 3. Save Hotstrings ---
            var validHotstrings = new List<HotstringEntry>();
            foreach (var entry in Hotstrings)
            {
                if (!string.IsNullOrWhiteSpace(entry.Trigger) && !string.IsNullOrWhiteSpace(entry.Replacement))
                {
                    validHotstrings.Add(entry);
                }
            }

            string hotstringsJson = JsonSerializer.Serialize(validHotstrings, options);
            string hotstringsPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "hotstrings.json"));
            File.WriteAllText(hotstringsPath, hotstringsJson);

            // Generate hotstrings.ahk
            string ahkPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "hotstrings.ahk"));
            StringBuilder ahkContent = new StringBuilder();
            ahkContent.AppendLine("; Auto-generated hotstrings");
            foreach (var entry in validHotstrings)
            {
                string trigger = entry.Trigger.Replace(":", "`:"); // Escape colons
                string replacement = entry.Replacement
                    .Replace("`", "``")
                    .Replace("\r", "`r")
                    .Replace("\n", "`n")
                    .Replace("\"", "\"\"");
                
                ahkContent.AppendLine($"::{trigger}::");
                ahkContent.AppendLine($"SendAsPaste(\"{replacement}\")");
                ahkContent.AppendLine("return");
            }
            File.WriteAllText(ahkPath, ahkContent.ToString(), Encoding.UTF8);

            // --- 4. Notify & Reload ---
            string execFile = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "execute.txt"));
            File.WriteAllText(execFile, "RELOAD");

            NotifyMainWindow();

            System.Windows.MessageBox.Show("All settings saved successfully!\n\nThe Macro Engine has been reloaded.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void NotifyMainWindow()
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    mainWindow.ReloadConfig();
                    break;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            App.MinimizeMemoryFootprint();
        }
    }

    public class HotstringEntry : INotifyPropertyChanged
    {
        private string _trigger;
        public string Trigger { get => _trigger; set { _trigger = value; OnPropertyChanged(nameof(Trigger)); } }

        private string _replacement;
        public string Replacement { get => _replacement; set { _replacement = value; OnPropertyChanged(nameof(Replacement)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public class TreeNodeViewModel : INotifyPropertyChanged
    {
        public string Key { get; set; }
        public TreeNodeViewModel Parent { get; set; }
        public ObservableCollection<TreeNodeViewModel> Children { get; set; } = new ObservableCollection<TreeNodeViewModel>();

        private bool _isSelected;
        public bool IsSelected 
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        private string _action;
        public string Action
        {
            get => _action;
            set 
            { 
                _action = value; 
                OnPropertyChanged(nameof(Action)); 
                OnPropertyChanged(nameof(Icon));
            }
        }

        public string Icon => (MacroType == "Category") ? "📁" : "⚡";

        private string _macroType;
        public string MacroType 
        {
            get => _macroType;
            set { _macroType = value; OnPropertyChanged(nameof(MacroType)); RebuildAction(); OnPropertyChanged(nameof(Icon)); }
        }

        private string _rawActionValue;
        public string RawActionValue 
        {
            get => _rawActionValue;
            set { _rawActionValue = value; OnPropertyChanged(nameof(RawActionValue)); RebuildAction(); }
        }

        private bool _isParsing = false;

        private void RebuildAction()
        {
            if (_isParsing) return;

            if (MacroType == "Category")
                Action = "";
            else if (MacroType == "Send")
                Action = "send:" + RawActionValue;
            else if (MacroType == "Run")
                Action = "run:" + RawActionValue;
            else if (MacroType == "SendText")
                Action = "sendtext:" + RawActionValue;
        }

        private void ParseAction()
        {
            _isParsing = true;
            if (string.IsNullOrEmpty(Action))
            {
                MacroType = "Category";
                RawActionValue = "";
            }
            else if (Action.StartsWith("sendtext:"))
            {
                MacroType = "SendText";
                RawActionValue = Action.Substring(9);
            }
            else if (Action.StartsWith("send:"))
            {
                MacroType = "Send";
                RawActionValue = Action.Substring(5);
            }
            else if (Action.StartsWith("run:"))
            {
                MacroType = "Run";
                RawActionValue = Action.Substring(4);
            }
            else
            {
                MacroType = "Send";
                RawActionValue = Action;
            }
            _isParsing = false;
        }

        public TreeNodeViewModel(string key, MacroNode node, TreeNodeViewModel parent)
        {
            Key = key;
            Parent = parent;
            Name = node.Name;
            Action = node.Action;
            ParseAction();
            if (node.Children != null)
            {
                foreach (var kvp in node.Children)
                {
                    Children.Add(new TreeNodeViewModel(kvp.Key, kvp.Value, this));
                }
            }
        }

        public MacroNode ToMacroNode()
        {
            var node = new MacroNode { Name = Name, Action = (MacroType == "Category") ? null : Action };
            if (Children.Count > 0 || MacroType == "Category")
            {
                node.Children = new Dictionary<string, MacroNode>();
                foreach (var child in Children)
                {
                    node.Children[child.Key] = child.ToMacroNode();
                }
            }
            return node;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}


