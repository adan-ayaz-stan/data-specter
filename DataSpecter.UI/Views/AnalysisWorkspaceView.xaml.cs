using System.Windows.Controls;
using System.Windows.Input;
using DataSpecter.UI.ViewModels;
using System.ComponentModel;

namespace DataSpecter.UI.Views
{
    public partial class AnalysisWorkspaceView : UserControl
    {
        public AnalysisWorkspaceView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is AnalysisWorkspaceViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is AnalysisWorkspaceViewModel newViewModel)
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
                UpdateTextDocument(newViewModel.TextDocument);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AnalysisWorkspaceViewModel.TextDocument))
            {
                if (sender is AnalysisWorkspaceViewModel viewModel)
                {
                    UpdateTextDocument(viewModel.TextDocument);
                }
            }
        }

        private void UpdateTextDocument(System.Windows.Documents.FlowDocument? document)
        {
            if (document != null)
            {
                // Check if the document is already assigned to this RichTextBox
                if (RawTextRichTextBox.Document == document)
                {
                    return; // Already assigned, nothing to do
                }
                
                // Create a new FlowDocument and copy the content to avoid the 
                // "Document already belongs to another RichTextBox" exception
                // This happens when navigating between views with the same ViewModel instance
                var newDocument = new System.Windows.Documents.FlowDocument();
                
                // Copy blocks from the source document to the new document
                var blocksToAdd = new System.Collections.Generic.List<System.Windows.Documents.Block>();
                foreach (var block in document.Blocks)
                {
                    blocksToAdd.Add(block);
                }
                
                // Remove blocks from source and add to new document
                document.Blocks.Clear();
                foreach (var block in blocksToAdd)
                {
                    newDocument.Blocks.Add(block);
                }
                
                RawTextRichTextBox.Document = newDocument;
            }
        }

        private void SearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is long offset)
            {
                if (DataContext is AnalysisWorkspaceViewModel viewModel)
                {
                    viewModel.NavigateToOffsetCommand.Execute(offset);
                }
            }
        }
    }
}
