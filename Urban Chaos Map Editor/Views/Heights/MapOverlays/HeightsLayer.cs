using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Heights;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Heights.MapOverlays
{
    public sealed class HeightsLayer : FrameworkElement
    {
        private readonly HeightsAccessor _accessor = new(MapDataService.Instance);
        private readonly DispatcherTimer _debounceTimer;

        private readonly Dictionary<int, FormattedText> _textCacheBlack = new(256);
        private readonly Dictionary<int, FormattedText> _textCacheWhite = new(256);
        private double _lastPixelsPerDip = -1.0;
        private Typeface _typeface = new("Segoe UI");

        private bool _isAreaDragging;
        private Point _areaStart;
        private Point _areaEnd;
        private Rect _areaRectPx = Rect.Empty;

        private static readonly Brush AreaFill;
        private static readonly Pen AreaOutline;

        // Distinct visuals for the randomise-area drag
        private static readonly Brush RandomizeAreaFill;
        private static readonly Pen RandomizeAreaOutline;

        private const double Radius = 14.0;
        private const double HitRadius = 12.0;
        private const double HitRadiusSq = HitRadius * HitRadius;
        private const double TileSize = 64.0;
        private const int TilesPerSide = 128;
        private const int MapPixels = 8192;

        private static readonly Brush DefaultFill;
        private static readonly Brush DefaultText;
        private static readonly Brush HoverFill;
        private static readonly Brush HoverText;
        private static readonly Pen DefaultOutline;
        private static readonly Pen HoverOutline;

        private static readonly Brush[] ElevationFillLut;
        private static readonly bool[] ElevationUseWhiteTextLut;

        private static readonly EllipseGeometry CircleGeometry;

        private MapViewModel? _vm;
        private int _lastHoverVX = -1;
        private int _lastHoverVZ = -1;
        private int _lastBrushSize = 1;

        static HeightsLayer()
        {
            DefaultFill = Brushes.White;
            DefaultText = Brushes.Black;

            var red = new SolidColorBrush(Color.FromRgb(0xE5, 0x00, 0x00));
            red.Freeze();
            HoverFill = red;

            HoverText = Brushes.White;

            DefaultOutline = new Pen(Brushes.Black, 1.0);
            DefaultOutline.Freeze();

            HoverOutline = new Pen(Brushes.White, 1.25);
            HoverOutline.Freeze();

            var areaFill = new SolidColorBrush(Color.FromArgb(48, 0, 120, 215));
            areaFill.Freeze();
            AreaFill = areaFill;

            var areaStrokeBrush = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
            areaStrokeBrush.Freeze();

            AreaOutline = new Pen(areaStrokeBrush, 1.5);
            AreaOutline.Freeze();

            // Orange fill/outline for the randomise-area drag rectangle
            var randFill = new SolidColorBrush(Color.FromArgb(60, 255, 140, 0));
            randFill.Freeze();
            RandomizeAreaFill = randFill;

            var randStroke = new SolidColorBrush(Color.FromArgb(220, 255, 165, 0));
            randStroke.Freeze();
            RandomizeAreaOutline = new Pen(randStroke, 2.0);
            RandomizeAreaOutline.Freeze();

            CircleGeometry = new EllipseGeometry(new Point(0, 0), Radius, Radius);
            CircleGeometry.Freeze();

            var low = Color.FromRgb(0x7A, 0xD7, 0xFF);
            var high = Color.FromRgb(0x2E, 0xB8, 0x4A);

            ElevationFillLut = new Brush[256];
            ElevationUseWhiteTextLut = new bool[256];

            for (int i = 0; i < 256; i++)
            {
                double t = i / 255.0;

                byte r = (byte)(low.R + (high.R - low.R) * t);
                byte g = (byte)(low.G + (high.G - low.G) * t);
                byte b = (byte)(low.B + (high.B - low.B) * t);

                var c = Color.FromRgb(r, g, b);

                var br = new SolidColorBrush(c);
                br.Freeze();
                ElevationFillLut[i] = br;

                double luma = 0.299 * r + 0.587 * g + 0.114 * b;
                ElevationUseWhiteTextLut[i] = luma < 140;
            }
        }

        public HeightsLayer()
        {
            MapDataService.Instance.MapLoaded += (_, __) => Dispatcher.BeginInvoke(InvalidateVisual);
            MapDataService.Instance.MapCleared += (_, __) => Dispatcher.BeginInvoke(InvalidateVisual);
            MapDataService.Instance.MapSaved += (_, __) => Dispatcher.BeginInvoke(InvalidateVisual);
            MapDataService.Instance.MapBytesReset += (_, __) => Dispatcher.BeginInvoke(InvalidateVisual);

            HeightsChangeBus.Instance.TileChanged += (_, __) => KickRepaint();
            HeightsChangeBus.Instance.RegionChanged += (_, __) => KickRepaint();

            // Repaint on scroll so view culling updates
            Loaded += (_, __) =>
            {
                var sv = FindParentScrollViewer();
                if (sv != null)
                    sv.ScrollChanged += (_, __) => KickRepaint();
            };

            Width = MapPixels;
            Height = MapPixels;
            IsHitTestVisible = true;
            Focusable = true;

            _debounceTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(32)
            };
            _debounceTimer.Tick += (_, __) =>
            {
                _debounceTimer.Stop();
                InvalidateVisual();
            };

            DataContextChanged += (_, __) => HookVm();
        }

        private ScrollViewer? FindParentScrollViewer()
        {
            DependencyObject? current = this;
            while (current != null)
            {
                if (current is ScrollViewer sv)
                    return sv;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void HookVm()
        {
            if (_vm != null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as MapViewModel;
            if (_vm != null) _vm.PropertyChanged += OnVmChanged;
            InvalidateVisual();
        }

        private void OnVmChanged(object? s, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MapViewModel.CursorX):
                case nameof(MapViewModel.CursorZ):
                    {
                        var (vx, vz) = GetHoveredVertex();
                        if (vx != _lastHoverVX || vz != _lastHoverVZ)
                        {
                            _lastHoverVX = vx;
                            _lastHoverVZ = vz;
                            InvalidateVisual();
                        }
                        break;
                    }

                case nameof(MapViewModel.SelectedTool):
                    _lastHoverVX = -1;
                    _lastHoverVZ = -1;

                    if (_vm == null || !IsAreaDragTool(_vm.SelectedTool))
                    {
                        _isAreaDragging = false;
                        _areaRectPx = Rect.Empty;
                        if (IsMouseCaptured)
                            ReleaseMouseCapture();
                    }

                    InvalidateVisual();
                    break;

                case nameof(MapViewModel.BrushSize):
                    {
                        var newSize = _vm?.BrushSize ?? 1;
                        if (newSize != _lastBrushSize)
                        {
                            _lastBrushSize = newSize;
                            InvalidateVisual();
                        }
                        break;
                    }

                case nameof(MapViewModel.Zoom):
                    KickRepaint();
                    break;
            }
        }

        private void KickRepaint()
        {
            if (!_debounceTimer.IsEnabled)
                _debounceTimer.Start();
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapPixels, MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            if (!MapDataService.Instance.IsLoaded || MapDataService.Instance.MapBytes is null)
                return;

            var clipBounds = GetVisibleBounds();
            if (clipBounds.IsEmpty || clipBounds.Width <= 0 || clipBounds.Height <= 0)
                return;

            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            EnsureTextCache(ppd);

            var (centerVX, centerVZ) = GetHoveredVertex();
            var brushBounds = GetBrushBounds(centerVX, centerVZ);

            int minVX = Math.Max(1, (int)Math.Floor(clipBounds.Left / TileSize));
            int maxVX = Math.Min(TilesPerSide, (int)Math.Ceiling(clipBounds.Right / TileSize));
            int minVZ = Math.Max(1, (int)Math.Floor(clipBounds.Top / TileSize));
            int maxVZ = Math.Min(TilesPerSide, (int)Math.Ceiling(clipBounds.Bottom / TileSize));

            if (minVX > maxVX || minVZ > maxVZ)
                return;

            DrawVertices(dc, minVX, maxVX, minVZ, maxVZ, brushBounds, ppd, isHover: false);

            if (!brushBounds.IsEmpty)
            {
                DrawVertices(dc,
                    Math.Max(minVX, brushBounds.MinX),
                    Math.Min(maxVX, brushBounds.MaxX),
                    Math.Max(minVZ, brushBounds.MinZ),
                    Math.Min(maxVZ, brushBounds.MaxZ),
                    brushBounds, ppd, isHover: true);
            }

            if (_isAreaDragging && !_areaRectPx.IsEmpty)
            {
                var r = NormalizeRect(_areaRectPx);
                if (_vm?.SelectedTool == EditorTool.AreaSetHeight)
                    dc.DrawRectangle(AreaFill, AreaOutline, r);
                else if (_vm?.SelectedTool == EditorTool.RandomizeHeightArea)
                    dc.DrawRectangle(RandomizeAreaFill, RandomizeAreaOutline, r);
            }
        }

        private void DrawVertices(
            DrawingContext dc,
            int minVX,
            int maxVX,
            int minVZ,
            int maxVZ,
            in VertexBounds brushBounds,
            double ppd,
            bool isHover)
        {
            var stroke = isHover ? HoverOutline : DefaultOutline;

            for (int vx = minVX; vx <= maxVX; vx++)
            {
                double cx = vx * TileSize;

                for (int vz = minVZ; vz <= maxVZ; vz++)
                {
                    bool inBrush = brushBounds.Contains(vx, vz);

                    if (isHover != inBrush)
                        continue;

                    double cy = vz * TileSize;

                    int tx = vx - 1;
                    int ty = vz - 1;

                    int h = _accessor.ReadHeight(tx, ty);

                    int lutIndex = h + 128;
                    if (lutIndex < 0) lutIndex = 0;
                    if (lutIndex > 255) lutIndex = 255;

                    Brush fill;
                    Brush textBrush;
                    var textCache = _textCacheBlack;

                    if (isHover)
                    {
                        fill = HoverFill;
                        textBrush = HoverText;
                        textCache = _textCacheWhite;
                    }
                    else
                    {
                        if (h == 0)
                        {
                            fill = Brushes.White;
                            textBrush = Brushes.Black;
                            textCache = _textCacheBlack;
                        }
                        else
                        {
                            fill = ElevationFillLut[lutIndex];
                            bool whiteText = ElevationUseWhiteTextLut[lutIndex];
                            textBrush = whiteText ? Brushes.White : Brushes.Black;
                            textCache = whiteText ? _textCacheWhite : _textCacheBlack;
                        }
                    }

                    var center = new Point(cx, cy);
                    dc.DrawEllipse(fill, stroke, center, Radius, Radius);

                    var ft = GetOrCreateText(h, textBrush, textCache, ppd);
                    dc.DrawText(ft, new Point(cx - ft.Width * 0.5, cy - ft.Height * 0.5));
                }
            }
        }

        private FormattedText GetOrCreateText(
            int height,
            Brush brush,
            Dictionary<int, FormattedText> cache,
            double ppd)
        {
            if (!cache.TryGetValue(height, out var ft))
            {
                ft = new FormattedText(
                    height.ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    12,
                    brush,
                    ppd);
                cache[height] = ft;
            }
            return ft;
        }

        private Rect GetVisibleBounds()
        {
            var clip = VisualClip;
            if (clip != null && !clip.Bounds.IsEmpty)
                return clip.Bounds;

            DependencyObject? current = this;
            while (current != null)
            {
                if (current is ScrollViewer sv && sv.IsLoaded)
                {
                    try
                    {
                        var transform = sv.TransformToDescendant(this);
                        var viewport = new Rect(
                            sv.HorizontalOffset,
                            sv.VerticalOffset,
                            sv.ViewportWidth,
                            sv.ViewportHeight);
                        return transform.TransformBounds(viewport);
                    }
                    catch { }
                }

                if (current is ScrollContentPresenter scp && scp.IsLoaded)
                {
                    try
                    {
                        var transform = scp.TransformToDescendant(this);
                        var viewport = new Rect(0, 0, scp.ActualWidth, scp.ActualHeight);
                        return transform.TransformBounds(viewport);
                    }
                    catch { }
                }

                current = VisualTreeHelper.GetParent(current);
            }

            if (ActualWidth > 0 && ActualHeight > 0)
            {
                return new Rect(0, 0, Math.Min(ActualWidth, MapPixels), Math.Min(ActualHeight, MapPixels));
            }

            return new Rect(0, 0, MapPixels, MapPixels);
        }

        private void EnsureTextCache(double ppd)
        {
            if (Math.Abs(ppd - _lastPixelsPerDip) > 0.001)
            {
                _textCacheBlack.Clear();
                _textCacheWhite.Clear();
                _lastPixelsPerDip = ppd;
            }
        }

        private static bool IsHeightTool(EditorTool t) =>
            t is EditorTool.RaiseHeight or EditorTool.LowerHeight or
               EditorTool.LevelHeight or EditorTool.FlattenHeight or
               EditorTool.DitchTemplate or EditorTool.AreaSetHeight or
               EditorTool.RandomizeHeightArea;

        private static bool IsAreaDragTool(EditorTool t) =>
            t == EditorTool.AreaSetHeight || t == EditorTool.RandomizeHeightArea;

        private (int vx, int vz) GetHoveredVertex()
        {
            if (_vm == null || !IsHeightTool(_vm.SelectedTool))
                return (-1, -1);

            double uiX = MapPixels - _vm.CursorX;
            double uiZ = MapPixels - _vm.CursorZ;

            int vx = (int)Math.Round(uiX / TileSize);
            int vz = (int)Math.Round(uiZ / TileSize);

            if (vx < 0 || vx > TilesPerSide || vz < 0 || vz > TilesPerSide)
                return (-1, -1);

            double cx = vx * TileSize;
            double cz = vz * TileSize;
            double dx = uiX - cx;
            double dz = uiZ - cz;
            double distSq = dx * dx + dz * dz;

            return distSq <= HitRadiusSq ? (vx, vz) : (-1, -1);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (_vm == null) return;
            if (!IsAreaDragTool(_vm.SelectedTool)) return;

            Focus();
            CaptureMouse();

            _isAreaDragging = true;
            _areaStart = e.GetPosition(this);
            _areaEnd = _areaStart;
            _areaRectPx = new Rect(_areaStart, _areaEnd);

            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_isAreaDragging) return;
            if (_vm == null || !IsAreaDragTool(_vm.SelectedTool)) return;

            _areaEnd = e.GetPosition(this);
            _areaRectPx = new Rect(_areaStart, _areaEnd);

            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (!_isAreaDragging) return;

            try
            {
                if (_vm != null)
                {
                    _areaEnd = e.GetPosition(this);
                    _areaRectPx = new Rect(_areaStart, _areaEnd);

                    if (_vm.SelectedTool == EditorTool.AreaSetHeight)
                        ApplyAreaSetHeight(_areaRectPx);
                    else if (_vm.SelectedTool == EditorTool.RandomizeHeightArea)
                        ApplyAreaRandomize(_areaRectPx);
                }
            }
            finally
            {
                _isAreaDragging = false;
                _areaRectPx = Rect.Empty;

                ReleaseMouseCapture();
                InvalidateVisual();
            }

            e.Handled = true;
        }

        private void ApplyAreaSetHeight(Rect rectPx)
        {
            if (_vm == null) return;
            if (!MapDataService.Instance.IsLoaded) return;

            rectPx = NormalizeRect(rectPx);

            if (rectPx.Width < 2 || rectPx.Height < 2)
                return;

            int vx0 = ClampToVertexIndex((int)Math.Floor(rectPx.Left / TileSize));
            int vx1 = ClampToVertexIndex((int)Math.Ceiling(rectPx.Right / TileSize));
            int vz0 = ClampToVertexIndex((int)Math.Floor(rectPx.Top / TileSize));
            int vz1 = ClampToVertexIndex((int)Math.Ceiling(rectPx.Bottom / TileSize));

            int minVX = Math.Min(vx0, vx1);
            int maxVX = Math.Max(vx0, vx1);
            int minVZ = Math.Min(vz0, vz1);
            int maxVZ = Math.Max(vz0, vz1);

            int tx0 = Math.Clamp(minVX - 1, 0, TilesPerSide - 1);
            int tx1 = Math.Clamp(maxVX - 1, 0, TilesPerSide - 1);
            int ty0 = Math.Clamp(minVZ - 1, 0, TilesPerSide - 1);
            int ty1 = Math.Clamp(maxVZ - 1, 0, TilesPerSide - 1);

            int raw = Math.Clamp(_vm.AreaSetHeightValue, -127, 127);

            int changed = _accessor.WriteHeightRegion(tx0, ty0, tx1, ty1, (sbyte)raw);

            if (changed > 0 && Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
            {
                int width = tx1 - tx0 + 1;
                int height = ty1 - ty0 + 1;
                shell.StatusMessage = $"Set height {raw} on {width}-{height} vertices";
            }
        }

        private void ApplyAreaRandomize(Rect rectPx)
        {
            if (_vm == null) return;
            if (!MapDataService.Instance.IsLoaded) return;

            rectPx = NormalizeRect(rectPx);
            if (rectPx.Width < 2 || rectPx.Height < 2) return;

            int vx0 = ClampToVertexIndex((int)Math.Floor(rectPx.Left  / TileSize));
            int vx1 = ClampToVertexIndex((int)Math.Ceiling(rectPx.Right  / TileSize));
            int vz0 = ClampToVertexIndex((int)Math.Floor(rectPx.Top    / TileSize));
            int vz1 = ClampToVertexIndex((int)Math.Ceiling(rectPx.Bottom / TileSize));

            int minVX = Math.Min(vx0, vx1);
            int maxVX = Math.Max(vx0, vx1);
            int minVZ = Math.Min(vz0, vz1);
            int maxVZ = Math.Max(vz0, vz1);

            // Vertex index → tile index (vertex vx sits at the corner of tile tx = vx-1)
            int tx0 = Math.Clamp(minVX - 1, 0, TilesPerSide - 1);
            int tx1 = Math.Clamp(maxVX - 1, 0, TilesPerSide - 1);
            int ty0 = Math.Clamp(minVZ - 1, 0, TilesPerSide - 1);
            int ty1 = Math.Clamp(maxVZ - 1, 0, TilesPerSide - 1);

            int areaW = tx1 - tx0 + 1;
            int areaH = ty1 - ty0 + 1;

            int seed = Environment.TickCount;
            const double roughness  = 0.60;
            const int    blurPasses = 1;

            // Generate coherent fractal terrain sized exactly to the selected area
            sbyte[,] heights = Services.Heights.TerrainGenerator.GenerateHeightsArea(
                seed, areaW, areaH, roughness, blurPasses);

            for (int tx = tx0; tx <= tx1; tx++)
                for (int ty = ty0; ty <= ty1; ty++)
                    _accessor.WriteHeight(tx, ty, heights[tx - tx0, ty - ty0]);

            HeightsChangeBus.Instance.NotifyRegion(tx0, ty0, tx1, ty1);
            MapDataService.Instance.MarkDirty();

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                shell.StatusMessage = $"Randomised {areaW}\u00d7{areaH} vertices (seed {seed})";
        }

        private static Rect NormalizeRect(Rect r)
        {
            return new Rect(
                Math.Min(r.Left, r.Right),
                Math.Min(r.Top, r.Bottom),
                Math.Abs(r.Width),
                Math.Abs(r.Height));
        }

        private static int ClampToVertexIndex(int v)
        {
            if (v < 1) return 1;
            if (v > TilesPerSide) return TilesPerSide;
            return v;
        }

        private VertexBounds GetBrushBounds(int centerVX, int centerVZ)
        {
            if (centerVX < 0 || centerVZ < 0)
                return VertexBounds.Empty;

            int n = Math.Max(1, _vm?.BrushSize ?? 1);
            int left = (n - 1) / 2;
            int right = n / 2;

            int minX = Math.Max(1, centerVX - left);
            int maxX = Math.Min(TilesPerSide, centerVX + right);
            int minZ = Math.Max(1, centerVZ - left);
            int maxZ = Math.Min(TilesPerSide, centerVZ + right);

            return (minX > maxX || minZ > maxZ)
                ? VertexBounds.Empty
                : new VertexBounds(minX, maxX, minZ, maxZ);
        }

        private readonly struct VertexBounds
        {
            public readonly int MinX, MaxX, MinZ, MaxZ;
            public readonly bool IsEmpty;

            public static VertexBounds Empty => new(1, 0, 1, 0, true);

            public VertexBounds(int minX, int maxX, int minZ, int maxZ, bool isEmpty = false)
            {
                MinX = minX;
                MaxX = maxX;
                MinZ = minZ;
                MaxZ = maxZ;
                IsEmpty = isEmpty || minX > maxX || minZ > maxZ;
            }

            public bool Contains(int vx, int vz) =>
                !IsEmpty && vx >= MinX && vx <= MaxX && vz >= MinZ && vz <= MaxZ;
        }
    }
}