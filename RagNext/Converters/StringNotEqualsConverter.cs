using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace RagNext.Converters
{
    public sealed class StringNotEqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string valStr && parameter is string paramStr)
                return !string.Equals(valStr, paramStr, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
