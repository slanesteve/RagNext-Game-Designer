using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace RagNext.Converters
{
    public class NullToBoolConverter : IValueConverter
    {
        public bool Invert { get; set; }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => Invert ? value is null : value is not null;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}