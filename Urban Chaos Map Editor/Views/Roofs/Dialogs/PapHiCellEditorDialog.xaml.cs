// /Views/Dialogs/PapHiCellEditorDialog.xaml.cs
// Interactive editor for individual PAP_HI cells.
// Uses game-space coordinates (matching walkable X/Z values).
//
// PAP_Hi struct (6 bytes per tile, from pap.h):
//   +0-1  Texture  (UWORD)
//   +2-3  Flags    (UWORD)
//   +4    Height   (SBYTE) - terrain vertex height
//   +5    Alt      (SBYTE) - floor altitude (world = Alt << 3)
//
// File layout: 8-byte header, then 128*128 tiles in row-major order.
// Game coordinate (gx, gz) maps to file index: gx * 128 + gz
// (matches PAP_hi[gx][gz] in the C source)

using System.Text;
using System.Windows;
using System.Windows.Controls;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Views.Prims.Dialogs;

namespace UrbanChaosMapEditor.Views.Roofs.Dialogs
{
    public partial class PapHiCellEditorDialog : Window
    {
        // -- constants ------------------------------------------------
        private const int HeaderBytes = 8;
        private const int BytesPerTile = 6;
        private const int TilesPerSide = 128;  // PAP_SIZE_HI

        // Byte offsets within a single tile
        private const int Off_Texture = 0; // 2 bytes (UWORD)
        private const int Off_Flags = 2; // 2 bytes (UWORD)
        private const int Off_Height = 4; // 1 byte  (SBYTE)
        private const int Off_Alt = 5; // 1 byte  (SBYTE)

        // -- state ----------------------------------------------------
        private int _loadedGameX = -1;
        private int _loadedGameZ = -1;
        private bool _cellLoaded;
        private bool _suppressFlagSync; // prevents infinite loop between hex field and checkboxes

        // All 16 flag checkboxes in bit order for easy iteration
        private CheckBox[] _flagCheckBoxes;

        // -- constructor ----------------------------------------------
        public PapHiCellEditorDialog()
        {
            InitializeComponent();
            _flagCheckBoxes = new CheckBox[]
            {
                ChkShadow1, ChkShadow2, ChkShadow3, ChkReflective,
                ChkHidden,  ChkSinkSquare, ChkSinkPoint, ChkNoUpper,
                ChkNoGo,    ChkRoofExists, ChkZone1, ChkZone2,
                ChkZone3,   ChkZone4, ChkFlatRoof, ChkWater
            };
        }

        /// <summary>
        /// Optional: pre-set the coordinates before showing the dialog.
        /// Useful when called from a building inspector that knows the tile.
        /// </summary>
        public void PresetCoordinates(int gameX, int gameZ)
        {
            TxtGameX.Text = gameX.ToString();
            TxtGameZ.Text = gameZ.ToString();
        }

        // --------------------------------------------------------------
        //  FILE ? GAME COORDINATE MAPPING
        // --------------------------------------------------------------

        /// <summary>
        /// Returns the byte offset in the .iam file for a PAP_HI cell
        /// at game coordinates (gx, gz).
        /// PAP_hi is declared as PAP_Hi[128][128] in C, so
        /// PAP_hi[gx][gz] ? index = gx * 128 + gz.
        /// </summary>
        private static int CalcTileOffset(int gx, int gz)
        {
            int index = gx * TilesPerSide + gz;
            return HeaderBytes + index * BytesPerTile;
        }

        private static bool IsValidCoord(int v) => v >= 0 && v < TilesPerSide;

        // --------------------------------------------------------------
        //  LOAD
        // --------------------------------------------------------------

        private void BtnLoad_Click(object sender, RoutedEventArgs e) => LoadCell();

