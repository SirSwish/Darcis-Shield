// MainWindow.Diagnostics.cs
// Partial class containing diagnostic menu handlers
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Views.Roofs.Dialogs;
using UrbanChaosMapEditor.Views.Buildings.Dialogs;
using UrbanChaosMapEditor.Services.Roofs;

using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Core
{
    public partial class MainWindow
    {
        private void DiagnoseWalkables_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var report = WalkableDiagnostics.GenerateReport(MapDataService.Instance);
                var path = Path.Combine(Path.GetTempPath(), "walkable_diagnostics.txt");
                File.WriteAllText(path, report);
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating walkables report: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DumpBuildingBytes_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DataContext is not MainWindowViewModel mainVm || mainVm.Map == null)
            {
                MessageBox.Show("Cannot access map view model.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int buildingId = mainVm.Map.SelectedBuildingId;
            if (buildingId <= 0)
            {
                MessageBox.Show("No building selected.\n\nSelect a building in the Buildings tab first.",
                    "Dump Building Bytes", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var report = GenerateBuildingDump(buildingId);
                var path = Path.Combine(Path.GetTempPath(), $"building_{buildingId}_dump.txt");
                File.WriteAllText(path, report);
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error dumping building bytes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DumpFacetBytes_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DataContext is not MainWindowViewModel mainVm || mainVm.Map == null)
            {
                MessageBox.Show("Cannot access map view model.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int? facetId = mainVm.Map.SelectedFacetId;
            if (!facetId.HasValue || facetId.Value <= 0)
            {
                MessageBox.Show("No facet selected.\n\nSelect a facet in the Buildings tab first.",
                    "Dump Facet Bytes", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var report = GenerateFacetDump(facetId.Value);
                var path = Path.Combine(Path.GetTempPath(), $"facet_{facetId.Value}_dump.txt");
                File.WriteAllText(path, report);
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error dumping facet bytes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DumpBuildingWalkables_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DataContext is not MainWindowViewModel mainVm || mainVm.Map == null)
            {
                MessageBox.Show("Cannot access map view model.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int buildingId = mainVm.Map.SelectedBuildingId;
            if (buildingId <= 0)
            {
                MessageBox.Show("No building selected.\n\nSelect a building in the Buildings tab first.",
                    "Dump Building Walkables", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var report = GenerateBuildingWalkablesDump(buildingId);
                var path = Path.Combine(Path.GetTempPath(), $"building_{buildingId}_walkables_dump.txt");
                File.WriteAllText(path, report);
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error dumping building walkables: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateBuildingWalkablesDump(int buildingId1)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine($"  BUILDING #{buildingId1} WALKABLES + ROOFFACE4 DUMP");
            sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine();

            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
            {
                sb.AppendLine("ERROR: Building not found.");
                return sb.ToString();
            }

            var bld = snap.Buildings[buildingId1 - 1];

            sb.AppendLine($"BUILDING INFO:");
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine($"  Type: {bld.Type} ({bld.BuildingType})");
            sb.AppendLine($"  Walkable pointer: {bld.Walkable}");
            sb.AppendLine($"  Facet range: [{bld.StartFacet}..{bld.EndFacet})");
            sb.AppendLine();

            // Find all walkables for this building
            sb.AppendLine($"WALKABLES FOR THIS BUILDING:");
            sb.AppendLine($"---------------------------------------------------------------");

            int walkableCount = 0;
            int totalRf4 = 0;

            if (snap.Walkables != null && snap.Walkables.Length > 1)
            {
                for (int wIdx = 1; wIdx < snap.Walkables.Length; wIdx++)
                {
                    var w = snap.Walkables[wIdx];
                    if (w.Building != buildingId1)
                        continue;

                    walkableCount++;
                    sb.AppendLine($"  WALKABLE #{wIdx}:");
                    sb.AppendLine($"    Bounds: ({w.X1},{w.Z1}) to ({w.X2},{w.Z2}) = {w.X2 - w.X1}x{w.Z2 - w.Z1} tiles");
                    sb.AppendLine($"    Y: {w.Y} (world altitude: {w.Y * 32})");
                    sb.AppendLine($"    StoreyY: {w.StoreyY}");
                    sb.AppendLine($"    Next: {w.Next}");
                    sb.AppendLine($"    StartPoint: {w.StartPoint}, EndPoint: {w.EndPoint}");
                    sb.AppendLine($"    Face3 range: [{w.StartFace3}..{w.EndFace3}) = {w.EndFace3 - w.StartFace3} entries");
                    sb.AppendLine($"    Face4 range: [{w.StartFace4}..{w.EndFace4}) = {w.EndFace4 - w.StartFace4} RoofFace4 entries");

                    // Dump all RoofFace4 in this walkable's range
                    if (w.EndFace4 > w.StartFace4 && snap.RoofFaces4 != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"    ROOFFACE4 ENTRIES:");
                        for (int rfIdx = w.StartFace4; rfIdx < w.EndFace4 && rfIdx < snap.RoofFaces4.Length; rfIdx++)
                        {
                            var rf = snap.RoofFaces4[rfIdx];
                            int tileX = rf.RX;
                            int tileZ = rf.RZ - 128;

                            sb.AppendLine($"      RF4#{rfIdx}: Tile({tileX},{tileZ}) Y={rf.Y} DY=({rf.DY0},{rf.DY1},{rf.DY2}) DrawFlags=0x{rf.DrawFlags:X2} Next={rf.Next}");

                            // Check DrawFlags
                            bool isWalkable = (rf.DrawFlags & 0x08) != 0;
                            if (!isWalkable)
                            {
                                sb.AppendLine($"               ?? DrawFlags missing bit 0x08 (walkable flag)");
                            }
                            totalRf4++;
                        }
                    }
                    else if (w.StartFace4 == w.EndFace4)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"    ?? NO ROOFFACE4 ENTRIES (Face4 range is empty)");
                    }
                    sb.AppendLine();
                }
            }

            if (walkableCount == 0)
            {
                sb.AppendLine("  ?? NO WALKABLES FOUND FOR THIS BUILDING!");
                sb.AppendLine();
                sb.AppendLine("  For Warehouse buildings, you need walkables to define:");
                sb.AppendLine("    1. The roof surface where the player can walk");
                sb.AppendLine("    2. RoofFace4 tiles that render the roof");
                sb.AppendLine();
            }

            sb.AppendLine($"SUMMARY:");
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine($"  Building Type: {bld.BuildingType} (1=Warehouse has interior)");
            sb.AppendLine($"  Total Walkables: {walkableCount}");
            sb.AppendLine($"  Total RoofFace4 entries: {totalRf4}");
            sb.AppendLine();

            if (bld.Type == 1 && walkableCount == 0)
            {
                sb.AppendLine("  ?? PROBLEM: This is a Warehouse building but has no walkables!");
                sb.AppendLine("     The roof won't render without walkables + RoofFace4 entries.");
                sb.AppendLine();
            }
            else if (bld.Type == 1 && totalRf4 == 0)
            {
                sb.AppendLine("  ?? PROBLEM: This is a Warehouse with walkables but no RoofFace4!");
                sb.AppendLine("     The walkables exist but have no roof tiles linked (StartFace4 == EndFace4).");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GenerateBuildingDump(int buildingId1)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine($"  BUILDING #{buildingId1} RAW BYTE DUMP");
            sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine();

            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
            {
                sb.AppendLine("ERROR: Building not found.");
                return sb.ToString();
            }

            // Get raw bytes
            if (!acc.TryGetBuildingBytes(buildingId1, out var raw24, out var buildingOffset))
            {
                sb.AppendLine("ERROR: Could not read building bytes.");
                return sb.ToString();
            }

            var bld = snap.Buildings[buildingId1 - 1];

            sb.AppendLine($"FILE OFFSET: 0x{buildingOffset:X8}");
            sb.AppendLine();

            // Raw hex dump
            sb.AppendLine("RAW BYTES (24 bytes):");
            sb.AppendLine("---------------------------------------------------------------");
            sb.Append("  ");
            for (int i = 0; i < raw24.Length; i++)
            {
                sb.Append($"{raw24[i]:X2} ");
                if ((i + 1) % 8 == 0) sb.Append(" ");
            }
            sb.AppendLine();
            sb.AppendLine();

            // Parsed fields
            sb.AppendLine("PARSED FIELDS:");
            sb.AppendLine("---------------------------------------------------------------");
            sb.AppendLine($"  +00-01  StartFacet     : {bld.StartFacet} (0x{bld.StartFacet:X4})");
            sb.AppendLine($"  +02-03  EndFacet       : {bld.EndFacet} (0x{bld.EndFacet:X4})");
            sb.AppendLine($"  +04-07  WorldX         : {bld.WorldX} (0x{bld.WorldX:X8})");
            sb.AppendLine($"  +08-0A  WorldY (24bit) : {bld.WorldY} (0x{bld.WorldY & 0xFFFFFF:X6})");
            sb.AppendLine($"  +0B     Type           : {bld.Type} (0x{bld.Type:X2})");
            sb.AppendLine($"  +0C-0F  WorldZ         : {bld.WorldZ} (0x{bld.WorldZ:X8})");
            sb.AppendLine($"  +10-11  Walkable       : {bld.Walkable} (0x{bld.Walkable:X4})");
            sb.AppendLine($"  +12     Counter0       : {bld.Counter0} (0x{bld.Counter0:X2})");
            sb.AppendLine($"  +13     Counter1       : {bld.Counter1} (0x{bld.Counter1:X2})");
            sb.AppendLine($"  +16     Ware           : {bld.Ware} (0x{bld.Ware:X2})");
            sb.AppendLine();

            // List all facets for this building
            sb.AppendLine("FACETS IN THIS BUILDING:");
            sb.AppendLine("---------------------------------------------------------------");
            int facetCount = 0;
            for (int facetId1 = bld.StartFacet; facetId1 < bld.EndFacet && facetId1 <= snap.Facets.Length; facetId1++)
            {
                var f = snap.Facets[facetId1 - 1];
                sb.AppendLine($"  Facet #{facetId1}: Type={f.Type,-12} ({f.X0},{f.Z0})->({f.X1},{f.Z1}) H={f.Height} FH={f.FHeight} Flags=0x{(ushort)f.Flags:X4}");
                facetCount++;
            }
            sb.AppendLine($"  Total: {facetCount} facets");
            sb.AppendLine();

            return sb.ToString();
        }

        private string GenerateFacetDump(int facetId1)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine($"  FACET #{facetId1} RAW BYTE DUMP");
            sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine();

            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();

            if (snap.Facets == null || facetId1 < 1 || facetId1 > snap.Facets.Length)
            {
                sb.AppendLine("ERROR: Facet not found.");
                return sb.ToString();
            }

            // Get raw bytes
            if (!acc.TryGetFacetOffset(facetId1, out var facetOffset))
            {
                sb.AppendLine("ERROR: Could not get facet offset.");
                return sb.ToString();
            }

            var bytes = MapDataService.Instance.GetBytesCopy();
            var raw26 = new byte[26];
            Buffer.BlockCopy(bytes, facetOffset, raw26, 0, 26);

            var f = snap.Facets[facetId1 - 1];

            sb.AppendLine($"FILE OFFSET: 0x{facetOffset:X8}");
            sb.AppendLine();

            // Raw hex dump
            sb.AppendLine("RAW BYTES (26 bytes):");
            sb.AppendLine("---------------------------------------------------------------");
            sb.Append("  ");
            for (int i = 0; i < raw26.Length; i++)
            {
                sb.Append($"{raw26[i]:X2} ");
                if ((i + 1) % 8 == 0) sb.Append(" ");
            }
            sb.AppendLine();
            sb.AppendLine();

            // Parsed fields with offsets
            sb.AppendLine("PARSED FIELDS (DFacet structure - 26 bytes):");
            sb.AppendLine("---------------------------------------------------------------");
            sb.AppendLine($"  +00     Type           : {(byte)f.Type} ({f.Type})");
            sb.AppendLine($"  +01     Height         : {f.Height} (coarse height in 64-unit bands)");
            sb.AppendLine($"  +02     X0             : {f.X0}");
            sb.AppendLine($"  +03     X1             : {f.X1}");
            sb.AppendLine($"  +04-05  Y0             : {f.Y0} (0x{(ushort)f.Y0:X4})");
            sb.AppendLine($"  +06-07  Y1             : {f.Y1} (0x{(ushort)f.Y1:X4})");
            sb.AppendLine($"  +08     Z0             : {f.Z0}");
            sb.AppendLine($"  +09     Z1             : {f.Z1}");
            sb.AppendLine($"  +0A-0B  Flags          : 0x{(ushort)f.Flags:X4} ({f.Flags})");
            sb.AppendLine($"  +0C-0D  StyleIndex     : {f.StyleIndex} (0x{f.StyleIndex:X4})");
            sb.AppendLine($"  +0E-0F  Building       : {f.Building} (0x{f.Building:X4})");
            sb.AppendLine($"  +10-11  Storey         : {f.Storey} (0x{f.Storey:X4})");
            sb.AppendLine($"  +12     FHeight        : {f.FHeight} (fine height adjustment)");
            sb.AppendLine($"  +13     BlockHeight    : {f.BlockHeight}");
            sb.AppendLine($"  +14     Open           : {f.Open}");
            sb.AppendLine($"  +15     Dfcache        : {f.Dfcache}");
            sb.AppendLine($"  +16     Shake          : {f.Shake}");
            sb.AppendLine($"  +17     CutHole        : {f.CutHole}");
            sb.AppendLine($"  +18     Counter0       : {f.Counter0}");
            sb.AppendLine($"  +19     Counter1       : {f.Counter1}");
            sb.AppendLine();

            // Flag breakdown
            sb.AppendLine("FLAG BREAKDOWN:");
            sb.AppendLine("---------------------------------------------------------------");
            var flags = (ushort)f.Flags;
            sb.AppendLine($"  Bit 0  (0x0001) Open         : {((flags & 0x0001) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 1  (0x0002) Unclimbable  : {((flags & 0x0002) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 2  (0x0004) TwoSided     : {((flags & 0x0004) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 3  (0x0008) Barbed       : {((flags & 0x0008) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 4  (0x0010) Electrified  : {((flags & 0x0010) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 5  (0x0020) OnBuilding   : {((flags & 0x0020) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 6  (0x0040) ?            : {((flags & 0x0040) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 7  (0x0080) ?            : {((flags & 0x0080) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 8  (0x0100) ?            : {((flags & 0x0100) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 9  (0x0200) ?            : {((flags & 0x0200) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 10 (0x0400) ?            : {((flags & 0x0400) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 11 (0x0800) ?            : {((flags & 0x0800) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 12 (0x1000) ?            : {((flags & 0x1000) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 13 (0x2000) ?            : {((flags & 0x2000) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 14 (0x4000) ?            : {((flags & 0x4000) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 15 (0x8000) ?            : {((flags & 0x8000) != 0 ? "YES" : "no")}");
            sb.AppendLine();

            // Find which building owns this facet
            sb.AppendLine("OWNERSHIP:");
            sb.AppendLine("---------------------------------------------------------------");
            int ownerBuildingId = 0;
            for (int i = 0; i < snap.Buildings.Length; i++)
            {
                var b = snap.Buildings[i];
                if (facetId1 >= b.StartFacet && facetId1 < b.EndFacet)
                {
                    ownerBuildingId = i + 1;
                    break;
                }
            }
            if (ownerBuildingId > 0)
            {
                var owner = snap.Buildings[ownerBuildingId - 1];
                sb.AppendLine($"  Owner Building: #{ownerBuildingId}");
                sb.AppendLine($"  Building Facet Range: [{owner.StartFacet}..{owner.EndFacet})");
                sb.AppendLine($"  Building World Pos: ({owner.WorldX}, {owner.WorldY}, {owner.WorldZ})");
            }
            else
            {
                sb.AppendLine($"  Owner Building: ORPHAN (not in any building's range!)");
            }
            sb.AppendLine();

            // dstyles lookup if applicable
            if (f.StyleIndex > 0 && snap.Styles != null && f.StyleIndex < snap.Styles.Length)
            {
                short styleVal = snap.Styles[f.StyleIndex];
                sb.AppendLine("STYLE LOOKUP:");
                sb.AppendLine("---------------------------------------------------------------");
                sb.AppendLine($"  dstyles[{f.StyleIndex}] = {styleVal}");
                if (styleVal >= 0)
                    sb.AppendLine($"    -> Raw TMA texture index: {styleVal}");
                else
                    sb.AppendLine($"    -> Negative = storey reference: {-styleVal}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void DumpPapHiCell_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Prompt for coordinates
            var inputDialog = new PapHiInputDialog();
            inputDialog.Owner = this;
            if (inputDialog.ShowDialog() != true)
                return;

            int tileX = inputDialog.TileX;
            int tileZ = inputDialog.TileZ;

            if (tileX < 0 || tileX > 127 || tileZ < 0 || tileZ > 127)
            {
                MessageBox.Show("Tile coordinates must be 0-127.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var report = GeneratePapHiDump(tileX, tileZ);
                var path = Path.Combine(Path.GetTempPath(), $"paphi_{tileX}_{tileZ}_dump.txt");
                File.WriteAllText(path, report);
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error dumping PAP_HI cell: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditWalkablePoints_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Edit Walkable Points", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show input dialog to get walkable ID
            var inputDlg = new WalkableIdInputDialog { Owner = this };
            if (inputDlg.ShowDialog() != true)
                return;

            int walkableId1 = inputDlg.WalkableId;

            // Verify the walkable exists
            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();
            if (snap.Walkables == null || walkableId1 < 1 || walkableId1 >= snap.Walkables.Length)
            {
                MessageBox.Show($"Walkable #{walkableId1} not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var w = snap.Walkables[walkableId1];

            // Show the edit dialog
            // Note: You need to create the EditWalkablePointsDialog or use the inline editor below
            try
            {
                var dialog = new EditWalkablePointsDialog(walkableId1, w.Building)
                {
                    Owner = this
                };
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                // Fallback: inline editing via MessageBox prompts
                InlineEditWalkablePoints(walkableId1, w);
            }
        }

        private void InlineEditWalkablePoints(int walkableId1, DWalkableRec w)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Walkable #{walkableId1} in Building #{w.Building}");
            sb.AppendLine();
            sb.AppendLine($"Current values:");
            sb.AppendLine($"  StartPoint: {w.StartPoint}");
            sb.AppendLine($"  EndPoint: {w.EndPoint}");
            sb.AppendLine($"  Point count: {w.EndPoint - w.StartPoint}");
            sb.AppendLine();
            sb.AppendLine("Enter new values (comma-separated): StartPoint,EndPoint");
            sb.AppendLine("Example: 0,0 to zero both values");
            sb.AppendLine("Or: 40667,40735 to copy a known working range");

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                sb.ToString(),
                "Edit Walkable Points",
                $"{w.StartPoint},{w.EndPoint}");

            if (string.IsNullOrWhiteSpace(input))
                return;

            var parts = input.Split(',');
            if (parts.Length != 2 ||
                !ushort.TryParse(parts[0].Trim(), out ushort newStart) ||
                !ushort.TryParse(parts[1].Trim(), out ushort newEnd))
            {
                MessageBox.Show("Invalid format. Please enter two comma-separated numbers.",
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirm
            if (MessageBox.Show(
                $"Set Walkable #{walkableId1} point range?\n\n" +
                $"StartPoint: {w.StartPoint} ? {newStart}\n" +
                $"EndPoint: {w.EndPoint} ? {newEnd}\n\n" +
                "Save the map and test in-game to see the effect.",
                "Confirm Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            // Apply using WalkableEditor
            var editor = new WalkableEditor(MapDataService.Instance);
            if (editor.TrySetPointRange(walkableId1, newStart, newEnd))
            {
                MessageBox.Show(
                    $"Changes applied successfully.\n\n" +
                    $"Walkable #{walkableId1}:\n" +
                    $"StartPoint = {newStart}\n" +
                    $"EndPoint = {newEnd}\n\n" +
                    "Save the map and test in-game.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Failed to write changes.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DumpIndoorData_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Dump Indoor Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var report = GenerateIndoorDataDump();
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "indoor_data_dump.txt");
                System.IO.File.WriteAllText(path, report);
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Dump Indoor Data",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateIndoorDataDump()
        {
            var sb = new System.Text.StringBuilder();
            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();

            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("  INDOOR DATA DUMP — All InsideStorey, Staircase, and InsideBlock");
            sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Map: {MapDataService.Instance.CurrentPath}");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            // ---- Header counters ----
            sb.AppendLine("HEADER COUNTERS:");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine($"  NextInsideStorey : {snap.NextInsideStorey}");
            sb.AppendLine($"  NextInsideStair  : {snap.NextInsideStair}");
            sb.AppendLine($"  NextInsideBlock  : {snap.NextInsideBlock}");
            sb.AppendLine($"  IndoorsStart     : 0x{snap.IndoorsStart:X8}");
            sb.AppendLine();

            if (snap.NextInsideStorey == 0 && snap.NextInsideStair == 0 && snap.NextInsideBlock == 0)
            {
                sb.AppendLine("*** NO INDOOR DATA IN THIS MAP ***");
                sb.AppendLine();
                sb.AppendLine("This map has no InsideStorey/Staircase/InsideBlock entries.");
                sb.AppendLine("Type=1 (Warehouse/Indoor) buildings require this data for");
                sb.AppendLine("interior floor/ceiling rendering and door transitions.");
                return sb.ToString();
            }

            // ---- InsideStorey entries ----
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  INSIDESTOREY ENTRIES ({snap.InsideStoreys.Length} total)");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("C struct InsideStorey (22 bytes):");
            sb.AppendLine("  +0:  MinX (U8)        +1:  MinZ (U8)");
            sb.AppendLine("  +2:  MaxX (U8)        +3:  MaxZ (U8)");
            sb.AppendLine("  +4:  InsideBlock (U16) — index into inside_block[] byte array");
            sb.AppendLine("  +6:  StairCaseHead (U16) — linked list head into Staircase[]");
            sb.AppendLine("  +8:  TexType (U16)    — floor texture type");
            sb.AppendLine("  +10: FacetStart (U16) — first DFacet belonging to this interior");
            sb.AppendLine("  +12: FacetEnd (U16)   — last+1 DFacet");
            sb.AppendLine("  +14: StoreyY (S16)    — floor Y in world units (used directly by renderer)");
            sb.AppendLine("  +16: Building (U16)   — owning building ID");
            sb.AppendLine("  +18: Dummy0 (U16)     +20: Dummy1 (U16)");
            sb.AppendLine();

            for (int i = 0; i < snap.InsideStoreys.Length; i++)
            {
                var ist = snap.InsideStoreys[i];

                // Check if it's an empty/sentinel entry
                bool isEmpty = ist.MinX == 0 && ist.MinZ == 0 && ist.MaxX == 0 && ist.MaxZ == 0
                               && ist.StoreyY == 0 && ist.Building == 0;

                sb.AppendLine($"  InsideStorey #{i}{(isEmpty ? " (EMPTY/SENTINEL)" : "")}:");
                sb.AppendLine($"    Bounds     : ({ist.MinX},{ist.MinZ}) → ({ist.MaxX},{ist.MaxZ})  [{ist.MaxX - ist.MinX}×{ist.MaxZ - ist.MinZ} tiles]");
                sb.AppendLine($"    InsideBlock: {ist.InsideBlock}");
                sb.AppendLine($"    StairHead  : {ist.StairCaseHead}");
                sb.AppendLine($"    TexType    : {ist.TexType}");
                sb.AppendLine($"    Facets     : [{ist.FacetStart}..{ist.FacetEnd}) = {ist.FacetEnd - ist.FacetStart} facets");
                sb.AppendLine($"    StoreyY    : {ist.StoreyY}  (world = {ist.StoreyY}, roof = {ist.StoreyY + 256})");
                sb.AppendLine($"    Building   : {ist.Building}");
                sb.AppendLine($"    Dummy0/1   : {ist.Dummy0}, {ist.Dummy1}");

                // Raw bytes
                if (snap.InsideStoreysRaw.Length >= (i + 1) * 22)
                {
                    int off = i * 22;
                    sb.Append("    Raw bytes  : ");
                    for (int b = 0; b < 22; b++)
                    {
                        sb.Append($"{snap.InsideStoreysRaw[off + b]:X2} ");
                        if (b == 3 || b == 9 || b == 13 || b == 17) sb.Append(" ");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            // ---- Staircase entries ----
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  STAIRCASE ENTRIES ({snap.InsideStairs.Length} total)");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("C struct Staircase (10 bytes):");
            sb.AppendLine("  +0: X (U8)           +1: Z (U8)");
            sb.AppendLine("  +2: Flags (U8)       +3: Id (U8)");
            sb.AppendLine("  +4: NextStairs (S16) — linked list next pointer");
            sb.AppendLine("  +6: DownInside (S16) — InsideStorey index for floor below");
            sb.AppendLine("  +8: UpInside (S16)   — InsideStorey index for floor above");
            sb.AppendLine();

            if (snap.InsideStairs.Length == 0)
            {
                sb.AppendLine("  (no staircase entries)");
                sb.AppendLine();
            }
            else
            {
                for (int i = 0; i < snap.InsideStairs.Length; i++)
                {
                    var stair = snap.InsideStairs[i];
                    bool isEmpty = stair.X == 0 && stair.Z == 0 && stair.NextStairs == 0;

                    sb.AppendLine($"  Staircase #{i}{(isEmpty ? " (EMPTY/SENTINEL)" : "")}:");
                    sb.AppendLine($"    Position   : ({stair.X},{stair.Z})");
                    sb.AppendLine($"    Flags      : 0x{stair.Flags:X2}");
                    sb.AppendLine($"    Id         : {stair.Id}");
                    sb.AppendLine($"    NextStairs : {stair.NextStairs}");
                    sb.AppendLine($"    DownInside : {stair.DownInside}");
                    sb.AppendLine($"    UpInside   : {stair.UpInside}");

                    // Raw bytes
                    if (snap.InsideStairsRaw.Length >= (i + 1) * 10)
                    {
                        int off = i * 10;
                        sb.Append("    Raw bytes  : ");
                        for (int b = 0; b < 10; b++)
                            sb.Append($"{snap.InsideStairsRaw[off + b]:X2} ");
                        sb.AppendLine();
                    }
                    sb.AppendLine();
                }
            }

            // ---- InsideBlock data ----
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  INSIDE BLOCK DATA ({snap.InsideBlock.Length} bytes)");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("Each byte encodes room info for one tile within an InsideStorey:");
            sb.AppendLine("  Bits 0-3: Room ID (0 = no room, 1-15 = room number)");
            sb.AppendLine("  Bits 4-5: Direction");
            sb.AppendLine("  Bits 6-7: Type (0x40 = door?, 0x80 = ?)");
            sb.AppendLine();

            if (snap.InsideBlock.Length == 0)
            {
                sb.AppendLine("  (no inside block data)");
                sb.AppendLine();
            }
            else
            {
                // Show which InsideStorey entries reference which block ranges
                sb.AppendLine("  BLOCK RANGES PER INSIDESTOREY:");
                for (int i = 0; i < snap.InsideStoreys.Length; i++)
                {
                    var ist = snap.InsideStoreys[i];
                    if (ist.MinX == 0 && ist.MaxX == 0) continue;

                    int width = ist.MaxX - ist.MinX;
                    int depth = ist.MaxZ - ist.MinZ;
                    int blockSize = width * depth;
                    int blockStart = ist.InsideBlock;
                    int blockEnd = blockStart + blockSize;

                    sb.AppendLine($"    InsideStorey #{i}: InsideBlock[{blockStart}..{blockEnd}) = {blockSize} bytes ({width}×{depth} tiles)");

                    // Dump as grid
                    if (blockEnd <= snap.InsideBlock.Length && blockSize > 0 && blockSize <= 4096)
                    {
                        sb.AppendLine($"      Grid ({width} cols × {depth} rows), each cell = room byte:");
                        for (int dz = 0; dz < depth; dz++)
                        {
                            sb.Append("        ");
                            for (int dx = 0; dx < width; dx++)
                            {
                                byte val = snap.InsideBlock[blockStart + dx + dz * width];
                                int roomId = val & 0x0F;
                                int direction = (val >> 4) & 0x03;
                                int type = (val >> 6) & 0x03;

                                if (val == 0)
                                    sb.Append(" ·· ");
                                else
                                    sb.Append($" {val:X2} ");
                            }
                            sb.AppendLine();
                        }
                        sb.AppendLine();

                        // Interpret non-zero entries
                        bool anyNonZero = false;
                        for (int idx = blockStart; idx < blockEnd && idx < snap.InsideBlock.Length; idx++)
                        {
                            byte val = snap.InsideBlock[idx];
                            if (val != 0)
                            {
                                if (!anyNonZero)
                                {
                                    sb.AppendLine("      Non-zero entries:");
                                    anyNonZero = true;
                                }
                                int relIdx = idx - blockStart;
                                int dx = relIdx % width;
                                int dz = relIdx / width;
                                int roomId = val & 0x0F;
                                int direction = (val >> 4) & 0x03;
                                int type = (val >> 6) & 0x03;
                                sb.AppendLine($"        [{idx}] tile({ist.MinX + dx},{ist.MinZ + dz}) = 0x{val:X2}: room={roomId} dir={direction} type={type}");
                            }
                        }
                        if (!anyNonZero)
                            sb.AppendLine("      (all zeros — empty interior)");
                        sb.AppendLine();
                    }
                }

                // Raw hex dump (first 256 bytes or all if smaller)
                int dumpLen = Math.Min(snap.InsideBlock.Length, 512);
                sb.AppendLine($"  RAW HEX (first {dumpLen} of {snap.InsideBlock.Length} bytes):");
                for (int row = 0; row < dumpLen; row += 16)
                {
                    sb.Append($"    {row:X4}: ");
                    for (int col = 0; col < 16 && row + col < dumpLen; col++)
                        sb.Append($"{snap.InsideBlock[row + col]:X2} ");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            // ---- Cross-reference: DFacets with non-zero DStorey ----
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("  FACETS WITH NON-ZERO DSTOREY (indoor door references)");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            int doorFacetCount = 0;
            if (snap.Facets != null)
            {
                for (int i = 0; i < snap.Facets.Length; i++)
                {
                    var f = snap.Facets[i];
                    if (f.Storey != 0)
                    {
                        doorFacetCount++;
                        sb.AppendLine($"  Facet #{i + 1}:");
                        sb.AppendLine($"    Type: {f.Type}  Building: {f.Building}  DStorey: {f.Storey}");
                        sb.AppendLine($"    Coords: ({f.X0},{f.Z0})→({f.X1},{f.Z1})  Y: {f.Y0},{f.Y1}");
                        sb.AppendLine($"    → Points to InsideStorey[{f.Storey}]");

                        // Cross-reference
                        if (f.Storey < snap.InsideStoreys.Length)
                        {
                            var ist = snap.InsideStoreys[f.Storey];
                            sb.AppendLine($"       InsideStorey #{f.Storey}: bounds ({ist.MinX},{ist.MinZ})→({ist.MaxX},{ist.MaxZ}) StoreyY={ist.StoreyY} Bld={ist.Building}");
                        }
                        else
                        {
                            sb.AppendLine($"       ⚠️ DStorey index {f.Storey} is OUT OF RANGE (max={snap.InsideStoreys.Length - 1})");
                        }
                        sb.AppendLine();
                    }
                }
            }

            if (doorFacetCount == 0)
                sb.AppendLine("  (no facets have DStorey set — no indoor door transitions)");

            sb.AppendLine();

            // ---- Summary ----
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("  SUMMARY");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  InsideStorey entries : {snap.InsideStoreys.Length}");
            sb.AppendLine($"  Staircase entries    : {snap.InsideStairs.Length}");
            sb.AppendLine($"  InsideBlock bytes    : {snap.InsideBlock.Length}");
            sb.AppendLine($"  Facets with DStorey  : {doorFacetCount}");
            sb.AppendLine();
            sb.AppendLine("  FILE LAYOUT (building region):");
            sb.AppendLine($"    Buildings start  : 0x{snap.StartOffset + 48:X8}");
            sb.AppendLine($"    Indoors start    : 0x{snap.IndoorsStart:X8}");
            sb.AppendLine($"    Walkables start  : 0x{snap.WalkablesStart:X8}");
            sb.AppendLine();

            return sb.ToString();
        }

        private byte[]? _buildingBlockSnapshot;
        private string? _snapshotLabel;

        private void SnapshotBuildingBlock_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Snapshot", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();
            var bytes = MapDataService.Instance.GetBytesCopy();

            if (_buildingBlockSnapshot == null)
            {
                // First click — capture BEFORE
                _buildingBlockSnapshot = bytes;
                _snapshotLabel = $"BEFORE — {DateTime.Now:HH:mm:ss}";
                MessageBox.Show(
                    $"BEFORE snapshot captured ({bytes.Length} bytes).\n\n" +
                    "Now perform the operation (e.g. delete a building),\n" +
                    "then click this again to generate the diff report.",
                    "Snapshot Captured", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Second click — capture AFTER and generate diff
            var beforeBytes = _buildingBlockSnapshot;
            var afterBytes = bytes;
            var beforeLabel = _snapshotLabel ?? "BEFORE";
            var afterLabel = $"AFTER — {DateTime.Now:HH:mm:ss}";

            // Clear for next use
            _buildingBlockSnapshot = null;
            _snapshotLabel = null;

            try
            {
                var report = GenerateBuildingBlockDiff(beforeBytes, afterBytes, beforeLabel, afterLabel);
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "building_block_diff.txt");
                System.IO.File.WriteAllText(path, report);
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Diff Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateBuildingBlockDiff(byte[] beforeBytes, byte[] afterBytes,
            string beforeLabel, string afterLabel)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("  BUILDING BLOCK BINARY DIFF");
            sb.AppendLine($"  {beforeLabel}  ({beforeBytes.Length} bytes)");
            sb.AppendLine($"  {afterLabel}  ({afterBytes.Length} bytes)");
            sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Map: {MapDataService.Instance.CurrentPath}");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            // Parse both snapshots
            var accBefore = new BuildingsAccessor(MapDataService.Instance);
            // We need to parse from raw bytes — use a helper
            var snapBefore = ParseSnapshotFromBytes(beforeBytes);
            var snapAfter = ParseSnapshotFromBytes(afterBytes);

            if (snapBefore == null || snapAfter == null)
            {
                sb.AppendLine("ERROR: Could not parse one or both snapshots.");
                return sb.ToString();
            }

            // ---- File size ----
            sb.AppendLine($"FILE SIZE: {beforeBytes.Length} → {afterBytes.Length} (delta: {afterBytes.Length - beforeBytes.Length})");
            sb.AppendLine();

            // ---- Header comparison ----
            sb.AppendLine("BUILDING BLOCK HEADER (at block start):");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            int bs = snapBefore.BlockStart;
            int as2 = snapAfter.BlockStart;
            DumpHeaderComparison(sb, beforeBytes, afterBytes, bs, as2);
            sb.AppendLine();

            // ---- Region offsets ----
            sb.AppendLine("REGION OFFSETS:");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine($"  {"Region",-25} {"BEFORE",-15} {"AFTER",-15} {"Delta",-10}");
            CompareOffset(sb, "Block Start", snapBefore.BlockStart, snapAfter.BlockStart);
            CompareOffset(sb, "Buildings", snapBefore.BuildingsOff, snapAfter.BuildingsOff);
            CompareOffset(sb, "Padding (14B)", snapBefore.PadOff, snapAfter.PadOff);
            CompareOffset(sb, "Facets", snapBefore.FacetsOff, snapAfter.FacetsOff);
            CompareOffset(sb, "DStyles", snapBefore.StylesOff, snapAfter.StylesOff);
            CompareOffset(sb, "PaintMem", snapBefore.PaintOff, snapAfter.PaintOff);
            CompareOffset(sb, "DStoreys", snapBefore.StoreysOff, snapAfter.StoreysOff);
            CompareOffset(sb, "Indoors", snapBefore.IndoorsOff, snapAfter.IndoorsOff);
            CompareOffset(sb, "Walkables", snapBefore.WalkablesOff, snapAfter.WalkablesOff);
            CompareOffset(sb, "Block End", snapBefore.BlockEnd, snapAfter.BlockEnd);
            sb.AppendLine();

            // ---- Padding bytes ----
            sb.AppendLine("PADDING BYTES (14 bytes between buildings and facets):");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.Append("  BEFORE: ");
            for (int i = 0; i < 14 && snapBefore.PadOff + i < beforeBytes.Length; i++)
                sb.Append($"{beforeBytes[snapBefore.PadOff + i]:X2} ");
            sb.AppendLine();
            sb.Append("  AFTER:  ");
            for (int i = 0; i < 14 && snapAfter.PadOff + i < afterBytes.Length; i++)
                sb.Append($"{afterBytes[snapAfter.PadOff + i]:X2} ");
            sb.AppendLine();
            bool padChanged = false;
            for (int i = 0; i < 14; i++)
            {
                if (snapBefore.PadOff + i < beforeBytes.Length && snapAfter.PadOff + i < afterBytes.Length)
                    if (beforeBytes[snapBefore.PadOff + i] != afterBytes[snapAfter.PadOff + i])
                        padChanged = true;
            }
            sb.AppendLine(padChanged ? "  ⚠️ PADDING CHANGED!" : "  ✓ Padding unchanged.");
            sb.AppendLine();

            // ---- Building #49 specific ----
            sb.AppendLine("BUILDING #49 RAW BYTES (24 bytes):");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            DumpBuildingRaw(sb, "BEFORE", beforeBytes, snapBefore.BuildingsOff, 49, snapBefore.NextBuilding);
            DumpBuildingRaw(sb, "AFTER", afterBytes, snapAfter.BuildingsOff, 49, snapAfter.NextBuilding);
            sb.AppendLine();

            // ---- Walkables for building 49 ----
            sb.AppendLine("WALKABLES HEADER:");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            if (snapBefore.WalkablesOff + 4 <= beforeBytes.Length)
            {
                ushort bNextW = ReadU16(beforeBytes, snapBefore.WalkablesOff);
                ushort bNextR = ReadU16(beforeBytes, snapBefore.WalkablesOff + 2);
                sb.AppendLine($"  BEFORE: nextWalkable={bNextW} nextRF4={bNextR}");
            }
            if (snapAfter.WalkablesOff + 4 <= afterBytes.Length)
            {
                ushort aNextW = ReadU16(afterBytes, snapAfter.WalkablesOff);
                ushort aNextR = ReadU16(afterBytes, snapAfter.WalkablesOff + 2);
                sb.AppendLine($"  AFTER:  nextWalkable={aNextW} nextRF4={aNextR}");
            }
            sb.AppendLine();

            // ---- Dump walkable #124 raw bytes (building 49's main walkable) ----
            sb.AppendLine("WALKABLE #124 RAW BYTES (22 bytes):");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            DumpWalkableRaw(sb, "BEFORE", beforeBytes, snapBefore.WalkablesOff, 124);
            DumpWalkableRaw(sb, "AFTER", afterBytes, snapAfter.WalkablesOff, 124);
            sb.AppendLine();

            // ---- Dump walkables 125-128 ----
            for (int wid = 125; wid <= 128; wid++)
            {
                sb.AppendLine($"WALKABLE #{wid} RAW BYTES:");
                DumpWalkableRaw(sb, "BEFORE", beforeBytes, snapBefore.WalkablesOff, wid);
                DumpWalkableRaw(sb, "AFTER", afterBytes, snapAfter.WalkablesOff, wid);
                sb.AppendLine();
            }

            // ---- Full byte-level diff of entire file ----
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("  BYTE-LEVEL CHANGES (first 500 differences)");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            int minLen = Math.Min(beforeBytes.Length, afterBytes.Length);
            int diffCount = 0;
            int maxDiffs = 500;

            // Group consecutive changes into ranges
            int rangeStart = -1;
            for (int i = 0; i <= minLen; i++)
            {
                bool isDiff = (i < minLen) && (beforeBytes[i] != afterBytes[i]);
                if (isDiff && rangeStart < 0)
                    rangeStart = i;
                else if (!isDiff && rangeStart >= 0)
                {
                    // End of a changed range
                    int rangeLen = i - rangeStart;
                    if (diffCount < maxDiffs)
                    {
                        string region = IdentifyRegion(rangeStart, snapBefore, snapAfter);
                        sb.AppendLine($"  CHANGED @ 0x{rangeStart:X6}..0x{i - 1:X6} ({rangeLen} bytes) [{region}]");

                        // Show up to 32 bytes
                        int showLen = Math.Min(rangeLen, 32);
                        sb.Append("    BEFORE: ");
                        for (int j = 0; j < showLen; j++)
                            sb.Append($"{beforeBytes[rangeStart + j]:X2} ");
                        if (rangeLen > 32) sb.Append("...");
                        sb.AppendLine();

                        sb.Append("    AFTER:  ");
                        for (int j = 0; j < showLen; j++)
                            sb.Append($"{afterBytes[rangeStart + j]:X2} ");
                        if (rangeLen > 32) sb.Append("...");
                        sb.AppendLine();
                        sb.AppendLine();
                    }
                    diffCount++;
                    rangeStart = -1;
                }
            }

            if (afterBytes.Length != beforeBytes.Length)
            {
                sb.AppendLine($"  FILE SIZE CHANGED: {beforeBytes.Length} → {afterBytes.Length}");
                sb.AppendLine($"  Tail difference: {afterBytes.Length - beforeBytes.Length} bytes");
            }

            sb.AppendLine($"  Total changed ranges: {diffCount}");

            return sb.ToString();
        }

        // ---- Helper types and methods for the diff ----

        private sealed class BlockOffsets
        {
            public int BlockStart;
            public int BuildingsOff;
            public int PadOff;
            public int FacetsOff;
            public int StylesOff;
            public int PaintOff;
            public int StoreysOff;
            public int IndoorsOff;
            public int WalkablesOff;
            public int BlockEnd;
            public ushort NextBuilding;
            public ushort NextFacet;
            public ushort NextStyle;
            public ushort NextPaintMem;
            public ushort NextStorey;
        }

        // ====================================================================
        // ADD TO MainWindow_Diagnostics.cs
        // Menu: <MenuItem Header="Dump All Buildings Raw..." Click="DumpAllBuildingsRaw_Click"/>
        // ====================================================================

        private void DumpAllBuildingsRaw_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Dump Buildings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var bytes = MapDataService.Instance.GetBytesCopy();
                int saveType = BitConverter.ToInt32(bytes, 0);
                int blockStart = 8 + 128 * 128 * 6;
                ushort nextBuilding = ReadU16(bytes, blockStart + 2);

                int buildingsOff = blockStart + 48;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("═══════════════════════════════════════════════════════════════════");
                sb.AppendLine("  ALL BUILDINGS — DUAL LAYOUT INTERPRETATION");
                sb.AppendLine($"  Map: {MapDataService.Instance.CurrentPath}");
                sb.AppendLine($"  NextDBuilding: {nextBuilding}  (buildings: {nextBuilding - 1})");
                sb.AppendLine("═══════════════════════════════════════════════════════════════════");
                sb.AppendLine();
                sb.AppendLine("LAYOUT A (our current interpretation):");
                sb.AppendLine("  +0: StartFacet(U16) +2: EndFacet(U16) +4: X(S32)");
                sb.AppendLine("  +8: Y(S24) +11: Type(U8) +12: Z(S32) +16: Walkable(U16)");
                sb.AppendLine("  +18: Counter[2] +20: Padding(U16) +22: Ware(U8) +23: unused");
                sb.AppendLine();
                sb.AppendLine("LAYOUT B (C struct order from supermap.h):");
                sb.AppendLine("  +0: X(S32) +4: Y(S32) +8: Z(S32) +12: StartFacet(U16)");
                sb.AppendLine("  +14: EndFacet(U16) +16: Walkable(U16) +18: Counter[2]");
                sb.AppendLine("  +20: Padding(U16) +22: Ware(U8) +23: Type(U8)");
                sb.AppendLine();

                // Also read walkables to cross-reference
                var acc = new BuildingsAccessor(MapDataService.Instance);
                var snap = acc.ReadSnapshot();

                for (int i = 0; i < nextBuilding - 1; i++)
                {
                    int off = buildingsOff + i * 24;
                    int bldId = i + 1;

                    sb.AppendLine($"───── BUILDING #{bldId} @ 0x{off:X6} ─────");
                    sb.Append("  Raw: ");
                    for (int b = 0; b < 24; b++) sb.Append($"{bytes[off + b]:X2} ");
                    sb.AppendLine();

                    // Layout A (our current)
                    ushort aStartFacet = ReadU16(bytes, off + 0);
                    ushort aEndFacet = ReadU16(bytes, off + 2);
                    int aX = BitConverter.ToInt32(bytes, off + 4);
                    byte aType = bytes[off + 11];
                    int aZ = BitConverter.ToInt32(bytes, off + 12);
                    ushort aWalkable = ReadU16(bytes, off + 16);
                    byte aWare = bytes[off + 22];

                    sb.AppendLine($"  [A] SF={aStartFacet} EF={aEndFacet} X={aX} Type={aType} Z={aZ} Walk={aWalkable} Ware={aWare}");

                    // Layout B (C struct)
                    int bX = BitConverter.ToInt32(bytes, off + 0);
                    int bY = BitConverter.ToInt32(bytes, off + 4);
                    int bZ = BitConverter.ToInt32(bytes, off + 8);
                    ushort bStartFacet = ReadU16(bytes, off + 12);
                    ushort bEndFacet = ReadU16(bytes, off + 14);
                    ushort bWalkable = ReadU16(bytes, off + 16);
                    byte bWare = bytes[off + 22];
                    byte bType = bytes[off + 23];

                    sb.AppendLine($"  [B] X={bX} Y={bY} Z={bZ} SF={bStartFacet} EF={bEndFacet} Walk={bWalkable} Ware={bWare} Type={bType}");

                    // Find walkables that reference this building
                    if (snap.Walkables != null)
                    {
                        int wCount = 0;
                        int wHead = 0;
                        for (int w = 1; w < snap.Walkables.Length; w++)
                        {
                            if (snap.Walkables[w].Building == bldId)
                            {
                                wCount++;
                                // Find chain head (the one with highest index in chain)
                                if (w > wHead) wHead = w;
                            }
                        }
                        if (wCount > 0)
                            sb.AppendLine($"  [W] {wCount} walkable(s) with Building={bldId}, highest index={wHead}");
                    }

                    sb.AppendLine();
                }

                // Summary: focus on warehouses
                sb.AppendLine("═══════════════════════════════════════════════════════════════════");
                sb.AppendLine("  WAREHOUSE BUILDINGS ONLY (Type=1 in Layout A, or Type=1 in Layout B)");
                sb.AppendLine("═══════════════════════════════════════════════════════════════════");
                sb.AppendLine();

                for (int i = 0; i < nextBuilding - 1; i++)
                {
                    int off = buildingsOff + i * 24;
                    byte aType = bytes[off + 11];
                    byte bType = bytes[off + 23];

                    if (aType != 1 && bType != 1) continue;

                    int bldId = i + 1;
                    sb.AppendLine($"  Building #{bldId}:");

                    ushort aWalkable = ReadU16(bytes, off + 16);
                    int aX = BitConverter.ToInt32(bytes, off + 4);

                    int bY = BitConverter.ToInt32(bytes, off + 4);
                    ushort bWalkable = ReadU16(bytes, off + 16);

                    sb.AppendLine($"    Layout A: Type={aType} Walkable@+16={aWalkable} X@+4={aX}");
                    sb.AppendLine($"    Layout B: Type={bType} Walkable@+16={bWalkable} Y@+4={bY}");

                    // Check: does any byte pair match a known walkable chain head?
                    sb.Append("    Possible walkable refs: ");
                    if (snap.Walkables != null)
                    {
                        for (int boff = 0; boff < 22; boff += 2)
                        {
                            ushort val = ReadU16(bytes, off + boff);
                            if (val > 0 && val < snap.Walkables.Length)
                            {
                                if (snap.Walkables[val].Building == bldId)
                                    sb.Append($"+{boff}={val}(✓bld match) ");
                            }
                        }
                    }
                    sb.AppendLine();
                    sb.AppendLine();
                }

                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "all_buildings_raw_dump.txt");
                System.IO.File.WriteAllText(path, sb.ToString());
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Dump Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static ushort ReadU16(byte[] b, int off)
            => (ushort)(b[off] | (b[off + 1] << 8));

        private BlockOffsets? ParseSnapshotFromBytes(byte[] bytes)
        {
            try
            {
                int saveType = BitConverter.ToInt32(bytes, 0);

                // PAP_HI = 8 + 128*128*6 = 98312, building block starts after
                int blockStart = 8 + 128 * 128 * 6;

                ushort nextBuilding = ReadU16(bytes, blockStart + 2);
                ushort nextFacet = ReadU16(bytes, blockStart + 4);
                ushort nextStyle = ReadU16(bytes, blockStart + 6);
                ushort nextPaintMem = (saveType >= 17) ? ReadU16(bytes, blockStart + 8) : (ushort)0;
                ushort nextStorey = (saveType >= 17) ? ReadU16(bytes, blockStart + 10) : (ushort)0;

                int buildingsOff = blockStart + 48; // HeaderSize
                int padOff = buildingsOff + (nextBuilding - 1) * 24; // DBuildingSize
                int facetsOff = padOff + 14; // AfterBuildingsPad
                int stylesOff = facetsOff + (nextFacet - 1) * 26; // DFacetSize
                int paintOff = stylesOff + nextStyle * 2;
                int storeysOff = paintOff + ((saveType >= 17) ? nextPaintMem : 0);
                int indoorsOff = storeysOff + ((saveType >= 17) ? nextStorey * 6 : 0);

                int indoorsLen = 0;
                if (saveType >= 21 && indoorsOff + 8 <= bytes.Length)
                {
                    ushort nextIS = ReadU16(bytes, indoorsOff);
                    ushort nextISt = ReadU16(bytes, indoorsOff + 2);
                    ushort nextIB = ReadU16(bytes, indoorsOff + 4);
                    indoorsLen = 8 + nextIS * 22 + nextISt * 10 + nextIB;
                }

                int walkablesOff = indoorsOff + indoorsLen;
                ushort nextWalkable = ReadU16(bytes, walkablesOff);
                ushort nextRF4 = ReadU16(bytes, walkablesOff + 2);
                int blockEnd = walkablesOff + 4 + nextWalkable * 22 + nextRF4 * 10;

                return new BlockOffsets
                {
                    BlockStart = blockStart,
                    BuildingsOff = buildingsOff,
                    PadOff = padOff,
                    FacetsOff = facetsOff,
                    StylesOff = stylesOff,
                    PaintOff = paintOff,
                    StoreysOff = storeysOff,
                    IndoorsOff = indoorsOff,
                    WalkablesOff = walkablesOff,
                    BlockEnd = blockEnd,
                    NextBuilding = nextBuilding,
                    NextFacet = nextFacet,
                    NextStyle = nextStyle,
                    NextPaintMem = nextPaintMem,
                    NextStorey = nextStorey,
                };
            }
            catch
            {
                return null;
            }
        }

        private void DumpHeaderComparison(System.Text.StringBuilder sb, byte[] before, byte[] after, int bOff, int aOff)
        {
            string[] fields = { "SaveType(4B)", "NextDBuilding", "NextDFacet", "NextDStyle", "NextPaintMem", "NextDStorey" };
            int[] offsets = { 0, 2, 4, 6, 8, 10 };
            int[] sizes = { 4, 2, 2, 2, 2, 2 };

            for (int i = 0; i < fields.Length; i++)
            {
                int bVal, aVal;
                if (sizes[i] == 4)
                {
                    bVal = BitConverter.ToInt32(before, bOff + offsets[i]);
                    aVal = BitConverter.ToInt32(after, aOff + offsets[i]);
                }
                else
                {
                    bVal = ReadU16(before, bOff + offsets[i]);
                    aVal = ReadU16(after, aOff + offsets[i]);
                }
                string marker = bVal != aVal ? " ← CHANGED" : "";
                sb.AppendLine($"  {fields[i],-20}: {bVal,6} → {aVal,6}{marker}");
            }
        }

        private void CompareOffset(System.Text.StringBuilder sb, string name, int before, int after)
        {
            int delta = after - before;
            string marker = delta != 0 ? $"  ← SHIFTED {(delta > 0 ? "+" : "")}{delta}" : "";
            sb.AppendLine($"  {name,-25} 0x{before:X8}    0x{after:X8}    {delta,6}{marker}");
        }

        private void DumpBuildingRaw(System.Text.StringBuilder sb, string label, byte[] bytes, int buildingsOff, int buildingId1, ushort nextBuilding)
        {
            int idx0 = buildingId1 - 1;
            if (idx0 < 0 || idx0 >= nextBuilding - 1)
            {
                sb.AppendLine($"  {label}: Building #{buildingId1} not present (nextBuilding={nextBuilding})");
                return;
            }
            int off = buildingsOff + idx0 * 24;
            if (off + 24 > bytes.Length)
            {
                sb.AppendLine($"  {label}: offset 0x{off:X} out of range");
                return;
            }
            sb.Append($"  {label} @ 0x{off:X6}: ");
            for (int i = 0; i < 24; i++) sb.Append($"{bytes[off + i]:X2} ");
            sb.AppendLine();

            // Interpret key fields
            ushort startFacet = ReadU16(bytes, off + 0);
            ushort endFacet = ReadU16(bytes, off + 2);
            int x = BitConverter.ToInt32(bytes, off + 4);
            ushort walkable = ReadU16(bytes, off + 16);
            byte type = bytes[off + 11];
            byte ware = bytes[off + 22];
            sb.AppendLine($"          StartFacet={startFacet} EndFacet={endFacet} X={x} Type={type} Walkable={walkable} Ware={ware}");
        }

        private void DumpWalkableRaw(System.Text.StringBuilder sb, string label, byte[] bytes, int walkablesOff, int walkableId)
        {
            if (walkablesOff + 4 > bytes.Length) return;
            ushort nextW = ReadU16(bytes, walkablesOff);
            if (walkableId >= nextW)
            {
                sb.AppendLine($"  {label}: Walkable #{walkableId} not present (nextWalkable={nextW})");
                return;
            }
            int dataOff = walkablesOff + 4;
            int off = dataOff + walkableId * 22;
            if (off + 22 > bytes.Length)
            {
                sb.AppendLine($"  {label}: offset 0x{off:X} out of range");
                return;
            }
            sb.Append($"  {label} @ 0x{off:X6}: ");
            for (int i = 0; i < 22; i++) sb.Append($"{bytes[off + i]:X2} ");
            sb.AppendLine();

            // Interpret
            ushort startF4 = ReadU16(bytes, off + 8);
            ushort endF4 = ReadU16(bytes, off + 10);
            byte y = bytes[off + 16];
            byte storeyY = bytes[off + 17];
            ushort next = ReadU16(bytes, off + 18);
            ushort building = ReadU16(bytes, off + 20);
            sb.AppendLine($"          StartF4={startF4} EndF4={endF4} Y={y} StoreyY={storeyY} Next={next} Building={building}");
        }

        private string IdentifyRegion(int offset, BlockOffsets? before, BlockOffsets? after)
        {
            // Use BEFORE offsets for identification
            var s = before ?? after;
            if (s == null) return "unknown";

            if (offset < 8) return "FILE HEADER";
            if (offset < s.BlockStart) return "PAP TILES";
            if (offset < s.BlockStart + 48) return "BLOCK HEADER";
            if (offset < s.PadOff) return "BUILDINGS";
            if (offset < s.FacetsOff) return "PADDING(14B)";
            if (offset < s.StylesOff) return "FACETS";
            if (offset < s.PaintOff) return "DSTYLES";
            if (offset < s.StoreysOff) return "PAINT_MEM";
            if (offset < s.IndoorsOff) return "DSTOREYS";
            if (offset < s.WalkablesOff) return "INDOORS";
            if (offset < s.BlockEnd) return "WALKABLES+RF4";
            return "OBJECTS/TAIL";
        }

        private void SetRF4DrawFlags_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Set RF4 DrawFlags", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show input dialog to get walkable ID
            var inputDlg = new WalkableIdInputDialog { Owner = this };
            if (inputDlg.ShowDialog() != true)
                return;

            int walkableId1 = inputDlg.WalkableId;

            // Get walkable and its RF4 range
            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();

            if (snap.Walkables == null || walkableId1 < 1 || walkableId1 >= snap.Walkables.Length)
            {
                MessageBox.Show($"Walkable #{walkableId1} not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var w = snap.Walkables[walkableId1];
            int rf4Count = w.EndFace4 - w.StartFace4;

            if (rf4Count <= 0)
            {
                MessageBox.Show($"Walkable #{walkableId1} has no RoofFace4 entries.\n\n" +
                    $"StartFace4={w.StartFace4}, EndFace4={w.EndFace4}",
                    "No RF4 Entries", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show current DrawFlags values
            var sb = new StringBuilder();
            sb.AppendLine($"Walkable #{walkableId1} has {rf4Count} RoofFace4 entries:");
            sb.AppendLine($"Range: [{w.StartFace4}..{w.EndFace4})");
            sb.AppendLine();

            if (snap.RoofFaces4 != null)
            {
                var flagCounts = new Dictionary<byte, int>();
                for (int i = w.StartFace4; i < w.EndFace4 && i < snap.RoofFaces4.Length; i++)
                {
                    byte flags = snap.RoofFaces4[i].DrawFlags;
                    if (!flagCounts.ContainsKey(flags))
                        flagCounts[flags] = 0;
                    flagCounts[flags]++;
                }

                sb.AppendLine("Current DrawFlags distribution:");
                foreach (var kvp in flagCounts.OrderBy(x => x.Key))
                {
                    sb.AppendLine($"  0x{kvp.Key:X2}: {kvp.Value} entries");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Enter new DrawFlags value (hex, e.g. 00 or 08):");
            sb.AppendLine("  0x00 = Normal (used by working buildings)");
            sb.AppendLine("  0x08 = Walkable bit (editor default)");

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                sb.ToString(),
                "Set RF4 DrawFlags",
                "00");

            if (string.IsNullOrWhiteSpace(input))
                return;

            // Parse hex value
            byte newFlags;
            try
            {
                newFlags = Convert.ToByte(input.Trim().Replace("0x", ""), 16);
            }
            catch
            {
                MessageBox.Show("Invalid hex value. Enter 00, 08, etc.", "Invalid Input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirm
            if (MessageBox.Show(
                $"Set DrawFlags to 0x{newFlags:X2} for all {rf4Count} RoofFace4 entries in Walkable #{walkableId1}?",
                "Confirm Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            // Apply changes
            if (!acc.TryGetWalkablesHeaderOffset(out int walkablesHeaderOff))
            {
                MessageBox.Show("Could not find walkables header offset.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var bytes = MapDataService.Instance.GetBytesCopy();

                // Calculate RF4 data offset
                // walkablesHeaderOff + 4 = walkables data start
                // walkables data size = nextWalkable * 22 bytes
                ushort nextWalkable = (ushort)(bytes[walkablesHeaderOff] | (bytes[walkablesHeaderOff + 1] << 8));
                int walkablesDataSize = nextWalkable * 22;
                int roofFacesDataOff = walkablesHeaderOff + 4 + walkablesDataSize;

                // RF4 structure is 10 bytes, DrawFlags at offset +5
                int changedCount = 0;
                for (int i = w.StartFace4; i < w.EndFace4; i++)
                {
                    int rf4Offset = roofFacesDataOff + (i * 10);
                    int drawFlagsOffset = rf4Offset + 5;

                    if (drawFlagsOffset < bytes.Length)
                    {
                        bytes[drawFlagsOffset] = newFlags;
                        changedCount++;
                    }
                }

                MapDataService.Instance.ReplaceBytes(bytes);

                MessageBox.Show(
                    $"Changed DrawFlags to 0x{newFlags:X2} for {changedCount} RoofFace4 entries.\n\n" +
                    $"Walkable #{walkableId1}, RF4 range [{w.StartFace4}..{w.EndFace4})\n\n" +
                    "Save the map and test in-game.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying changes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DumpWalkableRawBytes_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Dump Walkable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show input dialog to get walkable ID
            var inputDlg = new WalkableIdInputDialog { Owner = this };
            if (inputDlg.ShowDialog() != true)
                return;

            int walkableId1 = inputDlg.WalkableId;

            try
            {
                var report = GenerateWalkableRawDump(walkableId1);
                var path = Path.Combine(Path.GetTempPath(), $"walkable_{walkableId1}_raw_dump.txt");
                File.WriteAllText(path, report);
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error dumping walkable: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateWalkableRawDump(int walkableId1)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine($"  WALKABLE #{walkableId1} RAW BYTE DUMP");
            sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Map: {MapDataService.Instance.CurrentPath}");
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine();

            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();

            if (snap.Walkables == null || walkableId1 < 1 || walkableId1 >= snap.Walkables.Length)
            {
                sb.AppendLine($"ERROR: Walkable #{walkableId1} not found.");
                sb.AppendLine($"  Valid range: 1 to {(snap.Walkables?.Length ?? 1) - 1}");
                return sb.ToString();
            }

            // Get file offset
            if (!acc.TryGetDWalkableOffset(walkableId1, out int walkableOffset))
            {
                sb.AppendLine("ERROR: Could not calculate walkable offset.");
                return sb.ToString();
            }

            var bytes = MapDataService.Instance.GetBytesCopy();

            // DWalkable structure (22 bytes):
            //   +0-1:   StartPoint (ushort)
            //   +2-3:   EndPoint (ushort)
            //   +4-5:   StartFace3 (ushort)
            //   +6-7:   EndFace3 (ushort)
            //   +8-9:   StartFace4 (ushort)
            //   +10-11: EndFace4 (ushort)
            //   +12:    X1 (byte)
            //   +13:    Z1 (byte)
            //   +14:    X2 (byte)
            //   +15:    Z2 (byte)
            //   +16:    Y (byte)
            //   +17:    StoreyY (byte)
            //   +18-19: Next (ushort)
            //   +20-21: Building (ushort)

            const int DWalkableSize = 22;
            byte[] raw = new byte[DWalkableSize];
            Buffer.BlockCopy(bytes, walkableOffset, raw, 0, DWalkableSize);

            sb.AppendLine($"FILE OFFSET: 0x{walkableOffset:X8}");
            sb.AppendLine();

            // Raw hex dump
            sb.AppendLine("RAW BYTES (22 bytes):");
            sb.AppendLine("---------------------------------------------------------------");
            sb.Append("  ");
            for (int i = 0; i < DWalkableSize; i++)
            {
                sb.Append($"{raw[i]:X2} ");
                if (i == 10) sb.Append(" "); // Visual separator
            }
            sb.AppendLine();
            sb.AppendLine();

            // Parsed fields with offsets
            sb.AppendLine("PARSED FIELDS:");
            sb.AppendLine("---------------------------------------------------------------");

            ushort startPoint = (ushort)(raw[0] | (raw[1] << 8));
            ushort endPoint = (ushort)(raw[2] | (raw[3] << 8));
            ushort startFace3 = (ushort)(raw[4] | (raw[5] << 8));
            ushort endFace3 = (ushort)(raw[6] | (raw[7] << 8));
            ushort startFace4 = (ushort)(raw[8] | (raw[9] << 8));
            ushort endFace4 = (ushort)(raw[10] | (raw[11] << 8));
            byte x1 = raw[12];
            byte z1 = raw[13];
            byte x2 = raw[14];
            byte z2 = raw[15];
            byte y = raw[16];
            byte storeyY = raw[17];
            ushort next = (ushort)(raw[18] | (raw[19] << 8));
            ushort building = (ushort)(raw[20] | (raw[21] << 8));

            sb.AppendLine($"  +00-01: StartPoint    = {startPoint} (0x{raw[0]:X2} {raw[1]:X2})");
            sb.AppendLine($"  +02-03: EndPoint      = {endPoint} (0x{raw[2]:X2} {raw[3]:X2})");
            sb.AppendLine($"          Point count   = {endPoint - startPoint}");
            sb.AppendLine();
            sb.AppendLine($"  +04-05: StartFace3    = {startFace3} (0x{raw[4]:X2} {raw[5]:X2})");
            sb.AppendLine($"  +06-07: EndFace3      = {endFace3} (0x{raw[6]:X2} {raw[7]:X2})");
            sb.AppendLine($"          Face3 count   = {endFace3 - startFace3}");
            sb.AppendLine();
            sb.AppendLine($"  +08-09: StartFace4    = {startFace4} (0x{raw[8]:X2} {raw[9]:X2})");
            sb.AppendLine($"  +10-11: EndFace4      = {endFace4} (0x{raw[10]:X2} {raw[11]:X2})");
            sb.AppendLine($"          RF4 count     = {endFace4 - startFace4}");
            sb.AppendLine();
            sb.AppendLine($"  +12:    X1            = {x1} (0x{raw[12]:X2})");
            sb.AppendLine($"  +13:    Z1            = {z1} (0x{raw[13]:X2})");
            sb.AppendLine($"  +14:    X2            = {x2} (0x{raw[14]:X2})");
            sb.AppendLine($"  +15:    Z2            = {z2} (0x{raw[15]:X2})");
            sb.AppendLine($"          Bounds        = ({x1},{z1}) -> ({x2},{z2}) = {x2 - x1}x{z2 - z1} tiles");
            sb.AppendLine();
            sb.AppendLine($"  +16:    Y             = {y} (0x{raw[16]:X2})  [world altitude = {y * 32}]");
            sb.AppendLine($"  +17:    StoreyY       = {storeyY} (0x{raw[17]:X2})");
            sb.AppendLine();
            sb.AppendLine($"  +18-19: Next          = {next} (0x{raw[18]:X2} {raw[19]:X2})");
            sb.AppendLine($"  +20-21: Building      = {building} (0x{raw[20]:X2} {raw[21]:X2})");
            sb.AppendLine();

            // Analysis section
            sb.AppendLine("ANALYSIS:");
            sb.AppendLine("---------------------------------------------------------------");

            // Check for potential issues
            bool hasIssues = false;

            if (startPoint == 0 && endPoint == 0)
            {
                sb.AppendLine("  ?? StartPoint/EndPoint are both 0 (typical for editor-created walkables)");
            }
            else
            {
                sb.AppendLine($"  ? Has prim_points range: [{startPoint}..{endPoint})");
            }

            if (startFace3 != 0 || endFace3 != 0)
            {
                sb.AppendLine($"  ?? Has Face3 entries: [{startFace3}..{endFace3})");
            }

            if (startFace4 == endFace4)
            {
                sb.AppendLine("  ?? No RoofFace4 entries (StartFace4 == EndFace4)");
                hasIssues = true;
            }
            else
            {
                sb.AppendLine($"  ? Has RoofFace4 entries: [{startFace4}..{endFace4}) = {endFace4 - startFace4} tiles");
            }

            if (building == 0)
            {
                sb.AppendLine("  ?? Building = 0 (not linked to any building)");
                hasIssues = true;
            }
            else
            {
                // Check if building exists and is Type=1
                if (snap.Buildings != null && building <= snap.Buildings.Length)
                {
                    var bld = snap.Buildings[building - 1];
                    sb.AppendLine($"  ? Linked to Building #{building} (Type={bld.Type})");
                    if (bld.Type != 1)
                    {
                        sb.AppendLine($"  ?? Building Type is {bld.Type}, expected 1 for warehouse/indoor");
                    }
                }
            }

            if (y == 0)
            {
                sb.AppendLine("  ?? Y = 0 (ground level - unusual for roof walkable)");
            }

            sb.AppendLine();

            // Dump first few RF4 entries if they exist
            if (endFace4 > startFace4 && snap.RoofFaces4 != null)
            {
                sb.AppendLine($"ROOFFACE4 ENTRIES [{startFace4}..{endFace4}):");
                sb.AppendLine("---------------------------------------------------------------");

                // Get RF4 data offset
                if (acc.TryGetWalkablesHeaderOffset(out int walkablesHeaderOff))
                {
                    ushort nextWalkable = (ushort)(bytes[walkablesHeaderOff] | (bytes[walkablesHeaderOff + 1] << 8));
                    int walkablesDataSize = nextWalkable * 22;
                    int rf4DataOff = walkablesHeaderOff + 4 + walkablesDataSize;

                    int count = 0;
                    for (int i = startFace4; i < endFace4 && i < snap.RoofFaces4.Length; i++)
                    {
                        int rf4Off = rf4DataOff + (i * 10);
                        byte[] rf4Raw = new byte[10];
                        Buffer.BlockCopy(bytes, rf4Off, rf4Raw, 0, 10);

                        var rf = snap.RoofFaces4[i];
                        int tileX = rf.RX;
                        int tileZ = rf.RZ - 128;

                        string rawHex = string.Join(" ", rf4Raw.Select(b => $"{b:X2}"));
                        sb.AppendLine($"  RF4 #{i} at 0x{rf4Off:X}: {rawHex}");
                        sb.AppendLine($"       Tile({tileX},{tileZ}) Y={rf.Y} DY=({rf.DY0},{rf.DY1},{rf.DY2}) Flags=0x{rf.DrawFlags:X2} Next={rf.Next}");

                        count++;
                        if (count >= 10)
                        {
                            sb.AppendLine($"  ... and {endFace4 - startFace4 - 10} more entries");
                            break;
                        }
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("---------------------------------------------------------------");
            sb.AppendLine("COMPARISON NOTES:");
            sb.AppendLine("---------------------------------------------------------------");
            sb.AppendLine("Compare this dump with a working walkable to identify differences.");
            sb.AppendLine("Key fields to compare:");
            sb.AppendLine("  - StartPoint/EndPoint (usually 0 for editor-created, non-zero for dev-created)");
            sb.AppendLine("  - StartFace3/EndFace3 (often 0)");
            sb.AppendLine("  - Y and StoreyY values");
            sb.AppendLine("  - Building link");
            sb.AppendLine("  - RF4 DrawFlags (0x00 vs 0x08)");

            return sb.ToString();
        }

        private void DumpInsideStoreyForBuilding_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Dump InsideStorey", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Prompt for building ID
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter Building ID (1-based):",
                "Dump InsideStorey",
                "49");

            if (string.IsNullOrWhiteSpace(input) || !int.TryParse(input, out int buildingId1) || buildingId1 < 1)
                return;

            try
            {
                var report = GenerateInsideStoreyDump(buildingId1);
                var path = Path.Combine(Path.GetTempPath(), $"building_{buildingId1}_insidestorey_dump.txt");
                File.WriteAllText(path, report);
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error dumping InsideStorey: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateInsideStoreyDump(int buildingId1)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine($"  INSIDESTOREY DUMP FOR BUILDING #{buildingId1}");
            sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Map: {MapDataService.Instance.CurrentPath}");
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine();

            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
            {
                sb.AppendLine($"ERROR: Building #{buildingId1} not found.");
                return sb.ToString();
            }

            var building = snap.Buildings[buildingId1 - 1];
            sb.AppendLine($"BUILDING #{buildingId1}:");
            sb.AppendLine($"  Type: {building.Type} ({(building.Type == 1 ? "Warehouse/Indoor" : "Normal")})");
            sb.AppendLine($"  Walkable pointer: {building.Walkable}");
            sb.AppendLine();

            // Dump all InsideStorey entries
            var insideStoreys = snap.InsideStoreys;
            if (insideStoreys == null || insideStoreys.Length == 0)
            {
                sb.AppendLine("NO INSIDESTOREY ENTRIES IN THIS MAP.");
                sb.AppendLine();
                sb.AppendLine("This could explain why roofs don't render!");
                sb.AppendLine("InsideStorey entries define interior spaces for Type=1 buildings.");
                return sb.ToString();
            }

            sb.AppendLine($"TOTAL INSIDESTOREY ENTRIES: {insideStoreys.Length}");
            sb.AppendLine();

            // Find entries linked to this building
            sb.AppendLine($"INSIDESTOREY ENTRIES FOR BUILDING #{buildingId1}:");
            sb.AppendLine("---------------------------------------------------------------");

            int foundCount = 0;
            for (int i = 0; i < insideStoreys.Length; i++)
            {
                var ist = insideStoreys[i];
                if (ist.Building == buildingId1)
                {
                    foundCount++;
                    sb.AppendLine($"  InsideStorey #{i}:");
                    sb.AppendLine($"    Bounds: ({ist.MinX},{ist.MinZ}) -> ({ist.MaxX},{ist.MaxZ})");
                    sb.AppendLine($"    InsideBlock: {ist.InsideBlock}");
                    sb.AppendLine($"    StairCaseHead: {ist.StairCaseHead}");
                    sb.AppendLine($"    TexType: {ist.TexType}");
                    sb.AppendLine($"    FacetStart: {ist.FacetStart}");
                    sb.AppendLine($"    FacetEnd: {ist.FacetEnd}");
                    sb.AppendLine($"    StoreyY: {ist.StoreyY} (world = {ist.StoreyY * 32})");
                    sb.AppendLine($"    Building: {ist.Building}");
                    sb.AppendLine($"    Dummy0: {ist.Dummy0}, Dummy1: {ist.Dummy1}");
                    sb.AppendLine();
                }
            }

            if (foundCount == 0)
            {
                sb.AppendLine("  (No InsideStorey entries found for this building)");
                sb.AppendLine();
                sb.AppendLine("  ?? WARNING: Type=1 buildings typically need InsideStorey entries");
                sb.AppendLine("     for interior/roof rendering to work correctly.");
            }
            else
            {
                sb.AppendLine($"  Total: {foundCount} InsideStorey entries for building #{buildingId1}");
            }

            sb.AppendLine();

            // Also dump walkables for this building for cross-reference
            sb.AppendLine("WALKABLES FOR THIS BUILDING:");
            sb.AppendLine("---------------------------------------------------------------");

            if (snap.Walkables != null)
            {
                int walkableCount = 0;
                for (int i = 1; i < snap.Walkables.Length; i++)
                {
                    var w = snap.Walkables[i];
                    if (w.Building == buildingId1)
                    {
                        walkableCount++;
                        sb.AppendLine($"  Walkable #{i}:");
                        sb.AppendLine($"    Bounds: ({w.X1},{w.Z1}) -> ({w.X2},{w.Z2})");
                        sb.AppendLine($"    Y: {w.Y} (world = {w.Y * 32})");
                        sb.AppendLine($"    StoreyY: {w.StoreyY} ? Compare with InsideStorey.StoreyY above!");
                        sb.AppendLine($"    RF4 range: [{w.StartFace4}..{w.EndFace4}) = {w.EndFace4 - w.StartFace4} tiles");
                        sb.AppendLine($"    Next: {w.Next}");
                        sb.AppendLine();
                    }
                }

                if (walkableCount == 0)
                {
                    sb.AppendLine("  (No walkables found for this building)");
                }
            }

            sb.AppendLine();
            sb.AppendLine("---------------------------------------------------------------");
            sb.AppendLine("ANALYSIS:");
            sb.AppendLine("---------------------------------------------------------------");
            sb.AppendLine("The DWalkable.StoreyY field might reference an InsideStorey index");
            sb.AppendLine("or relate to storey height calculations for roof rendering.");
            sb.AppendLine();
            sb.AppendLine("If InsideStorey entries exist but walkable StoreyY doesn't match,");
            sb.AppendLine("that could explain invisible roof textures.");

            return sb.ToString();
        }

        private string GeneratePapHiDump(int tileX, int tileZ)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine($"  PAP_HI CELL ({tileX}, {tileZ}) DUMP");
            sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"---------------------------------------------------------------");
            sb.AppendLine();

            // Constants for PAP_HI structure
            const int HeaderBytes = 8;
            const int BytesPerTile = 6;
            const int TilesPerSide = 128;

            var bytes = MapDataService.Instance.GetBytesCopy();

            // Calculate file offset for this tile
            // Tiles are stored in row-major order: index = tileZ * 128 + tileX
            int tileIndex = tileZ * TilesPerSide + tileX;
            int tileOffset = HeaderBytes + tileIndex * BytesPerTile;

            if (tileOffset + BytesPerTile > bytes.Length)
            {
                sb.AppendLine("ERROR: Tile offset out of bounds.");
                return sb.ToString();
            }

            // Read the 6 bytes
            byte[] tileBytes = new byte[BytesPerTile];
            Buffer.BlockCopy(bytes, tileOffset, tileBytes, 0, BytesPerTile);

            sb.AppendLine($"FILE OFFSET: 0x{tileOffset:X8}");
            sb.AppendLine($"TILE INDEX: {tileIndex}");
            sb.AppendLine();

            // Raw hex dump
            sb.AppendLine("RAW BYTES (6 bytes):");
            sb.AppendLine("---------------------------------------------------------------");
            sb.Append("  ");
            for (int i = 0; i < BytesPerTile; i++)
            {
                sb.Append($"{tileBytes[i]:X2} ");
            }
            sb.AppendLine();
            sb.AppendLine();

            // Parse individual fields
            ushort textureIndex = (ushort)(tileBytes[0] | (tileBytes[1] << 8));
            ushort flags = (ushort)(tileBytes[2] | (tileBytes[3] << 8));
            sbyte height = unchecked((sbyte)tileBytes[4]);
            sbyte alt = unchecked((sbyte)tileBytes[5]);

            sb.AppendLine("PARSED FIELDS (PAP_Hi structure - 6 bytes):");
            sb.AppendLine("---------------------------------------------------------------");
            sb.AppendLine($"  +00-01  Texture      : {textureIndex} (0x{textureIndex:X4})");
            sb.AppendLine($"  +02-03  Flags        : 0x{flags:X4}");
            sb.AppendLine($"  +04     Height       : {height} (terrain vertex offset, world = {height * 64})");
            sb.AppendLine($"  +05     Alt          : {alt} (floor altitude, world = {alt << 3})");
            sb.AppendLine();

            // Flag breakdown
            sb.AppendLine("FLAG BREAKDOWN (PapFlags):");
            sb.AppendLine("---------------------------------------------------------------");
            sb.AppendLine($"  Bit 0  (0x0001) Shadow1      : {((flags & 0x0001) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 1  (0x0002) Shadow2      : {((flags & 0x0002) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 2  (0x0004) Shadow3      : {((flags & 0x0004) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 3  (0x0008) Reflective   : {((flags & 0x0008) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 4  (0x0010) Hidden       : {((flags & 0x0010) != 0 ? "YES" : "no")}  <- PAP_HI flag for ledge grab");
            sb.AppendLine($"  Bit 5  (0x0020) SinkSquare   : {((flags & 0x0020) != 0 ? "YES" : "no")}  <- Lowers floorsquare (curb)");
            sb.AppendLine($"  Bit 6  (0x0040) SinkPoint    : {((flags & 0x0040) != 0 ? "YES" : "no")}  <- Transform point lower level");
            sb.AppendLine($"  Bit 7  (0x0080) NoUpper      : {((flags & 0x0080) != 0 ? "YES" : "no")}  <- Don't transform upper level");
            sb.AppendLine($"  Bit 8  (0x0100) NoGo         : {((flags & 0x0100) != 0 ? "YES" : "no")}  <- Square nobody allowed onto");
            sb.AppendLine($"  Bit 9  (0x0200) AnimTmap/Roof: {((flags & 0x0200) != 0 ? "YES" : "no")}  <- Animated texture OR roof exists");
            sb.AppendLine($"  Bit 10 (0x0400) Zone1        : {((flags & 0x0400) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 11 (0x0800) Zone2        : {((flags & 0x0800) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 12 (0x1000) Zone3        : {((flags & 0x1000) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 13 (0x2000) Zone4        : {((flags & 0x2000) != 0 ? "YES" : "no")}");
            sb.AppendLine($"  Bit 14 (0x4000) Wander/Flat  : {((flags & 0x4000) != 0 ? "YES" : "no")}  <- Wander zone OR flat roof");
            sb.AppendLine($"  Bit 15 (0x8000) Water        : {((flags & 0x8000) != 0 ? "YES" : "no")}");
            sb.AppendLine();

            // Check walkables
            sb.AppendLine("WALKABLE COVERAGE:");
            sb.AppendLine("---------------------------------------------------------------");

            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();

            bool foundWalkable = false;
            if (snap.Walkables != null && snap.Walkables.Length > 1)
            {
                for (int wIdx = 1; wIdx < snap.Walkables.Length; wIdx++)
                {
                    var w = snap.Walkables[wIdx];

                    // Check if tile is within this walkable's bounds
                    if (tileX >= w.X1 && tileX < w.X2 && tileZ >= w.Z1 && tileZ < w.Z2)
                    {
                        foundWalkable = true;
                        sb.AppendLine($"  *** TILE IS INSIDE WALKABLE #{wIdx} ***");
                        sb.AppendLine($"      Bounds: ({w.X1},{w.Z1}) to ({w.X2},{w.Z2})");
                        sb.AppendLine($"      Size: {w.X2 - w.X1} x {w.Z2 - w.Z1} tiles");
                        sb.AppendLine();
                        sb.AppendLine($"      Y (altitude/32): {w.Y}");
                        sb.AppendLine($"        -> World altitude: {w.Y * 32}");
                        sb.AppendLine($"        -> THIS IS THE ROOF HEIGHT for indoor buildings!");
                        sb.AppendLine();
                        sb.AppendLine($"      StoreyY: {w.StoreyY}");
                        sb.AppendLine($"      Building: #{w.Building}");
                        sb.AppendLine($"      Next: {w.Next}");
                        sb.AppendLine();
                        sb.AppendLine($"      Face3 Range: [{w.StartFace3}..{w.EndFace3}) = {w.EndFace3 - w.StartFace3} entries");
                        sb.AppendLine($"      Face4 Range: [{w.StartFace4}..{w.EndFace4}) = {w.EndFace4 - w.StartFace4} RoofFace4 entries");
                        sb.AppendLine($"      StartPoint: {w.StartPoint}, EndPoint: {w.EndPoint}");

                        // Check if Face4 range is valid
                        if (w.StartFace4 == w.EndFace4)
                        {
                            sb.AppendLine();
                            sb.AppendLine($"      ?? WARNING: Face4 range is EMPTY! No RoofFace4 tiles linked.");
                            sb.AppendLine($"         This means the walkable has no roof geometry.");
                        }
                        else if (w.StartFace4 > 0 && w.EndFace4 > w.StartFace4)
                        {
                            sb.AppendLine();
                            sb.AppendLine($"      RoofFace4 entries in this walkable's range:");
                            for (int rfIdx = w.StartFace4; rfIdx < w.EndFace4 && rfIdx < snap.RoofFaces4.Length; rfIdx++)
                            {
                                var rf = snap.RoofFaces4[rfIdx];
                                int rfTileX = rf.RX;
                                int rfTileZ = rf.RZ - 128;
                                sb.AppendLine($"        RF4#{rfIdx}: pos=({rfTileX},{rfTileZ}) Y={rf.Y} DY=({rf.DY0},{rf.DY1},{rf.DY2}) flags=0x{rf.DrawFlags:X2}");
                            }
                        }
                        sb.AppendLine();
                    }
                }
            }

            if (!foundWalkable)
            {
                sb.AppendLine("  Tile is NOT inside any walkable region.");
                sb.AppendLine("  ?? For indoor buildings, you NEED a walkable covering the roof area!");
                sb.AppendLine();
            }

            // Check RoofFace4 entries
            sb.AppendLine("ROOFFACE4 COVERAGE:");
            sb.AppendLine("---------------------------------------------------------------");

            bool foundRoof = false;
            if (snap.Walkables != null && snap.RoofFaces4 != null && snap.Walkables.Length > 1)
            {
                for (int wIdx = 1; wIdx < snap.Walkables.Length; wIdx++)
                {
                    var w = snap.Walkables[wIdx];

                    for (int rfIdx = w.StartFace4; rfIdx < w.EndFace4 && rfIdx < snap.RoofFaces4.Length; rfIdx++)
                    {
                        if (rfIdx < 1) continue;
                        var rf4 = snap.RoofFaces4[rfIdx];

                        // RF4 coordinates: RX = absolute tile X, RZ = absolute tile Z + 128
                        int rfTileX = rf4.RX;
                        int rfTileZ = rf4.RZ - 128;

                        if (rfTileX == tileX && rfTileZ == tileZ)
                        {
                            foundRoof = true;
                            sb.AppendLine($"  *** ROOFFACE4 #{rfIdx} COVERS THIS TILE ***");
                            sb.AppendLine($"      Owner Walkable: #{wIdx} (Building #{w.Building})");
                            sb.AppendLine($"      RX (abs tile X): {rf4.RX}");
                            sb.AppendLine($"      RZ (abs tile Z + 128): {rf4.RZ} (actual Z: {rfTileZ})");
                            sb.AppendLine($"      Y (base altitude): {rf4.Y}");
                            sb.AppendLine($"      DY0 (NW corner): {rf4.DY0}");
                            sb.AppendLine($"      DY1 (NE corner): {rf4.DY1}");
                            sb.AppendLine($"      DY2 (SE corner): {rf4.DY2}");
                            sb.AppendLine($"      (SW corner is implicit at Y, delta=0)");
                            sb.AppendLine($"      DrawFlags: 0x{rf4.DrawFlags:X2}");
                            sb.AppendLine($"        Bit 3 (0x08) Walkable: {((rf4.DrawFlags & 0x08) != 0 ? "YES" : "no")}");
                            sb.AppendLine($"      Next: {rf4.Next}");

                            // Roof type
                            bool isFlat = (rf4.DY0 == 0 && rf4.DY1 == 0 && rf4.DY2 == 0);
                            sb.AppendLine($"      Roof Type: {(isFlat ? "FLAT" : "PITCHED/SLOPED")}");
                            sb.AppendLine();
                        }
                    }
                }
            }

            if (!foundRoof)
            {
                sb.AppendLine("  No RoofFace4 entry covers this tile.");
                sb.AppendLine();
            }

            // Summary
            sb.AppendLine("SUMMARY:");
            sb.AppendLine("---------------------------------------------------------------");
            bool hasRoofFlag = (flags & 0x0200) != 0;
            bool hasHiddenFlag = (flags & 0x0010) != 0;
            bool hasFlatRoofFlag = (flags & 0x4000) != 0;

            sb.AppendLine($"  Has roof flag (0x0200):   {(hasRoofFlag ? "YES" : "no")}");
            sb.AppendLine($"  Has hidden flag (0x0010): {(hasHiddenFlag ? "YES" : "no")} <- Required for ledge grab");
            sb.AppendLine($"  Has flat roof (0x4000):   {(hasFlatRoofFlag ? "YES" : "no")}");
            sb.AppendLine($"  Inside walkable:          {(foundWalkable ? "YES" : "no")}");
            sb.AppendLine($"  Has RoofFace4:            {(foundRoof ? "YES" : "no")}");
            sb.AppendLine();

            return sb.ToString();
        }
    }

    /// <summary>
    /// Simple input dialog for PAP_HI cell coordinates.
    /// </summary>
    public class PapHiInputDialog : Window
    {
        private TextBox _txtX;
        private TextBox _txtZ;

        public int TileX { get; private set; }
        public int TileZ { get; private set; }

        public PapHiInputDialog()
        {
            Title = "Enter Cell Coordinates";
            Width = 280;
            Height = 160;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(0x20, 0x22, 0x25));

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lblPrompt = new TextBlock
            {
                Text = "Enter tile coordinates (0-127):",
                Foreground = Brushes.White
            };
            Grid.SetRow(lblPrompt, 0);
            Grid.SetColumnSpan(lblPrompt, 7);
            grid.Children.Add(lblPrompt);

            var lblX = new TextBlock { Text = "X:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblX, 2);
            Grid.SetColumn(lblX, 0);
            grid.Children.Add(lblX);

            _txtX = new TextBox { Text = "0", Background = new SolidColorBrush(Color.FromRgb(0x2F, 0x31, 0x36)), Foreground = Brushes.White };
            Grid.SetRow(_txtX, 2);
            Grid.SetColumn(_txtX, 2);
            grid.Children.Add(_txtX);

            var lblZ = new TextBlock { Text = "Z:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblZ, 2);
            Grid.SetColumn(lblZ, 4);
            grid.Children.Add(lblZ);

            _txtZ = new TextBox { Text = "0", Background = new SolidColorBrush(Color.FromRgb(0x2F, 0x31, 0x36)), Foreground = Brushes.White };
            Grid.SetRow(_txtZ, 2);
            Grid.SetColumn(_txtZ, 6);
            grid.Children.Add(_txtZ);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(buttonPanel, 4);
            Grid.SetColumnSpan(buttonPanel, 7);

            var btnCancel = new Button { Content = "Cancel", Width = 70, Margin = new Thickness(0, 0, 8, 0) };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            buttonPanel.Children.Add(btnCancel);

            var btnOk = new Button { Content = "OK", Width = 70 };
            btnOk.Click += (s, e) =>
            {
                if (int.TryParse(_txtX.Text, out int x) && int.TryParse(_txtZ.Text, out int z))
                {
                    TileX = x;
                    TileZ = z;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Please enter valid integer coordinates.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            buttonPanel.Children.Add(btnOk);

            grid.Children.Add(buttonPanel);
            Content = grid;

            _txtX.Focus();
            _txtX.SelectAll();
        }
    }

    // ============================================================
    // Simple input dialog for Walkable ID
    // ============================================================
    internal class WalkableIdInputDialog : Window
    {
        private TextBox _txtId;
        public int WalkableId { get; private set; }

        public WalkableIdInputDialog()
        {
            Title = "Edit Walkable Points";
            Width = 300;
            Height = 140;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = "Enter Walkable ID (1-based):",
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(lbl, 0);
            grid.Children.Add(lbl);

            _txtId = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                Padding = new Thickness(4, 2, 4, 2),
                Text = "1"
            };
            Grid.SetRow(_txtId, 1);
            grid.Children.Add(_txtId);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            Grid.SetRow(buttonPanel, 2);

            var btnOk = new Button
            {
                Content = "OK",
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(4, 0, 0, 0),
                IsDefault = true
            };
            btnOk.Click += (s, e) =>
            {
                if (int.TryParse(_txtId.Text, out int id) && id >= 1)
                {
                    WalkableId = id;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Please enter a valid integer >= 1.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            buttonPanel.Children.Add(btnOk);

            var btnCancel = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(4, 0, 0, 0),
                IsCancel = true
            };
            buttonPanel.Children.Add(btnCancel);

            grid.Children.Add(buttonPanel);
            Content = grid;

            _txtId.Focus();
            _txtId.SelectAll();
        }
    }

    // Handler for Paint RF4 Tiles dialog
    public partial class MainWindow
    {
        private void PaintRF4Tiles_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Paint RF4", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter Walkable ID (1-based):",
                "Paint RF4 Tiles",
                "124");

            if (string.IsNullOrWhiteSpace(input) || !int.TryParse(input, out int walkableId1) || walkableId1 < 1)
                return;

            var acc = new BuildingsAccessor(MapDataService.Instance);
            if (!acc.TryGetWalkables(out var walkables, out _))
            {
                MessageBox.Show("Failed to read walkables.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (walkableId1 >= walkables.Length)
            {
                MessageBox.Show($"Walkable #{walkableId1} not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var walkable = walkables[walkableId1];

            // Check if it's a valid (non-zeroed) walkable
            if (walkable.X1 == 0 && walkable.Z1 == 0 && walkable.X2 == 0 && walkable.Z2 == 0 && walkable.Building == 0)
            {
                MessageBox.Show($"Walkable #{walkableId1} appears to be deleted (all zeros).",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dialog = new RoofFace4PainterDialog(walkableId1, walkable.Building, walkable)
            {
                Owner = this
            };

            dialog.ShowDialog();
        }

        private void IndoorBuildingEditor_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Indoor Building Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new IndoorBuildingEditorDialog
            {
                Owner = this
            };

            dialog.ShowDialog();
        }
    }
}