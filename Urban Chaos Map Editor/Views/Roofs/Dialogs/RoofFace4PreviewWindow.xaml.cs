﻿// Views/Roofs/Dialogs/RoofFace4PreviewWindow.xaml.cs
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using UrbanChaosMapEditor.Models.Buildings;
using System.Windows;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Roofs;

namespace UrbanChaosMapEditor.Views.Roofs.Dialogs
{
    public partial class RoofFace4PreviewWindow : Window
    {
        private const int RF4_SIZE = 10;
        private const int DWalkableSize = 22;
        private const int PAP_HEADER = 8;
        private const int PAP_TILE_SIZE = 6;
        private const int PAP_TILES = 128;

        private readonly int _index;
        private readonly RoofFace4Rec _rf;
        private readonly int _walkableId1;  // parent walkable (for Apply to All)
        private CheckBox[] _drawFlagChecks = null!;
        private CheckBox[] _papFlagChecks = null!;

        /// <summary>
        /// Creates the RF4 editor.
        /// </summary>
        /// <param name="index">0-based RF4 index in the RoofFace4 array</param>
        /// <param name="rf">The RF4 record snapshot</param>
        /// <param name="walkableId1">1-based walkable ID that owns this RF4 (for Apply to All). Pass 0 if unknown.</param>
        public RoofFace4PreviewWindow(int index, RoofFace4Rec rf, int walkableId1 = 0)
        {
            InitializeComponent();

            _index = index;
            _rf = rf;
            _walkableId1 = walkableId1;

            _drawFlagChecks = new CheckBox[]
            {
                ChkDraw0, ChkDraw1, ChkDraw2, ChkDraw3,
                ChkDraw4, ChkDraw5, ChkDraw6, ChkDraw7
            };

            _papFlagChecks = new CheckBox[]
            {
                ChkPap0,  ChkPap1,  ChkPap2,  ChkPap3,
                ChkPap4,  ChkPap5,  ChkPap6,  ChkPap7,
                ChkPap8,  ChkPap9,  ChkPap10, ChkPap11,
                ChkPap12, ChkPap13, ChkPap14, ChkPap15
            };

            Title = $"Roof Tile Editor - #{index}";
            HeaderTextBlock.Text = $"Roof Tile #{index}";

            bool anyDy = rf.DY0 != 0 || rf.DY1 != 0 || rf.DY2 != 0;
            int gameZ = rf.RZ - 128;
            int tileX = rf.RX & 0x7F;
            DetailsTextBlock.Text =
                $"Tile ({tileX}, {gameZ})  |  Sloped: {(anyDy ? "Yes" : "No")}  |  " +
                $"DrawFlags: 0x{rf.DrawFlags:X2}  |  Next: {rf.Next}" +
                (walkableId1 > 0 ? $"  |  Walkable #{walkableId1}" : "");

            TxtPapCoords.Text = $"Game tile ({tileX}, {gameZ})";

            LoadRf4Fields();
            LoadPapHiFlags();
            UpdateSplitPreview();
        }

        // ====================================================================
        // Load
        // ====================================================================

        private void LoadRf4Fields()
        {
            // Y is stored as raw RF4 units; display in Quarter Storeys (1 QS = 64 raw)
            TxtY.Text = (_rf.Y / 64).ToString();
            TxtDY0.Text = _rf.DY0.ToString();
            TxtDY1.Text = _rf.DY1.ToString();
            TxtDY2.Text = _rf.DY2.ToString();

            // RX: bits 0-6 = tile X (0-127), bit 7 = diagonal flag
            TxtTileX.Text = (_rf.RX & 0x7F).ToString();
            ChkDiagonal.IsChecked = (_rf.RX & 0x80) != 0;

            // RZ: bits 0-6 = tile Z (0-127), bit 7 = always set in original maps
            TxtTileZ.Text = (_rf.RZ & 0x7F).ToString();
            ChkRzBit7.IsChecked = (_rf.RZ & 0x80) != 0;

            TxtNext.Text = _rf.Next.ToString();

            SyncDrawFlagChecks(_rf.DrawFlags);
        }