        private void LoadCell()
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "PAP_HI Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtGameX.Text.Trim(), out int gx) ||
                !int.TryParse(TxtGameZ.Text.Trim(), out int gz))
            {
                MessageBox.Show("Enter valid integer coordinates.", "Invalid Input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsValidCoord(gx) || !IsValidCoord(gz))
            {
                MessageBox.Show("Coordinates must be 0–127.", "Out of Range",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var bytes = MapDataService.Instance.GetBytesCopy();
            int offset = CalcTileOffset(gx, gz);

            if (offset + BytesPerTile > bytes.Length)
            {
                MessageBox.Show("Offset exceeds file size — file may be truncated.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Read 6 raw bytes
            byte[] raw = new byte[BytesPerTile];
            Buffer.BlockCopy(bytes, offset, raw, 0, BytesPerTile);

            // Parse fields
            ushort texture = (ushort)(raw[0] | (raw[1] << 8));
            ushort flags = (ushort)(raw[2] | (raw[3] << 8));
            sbyte height = unchecked((sbyte)raw[4]);
            sbyte alt = unchecked((sbyte)raw[5]);

            // Populate UI ---------------------------------------------
            TxtRawHex.Text = string.Join("  ", raw.Select(b => $"{b:X2}"));
            TxtFileInfo.Text = $"File offset: 0x{offset:X8}  |  Index: {gx * TilesPerSide + gz}  |  Game ({gx}, {gz})";

            TxtTexture.Text = texture.ToString();
            TxtFlagsHex.Text = $"{flags:X4}";
            TxtHeight.Text = height.ToString();
            TxtAlt.Text = alt.ToString();

            TxtHeightWorld.Text = $"(world ˜ {height * 64})";
            TxtAltWorld.Text = $"world = {alt << 3}  (Alt << 3)";

            // Sync flag checkboxes
            SyncCheckBoxesFromFlags(flags);

            // Pre-fill batch region from loaded cell
            if (!_cellLoaded)
            {
                TxtBatchX1.Text = gx.ToString();
                TxtBatchZ1.Text = gz.ToString();
                TxtBatchX2.Text = gx.ToString();
                TxtBatchZ2.Text = gz.ToString();
            }

            _loadedGameX = gx;
            _loadedGameZ = gz;
            _cellLoaded = true;

            // Context: find walkables / RF4 covering this cell
            BuildContextInfo(gx, gz);
        }

        // --------------------------------------------------------------
        //  CONTEXT — walkable / RF4 lookup
        // --------------------------------------------------------------

        private void BuildContextInfo(int gx, int gz)
        {
            var sb = new StringBuilder();
            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();

            bool foundWalkable = false;
            bool foundRf4 = false;

            if (snap.Walkables != null)
            {
                for (int wIdx = 1; wIdx < snap.Walkables.Length; wIdx++)
                {
                    var w = snap.Walkables[wIdx];
                    if (gx >= w.X1 && gx < w.X2 && gz >= w.Z1 && gz < w.Z2)
                    {
                        foundWalkable = true;
                        int rf4Count = w.EndFace4 - w.StartFace4;
                        sb.AppendLine($"Inside Walkable #{wIdx}  Bld#{w.Building}  Y={w.Y} (world {w.Y * 32})  StoreyY={w.StoreyY}  RF4=[{w.StartFace4}..{w.EndFace4}) = {rf4Count}");

                        // Check RF4 entries at this exact tile
                        if (snap.RoofFaces4 != null)
                        {
                            for (int ri = w.StartFace4; ri < w.EndFace4 && ri < snap.RoofFaces4.Length; ri++)
                            {
                                var rf = snap.RoofFaces4[ri];
                                int rfX = rf.RX;
                                int rfZ = rf.RZ - 128;
                                if (rfX == gx && rfZ == gz)
                                {
                                    foundRf4 = true;
                                    sb.AppendLine($"  RF4#{ri}: Y={rf.Y} DY=({rf.DY0},{rf.DY1},{rf.DY2}) DrawFlags=0x{rf.DrawFlags:X2} Next={rf.Next}");
                                }
                            }
                        }
                    }
                }
            }

            if (!foundWalkable)
                sb.AppendLine("Not inside any walkable.");
            if (!foundRf4 && foundWalkable)
                sb.AppendLine("No RF4 entry at this exact tile.");

            TxtContext.Text = sb.ToString().TrimEnd();
        }

        // --------------------------------------------------------------
        //  FLAG SYNC — checkboxes ? hex field
        // --------------------------------------------------------------

        private void SyncCheckBoxesFromFlags(ushort flags)
        {
            _suppressFlagSync = true;
            for (int bit = 0; bit < 16; bit++)
                _flagCheckBoxes[bit].IsChecked = (flags & (1 << bit)) != 0;
            _suppressFlagSync = false;
        }

        private ushort ReadFlagsFromCheckBoxes()
        {
            ushort flags = 0;
            for (int bit = 0; bit < 16; bit++)
            {
                if (_flagCheckBoxes[bit].IsChecked == true)
                    flags |= (ushort)(1 << bit);
            }
            return flags;
        }

        private void FlagCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressFlagSync) return;

            ushort flags = ReadFlagsFromCheckBoxes();
            _suppressFlagSync = true;
            TxtFlagsHex.Text = $"{flags:X4}";
            _suppressFlagSync = false;
        }

        private void TxtFlagsHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressFlagSync) return;

            string text = TxtFlagsHex.Text.Trim().Replace("0x", "").Replace("0X", "");
            if (ushort.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out ushort flags))
                SyncCheckBoxesFromFlags(flags);
        }

