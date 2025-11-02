using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace RagNext.Converters
{
    // Used with MultiBinding: value[0] = item, value[1] = Type (from x:Type)
    public sealed class TypeMatchConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is null || values[1] is not Type t) return false;
            return t.IsInstanceOfType(values[0]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}