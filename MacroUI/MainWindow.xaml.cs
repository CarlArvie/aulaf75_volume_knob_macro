using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MacroUI.Services;

namespace MacroUI
{
    public partial class MainWindow : Window
    {
        private const double MenuRadius = 280;
        private const double MenuInnerRadius = 110;
        private const double SliceGapAngle = 2.5;

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

            this.Opacity = 0;
            MainCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            MainCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            MainCanvas.Margin = new Thickness(0);

            AnimateShow();
        }

        private void AnimateShow()
        {
            DoubleAnimation anim = new DoubleAnimation
            {
                To = 1.0, Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(Window.OpacityProperty, anim);

            DoubleAnimation scaleAnim = new DoubleAnimation
            {
                From = 0.85, To = 1.0, Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            if (MainCanvas.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }
        }

        public async void AnimateHideAndClose()
        {
            DoubleAnimation anim = new DoubleAnimation
            {
                To = 0.0, Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(Window.OpacityProperty, anim);

            DoubleAnimation scaleAnim = new DoubleAnimation
            {
                To = 0.85, Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            if (MainCanvas.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }

            await Task.Delay(300);
            this.Close();
        }

        public void UpdateMenu(MacroNode currentNode, int selectedIndex, AppSettings settings)
        {
            MainCanvas.Children.Clear();
            if (currentNode?.Children == null || currentNode.Children.Count == 0) return;

            int childCount = currentNode.Children.Count;
            int totalCount = childCount + 1;
            double angleStep = 360.0 / totalCount;

            var cx = MainCanvas.Width / 2;
            var cy = MainCanvas.Height / 2;

            var centerHub = new Ellipse
            {
                Width = MenuInnerRadius * 2 - 20, Height = MenuInnerRadius * 2 - 20,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 20, 20, 25)),
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 255, 255)),
                StrokeThickness = 2,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 15, ShadowDepth = 0, Opacity = 0.8 }
            };
            Canvas.SetLeft(centerHub, cx - centerHub.Width / 2);
            Canvas.SetTop(centerHub, cy - centerHub.Height / 2);
            MainCanvas.Children.Add(centerHub);

