using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Textures
{
    public partial class TexturesTab : UserControl
    {
        private static readonly Regex _digits = new(@"^\d+$");

        public TexturesTab()
        {
            InitializeComponent();
            // DataContext will flow from MainWindow; no need to set it here.
        }

        private void TextureThumb_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            if ((sender as FrameworkElement)?.Tag is not TextureThumb thumb) return;

            var map = shell.Map;

            // Enter texture paint tool and set the chosen texture
            map.SelectedTool = EditorTool.PaintTexture;
            map.SelectedTextureGroup = thumb.Group;
            map.SelectedTextureNumber = thumb.Number;

            shell.StatusMessage = $"Texture paint: {thumb.RelativeKey} (rot {map.SelectedRotationIndex}) — click a tile to apply";
        }

        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            shell.Map.SelectedRotationIndex = (shell.Map.SelectedRotationIndex + 3) % 4; // -90°
            shell.StatusMessage = $"Rotation: {shell.Map.SelectedRotationIndex}";
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            shell.Map.SelectedRotationIndex = (shell.Map.SelectedRotationIndex + 1) % 4; // +90°
            shell.StatusMessage = $"Rotation: {shell.Map.SelectedRotationIndex}";
        }

        private void Units_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !_digits.IsMatch(e.Text);
        }

        private void Units_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var text = e.DataObject.GetData(DataFormats.Text) as string ?? "";
                if (!_digits.IsMatch(text)) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        private void Eyedropper_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;

            shell.Map.SelectedTool = EditorTool.EyedropTexture;
            shell.StatusMessage = "Eyedropper: click a map tile to sample its texture";
        }
    }

    public sealed class TextureSelectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3)
                return false;

            if (values[0] is not TextureThumb thumb)
                return false;

            if (values[1] == DependencyProperty.UnsetValue || values[2] == DependencyProperty.UnsetValue)
                return false;

            try
            {
                var selectedGroup = values[1];
                var selectedNumber = System.Convert.ToInt32(values[2], CultureInfo.InvariantCulture);

                return Equals(thumb.Group, selectedGroup) && thumb.Number == selectedNumber;
            }
            catch
            {
                return false;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public sealed class RotationArrowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int rotation = 0;

            try
            {
                rotation = System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                rotation = 0;
            }

            return rotation switch
            {
                0 => "↑",
                1 => "→",
                2 => "↓",
                3 => "←",
                _ => "↑"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}