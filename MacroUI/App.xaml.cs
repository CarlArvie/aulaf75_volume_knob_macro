using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using MacroUI.Services;
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

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;
        public const uint WM_COPYDATA = 0x004A;
        private uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

        private NotifyIcon _notifyIcon;
        private Process _ahkProcess;
        private DispatcherTimer _watchdogTimer;
        private DispatcherTimer _gameModeTimer;
        private bool _isSuspendedByGameMode = false;
        
        public MacroStateManager StateManager { get; private set; }
        public AppSettings Settings { get; private set; } = new AppSettings();
        private MainWindow _overlayWindow;
        private List<MediaPlayer> _tickSounds = new List<MediaPlayer>();
        private int _currentSoundIndex = 0;

        // MinimizeMemoryFootprint removed for performance optimization. Let the .NET GC handle memory.

        protected override void OnStartup(StartupEventArgs e)
        {
            File.AppendAllText("debug_log.txt", "OnStartup started.\n");
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) => 
            {
                File.WriteAllText("crash_log.txt", args.ExceptionObject.ToString());
            };
            this.DispatcherUnhandledException += (s, args) =>
            {
                File.WriteAllText("crash_log2.txt", args.Exception.ToString());
            };
            
            // Prevent WPF from shutting down when there are no windows
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Load Configuration first (this initializes StateManager)
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

            Task.Run(StartNamedPipeServer);
            File.AppendAllText("debug_log.txt", "OnStartup finished.\n");
            
            // Removed aggressive memory optimization call
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
            
            // Fallback to expecting it in current directory if not found
            return Path.Combine(Environment.CurrentDirectory, fileName);
        }

        public void LoadConfig()
        {
            try
            {
                string jsonPath = GetProjectRootFile("macros.json");
                if (!File.Exists(jsonPath))
                {
                    System.Windows.MessageBox.Show($"Could not find file \"{jsonPath}\"", "Error loading config");
                    return;
                }
                
                string json = File.ReadAllText(jsonPath);
                var rootDict = JsonSerializer.Deserialize<Dictionary<string, MacroNode>>(json);
                var rootNode = new MacroNode { Name = "Root", Children = rootDict };
                
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
                if (File.Exists(settingsPath))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath));
                    if (settings != null) Settings = settings;
                }
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

        private async Task StartNamedPipeServer()
        {
            while (true)
            {
                try
                {
                    using (var server = new NamedPipeServerStream("MacroUIPipe", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous))
                    {
                        await server.WaitForConnectionAsync();
                        using (var reader = new StreamReader(server))
                        using (var writer = new StreamWriter(server) { AutoFlush = true })
                        {
                            while (!reader.EndOfStream)
                            {
                                string line = await reader.ReadLineAsync();
                                if (line == null) break;

                                Dispatcher.Invoke(() => 
                                {
                                    string activeProcess = GetForegroundProcessName();
                                    StateManager.HandleCommand(line, activeProcess);
                                });
                            }
                        }
                    }
                }
                catch { await Task.Delay(1000); }
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
                    
                    // Removed aggressive memory optimization call
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
            SendIPCMessage(action);
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
                if (_isSuspendedByGameMode) { SendIPCMessage("suspend:off"); _isSuspendedByGameMode = false; }
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

                if (isFullscreen && !_isSuspendedByGameMode) { SendIPCMessage("suspend:on"); _isSuspendedByGameMode = true; }
                else if (!isFullscreen && _isSuspendedByGameMode) { SendIPCMessage("suspend:off"); _isSuspendedByGameMode = false; }
            }
        }

        private void SendIPCMessage(string message)
        {
            try
            {
                IntPtr hWnd = FindWindow("AutoHotkey", "AulaMacroEngine_IPC");
                if (hWnd != IntPtr.Zero)
                {
                    byte[] bytes = Encoding.Unicode.GetBytes(message + "\0");
                    COPYDATASTRUCT cds = new COPYDATASTRUCT { dwData = IntPtr.Zero, cbData = bytes.Length };
                    IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
                    Marshal.Copy(bytes, 0, ptr, bytes.Length);
                    cds.lpData = ptr;
                    SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cds);
                    Marshal.FreeHGlobal(ptr);
                }
            }
            catch { }
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
            new ConfigWindow().Show();
        }

        private void ShutdownApp()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            File.AppendAllText("debug_log.txt", $"OnExit called. Exit code: {e.ApplicationExitCode}\n");
            _gameModeTimer?.Stop();
            _watchdogTimer?.Stop();
            if (_ahkProcess != null) { try { if (!_ahkProcess.HasExited) _ahkProcess.Kill(); } catch { } }
            if (_notifyIcon != null) { _notifyIcon.Visible = false; _notifyIcon.Dispose(); }
            base.OnExit(e);
        }
    }
}
