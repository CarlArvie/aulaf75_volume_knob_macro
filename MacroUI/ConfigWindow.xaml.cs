using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Text;
using Microsoft.Win32;
using System.IO.Compression;
using System.Windows.Input;
using MacroUI.ViewModels;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Xml;

namespace MacroUI
{
    public partial class ConfigWindow : Window
    {
        public MacroTreeViewModel TreeVM { get; set; } = new MacroTreeViewModel();
        public ObservableCollection<HotstringEntry> Hotstrings { get; set; } = new ObservableCollection<HotstringEntry>();
        
        public SettingsViewModel SettingsVM { get; set; }
        
        private TreeNodeViewModel _selectedNode;
        private bool _isUpdatingUI = false;
        private AppSettings _appSettings = new AppSettings();
        private Stack<Action> _undoStack = new Stack<Action>();

        public ConfigWindow()
        {
            InitializeComponent();
            LoadData();
            MacroTreeView.ItemsSource = TreeVM.RootNodes;
            TreeVM.PropertyChanged += (s, e) => { if (e.PropertyName == "CanUndo") UpdateUndoButton(); };
            
            // Load custom AHK syntax highlighting for AvalonEdit
            try
            {
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("MacroUI.AHK.xshd"))
                {
                    if (stream != null)
                    {
                        using (var reader = new XmlTextReader(stream))
                        {
                            RawAHKEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading AHK syntax highlighting: " + ex.Message);
            }
        }

        private void LoadData()
        {
            // Load Macros
            string jsonPath = App.GetProjectRootFile("macros.json");
            TreeVM.LoadMacros(jsonPath);
            
            // Load Hotstrings
            string hotstringsPath = App.GetProjectRootFile("hotstrings.json");
            
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
                            if (entry.IsSecure && !string.IsNullOrEmpty(entry.Replacement))
                            {
                                entry.Replacement = CryptoHelper.Decrypt(entry.Replacement);
                            }
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
            string settingsPath = App.GetProjectRootFile("settings.json");
            if (File.Exists(settingsPath))
            {
                try
                {
                    string settingsJson = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(settingsJson);
                    if (settings != null)
                    {
                        _appSettings = settings;
                    }
                }
                catch { }
            }
            
            SettingsVM = new SettingsViewModel(_appSettings);
            SettingsVM.ImportCompleted += (s, ev) => LoadData();
            AestheticsTab.DataContext = SettingsVM;

            CenterTitleTextBox.Text = _appSettings.CenterTitle;
            if (!string.IsNullOrEmpty(_appSettings.CenterImagePath))
            {
                CenterLogoTextBlock.Text = _appSettings.CenterImagePath;
            }
            SelectKeyComboBox.Text = _appSettings.SelectKey;
            BackKeyComboBox.Text = _appSettings.BackKey;
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
                TargetProcessPanel.Visibility = Visibility.Collapsed;
                TriggerHotkeyPanel.Visibility = Visibility.Collapsed;
                RawAHKPanel.Visibility = Visibility.Collapsed;
                SystemCommandPanel.Visibility = Visibility.Collapsed;

                if (_selectedNode.MacroType == "Category")
                {
                    TargetProcessPanel.Visibility = Visibility.Visible;
                    TargetProcessTextBox.Text = _selectedNode.TargetProcess;
                }
                else
                {
                    TriggerHotkeyPanel.Visibility = Visibility.Visible;
                    TriggerHotkeyTextBox.Text = _selectedNode.TriggerHotkey;

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
                    else if (_selectedNode.MacroType == "RawAHK")
                    {
                        RawAHKPanel.Visibility = Visibility.Visible;
                        RawAHKEditor.Text = _selectedNode.RawActionValue ?? "";
                    }
                    else if (_selectedNode.MacroType == "SystemCommand")
                    {
                        SystemCommandPanel.Visibility = Visibility.Visible;
                        foreach (ComboBoxItem item in SystemCommandComboBox.Items)
                        {
                            if (item.Tag.ToString() == _selectedNode.RawActionValue)
                            {
                                SystemCommandComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                }
                
                ImagePathTextBlock.Text = string.IsNullOrWhiteSpace(_selectedNode.ImagePath) ? "No image selected." : _selectedNode.ImagePath;

                bool foundIcon = false;
                foreach (ComboBoxItem item in IconLibraryComboBox.Items)
                {
                    if ((item.Tag?.ToString() ?? "") == (_selectedNode.IconUnicode ?? ""))
                    {
                        IconLibraryComboBox.SelectedItem = item;
                        foundIcon = true;
                        break;
                    }
                }
                if (!foundIcon) IconLibraryComboBox.SelectedIndex = 0;

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

        private void RawAHKEditor_TextChanged(object sender, EventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            
            if (_selectedNode.MacroType == "RawAHK")
            {
                _selectedNode.RawActionValue = RawAHKEditor.Text;
            }
        }

        private void SystemCommandComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            if (_selectedNode.MacroType == "SystemCommand" && SystemCommandComboBox.SelectedItem is ComboBoxItem item)
            {
                _selectedNode.RawActionValue = item.Tag.ToString();
            }
        }

        private void ProcessOrHotkey_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            if (TargetProcessTextBox.IsFocused)
                _selectedNode.TargetProcess = TargetProcessTextBox.Text;
            else if (TriggerHotkeyTextBox.IsFocused)
                _selectedNode.TriggerHotkey = TriggerHotkeyTextBox.Text;
        }
        private void IconLibraryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            if (IconLibraryComboBox.SelectedItem is ComboBoxItem item)
            {
                _selectedNode.IconUnicode = item.Tag?.ToString();
            }
        }



        private void CenterTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI) return;
            if (_appSettings == null) return;
            
            _appSettings.CenterTitle = CenterTitleTextBox.Text;
        }

        private void DeleteOldImage(string relativePath)
        {
            if (!string.IsNullOrEmpty(relativePath))
            {
                try
                {
                    string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", relativePath));
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
                catch { } // Ignore if locked or missing
            }
        }

        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;
            
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico|All Files (*.*)|*.*";
            
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string imagesDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Images"));
                    if (!Directory.Exists(imagesDir))
                    {
                        Directory.CreateDirectory(imagesDir);
                    }
                    
                    string ext = System.IO.Path.GetExtension(dlg.FileName);
                    string newFileName = Guid.NewGuid().ToString() + ext;
                    string newPath = System.IO.Path.Combine(imagesDir, newFileName);
                    
                    File.Copy(dlg.FileName, newPath, true);
                    
                    // Delete old image if one existed
                    if (!string.IsNullOrEmpty(_selectedNode.ImagePath))
                    {
                        DeleteOldImage(_selectedNode.ImagePath);
                    }
                    
                    string relativePath = System.IO.Path.Combine("Images", newFileName);
                    _selectedNode.ImagePath = relativePath;
                    ImagePathTextBlock.Text = relativePath;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Failed to copy image: " + ex.Message);
                }
            }
        }

