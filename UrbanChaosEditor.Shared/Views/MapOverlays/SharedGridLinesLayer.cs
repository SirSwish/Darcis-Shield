// ============================================================
// UrbanChaosEditor.Shared/Views/MapOverlays/SharedGridLinesLayer.cs
// ============================================================
// Configurable grid lines overlay - supports simple and complex modes

using System.Windows;
using System.Windows.Media;
using UrbanChaosEditor.Shared.Models;

namespace UrbanChaosEditor.Shared.Views.MapOverlays;

/// <summary>
/// Grid line display style.
/// </summary>
public enum GridLineStyle
{
    /// <summary>Simple black lines every tile (Map Editor / Light Editor style)</summary>
    Simple,

    /// <summary>Complex with minor/major/center lines (Mission Editor style)</summary>
    Complex
}

/// <summary>
/// Shared grid lines overlay layer.
/// Supports both simple (black 64px grid) and complex (minor/major/center) styles.
/// </summary>
public class SharedGridLinesLayer : MapOverlayBase
{
    #region Dependency Properties

    public static readonly DependencyProperty GridStyleProperty =
        DependencyProperty.Register(nameof(GridStyle), typeof(GridLineStyle), typeof(SharedGridLinesLayer),
            new FrameworkPropertyMetadata(GridLineStyle.Simple, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MajorGridIntervalProperty =
        DependencyProperty.Register(nameof(MajorGridInterval), typeof(int), typeof(SharedGridLinesLayer),
            new FrameworkPropertyMetadata(8, FrameworkPropertyMetadataOptions.AffectsRender));

    #endregion

    #region Properties

    /// <summary>Grid display style (Simple or Complex)</summary>
    public GridLineStyle GridStyle
    {
        get => (GridLineStyle)GetValue(GridStyleProperty);
        set => SetValue(GridStyleProperty, value);
    }

    /// <summary>Interval for major grid lines (in tiles). Default is 8.</summary>
    public int MajorGridInterval
    {
        get => (int)GetValue(MajorGridIntervalProperty);
        set => SetValue(MajorGridIntervalProperty, value);
    }

    #endregion

    #region Static Pens

    // Simple style pen
    private static readonly Pen SimplePen;

    // Complex style pens
    private static readonly Pen MinorGridPen;
    private static readonly Pen MajorGridPen;
    private static readonly Pen CenterGridPen;

    static SharedGridLinesLayer()
    {
        // Simple style: 1px black lines
        SimplePen = new Pen(Brushes.Black, 1.0);
        SimplePen.Freeze();

        // Complex style: varying opacity white lines
        MinorGridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.5);
        MinorGridPen.Freeze();

        MajorGridPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 1.0);
        MajorGridPen.Freeze();

        CenterGridPen = new Pen(new SolidColorBrush(Color.FromArgb(150, 100, 100, 255)), 2.0);
        CenterGridPen.Freeze();
    }

    #endregion

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (GridStyle == GridLineStyle.Simple)
            RenderSimpleGrid(dc);
        else
            RenderComplexGrid(dc);
    }

    /// <summary>
    /// Render simple black grid lines every tile (Map Editor / Light Editor style).
    /// </summary>
    private void RenderSimpleGrid(DrawingContext dc)
    {
        double w = SharedMapConstants.MapPixelSize;
        double h = SharedMapConstants.MapPixelSize;
        int step = SharedMapConstants.TileSize;

        // Vertical lines every 64px
        for (int x = 0; x <= SharedMapConstants.MapPixelSize; x += step)
        {
            double gx = x + 0.5; // Offset for crisp 1px lines
            dc.DrawLine(SimplePen, new Point(gx, 0), new Point(gx, h));
        }

        // Horizontal lines every 64px
        for (int y = 0; y <= SharedMapConstants.MapPixelSize; y += step)
        {
            double gy = y + 0.5;
            dc.DrawLine(SimplePen, new Point(0, gy), new Point(w, gy));
        }
    }

    /// <summary>
    /// Render complex grid with minor/major/center lines (Mission Editor style).
    /// </summary>
    private void RenderComplexGrid(DrawingContext dc)
    {
        int mapSize = SharedMapConstants.MapPixelSize;
        int tileSize = SharedMapConstants.TileSize;
        int tilesPerSide = SharedMapConstants.TilesPerSide;
        int majorInterval = MajorGridInterval;
        int centerTile = tilesPerSide / 2;

        // Minor grid lines (every tile, skip major positions)
        for (int i = 0; i <= tilesPerSide; i++)
        {
            double pos = i * tileSize;

            if (i % majorInterval != 0)
            {
                dc.DrawLine(MinorGridPen, new Point(pos, 0), new Point(pos, mapSize));
                dc.DrawLine(MinorGridPen, new Point(0, pos), new Point(mapSize, pos));
            }
        }

        // Major grid lines (every N tiles, skip center)
        for (int i = 0; i <= tilesPerSide; i += majorInterval)
        {
            double pos = i * tileSize;

            if (i != centerTile)
            {
                dc.DrawLine(MajorGridPen, new Point(pos, 0), new Point(pos, mapSize));
                dc.DrawLine(MajorGridPen, new Point(0, pos), new Point(mapSize, pos));
            }
        }

        // Center lines
        double center = centerTile * tileSize;
        dc.DrawLine(CenterGridPen, new Point(center, 0), new Point(center, mapSize));
        dc.DrawLine(CenterGridPen, new Point(0, center), new Point(mapSize, center));
    }
}