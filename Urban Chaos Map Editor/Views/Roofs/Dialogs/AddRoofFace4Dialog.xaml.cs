// /Views/Dialogs/Buildings/AddRoofFace4Dialog.xaml.cs
using System.Windows;
using System.Windows.Controls;
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

            // Update info text
            int width = walkable.X2 - walkable.X1;
            int depth = walkable.Z2 - walkable.Z1;
            TxtWalkableInfo.Text = $"Walkable #{walkableId1} (Building #{buildingId1}) - {width}x{depth} tiles";

            // Set default altitude from walkable
            TxtAltitude.Text = (walkable.Y * 32).ToString();
        }

        private void RoofType_Changed(object sender, RoutedEventArgs e)
        {
            if (PnlPitchDirection == null || PnlPitchedSettings == null || PnlSingleTileSettings == null)
                return;

            bool isPitched = RbPitchedRoof.IsChecked == true;
            bool isSingle = RbSingleTile.IsChecked == true;

            PnlPitchDirection.Visibility = isPitched ? Visibility.Visible : Visibility.Collapsed;
            PnlPitchedSettings.Visibility = isPitched ? Visibility.Visible : Visibility.Collapsed;
            PnlSingleTileSettings.Visibility = isSingle ? Visibility.Visible : Visibility.Collapsed;

            // Update button text
            if (RbFlatRoof.IsChecked == true)
            {
                int width = _walkable.X2 - _walkable.X1;
                int depth = _walkable.Z2 - _walkable.Z1;
                BtnAdd.Content = $"Add Flat Roof ({width * depth} tiles)";
            }
            else if (isPitched)
            {
                int width = _walkable.X2 - _walkable.X1;
                int depth = _walkable.Z2 - _walkable.Z1;
                BtnAdd.Content = $"Add Pitched Roof ({width * depth} tiles)";
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
            // Parse altitude
            if (!short.TryParse(TxtAltitude.Text, out short altitude))
            {
                MessageBox.Show("Invalid altitude value.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var adder = new RoofFace4Adder(MapDataService.Instance);
            RoofFace4Result result;

            if (RbFlatRoof.IsChecked == true)
            {
                // Create flat roof
                result = adder.TryCreateFlatRoof(_walkableId1, altitude);
            }
            else if (RbPitchedRoof.IsChecked == true)
            {
                // Parse pitch settings
                if (!sbyte.TryParse(TxtPitchPerTile.Text, out sbyte pitchPerTile))
                {
                    MessageBox.Show("Invalid pitch per tile value.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get direction
                PitchDirection direction = PitchDirection.SlopesNorth;
                if (CmbPitchDirection.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
                {
                    if (int.TryParse(tagStr, out int dirVal))
                    {
                        direction = (PitchDirection)dirVal;
                    }
                }

                result = adder.TryCreatePitchedRoof(_walkableId1, altitude, pitchPerTile, direction);
            }
            else // Single tile
            {
                // Parse single tile settings
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