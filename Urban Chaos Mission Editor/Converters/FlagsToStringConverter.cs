using System.Globalization;
using System.Windows.Data;
using UrbanChaosMissionEditor.Constants;

namespace UrbanChaosMissionEditor.Converters;

/// <summary>
/// Converts WaypointFlags to a display string
/// </summary>
public class FlagsToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is WaypointFlags flags)
        {
            var parts = new List<string>();
            if (flags.HasFlag(WaypointFlags.Sucks)) parts.Add("Invalid");
            if (flags.HasFlag(WaypointFlags.Inverse)) parts.Add("Inverse");
            if (flags.HasFlag(WaypointFlags.Inside)) parts.Add("Inside");
            if (flags.HasFlag(WaypointFlags.Ware)) parts.Add("Warehouse");
            if (flags.HasFlag(WaypointFlags.Referenced)) parts.Add("Referenced");
            if (flags.HasFlag(WaypointFlags.Optional)) parts.Add("Optional");
            return parts.Count > 0 ? string.Join(", ", parts) : "None";
        }
        return "None";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}