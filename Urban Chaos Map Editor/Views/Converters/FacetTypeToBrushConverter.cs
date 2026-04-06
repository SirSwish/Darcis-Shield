using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;

namespace UrbanChaosMapEditor.Views.Converters
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
                FacetType ft => (byte)ft,
                _ => (byte)0
            };

            var wall   = values.ElementAtOrDefault(1) as Brush ?? Brushes.Lime;
            var fence  = values.ElementAtOrDefault(2) as Brush ?? Brushes.Yellow;
            var cable  = values.ElementAtOrDefault(3) as Brush ?? Brushes.Red;
            var door   = values.ElementAtOrDefault(4) as Brush ?? Brushes.MediumPurple;
            var ladder = values.ElementAtOrDefault(5) as Brush ?? Brushes.Orange;
            var other  = values.ElementAtOrDefault(6) as Brush ?? Brushes.LightSkyBlue;
            var gate   = values.ElementAtOrDefault(7) as Brush ?? Brushes.DodgerBlue;

            return type switch
            {
                3 => wall,
                10 or 11 or 13 => fence,
                9 => cable,
                18 or 19 => door,
                21 => gate,
                12 => ladder,
                _ => other
            };
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => Array.Empty<object>();
        public static FacetTypeToBrushConverter Instance { get; } = new();
    }
}
