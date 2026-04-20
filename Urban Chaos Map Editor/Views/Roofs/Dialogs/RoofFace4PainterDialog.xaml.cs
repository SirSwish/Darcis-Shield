// /Views/RoofFace4PainterDialog.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.Services.Buildings;

namespace UrbanChaosMapEditor.Views.Roofs.Dialogs
{
    /// <summary>
    /// Dialog for painting individual RF4 (roof) cells within a walkable region.
    /// Allows selective placement of roof tiles.
    /// </summary>
    public partial class RoofFace4PainterDialog : Window
    {
        private readonly int _walkableId1;
        private readonly int _buildingId1;
        private readonly DWalkableRec _walkable;
        private readonly int _width;
        private readonly int _depth;

        // Track which cells are selected (relative coords)
        private readonly HashSet<(int rx, int rz)> _selectedCells = new();

        // Track which cells already have RF4 entries, keyed by (rx,rz) → existing DrawFlags
        private readonly Dictionary<(int rx, int rz), byte> _existingRF4DrawFlags = new();

        // Grid buttons for toggling
        private readonly Dictionary<(int rx, int rz), Button> _cellButtons = new();

        private static readonly SolidColorBrush SelectedBrush = new(Color.FromRgb(76, 175, 80));   // Green
        private static readonly SolidColorBrush EmptyBrush = new(Color.FromRgb(51, 51, 51));       // Dark gray
        private static readonly SolidColorBrush HoverBrush = new(Color.FromRgb(100, 100, 100));    // Light gray

        public RoofFace4PainterDialog(int walkableId1, int buildingId1, DWalkableRec walkable)
        {
            InitializeComponent();

            _walkableId1 = walkableId1;
            _buildingId1 = buildingId1;
            _walkable = walkable;
            _width = walkable.X2 - walkable.X1;
            _depth = walkable.Z2 - walkable.Z1;

            // Update header
            txtWalkableId.Text = walkableId1.ToString();
            txtBuildingId.Text = buildingId1.ToString();
            txtBounds.Text = $"({walkable.X1},{walkable.Z1}) ? ({walkable.X2},{walkable.Z2}) = {_width}x{_depth}";

            // Set default altitude from walkable Y, seeded in Quarter Storeys (walkable.Y / 2 = QS)
            txtAltitude.Text = (walkable.Y / 2).ToString();

            LoadExistingRF4Cells();
            BuildCellGrid();
        }

        private void LoadExistingRF4Cells()
        {
            _existingRF4DrawFlags.Clear();

            var acc = new BuildingsAccessor(MapDataService.Instance);
            if (!acc.TryGetWalkables(out var walkables, out var roofFaces))
                return;

            if (roofFaces == null) return;

            // Get RF4 entries for this walkable
            for (int i = _walkable.StartFace4; i < _walkable.EndFace4 && i < roofFaces.Length; i++)
            {
                var rf = roofFaces[i];

                // Skip zeroed entries
                if (rf.Y == 0 && rf.RX == 0 && rf.RZ == 0) continue;

                // Convert absolute coords to relative
                int rx = rf.RX - _walkable.X1;
                int rz = (rf.RZ - 128) - _walkable.Z1;

                if (rx >= 0 && rx < _width && rz >= 0 && rz < _depth)
                {
                    _existingRF4DrawFlags[(rx, rz)] = rf.DrawFlags;
                    _selectedCells.Add((rx, rz)); // Pre-select existing cells
                }
            }
        }

        private void BuildCellGrid()
        {
            gridCells.Children.Clear();
            gridCells.RowDefinitions.Clear();
            gridCells.ColumnDefinitions.Clear();
            _cellButtons.Clear();

            // Add column for Z labels
            gridCells.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

            // Add columns for each X tile
            for (int x = 0; x < _width; x++)
            {
                gridCells.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            // Add row for X labels
            gridCells.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });

            // Add rows for each Z tile
            for (int z = 0; z < _depth; z++)
            {
                gridCells.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Add X coordinate labels (top row)
            for (int x = 0; x < _width; x++)
            {
                var label = new TextBlock
                {
                    Text = (_walkable.X1 + x).ToString(),
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 10
                };
                Grid.SetRow(label, 0);
                Grid.SetColumn(label, x + 1);
                gridCells.Children.Add(label);
            }

            // Add Z coordinate labels (left column) and cell buttons
            for (int z = 0; z < _depth; z++)
            {
                // Z label
                var zLabel = new TextBlock
                {
                    Text = (_walkable.Z1 + z).ToString(),
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 5, 0),
                    FontSize = 10
                };
                Grid.SetRow(zLabel, z + 1);
                Grid.SetColumn(zLabel, 0);
                gridCells.Children.Add(zLabel);

                // Cell buttons for this row
                for (int x = 0; x < _width; x++)
                {
                    var btn = CreateCellButton(x, z);
                    Grid.SetRow(btn, z + 1);
                    Grid.SetColumn(btn, x + 1);
                    gridCells.Children.Add(btn);
                    _cellButtons[(x, z)] = btn;
                }
            }
        }

        private Button CreateCellButton(int rx, int rz)
        {
            var isSelected = _selectedCells.Contains((rx, rz));

            var btn = new Button
            {
                Width = 40,
                Height = 40,
                Margin = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                BorderThickness = new Thickness(1),
                Tag = (rx, rz),
                Foreground = Brushes.White
            };

            UpdateCellAppearance(btn, isSelected);

            btn.Click += CellButton_Click;
            btn.MouseEnter += (s, e) => btn.Background = HoverBrush;
            btn.MouseLeave += (s, e) => UpdateCellAppearance(btn, _selectedCells.Contains((rx, rz)));

            return btn;
        }