        private void LoadPapHiFlags()
        {
            int gameX = _rf.RX & 0x7F;
            int gameZ = _rf.RZ - 128;

            if (gameX < 0 || gameX >= PAP_TILES || gameZ < 0 || gameZ >= PAP_TILES)
                return;

            var bytes = MapDataService.Instance.GetBytesCopy();
            int tileIndex = gameX * PAP_TILES + gameZ;
            int offset = PAP_HEADER + tileIndex * PAP_TILE_SIZE;

            if (offset + PAP_TILE_SIZE > bytes.Length)
                return;

            ushort flags = (ushort)(bytes[offset + 2] | (bytes[offset + 3] << 8));
            SyncPapFlagChecks(flags);
        }

        // ====================================================================
        // DrawFlags sync (8-bit)
        // ====================================================================

        private void SyncDrawFlagChecks(byte flags)
        {
            for (int i = 0; i < 8; i++)
                _drawFlagChecks[i].IsChecked = (flags & (1 << i)) != 0;
        }

        private byte ReadDrawFlagChecks()
        {
            byte flags = 0;
            for (int i = 0; i < 8; i++)
                if (_drawFlagChecks[i].IsChecked == true)
                    flags |= (byte)(1 << i);
            return flags;
        }

        private void DrawFlagCheck_Changed(object sender, RoutedEventArgs e) { }

        // ====================================================================
        // Split direction preview
        // ====================================================================

        private void UpdateSplitPreview()
        {
            bool alternate = ChkDiagonal.IsChecked == true;
            LineSplitA.Visibility = alternate ? Visibility.Visible : Visibility.Collapsed;
            LineSplitB.Visibility = alternate ? Visibility.Collapsed : Visibility.Visible;
            TxtSplitLabel.Text    = alternate ? "NW → SE" : "NE → SW";
        }

        private void ChkDiagonal_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSplitPreview();
        }

        // ====================================================================
        // PAP_HI flags sync (16-bit)
        // ====================================================================

        private void SyncPapFlagChecks(ushort flags)
        {
            for (int i = 0; i < 16; i++)
                _papFlagChecks[i].IsChecked = (flags & (1 << i)) != 0;
        }

        private ushort ReadPapFlagChecks()
        {
            ushort flags = 0;
            for (int i = 0; i < 16; i++)
                if (_papFlagChecks[i].IsChecked == true)
                    flags |= (ushort)(1 << i);
            return flags;
        }

        private void PapFlagCheck_Changed(object sender, RoutedEventArgs e) { }

        // ====================================================================
        // Parsing helpers
        // ====================================================================

        private bool TryParseRf4Fields(out short y, out sbyte dy0, out sbyte dy1, out sbyte dy2,
                                        out byte rx, out byte rz, out short next, out byte drawFlags)
        {
            y = 0; dy0 = dy1 = dy2 = 0; rx = rz = drawFlags = 0; next = 0;

            // Y is entered in Quarter Storeys; convert to raw RF4 units (1 QS = 64 raw)
            if (!short.TryParse(TxtY.Text, out short yQS) ||
                !sbyte.TryParse(TxtDY0.Text, out dy0) ||
                !sbyte.TryParse(TxtDY1.Text, out dy1) ||
                !sbyte.TryParse(TxtDY2.Text, out dy2) ||
                !byte.TryParse(TxtTileX.Text, out byte tileX) ||
                !byte.TryParse(TxtTileZ.Text, out byte tileZ) ||
                !short.TryParse(TxtNext.Text, out next))
                return false;

            if (tileX > 127 || tileZ > 127) return false;

            y = (short)(yQS * 64);

            // Repack RX: bits 0-6 = X, bit 7 = diagonal flag
            rx = (byte)(tileX | (ChkDiagonal.IsChecked == true ? 0x80 : 0x00));
            // Repack RZ: bits 0-6 = Z, bit 7 = RZ.b7 flag
            rz = (byte)(tileZ | (ChkRzBit7.IsChecked == true ? 0x80 : 0x00));

            drawFlags = ReadDrawFlagChecks();
            return true;
        }

