using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace UrbanChaosEditor.Shared.Converters
{
    public sealed class FacetTypeToBrushConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is not { Length: >= 2 }) return Brushes.LightGray;

            byte type = values[0] switch
            {
                byte b => b,
                int i => (byte)i,
                UrbanChaosMapEditor.Models.FacetType ft => (byte)ft,
                _ => (byte)0
            };

            var wall = values.ElementAtOrDefault(1) as Brush ?? Brushes.Lime;
            var fence = values.ElementAtOrDefault(2) as Brush ?? Brushes.Yellow;
            var cable = values.ElementAtOrDefault(3) as Brush ?? Brushes.Red;
            var door = values.ElementAtOrDefault(4) as Brush ?? Brushes.MediumPurple;
            var ladder = values.ElementAtOrDefault(5) as Brush ?? Brushes.Orange;
            var other = values.ElementAtOrDefault(6) as Brush ?? Brushes.LightSkyBlue;

            return type switch
            {
                3 => wall,
                10 or 11 or 13 => fence,
                9 => cable,
                18 or 19 or 21 => door,
                12 => ladder,
                _ => other
            };
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => Array.Empty<object>();
        public static FacetTypeToBrushConverter Instance { get; } = new();
    }
}
