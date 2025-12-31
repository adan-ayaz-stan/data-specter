using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DataSpecter.UI.ViewModels;

namespace DataSpecter.UI
{
    public partial class MainWindow : Window
    {
        // The ViewModel is injected here automatically by App.xaml.cs
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            // This links the XAML to the C# Code
            DataContext = viewModel;
            
            // Start maximized
            WindowState = WindowState.Maximized;
        }

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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}