        private bool TryParsePapFlags(out ushort papFlags)
        {
            papFlags = ReadPapFlagChecks();
            return true;
        }

        /// <summary>
        /// Calculates the RF4 data start offset in the file.
        /// Returns -1 on failure.
        /// </summary>
        private int GetRf4DataOffset(byte[] bytes, int walkablesStart)
        {
            if (walkablesStart < 0 || walkablesStart + 4 > bytes.Length) return -1;
            ushort nextDWalkable = (ushort)(bytes[walkablesStart] | (bytes[walkablesStart + 1] << 8));
            return walkablesStart + 4 + (nextDWalkable * DWalkableSize);
        }

        /// <summary>
        /// Writes a single RF4 record at the given offset.
        /// </summary>
        private void WriteRf4(byte[] bytes, int rf4Off, short y, sbyte dy0, sbyte dy1, sbyte dy2,
                              byte drawFlags, byte rx, byte rz, short next)
        {
            bytes[rf4Off + 0] = (byte)(y & 0xFF);
            bytes[rf4Off + 1] = (byte)((y >> 8) & 0xFF);
            bytes[rf4Off + 2] = unchecked((byte)dy0);
            bytes[rf4Off + 3] = unchecked((byte)dy1);
            bytes[rf4Off + 4] = unchecked((byte)dy2);
            bytes[rf4Off + 5] = drawFlags;
            bytes[rf4Off + 6] = rx;
            bytes[rf4Off + 7] = rz;
            bytes[rf4Off + 8] = (byte)(next & 0xFF);
            bytes[rf4Off + 9] = (byte)((next >> 8) & 0xFF);
        }

        /// <summary>
        /// Writes PAP_HI flags for a single tile (only bytes +2/+3).
        /// </summary>
        private void WritePapFlags(byte[] bytes, int gameX, int gameZ, ushort papFlags)
        {
            if (gameX < 0 || gameX >= PAP_TILES || gameZ < 0 || gameZ >= PAP_TILES) return;
            int tileIndex = gameX * PAP_TILES + gameZ;
            int papOff = PAP_HEADER + tileIndex * PAP_TILE_SIZE;
            if (papOff + PAP_TILE_SIZE > bytes.Length) return;
            bytes[papOff + 2] = (byte)(papFlags & 0xFF);
            bytes[papOff + 3] = (byte)(papFlags >> 8);
        }

