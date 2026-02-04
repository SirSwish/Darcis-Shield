using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UrbanChaosMissionEditor.Converters;

/// <summary>
/// Converts a selection state to a stroke brush
/// </summary>
public class SelectionToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
            return new SolidColorBrush(Colors.White);
        return new SolidColorBrush(Color.FromRgb(30, 30, 30));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}