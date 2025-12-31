using System;
using System.Globalization;
using System.Windows.Data;

namespace DataSpecter.UI.Converters
{
    /// <summary>
    /// Converter that returns true if the value is greater than 1.
    /// Used for enabling/disabling pagination buttons.
    /// </summary>
    public class IsGreaterThanOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 1;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
