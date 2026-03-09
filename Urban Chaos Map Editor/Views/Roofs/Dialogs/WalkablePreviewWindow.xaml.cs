using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;

namespace UrbanChaosMapEditor.Views.Roofs.Dialogs
{
    public partial class WalkablePreviewWindow : Window
    {
        private readonly int _walkableId1;
        private readonly DWalkableRec _walkable;
        private readonly RoofFace4Rec[] _roofFaces4;

        public WalkablePreviewWindow(int walkableId1, DWalkableRec walkable, RoofFace4Rec[] roofFaces4)
        {
            InitializeComponent();

            _walkableId1 = walkableId1;
            _walkable = walkable;
            _roofFaces4 = roofFaces4 ?? Array.Empty<RoofFace4Rec>();

            Title = $"Walkable Preview – Walkable #{walkableId1}";

            HeaderTextBlock.Text = $"Walkable #{walkableId1}";
            DetailsTextBlock.Text =
                $"Building={_walkable.Building}  Rect=({_walkable.X1},{_walkable.Z1})?({_walkable.X2},{_walkable.Z2})  " +
                $"Y={_walkable.Y}  StoreyY={_walkable.StoreyY}  " +
                $"Face4=[{_walkable.StartFace4}..{_walkable.EndFace4})";

            TxtBuilding.Text = _walkable.Building.ToString(CultureInfo.InvariantCulture);
            TxtRect.Text = $"({_walkable.X1},{_walkable.Z1}) ? ({_walkable.X2},{_walkable.Z2})";
            TxtY.Text = _walkable.Y.ToString(CultureInfo.InvariantCulture);
            TxtStoreyY.Text = _walkable.StoreyY.ToString(CultureInfo.InvariantCulture);
            TxtStartFace4.Text = _walkable.StartFace4.ToString(CultureInfo.InvariantCulture);
            TxtEndFace4.Text = _walkable.EndFace4.ToString(CultureInfo.InvariantCulture);

            int faceCount = Math.Max(0, _walkable.EndFace4 - _walkable.StartFace4);
            TxtFace4Count.Text = faceCount.ToString(CultureInfo.InvariantCulture);

            TxtNext.Text = _walkable.Next.ToString(CultureInfo.InvariantCulture);

            // Populate editable legacy fields
            TxtStartPoint.Text = _walkable.StartPoint.ToString(CultureInfo.InvariantCulture);
            TxtEndPoint.Text = _walkable.EndPoint.ToString(CultureInfo.InvariantCulture);
            TxtStartFace3.Text = _walkable.StartFace3.ToString(CultureInfo.InvariantCulture);
            TxtEndFace3.Text = _walkable.EndFace3.ToString(CultureInfo.InvariantCulture);

            SeedRoofFacesSpan();
        }

        private sealed class RoofFace4Row
        {
            public int Index1 { get; init; }
            public short Y { get; init; }
            public string DY { get; init; } = "";
            public string FlagsHex { get; init; } = "";
            public byte RX { get; init; }
            public string RZHex { get; init; } = "";
            public short Next { get; init; }
            public string Sloped { get; init; } = "";
        }

        private void SeedRoofFacesSpan()
        {
            var rows = new List<RoofFace4Row>();

            int start = _walkable.StartFace4;
            int end = _walkable.EndFace4;

            // Guard rails: roof_faces4 length may include sentinel; we just bounds-check.
            start = Math.Max(0, start);
            end = Math.Max(start, end);
            end = Math.Min(end, _roofFaces4.Length);

            for (int i = start; i < end; i++)
            {
                var rf = _roofFaces4[i];

                bool anyDy = rf.DY0 != 0 || rf.DY1 != 0 || rf.DY2 != 0;
                bool rzSlopeBit = (rf.RZ & 0x80) != 0;

                rows.Add(new RoofFace4Row
                {
                    Index1 = i, // index-as-stored; if you want 1-based, use i+1 (but keep consistent with StartFace4)
                    Y = rf.Y,
                    DY = $"{rf.DY0},{rf.DY1},{rf.DY2}",
                    FlagsHex = $"0x{rf.DrawFlags:X2}",
                    RX = rf.RX,
                    RZHex = $"0x{rf.RZ:X2}",
                    Next = rf.Next,
                    Sloped = (anyDy || rzSlopeBit) ? "Yes" : "No"
                });
            }

            RoofFacesList.ItemsSource = rows;
        }

