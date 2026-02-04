// /Converters/CountToBoolConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace UrbanChaosEditor.Shared.Converters
{
    /// <summary>Returns true when the bound value is an int > 0.</summary>
    public sealed class CountToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int i && i > 0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
