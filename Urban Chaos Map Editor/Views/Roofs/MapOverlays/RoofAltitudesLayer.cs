// Views/Roofs/MapOverlays/RoofAltitudesLayer.cs
// Displays cell altitude (PAP_HI.Alt) values for ALL walkable regions.
// Uses the same green glow style as AltitudeHoverLayer for visual consistency.

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

        // Green glow cell fill — matches AltitudeHoverLayer selection style
        private static readonly Brush CellFillBrush;       // semi-transparent green
        private static readonly Brush CellFillZeroBrush;   // dimmer green for zero-altitude cells
        private static readonly Pen CellBorderPen;         // bright green border per cell

        // Walkable region border — bright green outline
        private static readonly Pen WalkableBorderPen;

        // Text colours
        private static readonly Brush TextBrush;           // white text
        private static readonly Brush TextShadowBrush;     // black shadow for readability
        private static readonly Brush TextZeroBrush;       // dimmed for zero values
        private static readonly Brush TextNegativeBrush;   // red for negative values

        static RoofAltitudesLayer()
        {
            // Green glow fills (matching AltitudeHoverLayer's selection green)
            CellFillBrush = new SolidColorBrush(Color.FromArgb(100, 0, 255, 100));    // brighter green
            CellFillBrush.Freeze();
            CellFillZeroBrush = new SolidColorBrush(Color.FromArgb(40, 0, 200, 0));   // dim green for zero
            CellFillZeroBrush.Freeze();
            CellBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)), 0.5);
            CellBorderPen.Freeze();

            // Walkable region border — solid bright green like the selection outline
            WalkableBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 255, 0)), 3);
            WalkableBorderPen.Freeze();

            // Text
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

            // Redraw when data changes
            RoofsChangeBus.Instance.Changed += (_, _) => Dispatcher.Invoke(InvalidateVisual);
            BuildingsChangeBus.Instance.Changed += (_, _) => Dispatcher.Invoke(InvalidateVisual);
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

            // Read all walkables
            if (!svc.TryGetWalkables(out var walkables, out _))
                return;

            if (walkables.Length <= 1) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // For each walkable, draw green-glowing cells with altitude values
            for (int wIdx = 1; wIdx < walkables.Length; wIdx++)
            {
                var w = walkables[wIdx];
                if ((w.X1 | w.Z1 | w.X2 | w.Z2) == 0) continue;

                int gx1 = w.X1;
                int gz1 = w.Z1;
                int gx2 = w.X2;
                int gz2 = w.Z2;

                // Draw walkable region border (bright green, matching selection style)
                double uiLeft = (TilesPerSide - 1 - gx2) * PixelsPerTile;
                double uiTop = (TilesPerSide - 1 - gz2) * PixelsPerTile;
                double uiWidth = (gx2 - gx1) * PixelsPerTile;
                double uiHeight = (gz2 - gz1) * PixelsPerTile;

                dc.DrawRectangle(null, WalkableBorderPen,
                    new Rect(uiLeft, uiTop, uiWidth, uiHeight));

                // Draw each cell with green glow + altitude text
                for (int gx = gx1; gx < gx2; gx++)
                {
                    for (int gz = gz1; gz < gz2; gz++)
                    {
                        // Read alt from PAP_HI
                        int tileIndex = gx * TilesPerSide + gz;
                        int offset = HeaderBytes + tileIndex * BytesPerTile + Off_Alt;
                        if (offset >= fileBytes.Length) continue;

                        sbyte alt = unchecked((sbyte)fileBytes[offset]);
                        int worldAlt = alt << 3;

                        // Convert to UI position (flipped)
                        int uiTileX = TilesPerSide - 1 - gx;
                        int uiTileZ = TilesPerSide - 1 - gz;
                        double cellX = uiTileX * PixelsPerTile;
                        double cellZ = uiTileZ * PixelsPerTile;

                        // Green glow cell fill
                        Brush fill = alt != 0 ? CellFillBrush : CellFillZeroBrush;
                        dc.DrawRectangle(fill, CellBorderPen,
                            new Rect(cellX, cellZ, PixelsPerTile, PixelsPerTile));

                        // Choose text colour
                        Brush textColor = alt == 0 ? TextZeroBrush
                                        : alt < 0 ? TextNegativeBrush
                                        : TextBrush;

                        string label = worldAlt.ToString();

                        // Shadow text for readability
                        var shadowFt = new FormattedText(label, CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, LabelTypeface, 10, TextShadowBrush, dpi);
                        double tx = cellX + (PixelsPerTile - shadowFt.Width) / 2;
                        double ty = cellZ + (PixelsPerTile - shadowFt.Height) / 2;
                        dc.DrawText(shadowFt, new Point(tx + 1, ty + 1));

                        // Main text
                        var ft = new FormattedText(label, CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, LabelTypeface, 10, textColor, dpi);
                        dc.DrawText(ft, new Point(tx, ty));
                    }
                }
            }
        }
    }
}