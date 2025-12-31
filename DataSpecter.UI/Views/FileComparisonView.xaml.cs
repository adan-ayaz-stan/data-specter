using System;
using System.Windows;
using System.Windows.Controls;
using DataSpecter.UI.ViewModels;

namespace DataSpecter.UI.Views
{
    public partial class FileComparisonView : UserControl
    {
        public FileComparisonView()
        {
            InitializeComponent();
        }

        private void ComparisonVisualizer_MatchRegionClicked(object sender, EventArgs e)
        {
            // Scroll both hex viewers to show the matching content
            if (DataContext is FileComparisonViewModel viewModel)
            {
                viewModel.ScrollToMatchCommand.Execute(null);
            }
        }
    }
}
