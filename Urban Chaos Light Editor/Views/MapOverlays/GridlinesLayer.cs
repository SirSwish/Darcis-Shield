// /Views/MapOverlays/GridLinesLayer.cs
using System.Windows;
using System.Windows.Media;
using UrbanChaosLightEditor.Models;

namespace UrbanChaosLightEditor.Views.MapOverlays
{
    /// <summary>
    /// Draws 64x64 tile grid (black lines) over the map.
    /// </summary>
    public sealed class GridLinesLayer : FrameworkElement
    {
        public GridLinesLayer()
        {
            Width = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = false;
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            var pen = new Pen(Brushes.Black, 1.0);
            pen.Freeze();

            double w = MapConstants.MapPixels;
            double h = MapConstants.MapPixels;

            // Vertical lines every 64 px
            for (int x = 0; x <= MapConstants.MapPixels; x += MapConstants.TileSize)
            {
                double gx = x + 0.5;
                dc.DrawLine(pen, new Point(gx, 0), new Point(gx, h));
            }

            // Horizontal lines every 64 px
            for (int y = 0; y <= MapConstants.MapPixels; y += MapConstants.TileSize)
            {
                double gy = y + 0.5;
                dc.DrawLine(pen, new Point(0, gy), new Point(w, gy));
            }
        }
    }
}