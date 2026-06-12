using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MacroUI.ViewModels;
using MacroUI.Services;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Xml;

namespace MacroUI
{
    public partial class ConfigWindow : Window
    {
        public ConfigWindowViewModel ViewModel { get; }
        
        private TreeNodeViewModel _selectedNode;
        private bool _isUpdatingUI = false;

        public ConfigWindow(ISettingsService settingsService)
        {
            InitializeComponent();
            
            string macrosPath = App.GetProjectRootFile("macros.json");
            string hotstringsPath = App.GetProjectRootFile("hotstrings.json");
            string settingsPath = App.GetProjectRootFile("settings.json");
            
            ViewModel = new ConfigWindowViewModel(settingsService, macrosPath, hotstringsPath, settingsPath);
            DataContext = ViewModel;

            MacroTreeView.ItemsSource = ViewModel.TreeVM.RootNodes;
            
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

                foreach (ComboBoxItem item in TypeComboBox.Items)
                {
                    if (item.Tag.ToString() == _selectedNode.MacroType)
                    {
                        TypeComboBox.SelectedItem = item;
                        break;
                    }
                }

                KeystrokePanel.Visibility = Visibility.Collapsed;
                ProgramPanel.Visibility = Visibility.Collapsed;
                TextPanel.Visibility = Visibility.Collapsed;
                RawAHKPanel.Visibility = Visibility.Collapsed;
                SystemCommandPanel.Visibility = Visibility.Collapsed;

                if (_selectedNode.MacroType == "Category")
                {
                    // No extra input needed
                }
                else if (_selectedNode.MacroType == "Send")
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
                    RawAHKEditor.Text = _selectedNode.RawActionValue;
                }
                else if (_selectedNode.MacroType == "SystemCommand")
                {
                    SystemCommandPanel.Visibility = Visibility.Visible;
                    SystemCommandComboBox.Text = _selectedNode.RawActionValue;
                }

                TargetProcessTextBox.Text = _selectedNode.TargetProcess;
                TriggerHotkeyTextBox.Text = _selectedNode.TriggerHotkey;
                ImagePathTextBlock.Text = string.IsNullOrEmpty(_selectedNode.ImagePath) ? "No image selected." : _selectedNode.ImagePath;

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
                _selectedNode.MacroType = item.Tag.ToString();
                UpdateEditorUI();
            }
        }

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            
            if (sender == NameTextBox)
                _selectedNode.Name = NameTextBox.Text;
        }

        private void ProcessOrHotkey_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;

            if (sender == TargetProcessTextBox)
                _selectedNode.TargetProcess = TargetProcessTextBox.Text;
            else if (sender == TriggerHotkeyTextBox)
                _selectedNode.TriggerHotkey = TriggerHotkeyTextBox.Text;
        }

        private void RawField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            
            if (sender == KeystrokeTextBox)
                _selectedNode.RawActionValue = KeystrokeTextBox.Text;
            else if (sender == ProgramTextBox)
                _selectedNode.RawActionValue = ProgramTextBox.Text;
            else if (sender == TextTextBox)
                _selectedNode.RawActionValue = TextTextBox.Text;
        }

        private void RawAHKEditor_TextChanged(object sender, EventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            _selectedNode.RawActionValue = RawAHKEditor.Text;
        }

        private void SystemCommandComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            
            if (SystemCommandComboBox.SelectedItem is ComboBoxItem item)
            {
                _selectedNode.RawActionValue = item.Content.ToString();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox textBox)
            {
                textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
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

        private void LeftPanel_MouseDown(object sender, MouseButtonEventArgs e)
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

        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg;*.gif)|*.png;*.jpeg;*.jpg;*.gif|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                if (_selectedNode != null)
                {
                    _selectedNode.ImagePath = openFileDialog.FileName;
                    ImagePathTextBlock.Text = openFileDialog.FileName;
                }
            }
        }

        private void ClearImage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode != null)
            {
                _selectedNode.ImagePath = "";
                ImagePathTextBlock.Text = "No image selected.";
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

        // Dummy handlers for drag and drop because they are in XAML and not MVVM yet.
        private void TreeView_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {}
        private void TreeView_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) {}
        private void TreeView_DragEnter(object sender, System.Windows.DragEventArgs e) {}
        private void TreeView_Drop(object sender, System.Windows.DragEventArgs e) {}

        private void CenterTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel?.SettingsVM?.Settings != null && sender is System.Windows.Controls.TextBox tb)
                ViewModel.SettingsVM.Settings.CenterTitle = tb.Text;
        }

        private void MacroSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Optional: Implement Search logic for tree
        }
        
        private void HotstringSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Optional: Implement Search logic for hotstrings
        }
        
        private void IconLibraryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUI || _selectedNode == null) return;
            if (sender is System.Windows.Controls.ComboBox cb && cb.SelectedItem is ComboBoxItem item)
            {
                _selectedNode.IconUnicode = item.Tag?.ToString() ?? "";
            }
        }
    }
}
