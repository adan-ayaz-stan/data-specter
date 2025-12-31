using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DataSpecter.UI.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly Random _random = new Random();

        public DashboardView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ParticleCanvas.Children.Count == 0)
            {
                InitParticles(50);
            }
        }

        private void InitParticles(int count)
        {
            double width = this.ActualWidth > 0 ? this.ActualWidth : 800;
            double height = this.ActualHeight > 0 ? this.ActualHeight : 600;

            // Get the geometry from XAML resources
            var starGeometry = (Geometry)FindResource("StarIcon");

            for (int i = 0; i < count; i++)
            {
                // 1. Create the particle (Using Path for the Star Shape)
                var particle = new Path
                {
                    Data = starGeometry,
                    Fill = new SolidColorBrush(GetRandomCyberColor()),
                    Opacity = 0, // Start invisible
                    Stretch = Stretch.Uniform,
                    Width = _random.Next(5, 15),
                    Height = _random.Next(5, 15),
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };

                // Create a TransformGroup so we can Rotate AND Scale
                var transformGroup = new TransformGroup();
                var scaleTransform = new ScaleTransform(1.0, 1.0);
                var rotateTransform = new RotateTransform(0); // Start angle
                transformGroup.Children.Add(scaleTransform);
                transformGroup.Children.Add(rotateTransform);
                particle.RenderTransform = transformGroup;

                // 2. Position randomly
                Canvas.SetLeft(particle, _random.NextDouble() * width);
                Canvas.SetTop(particle, _random.NextDouble() * height);

                // 3. Create Animation Storyboard
                var storyboard = new Storyboard();
                var duration = TimeSpan.FromSeconds(_random.Next(3, 8));

                // --- Opacity (Twinkle) ---
                var opacityAnim = new DoubleAnimation
                {
                    From = 0.0,
                    To = _random.NextDouble() * 0.8 + 0.2,
                    Duration = duration,
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(_random.NextDouble() * 3)
                };

                // --- Scale (Pulse) ---
                var scaleAnim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 1.8,
                    Duration = duration,
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };

                // --- Rotation (Spin) ---
                var rotateAnim = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromSeconds(_random.Next(10, 20)),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                // Apply Animations
                Storyboard.SetTarget(opacityAnim, particle);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));

                Storyboard.SetTarget(scaleAnim, particle);
                Storyboard.SetTargetProperty(scaleAnim, new PropertyPath("RenderTransform.Children[0].ScaleX"));

                var scaleYAnim = scaleAnim.Clone();
                Storyboard.SetTarget(scaleYAnim, particle);
                Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("RenderTransform.Children[0].ScaleY"));

                Storyboard.SetTarget(rotateAnim, particle);
                Storyboard.SetTargetProperty(rotateAnim, new PropertyPath("RenderTransform.Children[1].Angle"));

                storyboard.Children.Add(opacityAnim);
                storyboard.Children.Add(scaleAnim);
                storyboard.Children.Add(scaleYAnim);
                storyboard.Children.Add(rotateAnim);

                ParticleCanvas.Children.Add(particle);
                storyboard.Begin();
            }
        }

        private Color GetRandomCyberColor()
        {
            int choice = _random.Next(0, 4);
            switch (choice)
            {
                case 0: return Color.FromRgb(255, 0, 50);      // Neon Red
                case 1: return Color.FromRgb(255, 100, 100);   // Light Red
                case 2: return Color.FromRgb(200, 0, 0);       // Dark Red
                default: return Color.FromArgb(150, 255, 255, 255); // White/Grey spark
            }
        }

        // --- Keep your existing event handlers (like GlassPanel_MouseMove) below ---
        private void GlassPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is ContentControl panel)
            {
                if (panel.Template.FindName("PointerHighlightBrush", panel) is RadialGradientBrush brush)
                {
                    Point p = e.GetPosition(panel);

                    if (panel.ActualWidth > 0 && panel.ActualHeight > 0)
                    {
                        double x = p.X / panel.ActualWidth;
                        double y = p.Y / panel.ActualHeight;

                        brush.Center = new Point(x, y);
                        brush.GradientOrigin = new Point(x, y);
                    }
                }
            }
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                
                if (files != null && files.Length > 0 && DataContext is ViewModels.DashboardViewModel viewModel)
                {
                    // Filter to only existing files
                    var existingFiles = files.Where(f => System.IO.File.Exists(f)).ToArray();
                    
                    if (existingFiles.Length > 0 && viewModel.DropFilesCommand.CanExecute(existingFiles))
                    {
                        viewModel.DropFilesCommand.Execute(existingFiles);
                    }
                }
            }
        }
    }
}