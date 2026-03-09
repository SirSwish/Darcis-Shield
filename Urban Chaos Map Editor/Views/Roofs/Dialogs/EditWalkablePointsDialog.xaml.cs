// /Views/Dialogs/EditWalkablePointsDialog.xaml.cs
using System.Windows;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;

namespace UrbanChaosMapEditor.Views.Roofs.Dialogs
{
    /// <summary>
    /// Dialog for editing StartPoint/EndPoint fields in a DWalkable structure.
    /// Used for testing if prim_points are required for roof rendering.
    /// </summary>
    public partial class EditWalkablePointsDialog : Window
    {
        private readonly int _walkableId1;
        private readonly int _buildingId1;
        private readonly WalkableEditor _editor;
        private readonly MapDataService _mapData;

        private ushort _originalStartPoint;
        private ushort _originalEndPoint;

        public EditWalkablePointsDialog(int walkableId1, int buildingId1)
        {
            InitializeComponent();

            _walkableId1 = walkableId1;
            _buildingId1 = buildingId1;
            _mapData = MapDataService.Instance;
            _editor = new WalkableEditor(_mapData);

            LoadCurrentValues();
        }

        private void LoadCurrentValues()
        {
            TxtWalkableInfo.Text = $"Walkable #{_walkableId1} in Building #{_buildingId1}";

            if (!_editor.TryGetWalkableOffset(_walkableId1, out int offset))
            {
                MessageBox.Show("Could not find walkable offset.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            TxtFileOffset.Text = $"File Offset: 0x{offset:X}";

            if (!_editor.TryGetRawBytes(_walkableId1, out byte[] raw))
            {
                MessageBox.Show("Could not read walkable data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Parse StartPoint and EndPoint from raw bytes
            _originalStartPoint = (ushort)(raw[0] | (raw[1] << 8));
            _originalEndPoint = (ushort)(raw[2] | (raw[3] << 8));

            TxtStartPoint.Text = _originalStartPoint.ToString();
            TxtEndPoint.Text = _originalEndPoint.ToString();

            UpdatePointCount();
        }

        private void UpdatePointCount()
        {
            if (ushort.TryParse(TxtStartPoint.Text, out ushort sp) &&
                ushort.TryParse(TxtEndPoint.Text, out ushort ep))
            {
                int count = ep - sp;
                TxtPointCount.Text = $"Point Count: {count} points (range {sp} to {ep})";
            }
            else
            {
                TxtPointCount.Text = "Point Count: (invalid)";
            }
        }

        private void BtnZeroBoth_Click(object sender, RoutedEventArgs e)
        {
            TxtStartPoint.Text = "0";
            TxtEndPoint.Text = "0";
            UpdatePointCount();
        }

        private void BtnCopyFrom_Click(object sender, RoutedEventArgs e)
        {
            // Show a simple input dialog to enter a walkable ID to copy from
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the 1-based Walkable ID to copy StartPoint/EndPoint from:",
                "Copy From Walkable",
                "1");

            if (string.IsNullOrWhiteSpace(input) || !int.TryParse(input, out int sourceId))
                return;

            if (sourceId < 1)
            {
                MessageBox.Show("Walkable ID must be >= 1", "Invalid ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_editor.TryGetRawBytes(sourceId, out byte[] sourceRaw))
            {
                MessageBox.Show($"Could not read walkable #{sourceId}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ushort sourceStart = (ushort)(sourceRaw[0] | (sourceRaw[1] << 8));
            ushort sourceEnd = (ushort)(sourceRaw[2] | (sourceRaw[3] << 8));

            TxtStartPoint.Text = sourceStart.ToString();
            TxtEndPoint.Text = sourceEnd.ToString();
            UpdatePointCount();

            MessageBox.Show($"Copied from Walkable #{sourceId}:\nStartPoint={sourceStart}, EndPoint={sourceEnd}",
                "Values Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (!ushort.TryParse(TxtStartPoint.Text, out ushort newStart))
            {
                MessageBox.Show("Invalid StartPoint value. Must be 0-65535.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ushort.TryParse(TxtEndPoint.Text, out ushort newEnd))
            {
                MessageBox.Show("Invalid EndPoint value. Must be 0-65535.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirm if values are different
            if (newStart != _originalStartPoint || newEnd != _originalEndPoint)
            {
                var result = MessageBox.Show(
                    $"Change walkable #{_walkableId1} point range?\n\n" +
                    $"StartPoint: {_originalStartPoint} ? {newStart}\n" +
                    $"EndPoint: {_originalEndPoint} ? {newEnd}\n\n" +
                    "The map will need to be saved and reloaded in-game to test changes.",
                    "Confirm Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            // Apply changes
            if (!_editor.TrySetPointRange(_walkableId1, newStart, newEnd))
            {
                MessageBox.Show("Failed to write changes.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show(
                $"Changes applied successfully.\n\n" +
                $"Walkable #{_walkableId1}:\n" +
                $"StartPoint = {newStart}\n" +
                $"EndPoint = {newEnd}\n\n" +
                "Save the map and test in-game.",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}