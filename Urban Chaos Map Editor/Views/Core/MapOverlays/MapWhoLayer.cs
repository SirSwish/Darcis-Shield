using System.Globalization;
using System.Windows;
using System.Windows.Media;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosMapEditor.Models.Core;

namespace UrbanChaosMapEditor.Views.Core.MapOverlays
{
    /// <summary>Draws 4x4-tile (256px) MapWho cell grid with thicker red lines + per-cell labels.</summary>
    public sealed class MapWhoLayer : FrameworkElement
    {
        // VS-like red used elsewhere (#861b2d). Tweak if desired.
        private static readonly Brush GridBrush = (Brush)new BrushConverter().ConvertFrom("#861b2d")!;
        private static readonly Pen GridPen = new Pen(GridBrush, 2.0);
        private static readonly Pen RedPen = new Pen(Brushes.Red, 2.0);

        // Label styling
        private static readonly Brush LabelText = Brushes.White;
        private static readonly Brush RedBrush = Brushes.Red;
        private static readonly Brush LabelBg = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)); // opaque-ish
        private const double LabelPadX = EditorUiConstants.MapLabelPadX;
        private const double LabelPadY = EditorUiConstants.MapLabelPadY;

        static MapWhoLayer()
        {
            GridPen.Freeze();
            LabelText.Freeze();
            LabelBg.Freeze();
        }

        protected override Size MeasureOverride(Size _) => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            RedPen.Freeze();

            double w = MapConstants.MapPixels;
            double h = MapConstants.MapPixels;
            int step = MapConstants.MapWhoCellSize; // 256

            // grid
            for (int x = 0; x <= MapConstants.MapPixels; x += step)
                dc.DrawLine(RedPen, new Point(x + 0.5, 0), new Point(x + 0.5, h));
            for (int y = 0; y <= MapConstants.MapPixels; y += step)
                dc.DrawLine(RedPen, new Point(0, y + 0.5), new Point(w, y + 0.5));

            // labels: small opaque badge in the TOP-LEFT of each UI cell
            var bg = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)); bg.Freeze();
            var fg = Brushes.White; // frozen

            double pad = 3;

            for (int uiRow = 0; uiRow < 32; uiRow++)
            {
                for (int uiCol = 0; uiCol < 32; uiCol++)
                {
                    // Convert UI cell ? game row/col/index (V1 scheme)
                    int gameRow = 31 - uiRow;
                    int gameCol = 31 - uiCol;
                    int idx = gameRow * 32 + gameCol;

                    string text = $"r{gameRow}, c{gameCol}, {idx}";

                    // (optional) tiny font for compactness
                    var ft = new FormattedText(
                        text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Consolas"), 10, fg, VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    double x = uiCol * step + pad;
                    double y = uiRow * step + pad;
                    var rect = new Rect(x - 2, y - 1, ft.Width + 4, ft.Height + 2);

                    dc.DrawRectangle(bg, null, rect);
                    dc.DrawText(ft, new Point(x, y));
                }
            }
        }
    }
}
