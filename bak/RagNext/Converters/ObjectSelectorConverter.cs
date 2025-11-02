using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Controls;
using RagsCore.Models;

namespace RagNext.Converters
{
    public sealed class ObjectSelectorConverter : IMultiValueConverter
    {
        // values[0] = Guid ObjectId, values[1] = IEnumerable<GameObject> objects
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return null!;
            var objectId = values[0] is Guid g ? g : Guid.Empty;
            var objects = values[1] as IEnumerable<GameObject>;
            return objects?.FirstOrDefault(o => o.Id == objectId);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            var obj = value as GameObject;
            return new object[]
            {
                obj?.Id ?? Guid.Empty,
                Binding.DoNothing // don't write back the objects collection
            };
        }
    }
}