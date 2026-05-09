using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using UrbanChaosEditor.Shared.Models;

namespace UrbanChaosEditor.Shared.Views.MapOverlays;

public class SharedRoofTilesLayer : FrameworkElement
{
    private static readonly Brush TileFillBrush = Frozen(new SolidColorBrush(Color.FromArgb(55, 0, 200, 200)));
    private static readonly Pen TileBorderPen = Frozen(new Pen(Brushes.Cyan, 1.5));
    private static readonly Pen DiagonalPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1) { DashStyle = DashStyles.Dot });
    private static readonly Typeface LabelTypeface = new("Consolas");

    private readonly List<TileInfo> _tiles = new();
    private IRoofDataProvider? _provider;
    private bool _cacheDirty = true;

    private readonly record struct TileInfo(double UiX, double UiZ, short Y, bool DiagonalFlag);

    public SharedRoofTilesLayer()
    {
        Width = SharedMapConstants.MapPixelSize;
        Height = SharedMapConstants.MapPixelSize;
        IsHitTestVisible = false;
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    public void SetDataProvider(IRoofDataProvider provider)
    {
        _provider = provider;
        provider.SubscribeMapLoaded(MarkCacheDirty);
        provider.SubscribeMapCleared(ClearCache);
        MarkCacheDirty();
    }

    public void MarkCacheDirty()
    {
        _cacheDirty = true;
        Dispatcher.BeginInvoke(InvalidateVisual, DispatcherPriority.Render);
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(SharedMapConstants.MapPixelSize, SharedMapConstants.MapPixelSize);

    protected override void OnRender(DrawingContext dc)
    {
        if (_provider == null || !_provider.IsLoaded)
            return;

        if (_cacheDirty)
            RebuildCache();

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var tile in _tiles)
        {
            var rect = new Rect(tile.UiX, tile.UiZ, SharedMapConstants.PixelsPerTile, SharedMapConstants.PixelsPerTile);
            dc.DrawRectangle(TileFillBrush, TileBorderPen, rect);

            if (tile.DiagonalFlag)
                dc.DrawLine(DiagonalPen, rect.TopLeft, rect.BottomRight);
            else
                dc.DrawLine(DiagonalPen, rect.BottomLeft, rect.TopRight);

            string label = (tile.Y / 64).ToString(CultureInfo.InvariantCulture);
            var ft = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                LabelTypeface, 9, Brushes.White, dpi);
            dc.DrawText(ft, new Point(rect.X + 3, rect.Y + 3));
        }
    }

    private void RebuildCache()
    {
        _tiles.Clear();
        _cacheDirty = false;

        if (_provider == null || !SharedRoofDataParser.TryReadRoofData(_provider, out _, out var roofFaces4))
            return;

        for (int i = 1; i < roofFaces4.Length; i++)
        {
            var rf4 = roofFaces4[i];
            if (rf4.RX == 0 && rf4.RZ == 0 && rf4.Y == 0)
                continue;

            int tileX = rf4.RX & 0x7F;
            int tileZ = rf4.RZ - 128;
            if (tileX < 0 || tileX >= SharedMapConstants.TilesPerSide ||
                tileZ < 0 || tileZ >= SharedMapConstants.TilesPerSide)
                continue;

            double uiX = (SharedMapConstants.TilesPerSide - 1 - tileX) * SharedMapConstants.PixelsPerTile;
            double uiZ = (SharedMapConstants.TilesPerSide - 1 - tileZ) * SharedMapConstants.PixelsPerTile;
            _tiles.Add(new TileInfo(uiX, uiZ, rf4.Y, (rf4.RX & 0x80) != 0));
        }
    }

    private void ClearCache()
    {
        _tiles.Clear();
        _cacheDirty = true;
        Dispatcher.BeginInvoke(InvalidateVisual, DispatcherPriority.Render);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
