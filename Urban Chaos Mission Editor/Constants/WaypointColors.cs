using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace UrbanChaosMissionEditor.Constants
{
    /// <summary>
    /// Waypoint color definitions (from WayWind.cpp button_colours)
    /// </summary>
    public static class WaypointColors
    {
        /// <summary>
        /// RGB color values for the 15 waypoint colors
        /// </summary>
        public static readonly (byte R, byte G, byte B, string Name)[] Colors =
        {
        (0, 0, 0, "Black"),
        (255, 255, 255, "White"),
        (255, 0, 0, "Red"),
        (255, 255, 0, "Yellow"),
        (0, 255, 0, "Green"),
        (0, 255, 255, "Light Blue"),
        (0, 0, 255, "Blue"),
        (255, 0, 255, "Purple"),
        (238, 176, 176, "Mark's Pink"),
        (139, 112, 85, "Burnt Umber"),
        (127, 76, 180, "Deep Purple"),
        (76, 196, 174, "Soylent Green"),
        (195, 52, 3, "Terracotta"),
        (171, 249, 167, "Mint"),
        (168, 178, 54, "Rat's Piss")
    };

        /// <summary>
        /// Get Color for a given color index
        /// </summary>
        public static Color GetColor(int index)
        {
            if (index < 0 || index >= Colors.Length)
                return System.Windows.Media.Colors.Gray;

            var c = Colors[index];
            return Color.FromRgb(c.R, c.G, c.B);
        }

        /// <summary>
        /// Get SolidColorBrush for a given color index
        /// </summary>
        public static SolidColorBrush GetBrush(int index)
        {
            return new SolidColorBrush(GetColor(index));
        }

        /// <summary>
        /// Get color name for a given color index
        /// </summary>
        public static string GetColorName(int index)
        {
            if (index < 0 || index >= Colors.Length)
                return "Unknown";
            return Colors[index].Name;
        }

        /// <summary>
        /// Category colors for filter pills
        /// </summary>
        public static readonly Dictionary<WaypointCategory, Color> CategoryColors = new()
    {
        { WaypointCategory.Player, Color.FromRgb(65, 105, 225) },      // Royal Blue
        { WaypointCategory.Enemies, Color.FromRgb(220, 20, 60) },      // Crimson
        { WaypointCategory.Items, Color.FromRgb(255, 215, 0) },        // Gold
        { WaypointCategory.Traps, Color.FromRgb(255, 69, 0) },         // Orange Red
        { WaypointCategory.Cameras, Color.FromRgb(138, 43, 226) },     // Blue Violet
        { WaypointCategory.Misc, Color.FromRgb(128, 128, 128) },       // Gray
        { WaypointCategory.MapExits, Color.FromRgb(34, 139, 34) },     // Forest Green
        { WaypointCategory.TextMessages, Color.FromRgb(0, 191, 255) }  // Deep Sky Blue
    };

        /// <summary>
        /// Get category color
        /// </summary>
        public static Color GetCategoryColor(WaypointCategory category)
        {
            return CategoryColors.TryGetValue(category, out var color) ? color : System.Windows.Media.Colors.Gray;
        }

        /// <summary>
        /// Get category brush
        /// </summary>
        public static SolidColorBrush GetCategoryBrush(WaypointCategory category)
        {
            return new SolidColorBrush(GetCategoryColor(category));
        }
    }
}
