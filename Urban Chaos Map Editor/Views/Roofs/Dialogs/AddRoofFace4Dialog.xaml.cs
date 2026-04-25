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

            BtnAdd.Content = $"Add Flat Roof ({width * depth} tiles)";
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
            var result = adder.TryCreateFlatRoof(_walkableId1, altitude);

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