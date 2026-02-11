using System.Globalization;
using System.Windows.Data;

namespace UrbanChaosMissionEditor.Converters;

/// <summary>
/// Converts between an integer value and a boolean for RadioButton binding.
/// Use ConverterParameter to specify which integer value should return true.
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is string paramStr && int.TryParse(paramStr, out int paramValue))
        {
            return intValue == paramValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter is string paramStr && int.TryParse(paramStr, out int paramValue))
        {
            return paramValue;
        }
        return Binding.DoNothing;
    }
}