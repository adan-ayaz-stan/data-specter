using System.Windows;
using DataSpecter.UI.ViewModels;

namespace DataSpecter.UI.Views
{
    public partial class EntropyAnalysisWindow : Window
    {
        public EntropyAnalysisWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
