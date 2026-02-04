// /Converters/NullToBoolConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace UrbanChaosEditor.Shared.Converters

{
    [ValueConversion(typeof(object), typeof(bool))]
    public class NullToBoolConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool notNull = value != null;
            return Invert ? !notNull : notNull;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
