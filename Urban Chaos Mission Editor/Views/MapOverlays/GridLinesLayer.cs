using System.Windows;
using System.Windows.Media;
using UrbanChaosMissionEditor.Models;

namespace UrbanChaosMissionEditor.Views.MapOverlays;

/// <summary>
/// Renders grid lines overlay.
/// </summary>
public class GridLinesLayer : FrameworkElement
{
    private static readonly Pen MinorGridPen = new(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.5);
    private static readonly Pen MajorGridPen = new(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 1.0);
    private static readonly Pen CenterGridPen = new(new SolidColorBrush(Color.FromArgb(150, 100, 100, 255)), 2.0);

    static GridLinesLayer()
    {
        MinorGridPen.Freeze();
        MajorGridPen.Freeze();
        CenterGridPen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        int mapSize = MapConstants.MapPixelSize;
        int tileSize = MapConstants.PixelsPerTile;
        int tilesPerSide = MapConstants.TilesPerSide;

        // Minor grid lines (every tile)
        for (int i = 0; i <= tilesPerSide; i++)
        {
            double pos = i * tileSize;

            // Skip major grid positions for minor lines
            if (i % 8 != 0)
            {
                dc.DrawLine(MinorGridPen, new Point(pos, 0), new Point(pos, mapSize));
                dc.DrawLine(MinorGridPen, new Point(0, pos), new Point(mapSize, pos));
            }
        }

        // Major grid lines (every 8 tiles = 512 pixels)
        for (int i = 0; i <= tilesPerSide; i += 8)
        {
            double pos = i * tileSize;

            // Skip center line for major lines
            if (i != tilesPerSide / 2)
            {
                dc.DrawLine(MajorGridPen, new Point(pos, 0), new Point(pos, mapSize));
                dc.DrawLine(MajorGridPen, new Point(0, pos), new Point(mapSize, pos));
            }
        }

        // Center lines
        double center = mapSize / 2.0;
        dc.DrawLine(CenterGridPen, new Point(center, 0), new Point(center, mapSize));
        dc.DrawLine(CenterGridPen, new Point(0, center), new Point(mapSize, center));
    }
}