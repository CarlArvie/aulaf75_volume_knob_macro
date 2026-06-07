using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using MacroUI.Commands;

namespace MacroUI.ViewModels
{
    public class MacroTreeViewModel : ViewModelBase
    {
        public ObservableCollection<TreeNodeViewModel> RootNodes { get; set; } = new ObservableCollection<TreeNodeViewModel>();

        private TreeNodeViewModel _selectedNode;
        public TreeNodeViewModel SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != null) _selectedNode.IsSelected = false;
                _selectedNode = value;
                if (_selectedNode != null) _selectedNode.IsSelected = true;
                OnPropertyChanged(nameof(SelectedNode));
            }
        }

        public Stack<Action> UndoStack { get; } = new Stack<Action>();

        public void PushUndo(Action action)
        {
            UndoStack.Push(action);
            OnPropertyChanged(nameof(CanUndo));
        }

        public bool CanUndo => UndoStack.Count > 0;

        public ICommand UndoCommand => new RelayCommand(_ =>
        {
            if (CanUndo)
            {
                var action = UndoStack.Pop();
                action();
                OnPropertyChanged(nameof(CanUndo));
            }
        });

        // Enum for drop position
        public enum DropPosition { Above, Below, Inside }

        public void HandleDrop(TreeNodeViewModel sourceNode, TreeNodeViewModel targetNode, DropPosition position)
        {
            if (sourceNode == null || targetNode == null || sourceNode == targetNode) return;
            
            // Prevent dropping a parent into its own child
            if (IsDescendant(sourceNode, targetNode)) return;

            var sourceList = sourceNode.Parent != null ? sourceNode.Parent.Children : RootNodes;
            var oldParent = sourceNode.Parent;
            int oldIndex = sourceList.IndexOf(sourceNode);

            sourceList.Remove(sourceNode);

            var targetList = targetNode.Parent != null ? targetNode.Parent.Children : RootNodes;
            
            if (position == DropPosition.Inside && targetNode.MacroType == "Category")
            {
                targetNode.Children.Add(sourceNode);
                sourceNode.Parent = targetNode;
                // Expanding target node should be handled by view if possible, or bind IsExpanded
            }
            else
            {
                int insertIndex = targetList.IndexOf(targetNode);
                if (position == DropPosition.Below)
                {
                    insertIndex++;
                }
                
                targetList.Insert(insertIndex, sourceNode);
                sourceNode.Parent = targetNode.Parent;
            }

            var newParent = sourceNode.Parent;
            var newList = newParent != null ? newParent.Children : RootNodes;
            int newIndex = newList.IndexOf(sourceNode);

            PushUndo(() =>
            {
                newList.Remove(sourceNode);
                sourceNode.Parent = oldParent;
                if (oldParent != null)
                {
                    oldParent.Children.Insert(oldIndex, sourceNode);
                }
                else
                {
                    RootNodes.Insert(oldIndex, sourceNode);
                }
            });
        }

        public bool IsDescendant(TreeNodeViewModel source, TreeNodeViewModel target)
        {
            var current = target.Parent;
            while (current != null)
            {
                if (current == source) return true;
                current = current.Parent;
            }
            return false;
        }

        public void HandleDropToRoot(TreeNodeViewModel sourceNode)
        {
            var sourceList = sourceNode.Parent != null ? sourceNode.Parent.Children : RootNodes;
            if (sourceNode.Parent != null)
            {
                var oldParent = sourceNode.Parent;
                int oldIndex = sourceList.IndexOf(sourceNode);
                sourceList.Remove(sourceNode);
                RootNodes.Add(sourceNode);
                sourceNode.Parent = null;

                PushUndo(() =>
                {
                    RootNodes.Remove(sourceNode);
                    sourceNode.Parent = oldParent;
                    oldParent.Children.Insert(oldIndex, sourceNode);
                });
            }
        }

        public void LoadMacros(string jsonPath)
        {
            if (File.Exists(jsonPath))
            {
                try
                {
                    string json = File.ReadAllText(jsonPath);
                    var rootDict = JsonSerializer.Deserialize<Dictionary<string, MacroNode>>(json);
                    RootNodes.Clear();
                    if (rootDict != null)
                    {
                        foreach (var kvp in rootDict)
                        {
                            DecryptNodeRecursive(kvp.Value);
                            RootNodes.Add(new TreeNodeViewModel(kvp.Key, kvp.Value, null));
                        }
                    }
                }
                catch { }
            }
        }

        public void SaveMacros(string jsonPath)
        {
            var rootDict = new Dictionary<string, MacroNode>();
            foreach (var node in RootNodes)
            {
                var macroNode = node.ToMacroNode();
                EncryptNodeRecursive(macroNode);
                rootDict[node.Key] = macroNode;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string macrosJson = JsonSerializer.Serialize(rootDict, options);
            File.WriteAllText(jsonPath, macrosJson);
        }

        private void EncryptNodeRecursive(MacroNode node)
        {
            if (node.IsSecure && !string.IsNullOrEmpty(node.Action))
            {
                node.Action = CryptoHelper.Encrypt(node.Action);
            }
            if (node.Children != null)
            {
                foreach (var child in node.Children.Values)
                    EncryptNodeRecursive(child);
            }
        }

        private void DecryptNodeRecursive(MacroNode node)
        {
            if (node.IsSecure && !string.IsNullOrEmpty(node.Action))
            {
                node.Action = CryptoHelper.Decrypt(node.Action);
            }
            if (node.Children != null)
            {
                foreach (var child in node.Children.Values)
                    DecryptNodeRecursive(child);
            }
        }
    }
}
