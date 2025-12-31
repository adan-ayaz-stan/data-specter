using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DataSpecter.UI.Controls
{
    public partial class ComparisonVisualizer : UserControl
    {
        public static readonly DependencyProperty File1LengthProperty =
            DependencyProperty.Register(nameof(File1Length), typeof(long), typeof(ComparisonVisualizer),
                new PropertyMetadata(0L, OnDataChanged));

        public static readonly DependencyProperty File2LengthProperty =
            DependencyProperty.Register(nameof(File2Length), typeof(long), typeof(ComparisonVisualizer),
                new PropertyMetadata(0L, OnDataChanged));

        public static readonly DependencyProperty MatchOffsetProperty =
            DependencyProperty.Register(nameof(MatchOffset), typeof(long), typeof(ComparisonVisualizer),
                new PropertyMetadata(0L, OnDataChanged));

        public static readonly DependencyProperty MatchOffset2Property =
            DependencyProperty.Register(nameof(MatchOffset2), typeof(long), typeof(ComparisonVisualizer),
                new PropertyMetadata(0L, OnDataChanged));

        public static readonly DependencyProperty MatchLengthProperty =
            DependencyProperty.Register(nameof(MatchLength), typeof(long), typeof(ComparisonVisualizer),
                new PropertyMetadata(0L, OnDataChanged));

        public long File1Length
        {
            get => (long)GetValue(File1LengthProperty);
            set => SetValue(File1LengthProperty, value);
        }

        public long File2Length
        {
            get => (long)GetValue(File2LengthProperty);
            set => SetValue(File2LengthProperty, value);
        }

        public long MatchOffset
        {
            get => (long)GetValue(MatchOffsetProperty);
            set => SetValue(MatchOffsetProperty, value);
        }

        public long MatchOffset2
        {
            get => (long)GetValue(MatchOffset2Property);
            set => SetValue(MatchOffset2Property, value);
        }

        public long MatchLength
        {
            get => (long)GetValue(MatchLengthProperty);
            set => SetValue(MatchLengthProperty, value);
        }

        public event EventHandler<EventArgs>? MatchRegionClicked;

        public ComparisonVisualizer()
        {
            InitializeComponent();
            SizeChanged += (s, e) => RedrawVisualization();
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ComparisonVisualizer visualizer)
            {
                visualizer.RedrawVisualization();
            }
        }

        private void RedrawVisualization()
        {
            VisualizationCanvas.Children.Clear();

            if (File1Length == 0 || File2Length == 0 || ActualWidth < 100)
                return;

            double canvasWidth = VisualizationCanvas.ActualWidth;
            double canvasHeight = VisualizationCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0)
                return;

            const double barHeight = 20;
            const double spacing = 40;

            double bar1Y = 15;
            double bar2Y = bar1Y + barHeight + spacing;

            // Draw File 1 Bar (Baseline - Green) with gradient
            var bar1 = new Rectangle
            {
                Width = canvasWidth - 20,
                Height = barHeight,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(10, 80, 10), 0.0),
                        new GradientStop(Color.FromRgb(5, 34, 5), 1.0)
                    }
                },
                Stroke = new SolidColorBrush(Colors.Green),
                StrokeThickness = 1
            };
            Canvas.SetLeft(bar1, 10);
            Canvas.SetTop(bar1, bar1Y);
            VisualizationCanvas.Children.Add(bar1);

            // Draw File 2 Bar (Suspect - Red) with gradient
            var bar2 = new Rectangle
            {
                Width = canvasWidth - 20,
                Height = barHeight,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(80, 10, 10), 0.0),
                        new GradientStop(Color.FromRgb(34, 5, 5), 1.0)
                    }
                },
                Stroke = new SolidColorBrush(Colors.Red),
                StrokeThickness = 1
            };
            Canvas.SetLeft(bar2, 10);
            Canvas.SetTop(bar2, bar2Y);
            VisualizationCanvas.Children.Add(bar2);

            // If there's a match, draw the connection
            if (MatchLength > 0)
            {
                double barWidth = canvasWidth - 20;

                // Calculate positions for match regions
                double match1Start = 10 + (MatchOffset / (double)File1Length) * barWidth;
                double match1End = 10 + ((MatchOffset + MatchLength) / (double)File1Length) * barWidth;
                double match1Width = match1End - match1Start;

                double match2Start = 10 + (MatchOffset2 / (double)File2Length) * barWidth;
                double match2End = 10 + ((MatchOffset2 + MatchLength) / (double)File2Length) * barWidth;
                double match2Width = match2End - match2Start;

                // Draw match highlight on bar 1 with gradient
                var matchHighlight1 = new Rectangle
                {
                    Width = Math.Max(match1Width, 2),
                    Height = barHeight,
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(220, 0, 255, 0), 0.0),
                            new GradientStop(Color.FromArgb(180, 0, 200, 0), 1.0)
                        }
                    }
                };
                Canvas.SetLeft(matchHighlight1, match1Start);
                Canvas.SetTop(matchHighlight1, bar1Y);
                VisualizationCanvas.Children.Add(matchHighlight1);

                // Draw match highlight on bar 2 with gradient
                var matchHighlight2 = new Rectangle
                {
                    Width = Math.Max(match2Width, 2),
                    Height = barHeight,
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(220, 255, 0, 0), 0.0),
                            new GradientStop(Color.FromArgb(180, 200, 0, 0), 1.0)
                        }
                    }
                };
                Canvas.SetLeft(matchHighlight2, match2Start);
                Canvas.SetTop(matchHighlight2, bar2Y);
                VisualizationCanvas.Children.Add(matchHighlight2);

                // Draw connecting polygon (filled area between matches) with gradient
                var connectionPolygon = new Polygon
                {
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(120, 255, 215, 0), 0.0),
                            new GradientStop(Color.FromArgb(80, 255, 180, 0), 1.0)
                        }
                    },
                    Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 215, 0)),
                    StrokeThickness = 1,
                    Cursor = Cursors.Hand
                };

                connectionPolygon.Points = new PointCollection
                {
                    new Point(match1Start, bar1Y + barHeight),           // Top-left
                    new Point(match1End, bar1Y + barHeight),             // Top-right
                    new Point(match2End, bar2Y),                         // Bottom-right
                    new Point(match2Start, bar2Y)                        // Bottom-left
                };

                connectionPolygon.MouseLeftButtonDown += (s, e) =>
                {
                    MatchRegionClicked?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                };

                VisualizationCanvas.Children.Add(connectionPolygon);

                // Update border color to gold when match exists
                if (Parent is FrameworkElement parent)
                {
                    var border = parent as Border;
                    if (border == null)
                    {
                        // Find the border in the visual tree
                        border = FindVisualParent<Border>(this);
                    }
                    
                    if (border != null)
                    {
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)); // Gold
                    }
                }

                // Add text labels
                var label1 = new TextBlock
                {
                    Text = $"Match: 0x{MatchOffset:X}",
                    Foreground = Brushes.LightGreen,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };
                Canvas.SetLeft(label1, match1Start);
                Canvas.SetTop(label1, bar1Y - 12);
                VisualizationCanvas.Children.Add(label1);

                var label2 = new TextBlock
                {
                    Text = $"Match: 0x{MatchOffset2:X}",
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100)),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };
                Canvas.SetLeft(label2, match2Start);
                Canvas.SetTop(label2, bar2Y + barHeight + 5);
                VisualizationCanvas.Children.Add(label2);
            }
            else
            {
                // No match - reset border to default
                if (Parent is FrameworkElement parent)
                {
                    var border = parent as Border;
                    if (border == null)
                    {
                        border = FindVisualParent<Border>(this);
                    }
                    
                    if (border != null)
                    {
                        border.BorderBrush = Application.Current.TryFindResource("Brush.Action.Primary") as Brush 
                                           ?? new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    }
                }
            }

            // Add file labels
            var file1Label = new TextBlock
            {
                Text = "BASELINE",
                Foreground = Brushes.Green,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(file1Label, 10);
            Canvas.SetTop(file1Label, bar1Y + barHeight / 2 - 7);
            VisualizationCanvas.Children.Add(file1Label);

            var file2Label = new TextBlock
            {
                Text = "SUSPECT",
                Foreground = Brushes.Red,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(file2Label, 10);
            Canvas.SetTop(file2Label, bar2Y + barHeight / 2 - 7);
            VisualizationCanvas.Children.Add(file2Label);
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindVisualParent<T>(parentObject);
        }
    }
}
