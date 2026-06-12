using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using MacroUI.Services;
using MacroUI.ViewModels;
using Application = System.Windows.Application;

namespace MacroUI
{
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;
        private uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        private NotifyIcon _notifyIcon;
        private Process _ahkProcess;
        private DispatcherTimer _watchdogTimer;
        private DispatcherTimer _gameModeTimer;
        private bool _isSuspendedByGameMode = false;
        
        public MacroStateManager StateManager { get; private set; }
        public AppSettings Settings { get; private set; }
        private MainWindow _overlayWindow;
        private List<MediaPlayer> _tickSounds = new List<MediaPlayer>();
        private int _currentSoundIndex = 0;

        private IIpcService _ipcService;
        private ISettingsService _settingsService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _settingsService = new SettingsService();
            _ipcService = new IpcService();

            AppDomain.CurrentDomain.UnhandledException += (s, args) => 
            {
                File.WriteAllText("crash_log.txt", args.ExceptionObject.ToString());
            };
            this.DispatcherUnhandledException += (s, args) =>
            {
                File.WriteAllText("crash_log2.txt", args.Exception.ToString());
            };
            
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            LoadConfig();
            StartAhkEngine();

            _notifyIcon = new NotifyIcon { Icon = SystemIcons.Application, Visible = true, Text = "Aula Macro Configuration" };
            var menu = new ContextMenuStrip();
            var configItem = new ToolStripMenuItem("Configure Macros...");
            configItem.Click += (s, args) => OpenConfigWindow();
            menu.Items.Add(configItem);
            menu.Items.Add(new ToolStripSeparator());
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, args) => ShutdownApp();
            menu.Items.Add(exitItem);
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (s, args) => OpenConfigWindow();

            _gameModeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _gameModeTimer.Tick += GameModeTimer_Tick;
            _gameModeTimer.Start();

            _watchdogTimer = new DispatcherTimer();
            _watchdogTimer.Interval = TimeSpan.FromSeconds(5);
            _watchdogTimer.Tick += WatchdogTimer_Tick;
            _watchdogTimer.Start();

            _ipcService.StartServerAsync((line, activeProcess) =>
            {
                Dispatcher.Invoke(() => 
                {
                    StateManager.HandleCommand(line, activeProcess);
                });
            });
        }

        private void StartAhkEngine()
        {
            string ahkPath = GetProjectRootFile("MacroEngine.ahk");
            if (File.Exists(ahkPath))
            {
                try { _ahkProcess = Process.Start(new ProcessStartInfo { FileName = ahkPath, UseShellExecute = true }); }
                catch (Exception) { }
            }
        }

        public static string GetProjectRootFile(string fileName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] possiblePaths = new[]
            {
                Path.Combine(baseDir, fileName),
                Path.Combine(baseDir, "..", fileName),
                Path.Combine(baseDir, "..", "..", "..", "..", fileName)
            };

            foreach (var path in possiblePaths)
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath)) return fullPath;
            }
            
            return Path.Combine(Environment.CurrentDirectory, fileName);
        }

        public void LoadConfig()
        {
            try
            {
                string jsonPath = GetProjectRootFile("macros.json");
                var macros = _settingsService.LoadMacros(jsonPath);
                var rootNode = new MacroNode { Name = "Root", Children = macros };
                
                if (StateManager != null)
                {
                    StateManager.OnVisibilityChanged -= StateManager_OnVisibilityChanged;
                    StateManager.OnMenuChanged -= StateManager_OnMenuChanged;
                    StateManager.OnExecuteAction -= StateManager_OnExecuteAction;
                }

                StateManager = new MacroStateManager(rootNode, new DispatcherTimerAdapter());
                StateManager.OnVisibilityChanged += StateManager_OnVisibilityChanged;
                StateManager.OnMenuChanged += StateManager_OnMenuChanged;
                StateManager.OnExecuteAction += StateManager_OnExecuteAction;

                string settingsPath = GetProjectRootFile("settings.json");
                Settings = _settingsService.LoadSettings(settingsPath);
                
                StateManager.EnableAutoSelect = Settings.EnableAutoSelect;
                
                try 
                {
                    foreach (var player in _tickSounds) player.Close();
                    _tickSounds.Clear();

                    if (!string.IsNullOrWhiteSpace(Settings.TickSoundPath) && File.Exists(Settings.TickSoundPath)) {
                        for (int i = 0; i < 8; i++) {
                            var player = new MediaPlayer();
                            player.Volume = Settings.TickSoundVolume;
                            player.Open(new Uri(Settings.TickSoundPath));
                            _tickSounds.Add(player);
                        }
                    }
                } catch { }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading config: {ex.Message}");
            }
        }

        private void StateManager_OnVisibilityChanged()
        {
            if (StateManager.IsVisible)
            {
                if (_overlayWindow == null)
                {
                    _overlayWindow = new MainWindow();
                    _overlayWindow.Show();
                }
                _overlayWindow.UpdateMenu(StateManager.CurrentNode, StateManager.SelectedIndex, Settings);
            }
            else
            {
                if (_overlayWindow != null)
                {
                    _overlayWindow.AnimateHideAndClose();
                    _overlayWindow = null;
                }
            }
        }

        private void StateManager_OnMenuChanged()
        {
            if (_overlayWindow != null && StateManager.IsVisible)
            {
                _overlayWindow.UpdateMenu(StateManager.CurrentNode, StateManager.SelectedIndex, Settings);
                if (Settings.EnableAudio && _tickSounds.Count > 0)
                {
                    try 
                    {
                        var player = _tickSounds[_currentSoundIndex];
                        player.Stop();
                        player.Position = TimeSpan.Zero;
                        player.Play(); 
                        _currentSoundIndex = (_currentSoundIndex + 1) % _tickSounds.Count;
                    } catch { }
                }
            }
        }

        private void StateManager_OnExecuteAction(string action)
        {
            _ipcService.SendMessage(action);
        }

        private string GetForegroundProcessName()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return null;
                GetWindowThreadProcessId(hWnd, out uint processId);
                IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProcess == IntPtr.Zero) return null;

                uint capacity = 1024;
                StringBuilder sb = new StringBuilder((int)capacity);
                if (QueryFullProcessImageName(hProcess, 0, sb, ref capacity))
                {
                    CloseHandle(hProcess);
                    return Path.GetFileName(sb.ToString());
                }
                CloseHandle(hProcess);
            }
            catch { }
            return null;
        }

        private void WatchdogTimer_Tick(object sender, EventArgs e)
        {
            if (_ahkProcess == null || _ahkProcess.HasExited)
            {
                Debug.WriteLine("AHK process not running. Restarting...");
                StartAhkEngine();
            }
        }

        private void GameModeTimer_Tick(object sender, EventArgs e)
        {
            if (!Settings.EnableGameMode) 
            {
                if (_isSuspendedByGameMode) { _ipcService.SendMessage("suspend:off"); _isSuspendedByGameMode = false; }
                return;
            }

            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return;
            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == Process.GetCurrentProcess().Id) return;

            if (GetWindowRect(hWnd, out RECT rect))
            {
                bool isFullscreen = ((rect.Right - rect.Left) >= GetSystemMetrics(SM_CXSCREEN) && (rect.Bottom - rect.Top) >= GetSystemMetrics(SM_CYSCREEN));
                string processName = GetForegroundProcessName() ?? "";
                if (processName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)) isFullscreen = false;

                if (isFullscreen && !_isSuspendedByGameMode) { _ipcService.SendMessage("suspend:on"); _isSuspendedByGameMode = true; }
                else if (!isFullscreen && _isSuspendedByGameMode) { _ipcService.SendMessage("suspend:off"); _isSuspendedByGameMode = false; }
            }
        }

        private void OpenConfigWindow()
        {
            foreach (Window w in Current.Windows)
            {
                if (w is ConfigWindow cw)
                {
                    if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                    w.Activate();
                    return;
                }
            }
            new ConfigWindow(_settingsService).Show();
        }

        private void ShutdownApp()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _gameModeTimer?.Stop();
            _watchdogTimer?.Stop();
            if (_ahkProcess != null) { try { if (!_ahkProcess.HasExited) _ahkProcess.Kill(); } catch { } }
            if (_notifyIcon != null) { _notifyIcon.Visible = false; _notifyIcon.Dispose(); }
            base.OnExit(e);
        }
    }
}
