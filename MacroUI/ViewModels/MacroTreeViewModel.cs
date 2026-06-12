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

        public ICommand AddCategoryCommand => new RelayCommand(_ => AddNode(true));
        public ICommand AddMacroCommand => new RelayCommand(_ => AddNode(false));
        public ICommand DeleteNodeCommand => new RelayCommand(_ => DeleteSelectedNode(), _ => SelectedNode != null);

        private void AddNode(bool isCategory)
        {
            var name = isCategory ? "New Category" : "New Macro";
            var macroNode = new MacroNode { Name = name };
            if (isCategory) macroNode.Children = new Dictionary<string, MacroNode>();
            else macroNode.Action = "send:";

            var node = new TreeNodeViewModel(name, macroNode, SelectedNode);

            if (SelectedNode != null && SelectedNode.MacroType == "Category")
            {
                var parentList = SelectedNode.Children;
                parentList.Add(node);
                SelectedNode.IsExpanded = true;
                PushUndo(() => parentList.Remove(node));
            }
            else
            {
                var parentList = SelectedNode?.Parent?.Children ?? RootNodes;
                int index = SelectedNode != null ? parentList.IndexOf(SelectedNode) + 1 : parentList.Count;
                parentList.Insert(index, node);
                PushUndo(() => parentList.Remove(node));
            }
            SelectedNode = node;
        }

        private void DeleteSelectedNode()
        {
            if (SelectedNode == null) return;
            var parentList = SelectedNode.Parent?.Children ?? RootNodes;
            int index = parentList.IndexOf(SelectedNode);
            var nodeToDelete = SelectedNode;
            
            parentList.Remove(SelectedNode);
            SelectedNode = null;

            PushUndo(() =>
            {
                parentList.Insert(index, nodeToDelete);
            });
        }

        // Removed internal Load/Save methods, they are now in ISettingsService
    }
}
