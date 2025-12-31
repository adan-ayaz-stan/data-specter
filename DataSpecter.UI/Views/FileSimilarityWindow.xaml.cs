using System.Windows;

namespace DataSpecter.UI.Views
{
    public partial class FileSimilarityWindow : Window
    {
        public FileSimilarityWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
