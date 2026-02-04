using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using UrbanChaosMissionEditor.Constants;

namespace UrbanChaosMissionEditor.Converters;

/// <summary>
/// Converts a color index to a SolidColorBrush
/// </summary>
public class ColorIndexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is byte colorIndex)
        {
            return WaypointColors.GetBrush(colorIndex);
        }
        if (value is int intIndex)
        {
            return WaypointColors.GetBrush(intIndex);
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}