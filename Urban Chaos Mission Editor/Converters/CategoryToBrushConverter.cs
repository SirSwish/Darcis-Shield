using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using UrbanChaosMissionEditor.Constants;

namespace UrbanChaosMissionEditor.Converters;

/// <summary>
/// Converts a WaypointCategory to a SolidColorBrush
/// </summary>
public class CategoryToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is WaypointCategory category)
        {
            return WaypointColors.GetCategoryBrush(category);
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}