        private void UpdateCellAppearance(Button btn, bool isSelected)
        {
            if (isSelected)
            {
                btn.Background = SelectedBrush;
                btn.ToolTip = "RF4 tile will be placed here";
            }
            else
            {
                btn.Background = EmptyBrush;
                btn.ToolTip = "Click to add RF4 tile";
            }
        }

        private void CellButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not (int rx, int rz)) return;

            // Toggle selection
            if (_selectedCells.Contains((rx, rz)))
            {
                _selectedCells.Remove((rx, rz));
            }
            else
            {
                _selectedCells.Add((rx, rz));
            }

            UpdateCellAppearance(btn, _selectedCells.Contains((rx, rz)));
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            for (int z = 0; z < _depth; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    _selectedCells.Add((x, z));
                    if (_cellButtons.TryGetValue((x, z), out var btn))
                    {
                        UpdateCellAppearance(btn, true);
                    }
                }
            }
        }

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            _selectedCells.Clear();
            foreach (var kvp in _cellButtons)
            {
                UpdateCellAppearance(kvp.Value, false);
            }
        }

        private void BtnInvert_Click(object sender, RoutedEventArgs e)
        {
            var newSelection = new HashSet<(int, int)>();

            for (int z = 0; z < _depth; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (!_selectedCells.Contains((x, z)))
                    {
                        newSelection.Add((x, z));
                    }
                }
            }

            _selectedCells.Clear();
            foreach (var cell in newSelection)
            {
                _selectedCells.Add(cell);
            }

            foreach (var kvp in _cellButtons)
            {
                var isSelected = _selectedCells.Contains(kvp.Key);
                UpdateCellAppearance(kvp.Value, isSelected);
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            // Altitude entered in Quarter Storeys; convert to raw RF4 units (1 QS = 64 raw)
            if (!short.TryParse(txtAltitude.Text, out short altitudeQS))
            {
                MessageBox.Show("Invalid altitude value.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            short altitude = (short)(altitudeQS * 64);

            if (!sbyte.TryParse(txtDY0.Text, out sbyte dy0) ||
                !sbyte.TryParse(txtDY1.Text, out sbyte dy1) ||
                !sbyte.TryParse(txtDY2.Text, out sbyte dy2))
            {
                MessageBox.Show("Invalid DY values. Must be -128 to 127.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Determine what needs to be added/removed
            var toAdd = _selectedCells.Except(_existingRF4DrawFlags.Keys).ToList();
            var toRemove = _existingRF4DrawFlags.Keys.Except(_selectedCells).ToList();

            if (toAdd.Count == 0 && toRemove.Count == 0)
            {
                MessageBox.Show("No changes to apply.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var msg = $"Changes to apply:\n" +
                     $"  Add {toAdd.Count} RF4 tiles\n" +
                     $"  Remove {toRemove.Count} RF4 tiles\n\n" +
                     $"Continue?";

            if (MessageBox.Show(msg, "Confirm Changes", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                ApplyChanges(toAdd, toRemove, altitude, dy0, dy1, dy2);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyChanges(List<(int rx, int rz)> toAdd, List<(int rx, int rz)> toRemove,
                                  short altitude, sbyte dy0, sbyte dy1, sbyte dy2)
        {
            var adder = new RoofFace4Adder(MapDataService.Instance);

            // First, handle removals by zeroing the RF4 entries
            if (toRemove.Count > 0)
            {
                var acc = new BuildingsAccessor(MapDataService.Instance);
                if (acc.TryGetWalkables(out var walkables, out var roofFaces) && roofFaces != null)
                {
                    var bytes = MapDataService.Instance.GetBytesCopy();

                    // Calculate RF4 data offset
                    if (acc.TryGetWalkablesHeaderOffset(out int walkablesHeaderOff))
                    {
                        ushort nextWalkable = (ushort)(bytes[walkablesHeaderOff] | (bytes[walkablesHeaderOff + 1] << 8));
                        int walkablesDataSize = nextWalkable * 22;
                        int rf4DataOff = walkablesHeaderOff + 4 + walkablesDataSize;

                        // Find and zero each RF4 to remove
                        for (int i = _walkable.StartFace4; i < _walkable.EndFace4 && i < roofFaces.Length; i++)
                        {
                            var rf = roofFaces[i];
                            if (rf.Y == 0 && rf.RX == 0 && rf.RZ == 0) continue;

                            int rx = rf.RX - _walkable.X1;
                            int rz = (rf.RZ - 128) - _walkable.Z1;

                            if (toRemove.Contains((rx, rz)))
                            {
                                // Zero this RF4 entry
                                int rf4Off = rf4DataOff + i * 10;
                                for (int j = 0; j < 10; j++)
                                {
                                    bytes[rf4Off + j] = 0;
                                }
                            }
                        }

                        MapDataService.Instance.ReplaceBytes(bytes);
                    }
                }
            }

            // Now add new entries, preserving DrawFlags from any previously-existing tile at that cell
            int addedCount = 0;
            foreach (var (rx, rz) in toAdd.OrderBy(c => c.rz).ThenBy(c => c.rx))
            {
                byte drawFlags = _existingRF4DrawFlags.TryGetValue((rx, rz), out byte existing) ? existing : (byte)0x08;
                var result = adder.TryAddRoofFace4(
                    _walkableId1,
                    (byte)rx, (byte)rz,
                    altitude,
                    dy0, dy1, dy2,
                    drawFlags);

                if (result.IsSuccess)
                {
                    addedCount++;
                }
            }

            BuildingsChangeBus.Instance.NotifyChanged();

            MessageBox.Show(
                $"Applied changes:\n" +
                $"  Removed: {toRemove.Count} RF4 tiles\n" +
                $"  Added: {addedCount} RF4 tiles",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}