            if (currentNode.Name == "Root" && !string.IsNullOrEmpty(settings.CenterImagePath))
            {
                try
                {
                    string fullImagePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", settings.CenterImagePath));
                    if (System.IO.File.Exists(fullImagePath))
                    {
                        var bmp = new BitmapImage(new Uri(fullImagePath));
                        var imgEllipse = new Ellipse { Width = MenuInnerRadius * 2 - 20, Height = MenuInnerRadius * 2 - 20, Fill = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill } };
                        Canvas.SetLeft(imgEllipse, cx - (imgEllipse.Width / 2));
                        Canvas.SetTop(imgEllipse, cy - (imgEllipse.Height / 2));
                        MainCanvas.Children.Add(imgEllipse);
                    }
                } catch { }
            }
            else
            {
                var centerText = new TextBlock
                {
                Text = currentNode.Name == "Root" ? settings.CenterTitle : currentNode.Name.ToUpper(),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 200, 255)),
                FontSize = 20, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Width = 160
            };
            centerText.Measure(new System.Windows.Size(Double.PositiveInfinity, Double.PositiveInfinity));
                Canvas.SetLeft(centerText, cx - 80);
                Canvas.SetTop(centerText, cy - centerText.DesiredSize.Height / 2);
                MainCanvas.Children.Add(centerText);
            }

            int i = 0;
            foreach (var kvp in currentNode.Children)
            {
                DrawSlice(cx, cy, MenuRadius, MenuInnerRadius, i * angleStep, (i + 1) * angleStep, (i == selectedIndex), kvp.Value.Name, settings, kvp.Value.ImagePath, kvp.Value.IconUnicode);
                i++;
            }

            string backText = (currentNode.Name == "Root") ? "Close" : "Back";
            DrawSlice(cx, cy, MenuRadius, MenuInnerRadius, i * angleStep, (i + 1) * angleStep, (i == selectedIndex), backText, settings);
        }

        private void DrawSlice(double cx, double cy, double baseRadius, double innerRadius, double startAngle, double endAngle, bool isSelected, string text, AppSettings settings, string imagePath = null, string iconUnicode = null)
        {
            double adjustedStartAngle = startAngle + (SliceGapAngle / 2) - 90;
            double adjustedEndAngle = endAngle - (SliceGapAngle / 2) - 90;
            double radius = isSelected ? baseRadius + 15 : baseRadius;

            double startRad = adjustedStartAngle * Math.PI / 180.0;
            double endRad = adjustedEndAngle * Math.PI / 180.0;

            System.Windows.Point p1 = new System.Windows.Point(cx + innerRadius * Math.Cos(startRad), cy + innerRadius * Math.Sin(startRad));
            System.Windows.Point p2 = new System.Windows.Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad));
            System.Windows.Point p3 = new System.Windows.Point(cx + radius * Math.Cos(endRad), cy + radius * Math.Sin(endRad));
            System.Windows.Point p4 = new System.Windows.Point(cx + innerRadius * Math.Cos(endRad), cy + innerRadius * Math.Sin(endRad));

            bool isLargeArc = (adjustedEndAngle - adjustedStartAngle) > 180.0;
            PathGeometry geom = new PathGeometry();
            PathFigure fig = new PathFigure { StartPoint = p1, IsClosed = true };
            fig.Segments.Add(new LineSegment(p2, true));
            fig.Segments.Add(new ArcSegment(p3, new System.Windows.Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(p4, true));
            fig.Segments.Add(new ArcSegment(p1, new System.Windows.Size(innerRadius, innerRadius), 0, isLargeArc, SweepDirection.Counterclockwise, true));
            geom.Figures.Add(fig);

            System.Windows.Media.Color themeMain = System.Windows.Media.Color.FromArgb(230, 0, 180, 235);
            System.Windows.Media.Color themeStroke = System.Windows.Media.Color.FromArgb(255, 100, 220, 255);
            System.Windows.Media.Color shadowColor = System.Windows.Media.Color.FromRgb(0, 180, 235);

            if (settings.Theme == "Crimson Red") { themeMain = System.Windows.Media.Color.FromArgb(230, 235, 30, 30); themeStroke = System.Windows.Media.Color.FromArgb(255, 255, 100, 100); shadowColor = System.Windows.Media.Color.FromRgb(235, 30, 30); }
            else if (settings.Theme == "Toxic Green") { themeMain = System.Windows.Media.Color.FromArgb(230, 30, 235, 30); themeStroke = System.Windows.Media.Color.FromArgb(255, 100, 255, 100); shadowColor = System.Windows.Media.Color.FromRgb(30, 235, 30); }
            else if (settings.Theme == "Royal Purple") { themeMain = System.Windows.Media.Color.FromArgb(230, 130, 0, 255); themeStroke = System.Windows.Media.Color.FromArgb(255, 180, 100, 255); shadowColor = System.Windows.Media.Color.FromRgb(130, 0, 255); }
            else if (settings.Theme == "Amethyst") { themeMain = System.Windows.Media.Color.FromArgb(230, 190, 80, 255); themeStroke = System.Windows.Media.Color.FromArgb(255, 220, 150, 255); shadowColor = System.Windows.Media.Color.FromRgb(190, 80, 255); }

            System.Windows.Shapes.Path path = new System.Windows.Shapes.Path
            {
                Data = geom,
                Fill = isSelected ? new SolidColorBrush(themeMain) : new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 30, 30, 35)),
                Stroke = isSelected ? new SolidColorBrush(themeStroke) : new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 80, 80, 80)),
                StrokeThickness = isSelected ? 3 : 1
            };
            if (isSelected) path.Effect = new DropShadowEffect { Color = shadowColor, BlurRadius = 25, ShadowDepth = 0, Opacity = 0.8 };
            MainCanvas.Children.Add(path);

            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    string fullImagePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", imagePath));
                    if (System.IO.File.Exists(fullImagePath))
                    {
                        var bitmap = new BitmapImage(new Uri(fullImagePath));
                        System.Windows.Shapes.Path imgPath = new System.Windows.Shapes.Path { Data = geom, Fill = new ImageBrush { ImageSource = bitmap, Stretch = Stretch.UniformToFill }, Opacity = isSelected ? 1.0 : 0.3 };
                        MainCanvas.Children.Add(imgPath);
                    }
                } catch { }
            }

            StackPanel sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
            if (!string.IsNullOrEmpty(iconUnicode)) sp.Children.Add(new TextBlock { Text = iconUnicode, FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"), Foreground = isSelected ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White, FontSize = isSelected ? 28 : 24, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 2) });
            sp.Children.Add(new TextBlock { Text = text, Foreground = isSelected ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White, FontSize = isSelected ? 22 : 18, FontWeight = isSelected ? FontWeights.ExtraBold : FontWeights.SemiBold, TextAlignment = TextAlignment.Center });
            sp.Measure(new System.Windows.Size(Double.PositiveInfinity, Double.PositiveInfinity));

            double midRad = ((adjustedStartAngle + adjustedEndAngle) / 2.0) * Math.PI / 180.0;
            double textRadius = (radius + innerRadius) / 2.0;
            Canvas.SetLeft(sp, cx + textRadius * Math.Cos(midRad) - sp.DesiredSize.Width / 2.0);
            Canvas.SetTop(sp, cy + textRadius * Math.Sin(midRad) - sp.DesiredSize.Height / 2.0);
            MainCanvas.Children.Add(sp);
        }
    }
}