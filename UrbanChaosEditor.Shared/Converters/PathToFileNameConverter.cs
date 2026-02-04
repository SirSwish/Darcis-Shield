using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace UrbanChaosEditor.Shared.Converters

{
    /// <summary>Turns a full path into just the file name.</summary>
    public sealed class PathToFileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string s && !string.IsNullOrWhiteSpace(s) ? Path.GetFileName(s) : value ?? string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
