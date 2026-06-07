using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using System.Media;

namespace MacroUI
{
    public partial class MainWindow : Window
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
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public const uint WM_COPYDATA = 0x004A;

        private uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

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
                    return System.IO.Path.GetFileName(sb.ToString());
                }
                CloseHandle(hProcess);
            }
            catch { }
            return null;
        }

        private MacroNode _rootNode;
        private Stack<MacroNode> _navigationHistory = new Stack<MacroNode>();
        private MacroNode _currentNode;
        private int _selectedIndex = 0;
        private bool _isVisible = false;
        private AppSettings _appSettings = new AppSettings();
        
        private DispatcherTimer _gameModeTimer;
        private bool _isSuspendedByGameMode = false;
        
        // Aesthetic Configuration
        private const double MenuRadius = 280;
        private const double MenuInnerRadius = 110;
        private const double SliceGapAngle = 2.5; // Gap between slices in degrees

        private MediaPlayer _tickSound = new MediaPlayer();

        public MainWindow()
        {
            InitializeComponent();
            _tickSound.Volume = _appSettings?.TickSoundVolume ?? 0.6;

            var dummy = new Window
            {
                Top = -100, Left = -100, Width = 1, Height = 1,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false
            };
            dummy.Show();
            this.Owner = dummy;
            dummy.Hide();

            LoadConfig();
            this.Opacity = 0;
            this.Visibility = Visibility.Hidden;

            _gameModeTimer = new DispatcherTimer();
            _gameModeTimer.Interval = TimeSpan.FromSeconds(2);
            _gameModeTimer.Tick += GameModeTimer_Tick;
            _gameModeTimer.Start();

            Task.Run(StartNamedPipeServer);
        }

        private void GameModeTimer_Tick(object sender, EventArgs e)
        {
            if (!_appSettings.EnableGameMode) 
            {
                if (_isSuspendedByGameMode)
                {
                    SendSuspendCommand(false);
                    _isSuspendedByGameMode = false;
                }
                return;
            }

            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return;

            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == System.Diagnostics.Process.GetCurrentProcess().Id) return;

            if (GetWindowRect(hWnd, out RECT rect))
            {
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                bool isFullscreen = (width >= screenWidth && height >= screenHeight);
                string processName = GetForegroundProcessName() ?? "";

                // Ignore Explorer/Desktop
                if (processName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                    isFullscreen = false;

                if (isFullscreen && !_isSuspendedByGameMode)
                {
                    SendSuspendCommand(true);
                    _isSuspendedByGameMode = true;
                }
                else if (!isFullscreen && _isSuspendedByGameMode)
                {
                    SendSuspendCommand(false);
                    _isSuspendedByGameMode = false;
                }
            }
        }

        private void SendSuspendCommand(bool suspend)
        {
            SendIPCMessage(suspend ? "suspend:on" : "suspend:off");
        }

        private void SendIPCMessage(string message)
        {
            try
            {
                IntPtr hWnd = FindWindow("AutoHotkey", "AulaMacroEngine_IPC");
                if (hWnd != IntPtr.Zero)
                {
                    byte[] bytes = Encoding.Unicode.GetBytes(message + "\0");
                    COPYDATASTRUCT cds = new COPYDATASTRUCT();
                    cds.dwData = IntPtr.Zero;
                    cds.cbData = bytes.Length;

                    IntPtr unmanagedPointer = Marshal.AllocHGlobal(bytes.Length);
                    Marshal.Copy(bytes, 0, unmanagedPointer, bytes.Length);
                    cds.lpData = unmanagedPointer;

                    SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cds);

                    Marshal.FreeHGlobal(unmanagedPointer);
                }
            }
            catch { }
        }


        private void LoadConfig()
        {
            try
            {
                string jsonPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "macros.json"));
                string json = File.ReadAllText(jsonPath);
                var rootDict = JsonSerializer.Deserialize<Dictionary<string, MacroNode>>(json);
                _rootNode = new MacroNode { Name = "Root", Children = rootDict };
                _currentNode = _rootNode;

                string settingsPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "settings.json"));
                if (File.Exists(settingsPath))
                {
                    string settingsJson = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(settingsJson);
                    if (settings != null) _appSettings = settings;
                }

                try {
                    _tickSound.Close();
                    _tickSound = new MediaPlayer();
                    _tickSound.Volume = _appSettings.TickSoundVolume;
                    if (!string.IsNullOrWhiteSpace(_appSettings.TickSoundPath) && File.Exists(_appSettings.TickSoundPath)) {
                        _tickSound.Open(new Uri(_appSettings.TickSoundPath));
                    }
                } catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading config: {ex.Message}");
            }
        }

        public void ReloadConfig()
        {
            LoadConfig();
            _navigationHistory.Clear();
            _selectedIndex = 0;
            if (_isVisible)
            {
                DrawMenu();
            }
        }

        private async void AnimateVisibility(bool show)
        {
            if (show)
            {
                this.Visibility = Visibility.Visible;
            }

            DoubleAnimation anim = new DoubleAnimation
            {
                To = show ? 1.0 : 0.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            if (!show)
            {
                anim.Completed += (s, e) => { this.Visibility = Visibility.Hidden; };
            }

            this.BeginAnimation(Window.OpacityProperty, anim);

            IEasingFunction easingFunc = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            if (_appSettings.AnimationEasing == "Elastic")
            {
                easingFunc = new ElasticEase { Oscillations = 1, Springiness = 5, EasingMode = EasingMode.EaseOut };
            }
            else if (_appSettings.AnimationEasing == "Linear")
            {
                easingFunc = null; // null easing function means linear
            }
            else if (_appSettings.AnimationEasing == "Back")
            {
                easingFunc = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut };
            }
            else if (_appSettings.AnimationEasing == "Cubic")
            {
                easingFunc = new CubicEase { EasingMode = EasingMode.EaseOut };
            }
            else if (_appSettings.AnimationEasing == "Bounce")
            {
                easingFunc = new BounceEase { Bounces = 2, Bounciness = 2, EasingMode = EasingMode.EaseOut };
            }

            DoubleAnimation scaleAnim = new DoubleAnimation
            {
                To = show ? 1.0 : 0.85,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = easingFunc
            };

            if (MainCanvas.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }

            if (!show)
            {
                await Task.Delay(300); // Wait for animation to finish
                App.MinimizeMemoryFootprint();
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

                                Dispatcher.Invoke(() => HandleCommand(line, writer));
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    await Task.Delay(1000);
                }
            }
        }

        private void HandleCommand(string cmd, StreamWriter writer)
        {
            if (cmd == "SHOW")
            {
                _currentNode = _rootNode; // Default
                string activeProcess = GetForegroundProcessName();

                if (!string.IsNullOrEmpty(activeProcess) && _rootNode?.Children != null)
                {
                    foreach (var kvp in _rootNode.Children)
                    {
                        if (string.Equals(kvp.Value.TargetProcess, activeProcess, StringComparison.OrdinalIgnoreCase))
                        {
                            _currentNode = kvp.Value;
                            break;
                        }
                    }
                }

                _selectedIndex = 0; // reset selection

                MainCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                MainCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                MainCanvas.Margin = new Thickness(0);

                _isVisible = true;
                AnimateVisibility(true);
                this.Activate();
                DrawMenu();
            }
            else if (cmd == "HIDE")
            {
                _isVisible = false;
                AnimateVisibility(false);
            }
            else if (cmd == "NEXT")
            {
                if (!_isVisible) {
                    _isVisible = true;
                    AnimateVisibility(true);
                    this.Activate();
                }
                if (_currentNode?.Children != null && _currentNode.Children.Count > 0)
                {
                    int totalCount = _currentNode.Children.Count + 1;
                    _selectedIndex = (_selectedIndex + 1) % totalCount;
                    DrawMenu();
                    PlaySound(_tickSound);
                }
            }
            else if (cmd == "PREV")
            {
                if (!_isVisible) {
                    _isVisible = true;
                    AnimateVisibility(true);
                    this.Activate();
                }
                if (_currentNode?.Children != null && _currentNode.Children.Count > 0)
                {
                    int totalCount = _currentNode.Children.Count + 1;
                    _selectedIndex = (_selectedIndex - 1 + totalCount) % totalCount;
                    DrawMenu();
                    PlaySound(_tickSound);
                }
            }
            else if (cmd == "BACK" && _isVisible)
            {
                if (_navigationHistory.Count > 0)
                {
                    _currentNode = _navigationHistory.Pop();
                    _selectedIndex = 0;
                    DrawMenu();
                    PlaySound(_tickSound);
                }
                else
                {
                    _isVisible = false;
                    AnimateVisibility(false);
                }
            }
            else if (cmd == "SELECT" && _isVisible)
            {
                if (_currentNode?.Children != null && _currentNode.Children.Count > 0)
                {
                    if (_selectedIndex == _currentNode.Children.Count)
                    {
                        HandleCommand("BACK", writer);
                        return;
                    }

                    var selectedKey = _currentNode.Children.Keys.ElementAt(_selectedIndex);
                    var selectedChild = _currentNode.Children[selectedKey];

                    if (selectedChild.Children != null && selectedChild.Children.Count > 0)
                    {
                        _navigationHistory.Push(_currentNode);
                        _currentNode = selectedChild;
                        _selectedIndex = 0;
                        DrawMenu();
                        PlaySound(_tickSound);
                    }
                    else
                    {
                        string action = !string.IsNullOrEmpty(selectedChild.Action) ? selectedChild.Action : ("send:" + selectedKey);
                        SendIPCMessage(action);
                        
                        _isVisible = false;
                        AnimateVisibility(false);
                        
                        _currentNode = _rootNode;
                        _navigationHistory.Clear();
                        _selectedIndex = 0;
                    }
                }
            }
        }

        private void PlaySound(MediaPlayer player)
        {
            if (!_appSettings.EnableAudio) return;
            try 
            {
                if (player.Source != null)
                {
                    player.Position = TimeSpan.Zero;
                    player.Play();
                }
            }
            catch { }
        }

        private void DrawMenu()
        {
            MainCanvas.Children.Clear();
            if (_currentNode?.Children == null || _currentNode.Children.Count == 0) return;

            int childCount = _currentNode.Children.Count;
            int totalCount = childCount + 1;
            double angleStep = 360.0 / totalCount;

            var cx = MainCanvas.Width / 2;
            var cy = MainCanvas.Height / 2;

            // Draw Center Hub Background
            var centerHub = new Ellipse
            {
                Width = MenuInnerRadius * 2 - 20,
                Height = MenuInnerRadius * 2 - 20,
                Fill = new SolidColorBrush(Color.FromArgb(230, 20, 20, 25)),
                Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                StrokeThickness = 2,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 15, ShadowDepth = 0, Opacity = 0.8 }
            };
            Canvas.SetLeft(centerHub, cx - centerHub.Width / 2);
            Canvas.SetTop(centerHub, cy - centerHub.Height / 2);
            MainCanvas.Children.Add(centerHub);

            // Read Center Text from Settings is handled by _appSettings

            // Draw center content
            if (_currentNode.Name == "Root" && !string.IsNullOrEmpty(_appSettings.CenterImagePath))
            {
                try
                {
                    string fullImagePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", _appSettings.CenterImagePath));
                    if (File.Exists(fullImagePath))
                    {
                        var bmp = new BitmapImage(new Uri(fullImagePath));
                        var imgBrush = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                        var imgEllipse = new Ellipse
                        {
                            Width = MenuInnerRadius * 2 - 20, // Exactly the same size as centerHub
                            Height = MenuInnerRadius * 2 - 20,
                            Fill = imgBrush
                        };
                        
                        // We will add an EllipseGeometry to clip exactly if needed, but Ellipse + UniformToFill already makes a perfect circle
                        Canvas.SetLeft(imgEllipse, cx - (imgEllipse.Width / 2));
                        Canvas.SetTop(imgEllipse, cy - (imgEllipse.Height / 2));
                        MainCanvas.Children.Add(imgEllipse);
                    }
                }
                catch { }
            }
            else
            {
                string centerTitleText = _appSettings.CenterTitle;
                var centerText = new TextBlock
                {
                    Text = _currentNode.Name == "Root" ? centerTitleText : _currentNode.Name.ToUpper(),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 200, 255)), // Cyan accent
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Width = 160
                };
                
                // Measure the text to center it properly vertically
                centerText.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
                Canvas.SetLeft(centerText, cx - 80);
                Canvas.SetTop(centerText, cy - centerText.DesiredSize.Height / 2);
                MainCanvas.Children.Add(centerText);
            }

            int i = 0;
            foreach (var kvp in _currentNode.Children)
            {
                bool isSelected = (i == _selectedIndex);
                DrawSlice(cx, cy, MenuRadius, MenuInnerRadius, i * angleStep, (i + 1) * angleStep, isSelected, kvp.Value.Name, kvp.Value.ImagePath, kvp.Value.IconUnicode);
                i++;
            }

            // Draw Back/Close slice
            bool isBackSelected = (i == _selectedIndex);
            string backText = (_currentNode == _rootNode) ? "Close" : "Back";
            DrawSlice(cx, cy, MenuRadius, MenuInnerRadius, i * angleStep, (i + 1) * angleStep, isBackSelected, backText);
        }

        private void DrawSlice(double cx, double cy, double baseRadius, double innerRadius, double startAngle, double endAngle, bool isSelected, string text, string imagePath = null, string iconUnicode = null)
        {
            // Apply Gap
            double adjustedStartAngle = startAngle + (SliceGapAngle / 2);
            double adjustedEndAngle = endAngle - (SliceGapAngle / 2);
            
            // Adjust radius and rotation for selected slice (Scale Effect)
            double radius = isSelected ? baseRadius + 15 : baseRadius;

            adjustedStartAngle -= 90; // Start at top
            adjustedEndAngle -= 90;

            double startRad = adjustedStartAngle * Math.PI / 180.0;
            double endRad = adjustedEndAngle * Math.PI / 180.0;

            Point p1 = new Point(cx + innerRadius * Math.Cos(startRad), cy + innerRadius * Math.Sin(startRad));
            Point p2 = new Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad));
            Point p3 = new Point(cx + radius * Math.Cos(endRad), cy + radius * Math.Sin(endRad));
            Point p4 = new Point(cx + innerRadius * Math.Cos(endRad), cy + innerRadius * Math.Sin(endRad));

            bool isLargeArc = (adjustedEndAngle - adjustedStartAngle) > 180.0;

            PathGeometry geom = new PathGeometry();
            PathFigure fig = new PathFigure { StartPoint = p1, IsClosed = true };

            fig.Segments.Add(new LineSegment(p2, true));
            fig.Segments.Add(new ArcSegment(p3, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(p4, true));
            fig.Segments.Add(new ArcSegment(p1, new Size(innerRadius, innerRadius), 0, isLargeArc, SweepDirection.Counterclockwise, true));

            geom.Figures.Add(fig);

            Color themeMain = Color.FromArgb(230, 0, 180, 235); // Cyan
            Color themeStroke = Color.FromArgb(255, 100, 220, 255);
            Color shadowColor = Color.FromRgb(0, 180, 235);

            if (_appSettings.Theme == "Crimson Red") {
                themeMain = Color.FromArgb(230, 235, 30, 30);
                themeStroke = Color.FromArgb(255, 255, 100, 100);
                shadowColor = Color.FromRgb(235, 30, 30);
            } else if (_appSettings.Theme == "Toxic Green") {
                themeMain = Color.FromArgb(230, 30, 235, 30);
                themeStroke = Color.FromArgb(255, 100, 255, 100);
                shadowColor = Color.FromRgb(30, 235, 30);
            } else if (_appSettings.Theme == "Royal Purple") {
                themeMain = Color.FromArgb(230, 130, 0, 255);
                themeStroke = Color.FromArgb(255, 180, 100, 255);
                shadowColor = Color.FromRgb(130, 0, 255);
            } else if (_appSettings.Theme == "Amethyst") {
                themeMain = Color.FromArgb(230, 190, 80, 255);
                themeStroke = Color.FromArgb(255, 220, 150, 255);
                shadowColor = Color.FromRgb(190, 80, 255);
            }

            var pathFill = isSelected 
                ? new SolidColorBrush(themeMain)
                : new SolidColorBrush(Color.FromArgb(200, 30, 30, 35));  // Dark Gray glass

            var pathStroke = isSelected
                ? new SolidColorBrush(themeStroke)
                : new SolidColorBrush(Color.FromArgb(100, 80, 80, 80));

            System.Windows.Shapes.Path path = new System.Windows.Shapes.Path
            {
                Data = geom,
                Fill = pathFill,
                Stroke = pathStroke,
                StrokeThickness = isSelected ? 3 : 1
            };

            if (isSelected)
            {
                path.Effect = new DropShadowEffect 
                { 
                    Color = shadowColor, 
                    BlurRadius = 25, 
                    ShadowDepth = 0,
                    Opacity = 0.8
                };
            }

            MainCanvas.Children.Add(path);

            // Draw Image and Text
            double midAngle = (adjustedStartAngle + adjustedEndAngle) / 2.0;
            double midRad = midAngle * Math.PI / 180.0;
            double textRadius = (radius + innerRadius) / 2.0;

            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    string fullImagePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", imagePath));
                    if (File.Exists(fullImagePath))
                    {
                        var bitmap = new BitmapImage(new Uri(fullImagePath));
                        ImageBrush imgBrush = new ImageBrush
                        {
                            ImageSource = bitmap,
                            Stretch = Stretch.UniformToFill
                        };

                        System.Windows.Shapes.Path imgPath = new System.Windows.Shapes.Path
                        {
                            Data = geom,
                            Fill = imgBrush,
                            Opacity = isSelected ? 1.0 : 0.3
                        };

                        MainCanvas.Children.Add(imgPath);
                    }
                }
                catch { }
            }

            StackPanel sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };

            if (!string.IsNullOrEmpty(iconUnicode))
            {
                TextBlock iconTb = new TextBlock
                {
                    Text = iconUnicode,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"),
                    Foreground = isSelected ? Brushes.Black : Brushes.White,
                    FontSize = isSelected ? 28 : 24,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                sp.Children.Add(iconTb);
            }

            TextBlock tb = new TextBlock
            {
                Text = text,
                Foreground = isSelected ? Brushes.Black : Brushes.White,
                FontSize = isSelected ? 22 : 18,
                FontWeight = isSelected ? FontWeights.ExtraBold : FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center
            };
            sp.Children.Add(tb);

            sp.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            
            // To prevent overlap, we can optionally rotate the text slightly, 
            // but for simplicity, we will just position it centered.
            Canvas.SetLeft(sp, cx + textRadius * Math.Cos(midRad) - sp.DesiredSize.Width / 2.0);
            Canvas.SetTop(sp, cy + textRadius * Math.Sin(midRad) - sp.DesiredSize.Height / 2.0);
            
            MainCanvas.Children.Add(sp);
        }
    }
}