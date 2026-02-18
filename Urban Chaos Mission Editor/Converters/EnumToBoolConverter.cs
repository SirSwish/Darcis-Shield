using System;
using System.Globalization;
using System.Windows.Data;

namespace UrbanChaosMissionEditor.Converters
{
    public class EnumToBoolConverter : IValueConverter
    {
        /// <summary>
        /// Converts an enum value to a boolean based on comparison with the converter parameter.
        /// </summary>
        /// <param name="value">The enum value to convert</param>
        /// <param name="targetType">The type of the binding target property (should be bool)</param>
        /// <param name="parameter">The enum value to compare against (as string)</param>
        /// <param name="culture">The culture to use in the converter</param>
        /// <returns>True if value equals the parameter value; otherwise false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            // Get the string representation of the parameter
            string parameterString = parameter.ToString();

            // If value is an enum, compare its string representation
            if (value.GetType().IsEnum)
            {
                return value.ToString().Equals(parameterString, StringComparison.OrdinalIgnoreCase);
            }

            // Fallback: direct comparison
            return value.Equals(parameter);
        }

        /// <summary>
        /// Converts a boolean back to an enum value.
        /// </summary>
        /// <param name="value">The boolean value from the binding target</param>
        /// <param name="targetType">The type to convert to (the enum type)</param>
        /// <param name="parameter">The enum value to return when true (as string)</param>
        /// <param name="culture">The culture to use in the converter</param>
        /// <returns>The enum value if true; otherwise null</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool isChecked || !isChecked || parameter == null)
                return null;

            // If targetType is an enum type, try to parse the parameter
            if (targetType.IsEnum)
            {
                try
                {
                    return Enum.Parse(targetType, parameter.ToString(), ignoreCase: true);
                }
                catch
                {
                    return null;
                }
            }

            return parameter;
        }
    }
}