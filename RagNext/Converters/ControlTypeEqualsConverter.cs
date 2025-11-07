using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using RagsCore.Actions;

namespace RagNext.Converters
{
    public sealed class ControlTypeEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is InputControlType ct && parameter is string s)
                return string.Equals(ct.ToString(), s, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}