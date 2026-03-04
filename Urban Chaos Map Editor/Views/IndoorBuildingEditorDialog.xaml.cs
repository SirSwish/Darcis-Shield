// /Views/IndoorBuildingEditorDialog.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;

namespace UrbanChaosMapEditor.Views
{
    public partial class IndoorBuildingEditorDialog : Window
    {
        private int _currentBuildingId = 0;
        private IndoorBuildingSummary _summary;
        private List<DStoreyViewModel> _allStoreys;
        private List<DStyleEntry> _allDStyles;
        private List<FacetChainEntry> _facetChain;
        private List<WalkableEntry> _walkables;

        public IndoorBuildingEditorDialog()
        {
            InitializeComponent();
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtBuildingId.Text, out int buildingId) || buildingId < 1)
            {
                MessageBox.Show("Please enter a valid building ID.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadBuilding(buildingId);
        }

        private void LoadBuilding(int buildingId)
        {
            _currentBuildingId = buildingId;

            var accessor = new DStoreyAccessor(MapDataService.Instance);
            _summary = accessor.GetIndoorBuildingSummary(buildingId);

            if (_summary == null)
            {
                txtStatus.Text = "Building not found";
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            // Update summary display
            txtType.Text = _summary.BuildingType == 1 ? "1 (Indoor/Warehouse)" : $"{_summary.BuildingType} (Normal)";
            txtType.Foreground = _summary.BuildingType == 1
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.Orange;

            txtFacets.Text = $"{_summary.FacetStart} - {_summary.FacetEnd - 1} ({_summary.FacetCount} total)";
            txtStoreys.Text = _summary.Storeys.Count.ToString();
            txtNegDStyles.Text = _summary.NegativeDStylesCount.ToString();

            txtStatus.Text = _summary.IsProperlyConfigured ? "✓ Properly configured" : "⚠ Missing storey refs";
            txtStatus.Foreground = _summary.IsProperlyConfigured
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.Orange;

            // Load all data
            _allStoreys = accessor.GetStoreyViewModels();
            _allDStyles = accessor.GetDStyleEntries();

            // Build facet chain
            LoadFacetChain(accessor);

            // Load walkables
            LoadWalkables();

            // Refresh grids
            RefreshStoreysGrid();
            RefreshDStylesGrid();
            RefreshFacetChainGrid();
            RefreshWalkablesGrid();
        }

        private void LoadFacetChain(DStoreyAccessor accessor)
        {
            _facetChain = new List<FacetChainEntry>();

            if (_summary == null) return;

            if (!accessor.TryGetSectionOffsets(out var offsets)) return;

            var bytes = MapDataService.Instance.GetBytesCopy();
            var storeys = accessor.ReadAllStoreys();
            var dstyles = accessor.ReadAllDStyles();

            for (int f = _summary.FacetStart; f < _summary.FacetEnd; f++)
            {
                int facetOff = offsets.FacetsData + (f - 1) * 26;
                if (facetOff + 26 > bytes.Length) break;

                byte height = bytes[facetOff + 1];
                byte fheight = bytes[facetOff + 10];
                ushort fadeLevel = (ushort)(bytes[facetOff + 12] | (bytes[facetOff + 13] << 8));

                var entry = new FacetChainEntry
                {
                    FacetId = f,
                    Height = height,
                    FHeight = fheight,
                    FadeLevel = fadeLevel
                };

                if (fadeLevel < dstyles.Length)
                {
                    entry.DStyleValue = dstyles[fadeLevel];

                    if (entry.DStyleValue < 0)
                    {
                        int storeyIdx = -entry.DStyleValue;
                        entry.StoreyIndex = storeyIdx;

                        if (storeyIdx < storeys.Length)
                        {
                            entry.StoreyStyle = storeys[storeyIdx].Style;
                            entry.StoreyHeight = storeys[storeyIdx].Height;
                            entry.Status = "✓ OK";
                        }
                        else
                        {
                            entry.Status = "⚠ Invalid storey";
                        }
                    }
                    else
                    {
                        entry.Status = fheight == 0 ? "Texture (outside)" : "⚠ Needs storey ref";
                    }
                }
                else
                {
                    entry.Status = "⚠ Invalid FadeLevel";
                }

                _facetChain.Add(entry);
            }
        }

        private void LoadWalkables()
        {
            _walkables = new List<WalkableEntry>();

            if (_summary == null) return;

            var bldAcc = new BuildingsAccessor(MapDataService.Instance);
            if (!bldAcc.TryGetWalkables(out var walkables, out var roofFaces)) return;

            foreach (int wId in _summary.WalkableIds)
            {
                if (wId < 1 || wId >= walkables.Length) continue;

                var w = walkables[wId];
                _walkables.Add(new WalkableEntry
                {
                    Id = wId,
                    X1 = w.X1,
                    Z1 = w.Z1,
                    X2 = w.X2,
                    Z2 = w.Z2,
                    Y = w.Y,
                    StoreyY = w.StoreyY,
                    StartFace4 = w.StartFace4,
                    EndFace4 = w.EndFace4,
                    Next = w.Next
                });
            }
        }

        private void RefreshStoreysGrid()
        {
            if (_allStoreys == null) return;

            var filtered = chkShowAllStoreys?.IsChecked == true
                ? _allStoreys
                : _allStoreys.Where(s => s.Building == _currentBuildingId).ToList();

            dgStoreys.ItemsSource = filtered;
        }

        private void RefreshDStylesGrid()
        {
            if (_allDStyles == null) return;

            var filtered = _allDStyles.AsEnumerable();

            if (chkShowOnlyNegative?.IsChecked == true)
            {
                filtered = filtered.Where(d => d.IsStoreyRef);
            }

            if (chkShowOnlyUsed?.IsChecked == true && _summary != null)
            {
                var usedIndices = _summary.UsedDStyles.Select(d => d.Index).ToHashSet();
                filtered = filtered.Where(d => usedIndices.Contains(d.Index));
            }

            dgDStyles.ItemsSource = filtered.ToList();
        }

        private void RefreshFacetChainGrid()
        {
            dgFacetChain.ItemsSource = _facetChain;
        }

        private void RefreshWalkablesGrid()
        {
            dgWalkables.ItemsSource = _walkables;
        }

        private void ChkShowAllStoreys_Changed(object sender, RoutedEventArgs e)
        {
            RefreshStoreysGrid();
        }

        private void ChkShowOnlyNegative_Changed(object sender, RoutedEventArgs e)
        {
            RefreshDStylesGrid();
        }

        private void ChkShowOnlyUsed_Changed(object sender, RoutedEventArgs e)
        {
            RefreshDStylesGrid();
        }

        private void BtnListType1_Click(object sender, RoutedEventArgs e)
        {
            var accessor = new DStoreyAccessor(MapDataService.Instance);
            if (!accessor.TryGetSectionOffsets(out var offsets)) return;

            var bytes = MapDataService.Instance.GetBytesCopy();
            var type1Buildings = new List<string>();

            for (int b = 1; b < offsets.NextBuilding; b++)
            {
                int bldOff = offsets.BuildingsData + (b - 1) * 24;
                if (bldOff + 24 > bytes.Length) break;

                byte type = bytes[bldOff + 11];
                if (type == 1)
                {
                    var summary = accessor.GetIndoorBuildingSummary(b);
                    string status = summary?.IsProperlyConfigured == true ? "✓" : "⚠";
                    type1Buildings.Add($"{status} Building #{b}: {summary?.FacetCount ?? 0} facets, {summary?.NegativeDStylesCount ?? 0} storey refs");
                }
            }

            if (type1Buildings.Count == 0)
            {
                MessageBox.Show("No Type=1 (Indoor/Warehouse) buildings found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Type=1 Buildings ({type1Buildings.Count}):\n\n" + string.Join("\n", type1Buildings),
                    "Indoor Buildings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void BtnAddStorey_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddStoreyDialog(_currentBuildingId) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                var accessor = new DStoreyAccessor(MapDataService.Instance);
                var (success, newIndex, error) = accessor.AddDStorey(
                    dialog.Style, dialog.StoreyHeight, dialog.Flags, (ushort)_currentBuildingId);

                if (success)
                {
                    MessageBox.Show($"Added DStorey #{newIndex}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadBuilding(_currentBuildingId);
                }
                else
                {
                    MessageBox.Show($"Failed: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEditStorey_Click(object sender, RoutedEventArgs e)
        {
            if (dgStoreys.SelectedItem is not DStoreyViewModel storey) return;

            var dialog = new EditStoreyDialog(storey) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                var accessor = new DStoreyAccessor(MapDataService.Instance);
                if (accessor.UpdateDStorey(storey.Index, dialog.Style, dialog.StoreyHeight, dialog.Flags, dialog.Building))
                {
                    LoadBuilding(_currentBuildingId);
                }
            }
        }

        private void BtnAddNegDStyle_Click(object sender, RoutedEventArgs e)
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the DStorey index to reference:",
                "Add Storey Reference",
                "1");

            if (string.IsNullOrWhiteSpace(input) || !int.TryParse(input, out int storeyIndex) || storeyIndex < 1)
                return;

            var accessor = new DStoreyAccessor(MapDataService.Instance);
            var (success, newIndex, error) = accessor.AddNegativeDStyle(storeyIndex);

            if (success)
            {
                MessageBox.Show(
                    $"Added dstyles[{newIndex}] = -{storeyIndex}\n\n" +
                    $"You can now update facet FadeLevel values to use index {newIndex}.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                LoadBuilding(_currentBuildingId);
            }
            else
            {
                MessageBox.Show($"Failed: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEditDStyle_Click(object sender, RoutedEventArgs e)
        {
            if (dgDStyles.SelectedItem is not DStyleEntry entry) return;

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter new value for dstyles[{entry.Index}]:\n" +
                "(Negative values reference DStorey entries)",
                "Edit dstyles",
                entry.Value.ToString());

            if (string.IsNullOrWhiteSpace(input) || !short.TryParse(input, out short newValue))
                return;

            var accessor = new DStoreyAccessor(MapDataService.Instance);
            if (accessor.UpdateDStyle(entry.Index, newValue))
            {
                LoadBuilding(_currentBuildingId);
            }
        }

        private void BtnEditFacetFade_Click(object sender, RoutedEventArgs e)
        {
            if (dgFacetChain.SelectedItem is not FacetChainEntry entry) return;

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter new FadeLevel for Facet #{entry.FacetId}:\n" +
                "(This is an index into the dstyles[] array)",
                "Edit FadeLevel",
                entry.FadeLevel.ToString());

            if (string.IsNullOrWhiteSpace(input) || !ushort.TryParse(input, out ushort newFadeLevel))
                return;

            var accessor = new DStoreyAccessor(MapDataService.Instance);
            if (accessor.UpdateFacetFadeLevel(entry.FacetId, newFadeLevel))
            {
                LoadBuilding(_currentBuildingId);
            }
        }

        private void BtnPaintRF4_Click(object sender, RoutedEventArgs e)
        {
            if (dgWalkables.SelectedItem is not WalkableEntry entry) return;

            var bldAcc = new BuildingsAccessor(MapDataService.Instance);
            if (!bldAcc.TryGetWalkables(out var walkables, out _)) return;

            if (entry.Id >= walkables.Length) return;

            var dialog = new RoofFace4PainterDialog(entry.Id, _currentBuildingId, walkables[entry.Id])
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                LoadBuilding(_currentBuildingId);
            }
        }

        private void BtnFixBuilding_Click(object sender, RoutedEventArgs e)
        {
            if (_summary == null || _currentBuildingId < 1)
            {
                MessageBox.Show("Please load a building first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_summary.BuildingType != 1)
            {
                MessageBox.Show(
                    "This building is not Type=1 (Indoor/Warehouse).\n\n" +
                    "You need to set the building Type to 1 first.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Count facets that need storey refs
            var facetsNeedingFix = _facetChain
                .Where(f => f.FHeight != 0 && f.DStyleValue >= 0)
                .ToList();

            if (facetsNeedingFix.Count == 0)
            {
                MessageBox.Show("All interior facets already have storey references.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Found {facetsNeedingFix.Count} interior facets (FHeight != 0) without storey references.\n\n" +
                "This wizard will:\n" +
                "1. Create a DStorey entry for this building\n" +
                "2. Create a negative dstyles entry pointing to it\n" +
                "3. Update the facets' FadeLevel to use this dstyles entry\n\n" +
                "Continue?",
                "Auto-Fix Indoor Building",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Get storey height from user
            var heightInput = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the storey height (Y position in coarse units):\n" +
                "For a roof at world Y=768, use height = 24 (768/32)",
                "Storey Height",
                "24");

            if (!byte.TryParse(heightInput, out byte storeyHeight))
                return;

            var accessor = new DStoreyAccessor(MapDataService.Instance);

            // Step 1: Add DStorey
            var (storeySuccess, storeyIndex, storeyError) = accessor.AddDStorey(
                style: 0,  // Default style
                height: storeyHeight,
                flags: 0x05,  // Common flag for interior storeys
                building: (ushort)_currentBuildingId);

            if (!storeySuccess)
            {
                MessageBox.Show($"Failed to add DStorey: {storeyError}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Step 2: Add negative dstyles entry
            var (dstyleSuccess, dstyleIndex, dstyleError) = accessor.AddNegativeDStyle(storeyIndex);

            if (!dstyleSuccess)
            {
                MessageBox.Show($"Failed to add dstyles entry: {dstyleError}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Step 3: Update facets
            int updatedCount = 0;
            foreach (var facet in facetsNeedingFix)
            {
                if (accessor.UpdateFacetFadeLevel(facet.FacetId, (ushort)dstyleIndex))
                {
                    updatedCount++;
                }
            }

            MessageBox.Show(
                $"Auto-fix complete:\n\n" +
                $"• Created DStorey #{storeyIndex} (Height={storeyHeight})\n" +
                $"• Created dstyles[{dstyleIndex}] = -{storeyIndex}\n" +
                $"• Updated {updatedCount} facet FadeLevel values\n\n" +
                "The building should now render interior textures correctly.",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            LoadBuilding(_currentBuildingId);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    #region Helper Classes

    public class FacetChainEntry
    {
        public int FacetId { get; set; }
        public byte Height { get; set; }
        public byte FHeight { get; set; }
        public ushort FadeLevel { get; set; }
        public short DStyleValue { get; set; }
        public int StoreyIndex { get; set; }
        public ushort StoreyStyle { get; set; }
        public byte StoreyHeight { get; set; }
        public string Status { get; set; }
    }

    public class WalkableEntry
    {
        public int Id { get; set; }
        public byte X1 { get; set; }
        public byte Z1 { get; set; }
        public byte X2 { get; set; }
        public byte Z2 { get; set; }
        public byte Y { get; set; }
        public byte StoreyY { get; set; }
        public ushort StartFace4 { get; set; }
        public ushort EndFace4 { get; set; }
        public ushort Next { get; set; }

        public string BoundsDisplay => $"({X1},{Z1}) → ({X2},{Z2})";
        public string RF4RangeDisplay => $"[{StartFace4}..{EndFace4})";
        public int RF4Count => EndFace4 - StartFace4;
    }

    #endregion
}