        private void RoofFacesList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (RoofFacesList.SelectedItem is not RoofFace4Row row)
                return;

            int idx = row.Index1;
            if (idx < 0 || idx >= _roofFaces4.Length)
                return;

            var dlg = new RoofFace4PreviewWindow(idx, _roofFaces4[idx])
            {
                Owner = this
            };
            dlg.Show();
        }

        private void BtnApplyLegacy_Click(object sender, RoutedEventArgs e)
        {
            // Parse values
            if (!ushort.TryParse(TxtStartPoint.Text.Trim(), out ushort startPoint))
            {
                TxtStatus.Text = "Invalid StartPoint value.";
                return;
            }
            if (!ushort.TryParse(TxtEndPoint.Text.Trim(), out ushort endPoint))
            {
                TxtStatus.Text = "Invalid EndPoint value.";
                return;
            }
            if (!ushort.TryParse(TxtStartFace3.Text.Trim(), out ushort startFace3))
            {
                TxtStatus.Text = "Invalid StartFace3 value.";
                return;
            }
            if (!ushort.TryParse(TxtEndFace3.Text.Trim(), out ushort endFace3))
            {
                TxtStatus.Text = "Invalid EndFace3 value.";
                return;
            }

            // Apply to map data
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
            {
                TxtStatus.Text = "No map loaded.";
                return;
            }

            var acc = new BuildingsAccessor(svc);
            if (!acc.TryGetDWalkableOffset(_walkableId1, out int walkableOffset))
            {
                TxtStatus.Text = $"Could not find walkable #{_walkableId1} offset.";
                return;
            }

            // DWalkable layout (22 bytes):
            // +0:  StartPoint (ushort)
            // +2:  EndPoint (ushort)
            // +4:  StartFace3 (ushort)
            // +6:  EndFace3 (ushort)
            // +8:  StartFace4 (ushort)
            // +10: EndFace4 (ushort)
            // +12: X1, +13: Z1, +14: X2, +15: Z2
            // +16: Y, +17: StoreyY
            // +18: Next (ushort)
            // +20: Building (ushort)

            svc.Edit(bytes =>
            {
                // StartPoint at offset +0
                bytes[walkableOffset + 0] = (byte)(startPoint & 0xFF);
                bytes[walkableOffset + 1] = (byte)((startPoint >> 8) & 0xFF);

                // EndPoint at offset +2
                bytes[walkableOffset + 2] = (byte)(endPoint & 0xFF);
                bytes[walkableOffset + 3] = (byte)((endPoint >> 8) & 0xFF);

                // StartFace3 at offset +4
                bytes[walkableOffset + 4] = (byte)(startFace3 & 0xFF);
                bytes[walkableOffset + 5] = (byte)((startFace3 >> 8) & 0xFF);

                // EndFace3 at offset +6
                bytes[walkableOffset + 6] = (byte)(endFace3 & 0xFF);
                bytes[walkableOffset + 7] = (byte)((endFace3 >> 8) & 0xFF);
            });

            Debug.WriteLine($"[WalkablePreviewWindow] Updated walkable #{_walkableId1}: " +
                           $"StartPoint={startPoint}, EndPoint={endPoint}, " +
                           $"StartFace3={startFace3}, EndFace3={endFace3}");

            TxtStatus.Text = $"Applied: StartPoint={startPoint}, EndPoint={endPoint}, " +
                            $"StartFace3={startFace3}, EndFace3={endFace3}. Save map to persist.";

            // Notify change bus
            BuildingsChangeBus.Instance.NotifyChanged();
        }
    }
}