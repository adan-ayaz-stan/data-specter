using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DataSpecter.UI.Models;

namespace DataSpecter.UI.Controls
{
    public partial class HexViewer : UserControl
    {
        public static readonly DependencyProperty BytesProperty =
            DependencyProperty.Register("Bytes", typeof(IEnumerable), typeof(HexViewer), new PropertyMetadata(null));

        public IEnumerable Bytes
        {
            get { return (IEnumerable)GetValue(BytesProperty); }
            set { SetValue(BytesProperty, value); }
        }


        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(HexViewer), new PropertyMetadata("HEX VIEW"));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty ScrollToOffsetProperty =
            DependencyProperty.Register("ScrollToOffset", typeof(long), typeof(HexViewer), 
                new PropertyMetadata(-1L, OnScrollToOffsetChanged));

        public long ScrollToOffset
        {
            get { return (long)GetValue(ScrollToOffsetProperty); }
            set { SetValue(ScrollToOffsetProperty, value); }
        }

        private static void OnScrollToOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexViewer viewer && e.NewValue is long offset && offset >= 0)
            {
                viewer.ScrollToByteOffset(offset);
            }
        }

        public HexViewer()
        {
            InitializeComponent();
        }

        private void ScrollToByteOffset(long offset)
        {
            // Find the ListBox in the visual tree
            var listBox = FindVisualChild<ListBox>(this);
            if (listBox == null || Bytes == null)
                return;

            // Calculate which row contains this offset (16 bytes per row)
            int rowIndex = (int)(offset / 16);

            // Scroll to that row
            if (rowIndex >= 0 && rowIndex < listBox.Items.Count)
            {
                var item = listBox.Items[rowIndex];
                listBox.ScrollIntoView(item);
                
                // Update highlight state if the item is a HexRow
                if (item is HexRow hexRow)
                {
                    // Trigger UI update to show highlighted bytes
                    listBox.Items.Refresh();
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild)
                    return typedChild;
                
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            
            return null;
        }
    }
}
