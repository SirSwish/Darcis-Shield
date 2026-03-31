// /Views/Dialogs/Buildings/AddWalkableWindow.xaml.cs
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UrbanChaosMapEditor.Views.Roofs.Dialogs
{
    /// <summary>
    /// Dialog for adding a walkable region to a building.
    /// The tile bounds are set by the caller (from click-drag on map).
    /// User specifies the height.
    /// </summary>
    public partial class AddWalkableWindow : Window
    {
        private static readonly Regex _signedDigitsOnly = new(@"^-?[0-9]*$");

        public bool WasCancelled { get; private set; } = true;

        // Input: Set by caller before showing dialog
        public int BuildingId1 { get; set; }
        public string BuildingName { get; set; } = "";
        public int TileX1 { get; set; }
        public int TileZ1 { get; set; }
        public int TileX2 { get; set; }
        public int TileZ2 { get; set; }

        // Output: Set when user clicks OK
        public int WorldY { get; private set; }
        public byte StoreyY { get; private set; }

        public AddWalkableWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Calculate normalized bounds
            int minX = Math.Min(TileX1, TileX2);
            int maxX = Math.Max(TileX1, TileX2);
            int minZ = Math.Min(TileZ1, TileZ2);
            int maxZ = Math.Max(TileZ1, TileZ2);

            int width = maxX - minX + 1;
            int depth = maxZ - minZ + 1;

            TxtBuilding.Text = $"#{BuildingId1}: {BuildingName}";
            TxtBounds.Text = $"({minX}, {minZ}) ? ({maxX}, {maxZ})";
            TxtSize.Text = $"{width} - {depth} tiles";

            // Default height - 1 storey = 256
            TxtWorldY.Text = "256";
            TxtWorldY.Focus();
            TxtWorldY.SelectAll();
        }

        private void SignedNumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            string newText = textBox?.Text.Insert(textBox.SelectionStart, e.Text) ?? e.Text;
            e.Handled = !_signedDigitsOnly.IsMatch(newText);
        }

        private void TxtWorldY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtInfo == null) return;

            if (int.TryParse(TxtWorldY.Text, out int worldY))
            {
                int storey = worldY / 256;
                int offset = ((worldY % 256) + 256) % 256;
                int walkableY = worldY >> 5;
                int storeyY = worldY >> 6;

                TxtInfo.Text = $"Storey {storey}, Offset {offset}\n" +
                              $"Walkable.Y = {walkableY} (worldY >> 5)\n" +
                              $"Walkable.StoreyY = {storeyY} (worldY >> 6)";
            }
            else
            {
                TxtInfo.Text = "(enter a valid number)";
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtWorldY.Text, out int worldY))
            {
                MessageBox.Show("Please enter a valid integer for World Y.",
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WorldY = worldY;
            StoreyY = (byte)Math.Clamp(worldY >> 6, 0, 255);
            WasCancelled = false;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            WasCancelled = true;
            DialogResult = false;
            Close();
        }

    }
}