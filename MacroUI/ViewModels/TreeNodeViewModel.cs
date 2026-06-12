using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;

namespace MacroUI.ViewModels
{
    public class TreeNodeViewModel : ViewModelBase
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

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        private string _imagePath;
        public string ImagePath
        {
            get => _imagePath;
            set { _imagePath = value; OnPropertyChanged(nameof(ImagePath)); }
        }

        private string _iconUnicode;
        public string IconUnicode
        {
            get => _iconUnicode;
            set { _iconUnicode = value; OnPropertyChanged(nameof(IconUnicode)); }
        }

        private bool _isSecure;
        public bool IsSecure
        {
            get => _isSecure;
            set { _isSecure = value; OnPropertyChanged(nameof(IsSecure)); }
        }

        private string _targetProcess;
        public string TargetProcess
        {
            get => _targetProcess;
            set { _targetProcess = value; OnPropertyChanged(nameof(TargetProcess)); }
        }

        private string _triggerHotkey;
        public string TriggerHotkey
        {
            get => _triggerHotkey;
            set { _triggerHotkey = value; OnPropertyChanged(nameof(TriggerHotkey)); }
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
            else if (MacroType == "RawAHK")
                Action = "ahk:" + RawActionValue;
            else if (MacroType == "SystemCommand")
                Action = "sys:" + RawActionValue;
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
            else if (Action.StartsWith("ahk:"))
            {
                MacroType = "RawAHK";
                RawActionValue = Action.Substring(4);
            }
            else if (Action.StartsWith("sys:"))
            {
                MacroType = "SystemCommand";
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
            if (node != null)
            {
                Name = node.Name;
                Action = node.Action;
                ImagePath = node.ImagePath;
                TargetProcess = node.TargetProcess;
                TriggerHotkey = node.TriggerHotkey;
                IconUnicode = node.IconUnicode;
                IsSecure = node.IsSecure;
                ParseAction();
            }
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
            var node = new MacroNode { 
                Name = Name, 
                Action = (MacroType == "Category") ? null : Action, 
                ImagePath = ImagePath,
                TargetProcess = TargetProcess,
                TriggerHotkey = TriggerHotkey,
                IconUnicode = IconUnicode,
                IsSecure = IsSecure
            };
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
    }
}
