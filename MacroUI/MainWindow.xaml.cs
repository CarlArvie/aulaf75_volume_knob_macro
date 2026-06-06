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
using System.Windows.Shapes;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace MacroUI
{
    public partial class MainWindow : Window
    {
        private MacroNode _rootNode;
        private Stack<MacroNode> _navigationHistory = new Stack<MacroNode>();
        private MacroNode _currentNode;
        private int _selectedIndex = 0;
        private bool _isVisible = false;
        
        // Aesthetic Configuration
        private const double MenuRadius = 280;
        private const double MenuInnerRadius = 110;
        private const double SliceGapAngle = 2.5; // Gap between slices in degrees

        public MainWindow()
        {
            InitializeComponent();

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

            Task.Run(StartNamedPipeServer);
        }

        private void LoadConfig()
        {
            try
            {
                string jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "macros.json");
                if (!File.Exists(jsonPath))
                    jsonPath = "macros.json";

                string json = File.ReadAllText(jsonPath);
                var rootDict = JsonSerializer.Deserialize<Dictionary<string, MacroNode>>(json);
                _rootNode = new MacroNode { Name = "Root", Children = rootDict };
                _currentNode = _rootNode;
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

        private void AnimateVisibility(bool show)
        {
            DoubleAnimation anim = new DoubleAnimation
            {
                To = show ? 1.0 : 0.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(Window.OpacityProperty, anim);

            DoubleAnimation scaleAnim = new DoubleAnimation
            {
                To = show ? 1.0 : 0.85,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 5, EasingMode = EasingMode.EaseOut }
            };

            if (MainCanvas.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
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
                }
            }
            else if (cmd == "BACK" && _isVisible)
            {
                if (_navigationHistory.Count > 0)
                {
                    _currentNode = _navigationHistory.Pop();
                    _selectedIndex = 0;
                    DrawMenu();
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
                    }
                    else
                    {
                        string execFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "execute.txt");
                        string action = !string.IsNullOrEmpty(selectedChild.Action) ? selectedChild.Action : ("send:" + selectedKey);
                        File.WriteAllText(execFile, action);
                        
                        _isVisible = false;
                        AnimateVisibility(false);
                        
                        _currentNode = _rootNode;
                        _navigationHistory.Clear();
                        _selectedIndex = 0;
                    }
                }
            }
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

            // Draw center text
            var centerText = new TextBlock
            {
                Text = _currentNode.Name == "Root" ? "AULA" : _currentNode.Name.ToUpper(),
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

            int i = 0;
            foreach (var kvp in _currentNode.Children)
            {
                bool isSelected = (i == _selectedIndex);
                DrawSlice(cx, cy, MenuRadius, MenuInnerRadius, i * angleStep, (i + 1) * angleStep, isSelected, kvp.Value.Name);
                i++;
            }

            // Draw Back/Close slice
            bool isBackSelected = (i == _selectedIndex);
            string backText = (_currentNode == _rootNode) ? "Close" : "Back";
            DrawSlice(cx, cy, MenuRadius, MenuInnerRadius, i * angleStep, (i + 1) * angleStep, isBackSelected, backText);
        }

        private void DrawSlice(double cx, double cy, double baseRadius, double innerRadius, double startAngle, double endAngle, bool isSelected, string text)
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

            var pathFill = isSelected 
                ? new SolidColorBrush(Color.FromArgb(230, 0, 180, 235)) // Neon Blue/Cyan
                : new SolidColorBrush(Color.FromArgb(200, 30, 30, 35));  // Dark Gray glass

            var pathStroke = isSelected
                ? new SolidColorBrush(Color.FromArgb(255, 100, 220, 255))
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
                    Color = Color.FromRgb(0, 180, 235), 
                    BlurRadius = 25, 
                    ShadowDepth = 0,
                    Opacity = 0.8
                };
            }

            MainCanvas.Children.Add(path);

            // Draw Text
            double midAngle = (adjustedStartAngle + adjustedEndAngle) / 2.0;
            double midRad = midAngle * Math.PI / 180.0;
            double textRadius = (radius + innerRadius) / 2.0;

            TextBlock tb = new TextBlock
            {
                Text = text,
                Foreground = isSelected ? Brushes.Black : Brushes.White,
                FontSize = isSelected ? 22 : 18,
                FontWeight = isSelected ? FontWeights.ExtraBold : FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center
            };

            tb.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            
            // To prevent overlap, we can optionally rotate the text slightly, 
            // but for simplicity, we will just position it centered.
            Canvas.SetLeft(tb, cx + textRadius * Math.Cos(midRad) - tb.DesiredSize.Width / 2.0);
            Canvas.SetTop(tb, cy + textRadius * Math.Sin(midRad) - tb.DesiredSize.Height / 2.0);
            
            MainCanvas.Children.Add(tb);
        }
    }
}