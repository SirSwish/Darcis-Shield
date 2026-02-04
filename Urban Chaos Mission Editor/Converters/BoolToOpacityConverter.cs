using System.Globalization;
using System.Windows.Data;

namespace UrbanChaosMissionEditor.Converters;

/// <summary>
/// Converts a boolean to opacity (true = 1.0, false = 0.4)
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? 1.0 : 0.4;
        return 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}