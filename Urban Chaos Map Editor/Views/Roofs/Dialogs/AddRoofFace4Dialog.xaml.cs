using System.Windows;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;

namespace UrbanChaosMapEditor.Views.Roofs.Dialogs
{
    public partial class AddRoofFace4Dialog : Window
    {
        private readonly int _walkableId1;
        private readonly int _buildingId1;
        private readonly DWalkableRec _walkable;

        public bool WasConfirmed { get; private set; }
        public int AddedCount { get; private set; }

        public AddRoofFace4Dialog(int walkableId1, int buildingId1, DWalkableRec walkable)
        {
            InitializeComponent();

            _walkableId1 = walkableId1;
            _buildingId1 = buildingId1;
            _walkable = walkable;

            int width = walkable.X2 - walkable.X1;
            int depth = walkable.Z2 - walkable.Z1;
            TxtWalkableInfo.Text = $"Walkable #{walkableId1} (Building #{buildingId1}) - {width}x{depth} tiles";

            // Seed in Quarter Storeys: walkable.Y / 2 = QS (same as RF4 Y / 64)
            TxtAltitude.Text = (walkable.Y / 2).ToString();

            RoofType_Changed(this, new RoutedEventArgs());
        }

        private void RoofType_Changed(object sender, RoutedEventArgs e)
        {
            if (PnlSingleTileSettings == null)
                return;

            bool isSingle = RbSingleTile.IsChecked == true;
            PnlSingleTileSettings.Visibility = isSingle ? Visibility.Visible : Visibility.Collapsed;

            if (RbFlatRoof.IsChecked == true)
            {
                int width = _walkable.X2 - _walkable.X1;
                int depth = _walkable.Z2 - _walkable.Z1;
                BtnAdd.Content = $"Add Flat Roof ({width * depth} tiles)";
            }
            else
            {
                BtnAdd.Content = "Add Single Tile";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            WasConfirmed = false;
            DialogResult = false;
            Close();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Altitude entered in Quarter Storeys; convert to raw RF4 units (1 QS = 64 raw)
            if (!short.TryParse(TxtAltitude.Text, out short altitudeQS))
            {
                MessageBox.Show("Invalid altitude value.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            short altitude = (short)(altitudeQS * 64);

            var adder = new RoofFace4Adder(MapDataService.Instance);
            RoofFace4Result result;

            if (RbFlatRoof.IsChecked == true)
            {
                result = adder.TryCreateFlatRoof(_walkableId1, altitude);
            }
            else
            {
                if (!byte.TryParse(TxtRX.Text, out byte rx) ||
                    !byte.TryParse(TxtRZ.Text, out byte rz) ||
                    !sbyte.TryParse(TxtDY0.Text, out sbyte dy0) ||
                    !sbyte.TryParse(TxtDY1.Text, out sbyte dy1) ||
                    !sbyte.TryParse(TxtDY2.Text, out sbyte dy2))
                {
                    MessageBox.Show("Invalid tile position or corner values.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                byte drawFlags = (byte)(ChkWalkable.IsChecked == true ? 0x08 : 0x00);

                result = adder.TryAddRoofFace4(_walkableId1, rx, rz, altitude, dy0, dy1, dy2, drawFlags);
            }

            if (result.IsSuccess)
            {
                AddedCount = result.ResultId;
                WasConfirmed = true;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show($"Failed to add roof:\n\n{result.ErrorMessage}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}