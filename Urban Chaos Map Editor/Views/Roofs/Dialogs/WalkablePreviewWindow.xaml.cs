using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
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

            Title = $"Walkable Preview - Walkable #{walkableId1}";

            HeaderTextBlock.Text = $"Walkable #{walkableId1}";
            DetailsTextBlock.Text =
                $"Building={_walkable.Building}  Rect=({_walkable.X1},{_walkable.Z1})-({_walkable.X2},{_walkable.Z2})  " +
                $"Height={_walkable.Y / 2} QS  StoreyY={_walkable.StoreyY}  " +
                $"Face4=[{_walkable.StartFace4}..{_walkable.EndFace4})";

            TxtBuilding.Text = _walkable.Building.ToString(CultureInfo.InvariantCulture);
            TxtRect.Text = $"({_walkable.X1},{_walkable.Z1}) - ({_walkable.X2},{_walkable.Z2})";

            bool rawMode = HeightDisplaySettings.ShowRawHeights;
            LblYField.Text = rawMode ? "Y (raw):" : "Quarter Storeys:";
            TxtY.Text = (rawMode ? _walkable.Y : _walkable.Y / 2).ToString(CultureInfo.InvariantCulture);
            TxtStoreyY.Text = _walkable.StoreyY.ToString(CultureInfo.InvariantCulture);
            TxtStartFace4.Text = _walkable.StartFace4.ToString(CultureInfo.InvariantCulture);
            TxtEndFace4.Text = _walkable.EndFace4.ToString(CultureInfo.InvariantCulture);

            int faceCount = Math.Max(0, _walkable.EndFace4 - _walkable.StartFace4);
            TxtFace4Count.Text = faceCount.ToString(CultureInfo.InvariantCulture);

            TxtNext.Text = _walkable.Next.ToString(CultureInfo.InvariantCulture);

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
                    Y = (short)(rf.Y / 64), // display in Quarter Storeys (1 QS = 64 raw)
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

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            byte newY;
            if (HeightDisplaySettings.ShowRawHeights)
            {
                if (!byte.TryParse(TxtY.Text.Trim(), out newY))
                {
                    TxtStatus.Text = "Invalid value (0–255 raw Y).";
                    return;
                }
            }
            else
            {
                if (!int.TryParse(TxtY.Text.Trim(), out int newQS) || newQS < 0 || newQS > 127)
                {
                    TxtStatus.Text = "Invalid value (0–127 Quarter Storeys).";
                    return;
                }
                newY = (byte)(newQS * 2);
            }

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

            // DWalkable layout: +16 = Y (UBYTE)
            svc.Edit(bytes => { bytes[walkableOffset + 16] = newY; });

            string displayStr = HeightDisplaySettings.ShowRawHeights
                ? $"raw Y={newY}"
                : $"{newY / 2} QS (raw Y={newY})";

            Debug.WriteLine($"[WalkablePreview] Updated walkable #{_walkableId1}: {displayStr}");
            TxtStatus.Text = $"Applied: {displayStr}. Save map to persist.";

            BuildingsChangeBus.Instance.NotifyChanged();
            Services.Roofs.RoofsChangeBus.Instance.NotifyChanged();
        }
    }
}