        // ====================================================================
        // Apply � write single RF4 + PAP_HI flags
        // ====================================================================

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            ApplySingleRf4();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (ApplySingleRf4())
                Close();
        }

        // ====================================================================
        // Apply to All � write Y, DY, DrawFlags, PAP flags to all RF4s in walkable
        // ====================================================================

        private bool ApplySingleRf4()
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!TryParseRf4Fields(out var y, out var dy0, out var dy1, out var dy2,
                                    out var rx, out var rz, out var next, out var drawFlags))
            {
                MessageBox.Show("Invalid RF4 field values.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            TryParsePapFlags(out var papFlags);

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();
            if (snap.WalkablesStart < 0)
            {
                MessageBox.Show("Cannot locate walkables data.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            var bytes = svc.GetBytesCopy();
            int rf4DataOff = GetRf4DataOffset(bytes, snap.WalkablesStart);
            int rf4Off = rf4DataOff + (_index * RF4_SIZE);

            if (rf4Off + RF4_SIZE > bytes.Length)
            {
                MessageBox.Show("RF4 offset out of range.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            WriteRf4(bytes, rf4Off, y, dy0, dy1, dy2, drawFlags, rx, rz, next);

            int gameX = rx & 0x7F;
            int gameZ = rz - 128;
            WritePapFlags(bytes, gameX, gameZ, papFlags);

            Debug.WriteLine($"[RF4Editor] Applied RF4 #{_index}: Y={y / 64} QS (raw={y}) DY=({dy0},{dy1},{dy2}) DrawFlags=0x{drawFlags:X2} RX={rx} RZ={rz}");

            svc.ReplaceBytes(bytes);
            svc.MarkDirty();

            RoofsChangeBus.Instance.NotifyChanged();
            BuildingsChangeBus.Instance.NotifyChanged();

            SetStatus($"Updated RoofFace4 #{_index}");
            return true;
        }

        private void BtnApplyToSelection_Click(object sender, RoutedEventArgs e)
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseRf4Fields(out var y, out var dy0, out var dy1, out var dy2,
                                    out _, out _, out _, out var drawFlags))
            {
                MessageBox.Show("Invalid RF4 field values.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TryParsePapFlags(out var papFlags);

            WindowState = WindowState.Minimized;

            // Restore window whether selection completes or is cancelled
            void onEnded(object? s, EventArgs a)
            {
                RoofTileSelectionService.Instance.SelectionEnded -= onEnded;
                Dispatcher.Invoke(() => WindowState = WindowState.Normal);
            }
            RoofTileSelectionService.Instance.SelectionEnded += onEnded;

            RoofTileSelectionService.Instance.BeginSelection(rect =>
                Dispatcher.Invoke(() => ApplyToSelectionRect(rect, y, dy0, dy1, dy2, drawFlags, papFlags)));
        }

        private void ApplyToSelectionRect(Rect selRect, short y, sbyte dy0, sbyte dy1, sbyte dy2,
                                           byte drawFlags, ushort papFlags)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded) return;

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();
            if (snap.WalkablesStart < 0 || snap.Walkables == null || snap.RoofFaces4 == null) return;

            // Resolve walkable range (same logic as Apply to All)
            int startFace4 = -1, endFace4 = -1, walkableId = _walkableId1;
            if (walkableId > 0 && walkableId < snap.Walkables.Length)
            {
                startFace4 = snap.Walkables[walkableId].StartFace4;
                endFace4   = snap.Walkables[walkableId].EndFace4;
            }
            else
            {
                for (int i = 1; i < snap.Walkables.Length; i++)
                {
                    var w = snap.Walkables[i];
                    if (_index >= w.StartFace4 && _index < w.EndFace4)
                    {
                        startFace4 = w.StartFace4; endFace4 = w.EndFace4; walkableId = i; break;
                    }
                }
            }

            if (startFace4 < 0 || endFace4 <= startFace4)
            {
                MessageBox.Show("Cannot determine parent walkable.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var bytes = svc.GetBytesCopy();
            int rf4DataOff = GetRf4DataOffset(bytes, snap.WalkablesStart);
            int updated = 0;

            for (int i = startFace4; i < endFace4 && i < snap.RoofFaces4.Length; i++)
            {
                if (i < 1) continue;
                var rf4 = snap.RoofFaces4[i];

                int tileX = rf4.RX & 0x7F;
                int tileZ = rf4.RZ - 128;
                if (tileX < 0 || tileX > 127 || tileZ < 0 || tileZ > 127) continue;

                // Tile center in map UI coordinates — matches col/row grid used by selection.
                double cx = (128 - tileX - 1) * 64.0 + 32;
                double cz = (128 - tileZ - 1) * 64.0 + 32;
                if (!selRect.Contains(new Point(cx, cz))) continue;

                int rf4Off = rf4DataOff + i * RF4_SIZE;
                if (rf4Off + RF4_SIZE > bytes.Length) continue;

                byte existingRX   = bytes[rf4Off + 6];
                byte existingRZ   = bytes[rf4Off + 7];
                short existingNext = (short)(bytes[rf4Off + 8] | (bytes[rf4Off + 9] << 8));

                WriteRf4(bytes, rf4Off, y, dy0, dy1, dy2, drawFlags, existingRX, existingRZ, existingNext);
                WritePapFlags(bytes, tileX, tileZ, papFlags);
                updated++;
            }

            if (updated == 0)
            {
                MessageBox.Show("No tiles found in the selected area.", "Apply to Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            svc.ReplaceBytes(bytes);
            svc.MarkDirty();
            RoofsChangeBus.Instance.NotifyChanged();
            BuildingsChangeBus.Instance.NotifyChanged();

            SetStatus($"Applied to {updated} RF4 tile(s) in selection");
            MessageBox.Show($"Updated {updated} RF4 tile(s) in the selection.", "Apply to Selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnApplyToAll_Click(object sender, RoutedEventArgs e)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseRf4Fields(out var y, out var dy0, out var dy1, out var dy2,
                                    out _, out _, out _, out var drawFlags))
            {
                MessageBox.Show("Invalid RF4 field values.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TryParsePapFlags(out var papFlags);

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();
            if (snap.WalkablesStart < 0 || snap.Walkables == null || snap.RoofFaces4 == null)
            {
                MessageBox.Show("Cannot locate walkables data.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Find the parent walkable
            int startFace4 = -1, endFace4 = -1;
            int walkableId = _walkableId1;

            if (walkableId > 0 && walkableId < snap.Walkables.Length)
            {
                var w = snap.Walkables[walkableId];
                startFace4 = w.StartFace4;
                endFace4 = w.EndFace4;
            }
            else
            {
                // Find walkable by searching which range contains _index
                for (int i = 1; i < snap.Walkables.Length; i++)
                {
                    var w = snap.Walkables[i];
                    if (_index >= w.StartFace4 && _index < w.EndFace4)
                    {
                        startFace4 = w.StartFace4;
                        endFace4 = w.EndFace4;
                        walkableId = i;
                        break;
                    }
                }
            }

            if (startFace4 < 0 || endFace4 <= startFace4)
            {
                MessageBox.Show("Cannot determine parent walkable for this RF4 entry.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int tileCount = endFace4 - startFace4;
            var confirm = MessageBox.Show(
                $"Apply to all {tileCount} RF4 tiles in Walkable #{walkableId}?\n\n" +
                $"RF4 range: [{startFace4}..{endFace4})\n\n" +
                $"This will set:\n" +
                $"  Y = {y / 64} QS, DY = ({dy0},{dy1},{dy2})\n" +
                $"  DrawFlags = 0x{drawFlags:X2}\n" +
                $"  PAP_HI Flags = 0x{papFlags:X4}\n\n" +
                "RX, RZ, and Next are NOT changed (they're per-tile).",
                "Confirm Apply to All",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            var bytes = svc.GetBytesCopy();
            int rf4DataOff = GetRf4DataOffset(bytes, snap.WalkablesStart);

            int updated = 0;
            for (int i = startFace4; i < endFace4 && i < snap.RoofFaces4.Length; i++)
            {
                int rf4Off = rf4DataOff + (i * RF4_SIZE);
                if (rf4Off + RF4_SIZE > bytes.Length) continue;

                // Read existing RX, RZ, Next � keep them per-tile
                byte existingRX = bytes[rf4Off + 6];
                byte existingRZ = bytes[rf4Off + 7];
                short existingNext = (short)(bytes[rf4Off + 8] | (bytes[rf4Off + 9] << 8));

                // Write Y, DY, DrawFlags (shared), keep RX/RZ/Next (per-tile)
                WriteRf4(bytes, rf4Off, y, dy0, dy1, dy2, drawFlags, existingRX, existingRZ, existingNext);

                // Write PAP_HI flags for this tile's coordinates
                int tileGameX = existingRX & 0x7F;
                int tileGameZ = existingRZ - 128;
                WritePapFlags(bytes, tileGameX, tileGameZ, papFlags);

                updated++;
            }

            Debug.WriteLine($"[RF4Editor] Applied to all: {updated} RF4 tiles in Walkable #{walkableId}, Y={y / 64} QS (raw={y}) DrawFlags=0x{drawFlags:X2} PapFlags=0x{papFlags:X4}");

            svc.ReplaceBytes(bytes);
            svc.MarkDirty();

            RoofsChangeBus.Instance.NotifyChanged();
            BuildingsChangeBus.Instance.NotifyChanged();

            MessageBox.Show($"Updated {updated} RF4 tiles in Walkable #{walkableId}.", "Applied to All",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SetStatus(string message)
        {
            if (Application.Current.MainWindow?.DataContext is UrbanChaosMapEditor.ViewModels.Core.MainWindowViewModel mainVm)
                mainVm.StatusMessage = message;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}