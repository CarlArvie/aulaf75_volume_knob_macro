using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace MacroUI
{
    public partial class ConfigWindow : Window
    {
        public ObservableCollection<TreeNodeViewModel> RootNodes { get; set; } = new ObservableCollection<TreeNodeViewModel>();
        private TreeNodeViewModel _selectedNode;

        public ConfigWindow()
        {
            InitializeComponent();
            LoadData();
            MacroTreeView.ItemsSource = RootNodes;
        }

        private void LoadData()
        {
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "macros.json");
            if (!File.Exists(jsonPath)) jsonPath = "macros.json";

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
        }

        private void MacroTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _selectedNode = e.NewValue as TreeNodeViewModel;
            if (_selectedNode != null)
            {
                EditorPanel.Visibility = Visibility.Visible;
                EmptyStatePanel.Visibility = Visibility.Hidden;
                NameTextBox.Text = _selectedNode.Name;
                ActionTextBox.Text = _selectedNode.Action;
            }
            else
            {
                EditorPanel.Visibility = Visibility.Hidden;
                EmptyStatePanel.Visibility = Visibility.Visible;
            }
        }

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedNode != null && NameTextBox.IsFocused)
            {
                _selectedNode.Name = NameTextBox.Text;
            }
            if (_selectedNode != null && ActionTextBox.IsFocused)
            {
                _selectedNode.Action = ActionTextBox.Text;
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            var newNode = new TreeNodeViewModel($"category_{Guid.NewGuid().ToString().Substring(0, 4)}", new MacroNode { Name = "New Category", Children = new Dictionary<string, MacroNode>() }, _selectedNode);
            if (_selectedNode != null)
            {
                _selectedNode.Children.Add(newNode);
            }
            else
            {
                RootNodes.Add(newNode);
            }
        }

        private void AddMacro_Click(object sender, RoutedEventArgs e)
        {
            var newNode = new TreeNodeViewModel($"macro_{Guid.NewGuid().ToString().Substring(0, 4)}", new MacroNode { Name = "New Macro", Action = "send:" }, _selectedNode);
            if (_selectedNode != null)
            {
                _selectedNode.Children.Add(newNode);
            }
            else
            {
                RootNodes.Add(newNode);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode != null)
            {
                if (_selectedNode.Parent != null)
                {
                    _selectedNode.Parent.Children.Remove(_selectedNode);
                }
                else
                {
                    RootNodes.Remove(_selectedNode);
                }
                _selectedNode = null;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var rootDict = new Dictionary<string, MacroNode>();
            foreach (var node in RootNodes)
            {
                rootDict[node.Key] = node.ToMacroNode();
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(rootDict, options);

            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "macros.json");
            if (!File.Exists(jsonPath)) jsonPath = "macros.json";

            File.WriteAllText(jsonPath, json);

            // Notify MainWindow to reload config dynamically
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    mainWindow.ReloadConfig();
                    break;
                }
            }

            System.Windows.MessageBox.Show("Configuration saved successfully!\n\nThe radial menu is immediately updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }

    public class TreeNodeViewModel : INotifyPropertyChanged
    {
        public string Key { get; set; }
        public TreeNodeViewModel Parent { get; set; }
        public ObservableCollection<TreeNodeViewModel> Children { get; set; } = new ObservableCollection<TreeNodeViewModel>();

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

        public string Icon => string.IsNullOrEmpty(Action) ? "📁" : "⚡";

        public TreeNodeViewModel(string key, MacroNode node, TreeNodeViewModel parent)
        {
            Key = key;
            Parent = parent;
            Name = node.Name;
            Action = node.Action;
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
            var node = new MacroNode { Name = Name, Action = string.IsNullOrEmpty(Action) ? null : Action };
            if (Children.Count > 0)
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
