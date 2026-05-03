using System.Windows;
using System.Windows.Controls;
using UrbanChaosMapEditor.Services.Export;

namespace UrbanChaosMapEditor.Views.Core.Dialogs
{
    public partial class ExportMapDialog : Window
    {
        public ExportLayerSelection Selection { get; private set; } = new();

        public ExportMapDialog()
        {
            InitializeComponent();

            // Default: all layers checked.
            ChkTextures.IsChecked = true;
            ChkRoofTextureOverlay.IsChecked = true;
            ChkGridLines.IsChecked = false;
            ChkMapWho.IsChecked = false;
            ChkHeights.IsChecked = false;
            ChkPapFlags.IsChecked = false;
            ChkPrimGraphics.IsChecked = true;
            ChkPrims.IsChecked = true;
            ChkBuildings.IsChecked = true;
            ChkWalkables.IsChecked = true;
            ChkRoofs.IsChecked = true;
            ChkRoofAltitudes.IsChecked = false;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);
        private void SelectNone_Click(object sender, RoutedEventArgs e) => SetAll(false);

        private void SetAll(bool value)
        {
            ChkTextures.IsChecked = value;
            ChkRoofTextureOverlay.IsChecked = value;
            ChkGridLines.IsChecked = value;
            ChkMapWho.IsChecked = value;
            ChkHeights.IsChecked = value;
            ChkPapFlags.IsChecked = value;
            ChkPrimGraphics.IsChecked = value;
            ChkPrims.IsChecked = value;
            ChkBuildings.IsChecked = value;
            ChkWalkables.IsChecked = value;
            ChkRoofs.IsChecked = value;
            ChkRoofAltitudes.IsChecked = value;
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            int outputSize = MapImageExporter.NativeSize;
            if (CmbResolution.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag &&
                int.TryParse(tag, out var parsed))
            {
                outputSize = parsed;
            }

            Selection = new ExportLayerSelection
            {
                Textures           = ChkTextures.IsChecked == true,
                RoofTextureOverlay = ChkRoofTextureOverlay.IsChecked == true,
                GridLines          = ChkGridLines.IsChecked == true,
                MapWho             = ChkMapWho.IsChecked == true,
                Heights            = ChkHeights.IsChecked == true,
                PapFlags           = ChkPapFlags.IsChecked == true,
                PrimGraphics       = ChkPrimGraphics.IsChecked == true,
                Prims              = ChkPrims.IsChecked == true,
                Buildings          = ChkBuildings.IsChecked == true,
                Walkables          = ChkWalkables.IsChecked == true,
                Roofs              = ChkRoofs.IsChecked == true,
                RoofAltitudes      = ChkRoofAltitudes.IsChecked == true,
                OutputSize         = outputSize,
                DropAlpha          = ChkDropAlpha.IsChecked == true,
            };
            DialogResult = true;
        }
    }
}
