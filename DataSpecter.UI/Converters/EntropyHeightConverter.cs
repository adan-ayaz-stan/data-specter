using System;
using System.Globalization;
using System.Windows.Data;

namespace DataSpecter.UI.Converters
{
    public class EntropyHeightConverter : IMultiValueConverter, IValueConverter
    {
        public static EntropyHeightConverter Instance { get; } = new EntropyHeightConverter();

        // IValueConverter implementation (for simple binding)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double entropy)
            {
                // Max entropy is 8.0. Max height is ~60.
                return (entropy / 8.0) * 60.0;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        // IMultiValueConverter implementation (for multi-binding with container height)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 1 && values[0] is double entropy)
            {
                double maxHeight = 60.0; // Default
                
                if (values.Length >= 2 && values[1] is double containerHeight)
                {
                    maxHeight = containerHeight - 10; // Leave some padding
                }

                // Max entropy is 8.0
                return (entropy / 8.0) * maxHeight;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
