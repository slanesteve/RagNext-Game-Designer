using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Controls;
using RagsCore.Models;

namespace RagNext.Converters
{
    public sealed class RoomSelectorConverter : IMultiValueConverter
    {
        // values[0] = Guid RoomId, values[1] = IEnumerable<Room> rooms
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return null!;
            var roomId = values[0] is Guid g ? g : Guid.Empty;
            var rooms = values[1] as IEnumerable<Room>;
            return rooms?.FirstOrDefault(r => r.Id == roomId);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            var room = value as Room;
            return new object[]
            {
                room?.Id ?? Guid.Empty,
                Binding.DoNothing // don't write back the rooms collection
            };
        }
    }
}