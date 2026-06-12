using System;
using System.Collections.Generic;
using System.Linq;

namespace MacroUI.Services
{
    public class MacroStateManager
    {
        public MacroNode RootNode { get; private set; }
        public MacroNode CurrentNode { get; private set; }
        public int SelectedIndex { get; private set; }
        public bool IsVisible { get; private set; }
        private Stack<MacroNode> _history = new Stack<MacroNode>();
        private ITimer _timer;
        public bool EnableAutoSelect { get; set; } = true;
        
        public event Action OnVisibilityChanged;
        public event Action OnMenuChanged;
        public event Action<string> OnExecuteAction;

        public MacroStateManager(MacroNode root, ITimer timer = null)
        {
            RootNode = root ?? new MacroNode { Name = "Root", Children = new Dictionary<string, MacroNode>() };
            CurrentNode = RootNode;
            _timer = timer;
            if (_timer != null)
            {
                _timer.OnTick += () => HandleCommand("SELECT");
            }
        }

        public void HandleCommand(string cmd, string activeProcess = null)
        {
            if (cmd == "SHOW")
            {
                CurrentNode = RootNode;
                if (!string.IsNullOrEmpty(activeProcess) && RootNode?.Children != null)
                {
                    foreach (var kvp in RootNode.Children)
                    {
                        if (string.Equals(kvp.Value.TargetProcess, activeProcess, StringComparison.OrdinalIgnoreCase))
                        {
                            CurrentNode = kvp.Value;
                            break;
                        }
                    }
                }
                SelectedIndex = 0;
                IsVisible = true;
                OnVisibilityChanged?.Invoke();
                OnMenuChanged?.Invoke();
            }
            else if (cmd == "HIDE")
            {
                _timer?.Stop();
                IsVisible = false;
                OnVisibilityChanged?.Invoke();
            }
            else if (cmd == "NEXT")
            {
                if (!IsVisible) {
                    IsVisible = true;
                    OnVisibilityChanged?.Invoke();
                }
                if (CurrentNode?.Children != null && CurrentNode.Children.Count > 0)
                {
                    int totalCount = CurrentNode.Children.Count + 1;
                    SelectedIndex = (SelectedIndex + 1) % totalCount;
                    OnMenuChanged?.Invoke();
                }
                if (EnableAutoSelect) _timer?.Start(500);
            }
            else if (cmd == "PREV")
            {
                if (!IsVisible) {
                    IsVisible = true;
                    OnVisibilityChanged?.Invoke();
                }
                if (CurrentNode?.Children != null && CurrentNode.Children.Count > 0)
                {
                    int totalCount = CurrentNode.Children.Count + 1;
                    SelectedIndex = (SelectedIndex - 1 + totalCount) % totalCount;
                    OnMenuChanged?.Invoke();
                }
                if (EnableAutoSelect) _timer?.Start(500);
            }
            else if (cmd == "BACK" && IsVisible)
            {
                if (_history.Count > 0)
                {
                    CurrentNode = _history.Pop();
                    SelectedIndex = 0;
                    OnMenuChanged?.Invoke();
                }
                else
                {
                    IsVisible = false;
                    OnVisibilityChanged?.Invoke();
                }
            }
            else if (cmd == "SELECT" && IsVisible)
            {
                _timer?.Stop();
                if (CurrentNode?.Children != null && CurrentNode.Children.Count > 0)
                {
                    if (SelectedIndex == CurrentNode.Children.Count)
                    {
                        HandleCommand("BACK", activeProcess);
                        return;
                    }

                    var selectedKey = CurrentNode.Children.Keys.ElementAt(SelectedIndex);
                    var selectedChild = CurrentNode.Children[selectedKey];

                    if (selectedChild.Children != null && selectedChild.Children.Count > 0)
                    {
                        _history.Push(CurrentNode);
                        CurrentNode = selectedChild;
                        SelectedIndex = 0;
                        OnMenuChanged?.Invoke();
                    }
                    else
                    {
                        string action = !string.IsNullOrEmpty(selectedChild.Action) ? selectedChild.Action : ("send:" + selectedKey);
                        OnExecuteAction?.Invoke(action);
                        
                        IsVisible = false;
                        OnVisibilityChanged?.Invoke();
                        
                        CurrentNode = RootNode;
                        _history.Clear();
                        SelectedIndex = 0;
                    }
                }
            }
        }
    }
}
