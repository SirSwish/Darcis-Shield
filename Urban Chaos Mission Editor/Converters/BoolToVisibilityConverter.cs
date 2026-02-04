using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UrbanChaosMissionEditor.Converters;

/// <summary>
/// Converts a boolean to Visibility (true = Visible, false = Collapsed)
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;

        // If parameter is "Inverse", invert the logic
        if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            boolValue = !boolValue;

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool result = value is Visibility v && v == Visibility.Visible;

        if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            result = !result;

        return result;
    }
}