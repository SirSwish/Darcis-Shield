using System.Windows;
using System.Windows.Controls;
using UrbanChaosLightEditor.Services.Export;

namespace UrbanChaosLightEditor.Views.Dialogs
{
    public partial class ExportLightMapDialog : Window
    {
        public LightExportLayerSelection Selection { get; private set; } = new();

        public ExportLightMapDialog()
        {
            InitializeComponent();

            // Default: all layers checked.
            ChkTextures.IsChecked     = true;
            ChkGridLines.IsChecked    = false;
            ChkBuildings.IsChecked    = true;
            ChkPrimGraphics.IsChecked = true;
            ChkLights.IsChecked       = true;
            ChkLightRanges.IsChecked  = true;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);
        private void SelectNone_Click(object sender, RoutedEventArgs e) => SetAll(false);

        private void SetAll(bool value)
        {
            ChkTextures.IsChecked     = value;
            ChkGridLines.IsChecked    = value;
            ChkBuildings.IsChecked    = value;
            ChkPrimGraphics.IsChecked = value;
            ChkLights.IsChecked       = value;
            ChkLightRanges.IsChecked  = value;
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            int outputSize = LightImageExporter.NativeSize;
            if (CmbResolution.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag &&
                int.TryParse(tag, out var parsed))
            {
                outputSize = parsed;
            }

            Selection = new LightExportLayerSelection
            {
                Textures     = ChkTextures.IsChecked     == true,
                GridLines    = ChkGridLines.IsChecked    == true,
                Buildings    = ChkBuildings.IsChecked    == true,
                PrimGraphics = ChkPrimGraphics.IsChecked == true,
                Lights       = ChkLights.IsChecked       == true,
                LightRanges  = ChkLightRanges.IsChecked  == true,
                OutputSize   = outputSize,
                DropAlpha    = ChkDropAlpha.IsChecked    == true,
            };
            DialogResult = true;
        }
    }
}
