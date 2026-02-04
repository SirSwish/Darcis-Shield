using System.Globalization;
using System.Windows.Data;

namespace UrbanChaosMissionEditor.Converters;

/// <summary>
/// Formats an integer as hex
/// </summary>
public class IntToHexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i)
            return $"0x{i:X8}";
        return "0x00000000";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}