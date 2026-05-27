using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using RagsCore.Actions;

namespace RagNext.Converters
{
    public sealed class DataTypeEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is InputDataType dt && parameter is string s)
                return string.Equals(dt.ToString(), s, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
