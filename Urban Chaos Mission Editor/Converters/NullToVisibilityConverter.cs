using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UrbanChaosMissionEditor.Converters;

/// <summary>
/// Null to visibility converter
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value == null;

        if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            isNull = !isNull;

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}