        private void ClearImage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode != null)
            {
                if (!string.IsNullOrEmpty(_selectedNode.ImagePath))
                {
                    DeleteOldImage(_selectedNode.ImagePath);
                }
                _selectedNode.ImagePath = null;
                ImagePathTextBlock.Text = "No image selected.";
            }
        }

        private void BrowseCenterLogo_Click(object sender, RoutedEventArgs e)
        {
            if (_appSettings == null) return;
            
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico|All Files (*.*)|*.*";
            
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string imagesDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Images"));
                    if (!Directory.Exists(imagesDir))
                    {
                        Directory.CreateDirectory(imagesDir);
                    }
                    
                    string ext = System.IO.Path.GetExtension(dlg.FileName);
                    string newFileName = Guid.NewGuid().ToString() + ext;
                    string newPath = System.IO.Path.Combine(imagesDir, newFileName);
                    
                    File.Copy(dlg.FileName, newPath, true);
                    
                    // Delete old image if one existed
                    if (!string.IsNullOrEmpty(_appSettings.CenterImagePath))
                    {
                        DeleteOldImage(_appSettings.CenterImagePath);
                    }
                    
                    string relativePath = System.IO.Path.Combine("Images", newFileName);
                    _appSettings.CenterImagePath = relativePath;
                    CenterLogoTextBlock.Text = relativePath;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Failed to copy image: " + ex.Message);
                }
            }
        }

        private void ClearCenterLogo_Click(object sender, RoutedEventArgs e)
        {
            if (_appSettings != null)
            {
                if (!string.IsNullOrEmpty(_appSettings.CenterImagePath))
                {
                    DeleteOldImage(_appSettings.CenterImagePath);
                }
                _appSettings.CenterImagePath = null;
                CenterLogoTextBlock.Text = "No logo selected.";
            }
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
                TreeVM.RootNodes.Add(newNode);
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
                TreeVM.RootNodes.Add(newNode);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            TreeNodeViewModel targetNode = _selectedNode;
            if (sender is System.Windows.Controls.MenuItem mi && mi.DataContext is TreeNodeViewModel ctxNode)
                targetNode = ctxNode;

            if (targetNode != null)
            {
                var parent = targetNode.Parent;
                var list = parent != null ? parent.Children : TreeVM.RootNodes;
                int index = list.IndexOf(targetNode);
                
                list.Remove(targetNode);
                
                if (_selectedNode == targetNode)
                {
                    _selectedNode = null;
                    UpdateEditorUI();
                }

                TreeVM.PushUndo(() => {
                    list.Insert(index, targetNode);
                });
                UpdateUndoButton();
            }
        }

        private void DeleteHotstring_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is HotstringEntry entry)
            {
                int index = Hotstrings.IndexOf(entry);
                Hotstrings.Remove(entry);
                
                _undoStack.Push(() => {
                    Hotstrings.Insert(index, entry);
                });
                UpdateUndoButton();
            }
        }

        private void BrowseHotstringImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is HotstringEntry entry)
            {
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
                openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg;*.gif)|*.png;*.jpeg;*.jpg;*.gif|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == true)
                {
                    entry.ImagePath = openFileDialog.FileName;
                }
            }
        }

        private void ClearHotstringImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is HotstringEntry entry)
            {
                entry.ImagePath = null;
            }
        }

        private void AddHotstring_Click(object sender, RoutedEventArgs e)
        {
            Hotstrings.Add(new HotstringEntry { Trigger = "", Replacement = "" });
        }

        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            // --- 1. Save Macros ---
            string macrosPath = App.GetProjectRootFile("macros.json");
            TreeVM.SaveMacros(macrosPath);

            var options = new JsonSerializerOptions { WriteIndented = true };

            // --- 2. Save Settings ---
            _appSettings.SelectKey = SelectKeyComboBox.Text;
            _appSettings.BackKey = BackKeyComboBox.Text;
            _appSettings.EnableAutoSelect = EnableAutoSelectCheckBox.IsChecked ?? true;
            _appSettings.EnableGameMode = EnableGameModeCheckBox.IsChecked ?? false;
            string settingsPath = App.GetProjectRootFile("settings.json");
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(_appSettings, options));

            // Also save as settings.ini for AHK to easily read
            string iniPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "settings.ini"));
            string iniContent = $"[Hotkeys]\nSelect={SelectKeyComboBox.Text}\nBack={BackKeyComboBox.Text}\n";
            File.WriteAllText(iniPath, iniContent);

            // --- 3. Save Hotstrings ---
            var validHotstrings = new List<HotstringEntry>();
            var secureCopies = new List<HotstringEntry>();
            foreach (var entry in Hotstrings)
            {
                if (!string.IsNullOrWhiteSpace(entry.Trigger) && !string.IsNullOrWhiteSpace(entry.Replacement))
                {
                    validHotstrings.Add(entry);
                    var copy = new HotstringEntry { Trigger = entry.Trigger, Replacement = entry.Replacement, ImagePath = entry.ImagePath, MatchTypedCase = entry.MatchTypedCase, IsSecure = entry.IsSecure };
                    if (copy.IsSecure && !string.IsNullOrEmpty(copy.Replacement))
                    {
                        copy.Replacement = CryptoHelper.Encrypt(copy.Replacement);
                    }
                    secureCopies.Add(copy);
                }
            }

            string hotstringsJson = JsonSerializer.Serialize(secureCopies, options);
            string hotstringsPath = App.GetProjectRootFile("hotstrings.json");
            File.WriteAllText(hotstringsPath, hotstringsJson);

            // Generate hotstrings.ahk
            string ahkPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "hotstrings.ahk"));
            StringBuilder ahkContent = new StringBuilder();
            ahkContent.AppendLine("; Auto-generated hotstrings");
            foreach (var entry in validHotstrings)
            {
                string trigger = entry.Trigger.Replace(":", "`:"); // Escape colons
                string ahkOptions = entry.MatchTypedCase ? "" : "C";
                
                ahkContent.AppendLine($":{ahkOptions}:{trigger}::");
                
                if (!string.IsNullOrWhiteSpace(entry.Replacement))
                {
                    string replacement = entry.Replacement
                        .Replace("`", "``")
                        .Replace("\r", "`r")
                        .Replace("\n", "`n")
                        .Replace("\"", "\"\"");
                    
                    ahkContent.AppendLine($"SendAsPaste(\"{replacement}\")");
                }
                
                if (!string.IsNullOrWhiteSpace(entry.ImagePath))
                {
                    ahkContent.AppendLine($"PasteImage(\"{entry.ImagePath.Replace("\\", "\\\\")}\")");
                }
                
                ahkContent.AppendLine("return");
            }
            File.WriteAllText(ahkPath, ahkContent.ToString(), Encoding.UTF8);

            // --- 3.5 Generate custom_hotkeys.ahk ---
            string customHotkeysPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "custom_hotkeys.ahk"));
            StringBuilder customHotkeysContent = new StringBuilder();
            customHotkeysContent.AppendLine("; Auto-generated direct hotkeys");
            
            // Define a recursive function to find all hotkeys
            Action<IEnumerable<TreeNodeViewModel>> scanHotkeys = null;
            scanHotkeys = (nodes) => {
                foreach (var node in nodes)
                {
                    if (node.MacroType != "Category" && !string.IsNullOrWhiteSpace(node.TriggerHotkey) && !string.IsNullOrWhiteSpace(node.Action))
                    {
                        string hotkey = node.TriggerHotkey;
                        string action = node.Action;
                        
                        customHotkeysContent.AppendLine($"{hotkey}::");
                        
                        if (action.StartsWith("send:"))
                        {
                            string keys = action.Substring(5);
                            customHotkeysContent.AppendLine($"SendInput, {keys}");
                        }
                        else if (action.StartsWith("run:"))
                        {
                            string prog = action.Substring(4);
                            customHotkeysContent.AppendLine($"Run, {prog}");
                        }
                        else if (action.StartsWith("sendtext:"))
                        {
                            string text = action.Substring(9)
                                .Replace("`", "``")
                                .Replace("\r", "`r")
                                .Replace("\n", "`n")
                                .Replace("\"", "\"\"");
                            customHotkeysContent.AppendLine($"SendAsPaste(\"{text}\")");
                        }
                        customHotkeysContent.AppendLine("return");
                    }
                    if (node.Children != null && node.Children.Count > 0)
                    {
                        scanHotkeys(node.Children);
                    }
                }
            };
            scanHotkeys(TreeVM.RootNodes);
            File.WriteAllText(customHotkeysPath, customHotkeysContent.ToString(), Encoding.UTF8);

            // --- 4. Notify & Reload ---
            string execFile = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "execute.txt"));
            File.WriteAllText(execFile, "RELOAD");

            NotifyMainWindow();

            System.Windows.MessageBox.Show("All settings saved successfully!\n\nThe Macro Engine has been reloaded.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void NotifyMainWindow()
        {
            if (System.Windows.Application.Current is App app)
            {
                app.LoadConfig();
            }
        }



        private void UpdateUndoButton()
        {
            UndoButton.Visibility = (_undoStack.Count > 0 || TreeVM.CanUndo) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (TreeVM.CanUndo)
            {
                TreeVM.UndoCommand.Execute(null);
            }
            else if (_undoStack.Count > 0)
            {
                var action = _undoStack.Pop();
                action();
                UpdateUndoButton();
            }
        }


        private void MacroSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = MacroSearchBox.Text.Trim().ToLower();
            MacroSearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
            
            foreach (var node in TreeVM.RootNodes)
            {
                FilterNode(node, query);
            }
        }

        private bool FilterNode(TreeNodeViewModel node, string query)
        {
            bool match = string.IsNullOrEmpty(query) || (node.Name?.ToLower().Contains(query) ?? false) || (node.TriggerHotkey?.ToLower().Contains(query) ?? false);
            bool childMatch = false;
            foreach (var child in node.Children)
            {
                if (FilterNode(child, query))
                    childMatch = true;
            }
            node.IsVisible = match || childMatch;
            return node.IsVisible;
        }

        private void HotstringSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = HotstringSearchBox.Text.Trim().ToLower();
            HotstringSearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

            foreach (var hs in Hotstrings)
            {
                hs.IsVisible = string.IsNullOrEmpty(query) || (hs.Trigger?.ToLower().Contains(query) ?? false) || (hs.Replacement?.ToLower().Contains(query) ?? false);
            }
        }



        private System.Windows.Point _startPoint;
        private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
        }

        private void TreeView_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    System.Windows.Controls.TreeView treeView = sender as System.Windows.Controls.TreeView;
                    TreeViewItem treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
                    if (treeViewItem != null)
                    {
                        TreeNodeViewModel dragData = treeViewItem.DataContext as TreeNodeViewModel;
                        if (dragData != null)
                        {
                            System.Windows.DragDrop.DoDragDrop(treeViewItem, dragData, System.Windows.DragDropEffects.Move);
                        }
                    }
                }
            }
        }

        private void TreeView_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(TreeNodeViewModel)))
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        private void TreeView_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TreeNodeViewModel)))
            {
                TreeNodeViewModel sourceNode = (TreeNodeViewModel)e.Data.GetData(typeof(TreeNodeViewModel));
                TreeViewItem targetItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

                if (targetItem != null)
                {
                    TreeNodeViewModel targetNode = targetItem.DataContext as TreeNodeViewModel;
                    if (targetNode != null && targetNode != sourceNode && !TreeVM.IsDescendant(sourceNode, targetNode))
                    {
                        System.Windows.Point dropPosition = e.GetPosition(targetItem);
                        double targetHeight = targetItem.ActualHeight;

                        // Check position
                        MacroTreeViewModel.DropPosition pos = MacroTreeViewModel.DropPosition.Inside;

                        if (dropPosition.Y < targetHeight * 0.25)
                        {
                            pos = MacroTreeViewModel.DropPosition.Above;
                        }
                        else if (dropPosition.Y > targetHeight * 0.75)
                        {
                            pos = MacroTreeViewModel.DropPosition.Below;
                        }

                        // If it's a Macro (not a Category) and we try to drop inside, change to Below instead
                        if (pos == MacroTreeViewModel.DropPosition.Inside && targetNode.MacroType != "Category")
                        {
                            pos = MacroTreeViewModel.DropPosition.Below;
                        }

                        TreeVM.HandleDrop(sourceNode, targetNode, pos);
                        if (pos == MacroTreeViewModel.DropPosition.Inside && targetNode.MacroType == "Category")
                        {
                            targetItem.IsExpanded = true;
                        }
                        UpdateUndoButton();
                    }
                }
                else
                {
                    TreeVM.HandleDropToRoot(sourceNode);
                    UpdateUndoButton();
                }
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }

    public class HotstringEntry : INotifyPropertyChanged
    {
        private string _trigger;
        public string Trigger { get => _trigger; set { _trigger = value; OnPropertyChanged(nameof(Trigger)); } }

        private string _replacement;
        public string Replacement { get => _replacement; set { _replacement = value; OnPropertyChanged(nameof(Replacement)); } }

        private bool _matchTypedCase = true;
        public bool MatchTypedCase { get => _matchTypedCase; set { _matchTypedCase = value; OnPropertyChanged(nameof(MatchTypedCase)); } }

        private string _imagePath;
        public string ImagePath { get => _imagePath; set { _imagePath = value; OnPropertyChanged(nameof(ImagePath)); } }

        private bool _isVisible = true;
        public bool IsVisible { get => _isVisible; set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); } }

        private bool _isSecure = false;
        public bool IsSecure { get => _isSecure; set { _isSecure = value; OnPropertyChanged(nameof(IsSecure)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }


}


