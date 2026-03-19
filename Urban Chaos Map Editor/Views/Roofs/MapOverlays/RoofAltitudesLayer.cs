// Views/Roofs/MapOverlays/RoofAltitudesLayer.cs
// Displays cell altitude (PAP_HI.Alt) values for ALL walkable regions.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Roofs;

namespace UrbanChaosMapEditor.Views.Roofs.MapOverlays
{
    public sealed class RoofAltitudesLayer : FrameworkElement
    {
        private const int TilesPerSide = 128;
        private const int PixelsPerTile = 64;    // 8192 / 128
        private const int HeaderBytes = 8;
        private const int BytesPerTile = 6;
        private const int Off_Alt = 5;           // Alt byte offset within PAP_HI tile

        private static readonly Typeface LabelTypeface = new("Segoe UI");

        // Cell fill
        private static readonly Brush CellFillBrush;
        private static readonly Brush CellFillZeroBrush;
        private static readonly Pen CellBorderPen;

        // Text colours
        private static readonly Brush TextBrush;
        private static readonly Brush TextShadowBrush;
        private static readonly Brush TextZeroBrush;
        private static readonly Brush TextNegativeBrush;

        static RoofAltitudesLayer()
        {
            CellFillBrush = new SolidColorBrush(Color.FromArgb(100, 0, 255, 100));
            CellFillBrush.Freeze();

            CellFillZeroBrush = new SolidColorBrush(Color.FromArgb(40, 0, 200, 0));
            CellFillZeroBrush.Freeze();

            CellBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)), 0.5);
            CellBorderPen.Freeze();

            TextBrush = Brushes.White;
            TextShadowBrush = Brushes.Black;

            TextZeroBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xAA, 0xAA, 0xAA));
            TextZeroBrush.Freeze();

            TextNegativeBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x66, 0x66));
            TextNegativeBrush.Freeze();
        }

        public RoofAltitudesLayer()
        {
            IsHitTestVisible = false;
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

            RoofsChangeBus.Instance.Changed += (_, _) => Dispatcher.Invoke(InvalidateVisual);
            BuildingsChangeBus.Instance.Changed += (_, _) => Dispatcher.Invoke(InvalidateVisual);
            AltitudeChangeBus.Instance.TileChanged += (tx, ty) => Dispatcher.Invoke(InvalidateVisual);
            AltitudeChangeBus.Instance.RegionChanged += (_, __, ___, ____) => Dispatcher.Invoke(InvalidateVisual);
            AltitudeChangeBus.Instance.AllChanged += () => Dispatcher.Invoke(InvalidateVisual);
            MapDataService.Instance.MapLoaded += (_, _) => Dispatcher.Invoke(InvalidateVisual);
            MapDataService.Instance.MapCleared += (_, _) => Dispatcher.Invoke(InvalidateVisual);
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (!MapDataService.Instance.IsLoaded) return;

            var svc = MapDataService.Instance;
            byte[] fileBytes;
            try { fileBytes = svc.GetBytesCopy(); }
            catch { return; }

            if (!svc.TryGetWalkables(out var walkables, out _))
                return;

            if (walkables.Length <= 1) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            for (int wIdx = 1; wIdx < walkables.Length; wIdx++)
            {
                var w = walkables[wIdx];
                if ((w.X1 | w.Z1 | w.X2 | w.Z2) == 0) continue;

                int gx1 = w.X1;
                int gz1 = w.Z1;
                int gx2 = w.X2;
                int gz2 = w.Z2;

                for (int gx = gx1; gx < gx2; gx++)
                {
                    for (int gz = gz1; gz < gz2; gz++)
                    {
                        int tileIndex = gx * TilesPerSide + gz;
                        int offset = HeaderBytes + tileIndex * BytesPerTile + Off_Alt;
                        if (offset >= fileBytes.Length) continue;

                        sbyte alt = unchecked((sbyte)fileBytes[offset]);
                        int worldAlt = alt << 3;

                        int uiTileX = TilesPerSide - 1 - gx;
                        int uiTileZ = TilesPerSide - 1 - gz;
                        double cellX = uiTileX * PixelsPerTile;
                        double cellZ = uiTileZ * PixelsPerTile;

                        Brush fill = alt != 0 ? CellFillBrush : CellFillZeroBrush;
                        dc.DrawRectangle(fill, CellBorderPen,
                            new Rect(cellX, cellZ, PixelsPerTile, PixelsPerTile));

                        Brush textColor = alt == 0 ? TextZeroBrush
                                        : alt < 0 ? TextNegativeBrush
                                        : TextBrush;

                        string label = worldAlt.ToString();

                        var shadowFt = new FormattedText(label, CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, LabelTypeface, 10, TextShadowBrush, dpi);
                        double tx = cellX + (PixelsPerTile - shadowFt.Width) / 2;
                        double ty = cellZ + (PixelsPerTile - shadowFt.Height) / 2;
                        dc.DrawText(shadowFt, new Point(tx + 1, ty + 1));

                        var ft = new FormattedText(label, CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, LabelTypeface, 10, textColor, dpi);
                        dc.DrawText(ft, new Point(tx, ty));
                    }
                }
            }
        }
    }
}