        // --------------------------------------------------------------
        //  NAVIGATION
        // --------------------------------------------------------------

        private void Navigate(int dx, int dz)
        {
            if (!_cellLoaded) return;
            int nx = _loadedGameX + dx;
            int nz = _loadedGameZ + dz;
            if (!IsValidCoord(nx) || !IsValidCoord(nz)) return;
            TxtGameX.Text = nx.ToString();
            TxtGameZ.Text = nz.ToString();
            LoadCell();
        }

        private void BtnLeft_Click(object sender, RoutedEventArgs e) => Navigate(-1, 0);
        private void BtnRight_Click(object sender, RoutedEventArgs e) => Navigate(1, 0);
        private void BtnUp_Click(object sender, RoutedEventArgs e) => Navigate(0, -1);
        private void BtnDown_Click(object sender, RoutedEventArgs e) => Navigate(0, 1);

        // --------------------------------------------------------------
        //  APPLY — write single cell
        // --------------------------------------------------------------

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (!_cellLoaded)
            {
                MessageBox.Show("Load a cell first.", "Nothing Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parse all edited fields
            if (!ushort.TryParse(TxtTexture.Text.Trim(), out ushort texture))
            {
                MessageBox.Show("Texture must be 0–65535.", "Invalid", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string flagsText = TxtFlagsHex.Text.Trim().Replace("0x", "").Replace("0X", "");
            if (!ushort.TryParse(flagsText, System.Globalization.NumberStyles.HexNumber, null, out ushort flags))
            {
                MessageBox.Show("Flags must be a hex value (e.g. 0210).", "Invalid", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtHeight.Text.Trim(), out int heightInt) || heightInt < -128 || heightInt > 127)
            {
                MessageBox.Show("Height must be -128..127.", "Invalid", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtAlt.Text.Trim(), out int altInt) || altInt < -128 || altInt > 127)
            {
                MessageBox.Show("Alt must be -128..127.", "Invalid", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            sbyte height = (sbyte)heightInt;
            sbyte alt = (sbyte)altInt;

            // Build the 6-byte tile
            byte[] tile = new byte[BytesPerTile];
            tile[0] = (byte)(texture & 0xFF);
            tile[1] = (byte)(texture >> 8);
            tile[2] = (byte)(flags & 0xFF);
            tile[3] = (byte)(flags >> 8);
            tile[4] = unchecked((byte)height);
            tile[5] = unchecked((byte)alt);

            // Write to file buffer
            var bytes = MapDataService.Instance.GetBytesCopy();
            int offset = CalcTileOffset(_loadedGameX, _loadedGameZ);
            Buffer.BlockCopy(tile, 0, bytes, offset, BytesPerTile);
            MapDataService.Instance.ReplaceBytes(bytes);

            TxtBatchStatus.Text = $"Applied to ({_loadedGameX}, {_loadedGameZ}) at 0x{offset:X8}";

            // Refresh the raw hex display
            TxtRawHex.Text = string.Join("  ", tile.Select(b => $"{b:X2}"));
            TxtAltWorld.Text = $"world = {alt << 3}  (Alt << 3)";
            TxtHeightWorld.Text = $"(world ˜ {height * 64})";
        }

        // --------------------------------------------------------------
        //  BATCH — region operations
        // --------------------------------------------------------------

        private bool TryParseBatchRegion(out int x1, out int z1, out int x2, out int z2)
        {
            x1 = x2 = z1 = z2 = 0;
            if (!int.TryParse(TxtBatchX1.Text.Trim(), out x1) ||
                !int.TryParse(TxtBatchZ1.Text.Trim(), out z1) ||
                !int.TryParse(TxtBatchX2.Text.Trim(), out x2) ||
                !int.TryParse(TxtBatchZ2.Text.Trim(), out z2))
            {
                MessageBox.Show("Enter valid integer coordinates for the batch region.",
                    "Invalid Region", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Normalise so x1<=x2, z1<=z2
            if (x1 > x2) (x1, x2) = (x2, x1);
            if (z1 > z2) (z1, z2) = (z2, z1);

            // Clamp to valid range
            x1 = Math.Max(0, x1); z1 = Math.Max(0, z1);
            x2 = Math.Min(TilesPerSide - 1, x2); z2 = Math.Min(TilesPerSide - 1, z2);

            int count = (x2 - x1 + 1) * (z2 - z1 + 1);
            if (count > 4096)
            {
                MessageBox.Show($"Region too large ({count} cells). Max 4096.", "Too Large",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Modifies flags for every cell in the batch region.
        /// orMask is OR'd in, andMask is AND'd (use 0xFFFF to keep all bits).
        /// </summary>
        private int BatchModifyFlags(int x1, int z1, int x2, int z2, ushort orMask, ushort andMask)
        {
            var bytes = MapDataService.Instance.GetBytesCopy();
            int count = 0;

            for (int gx = x1; gx <= x2; gx++)
            {
                for (int gz = z1; gz <= z2; gz++)
                {
                    int off = CalcTileOffset(gx, gz) + Off_Flags;
                    if (off + 1 >= bytes.Length) continue;

                    ushort flags = (ushort)(bytes[off] | (bytes[off + 1] << 8));
                    flags = (ushort)((flags & andMask) | orMask);
                    bytes[off] = (byte)(flags & 0xFF);
                    bytes[off + 1] = (byte)(flags >> 8);
                    count++;
                }
            }

            MapDataService.Instance.ReplaceBytes(bytes);
            return count;
        }

        private void BtnBatchSetHiddenRoof_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseBatchRegion(out int x1, out int z1, out int x2, out int z2)) return;

            // PAP_FLAG_HIDDEN (0x0010) | PAP_FLAG_ROOF_EXISTS (0x0200)
            const ushort mask = 0x0010 | 0x0200;

            int count = BatchModifyFlags(x1, z1, x2, z2, orMask: mask, andMask: 0xFFFF);
            TxtBatchStatus.Text = $"Set Hidden+RoofExists on {count} cells ({x1},{z1})?({x2},{z2})";

            // Reload current cell if it was in range
            if (_cellLoaded) LoadCell();
        }

        private void BtnBatchClearHiddenRoof_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseBatchRegion(out int x1, out int z1, out int x2, out int z2)) return;

            const ushort clearMask = unchecked((ushort)~(0x0010 | 0x0200));

            int count = BatchModifyFlags(x1, z1, x2, z2, orMask: 0, andMask: clearMask);
            TxtBatchStatus.Text = $"Cleared Hidden+RoofExists on {count} cells ({x1},{z1})?({x2},{z2})";

            if (_cellLoaded) LoadCell();
        }

        private void BtnBatchSetAlt_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseBatchRegion(out int x1, out int z1, out int x2, out int z2)) return;

            if (!int.TryParse(TxtAlt.Text.Trim(), out int altInt) || altInt < -128 || altInt > 127)
            {
                MessageBox.Show("The Alt field above must contain a valid value (-128..127).",
                    "Invalid Alt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            byte altByte = unchecked((byte)(sbyte)altInt);

            var bytes = MapDataService.Instance.GetBytesCopy();
            int count = 0;

            for (int gx = x1; gx <= x2; gx++)
            {
                for (int gz = z1; gz <= z2; gz++)
                {
                    int off = CalcTileOffset(gx, gz) + Off_Alt;
                    if (off >= bytes.Length) continue;
                    bytes[off] = altByte;
                    count++;
                }
            }

            MapDataService.Instance.ReplaceBytes(bytes);
            TxtBatchStatus.Text = $"Set Alt={altInt} (world {altInt << 3}) on {count} cells ({x1},{z1})?({x2},{z2})";

            if (_cellLoaded) LoadCell();
        }

        private void BtnBatchCopyFlags_Click(object sender, RoutedEventArgs e)
        {
            if (!_cellLoaded)
            {
                MessageBox.Show("Load a cell first to copy its flags.", "Nothing Loaded",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseBatchRegion(out int x1, out int z1, out int x2, out int z2)) return;

            string flagsText = TxtFlagsHex.Text.Trim().Replace("0x", "").Replace("0X", "");
            if (!ushort.TryParse(flagsText, System.Globalization.NumberStyles.HexNumber, null, out ushort flags))
            {
                MessageBox.Show("Flags hex value is invalid.", "Invalid", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var bytes = MapDataService.Instance.GetBytesCopy();
            int count = 0;

            for (int gx = x1; gx <= x2; gx++)
            {
                for (int gz = z1; gz <= z2; gz++)
                {
                    int off = CalcTileOffset(gx, gz) + Off_Flags;
                    if (off + 1 >= bytes.Length) continue;
                    bytes[off] = (byte)(flags & 0xFF);
                    bytes[off + 1] = (byte)(flags >> 8);
                    count++;
                }
            }

            MapDataService.Instance.ReplaceBytes(bytes);
            TxtBatchStatus.Text = $"Copied flags 0x{flags:X4} to {count} cells ({x1},{z1})?({x2},{z2})";

            if (_cellLoaded) LoadCell();
        }

        // --------------------------------------------------------------
        //  CLOSE
        // --------------------------------------------------------------

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}