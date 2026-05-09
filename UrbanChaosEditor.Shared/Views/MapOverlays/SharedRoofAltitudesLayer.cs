using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosEditor.Shared.Models;

namespace UrbanChaosEditor.Shared.Views.MapOverlays;

public class SharedRoofAltitudesLayer : FrameworkElement
{
    private const double LabelFontSize = 10.0;
    private static readonly Typeface LabelTypeface = new("Segoe UI");
    private static readonly Brush CellFillBrush = Frozen(new SolidColorBrush(Color.FromArgb(100, 0, 255, 100)));
    private static readonly Brush CellFillZeroBrush = Frozen(new SolidColorBrush(Color.FromArgb(40, 0, 200, 0)));
    private static readonly Pen CellBorderPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)), 0.5));
    private static readonly Brush TextBrush = Brushes.White;
    private static readonly Brush TextShadowBrush = Brushes.Black;
    private static readonly Brush TextNegativeBrush = Frozen(new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x66, 0x66)));

    private readonly List<AltitudeCell> _cells = new();
    private readonly Dictionary<TextCacheKey, FormattedText> _textCache = new();
    private readonly DispatcherTimer _viewportDebounceTimer;
    private IRoofDataProvider? _provider;
    private bool _cacheDirty = true;
    private double _cachedPixelsPerDip;
    private ScrollViewer? _scrollViewer;

    private readonly record struct AltitudeCell(int UiTileX, int UiTileZ, sbyte Alt);
    private readonly record struct TextCacheKey(string Text, int BrushKind, double PixelsPerDip);

    public bool ShowRawHeights
    {
        get => (bool)GetValue(ShowRawHeightsProperty);
        set => SetValue(ShowRawHeightsProperty, value);
    }

    public static readonly DependencyProperty ShowRawHeightsProperty =
        DependencyProperty.Register(
            nameof(ShowRawHeights),
            typeof(bool),
            typeof(SharedRoofAltitudesLayer),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, (_, _) => { }));

    public SharedRoofAltitudesLayer()
    {
        Width = SharedMapConstants.MapPixelSize;
        Height = SharedMapConstants.MapPixelSize;
        IsHitTestVisible = false;
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

        _viewportDebounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _viewportDebounceTimer.Tick += (_, _) =>
        {
            _viewportDebounceTimer.Stop();
            InvalidateVisual();
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => QueueViewportRepaint();
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

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (_cachedPixelsPerDip != dpi)
        {
            _cachedPixelsPerDip = dpi;
            _textCache.Clear();
        }

        if (_cacheDirty)
            RebuildCache();

        Rect visible = GetVisibleMapRect();
        foreach (var cell in _cells)
        {
            double cellX = cell.UiTileX * SharedMapConstants.PixelsPerTile;
            double cellZ = cell.UiTileZ * SharedMapConstants.PixelsPerTile;
            var cellRect = new Rect(cellX, cellZ, SharedMapConstants.PixelsPerTile, SharedMapConstants.PixelsPerTile);
            if (visible.IntersectsWith(cellRect))
                DrawCell(dc, cell, cellRect, dpi);
        }
    }

    private void RebuildCache()
    {
        _cells.Clear();
        _cacheDirty = false;

        if (_provider == null)
            return;

        byte[]? fileBytes = _provider.GetBytesCopy();
        if (fileBytes == null)
            return;

        var shouldDisplay = new bool[SharedMapConstants.TilesPerSide * SharedMapConstants.TilesPerSide];
        if (SharedRoofDataParser.TryReadRoofData(_provider, out var walkables, out _))
        {
            for (int wIdx = 1; wIdx < walkables.Length; wIdx++)
            {
                var w = walkables[wIdx];
                if ((w.X1 | w.Z1 | w.X2 | w.Z2) == 0) continue;

                int minGx = Math.Clamp((int)Math.Min(w.X1, w.X2), 0, SharedMapConstants.TilesPerSide - 1);
                int maxGx = Math.Clamp((int)Math.Max(w.X1, w.X2), 0, SharedMapConstants.TilesPerSide);
                int minGz = Math.Clamp((int)Math.Min(w.Z1, w.Z2), 0, SharedMapConstants.TilesPerSide - 1);
                int maxGz = Math.Clamp((int)Math.Max(w.Z1, w.Z2), 0, SharedMapConstants.TilesPerSide);

                for (int gx = minGx; gx < maxGx; gx++)
                for (int gz = minGz; gz < maxGz; gz++)
                {
                    int uiTileX = SharedMapConstants.TilesPerSide - 1 - gx;
                    int uiTileZ = SharedMapConstants.TilesPerSide - 1 - gz;
                    shouldDisplay[uiTileZ * SharedMapConstants.TilesPerSide + uiTileX] = true;
                }
            }
        }

        int tileCount = SharedMapConstants.TilesPerSide * SharedMapConstants.TilesPerSide;
        for (int fileIndex = 0; fileIndex < tileCount; fileIndex++)
        {
            int offset = TextureFormatConstants.HeaderBytes + fileIndex * TextureFormatConstants.BytesPerTile + MapFormatConstants.PapAltitudeByteIndex;
            if (offset >= fileBytes.Length) break;

            sbyte alt = unchecked((sbyte)fileBytes[offset]);
            int fileY = fileIndex / SharedMapConstants.TilesPerSide;
            int fileX = fileIndex % SharedMapConstants.TilesPerSide;
            int uiTileX = SharedMapConstants.TilesPerSide - 1 - fileY;
            int uiTileZ = SharedMapConstants.TilesPerSide - 1 - fileX;

            if (alt == 0 && !shouldDisplay[uiTileZ * SharedMapConstants.TilesPerSide + uiTileX])
                continue;

            _cells.Add(new AltitudeCell(uiTileX, uiTileZ, alt));
        }
    }

    private void DrawCell(DrawingContext dc, AltitudeCell cell, Rect cellRect, double dpi)
    {
        dc.DrawRectangle(cell.Alt != 0 ? CellFillBrush : CellFillZeroBrush, CellBorderPen, cellRect);

        int worldAlt = cell.Alt << MapFormatConstants.PapAltitudeShift;
        int displayAlt = ShowRawHeights ? worldAlt : worldAlt >> MapFormatConstants.PapAltitudeShift;
        string label = displayAlt.ToString(CultureInfo.InvariantCulture);
        Brush textColor = displayAlt < 0 ? TextNegativeBrush : TextBrush;

        var shadowFt = GetFormattedText(label, 3, TextShadowBrush, dpi);
        double tx = cellRect.X + (SharedMapConstants.PixelsPerTile - shadowFt.Width) / 2;
        double ty = cellRect.Y + (SharedMapConstants.PixelsPerTile - shadowFt.Height) / 2;
        dc.DrawText(shadowFt, new Point(tx + 1, ty + 1));

        var ft = GetFormattedText(label, displayAlt < 0 ? 2 : 1, textColor, dpi);
        dc.DrawText(ft, new Point(tx, ty));
    }

    private FormattedText GetFormattedText(string text, int brushKind, Brush brush, double dpi)
    {
        var key = new TextCacheKey(text, brushKind, dpi);
        if (_textCache.TryGetValue(key, out var cached))
            return cached;

        var formatted = new FormattedText(text, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, LabelTypeface, LabelFontSize, brush, dpi);
        _textCache[key] = formatted;
        return formatted;
    }

    private Rect GetVisibleMapRect()
    {
        if (_scrollViewer == null || _scrollViewer.ViewportWidth <= 0 || _scrollViewer.ViewportHeight <= 0)
            return new Rect(0, 0, SharedMapConstants.MapPixelSize, SharedMapConstants.MapPixelSize);

        try
        {
            GeneralTransform transform = _scrollViewer.TransformToDescendant(this);
            var visible = transform.TransformBounds(new Rect(0, 0, _scrollViewer.ViewportWidth, _scrollViewer.ViewportHeight));
            visible.Inflate(SharedMapConstants.PixelsPerTile, SharedMapConstants.PixelsPerTile);
            return visible;
        }
        catch
        {
            return new Rect(0, 0, SharedMapConstants.MapPixelSize, SharedMapConstants.MapPixelSize);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _scrollViewer = FindAncestor<ScrollViewer>(this);
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged += OnViewportChanged;
            _scrollViewer.SizeChanged += OnViewportSizeChanged;
        }
        QueueViewportRepaint();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged -= OnViewportChanged;
            _scrollViewer.SizeChanged -= OnViewportSizeChanged;
            _scrollViewer = null;
        }
    }

    private void OnViewportChanged(object sender, ScrollChangedEventArgs e) => QueueViewportRepaint();
    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e) => QueueViewportRepaint();

    private void QueueViewportRepaint()
    {
        if (!_viewportDebounceTimer.IsEnabled)
            _viewportDebounceTimer.Start();
    }

    private void ClearCache()
    {
        _cells.Clear();
        _textCache.Clear();
        _cacheDirty = true;
        Dispatcher.BeginInvoke(InvalidateVisual, DispatcherPriority.Render);
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node != null)
        {
            if (node is T match) return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
