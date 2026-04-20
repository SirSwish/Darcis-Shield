using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.Views.Roofs.Dialogs;
using HeightSettings = UrbanChaosMapEditor.Models.Core.HeightDisplaySettings;

namespace UrbanChaosMapEditor.Views.Roofs.MapOverlays
{
    /// <summary>
    /// Draws ONLY the selected walkable on the map.
    /// - If no walkable is selected: draws nothing.
    /// - If a walkable is selected: draws only that walkable.
    /// </summary>
    public sealed class WalkablesLayer : FrameworkElement
    {
        private readonly Pen _glowPenWide;
        private readonly Pen _glowPenNarrow;
        private readonly Pen _edgePen;

        private static readonly Brush FillUsed;
        private static readonly Brush HatchUsed;
        private static readonly Brush FillUnused;
        private static readonly Brush HatchUnused;
        private static readonly Brush FillHighlight;

        private static readonly Brush BubbleFill;
        private static readonly Pen BubbleEdge;
        private static readonly Brush BubbleText;

        private DWalkableRec[]? _walkables;
        private int _walkableCount;

        private MapViewModel? _vm;
        private int _selWalkableId1;

        // Cached bubble geometry for hit-testing
        private Point? _bubbleCenter;
        private double _bubbleRadius;

        static WalkablesLayer()
        {
            FillUsed = new SolidColorBrush(Color.FromArgb(210, 0, 210, 180));
            FillUsed.Freeze();

            FillUnused = new SolidColorBrush(Color.FromArgb(120, 120, 120, 120));
            FillUnused.Freeze();

            HatchUsed = CreateHatchBrush(Color.FromArgb(255, 0, 120, 100), 3.2, 14.0);
            HatchUnused = CreateHatchBrush(Color.FromArgb(255, 90, 90, 90), 2.4, 14.0);

            FillHighlight = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
            FillHighlight.Freeze();

            BubbleFill = new SolidColorBrush(Color.FromArgb(230, 10, 10, 10));
            BubbleFill.Freeze();

            var edgeBrush = new SolidColorBrush(Color.FromArgb(255, 245, 245, 245));
            edgeBrush.Freeze();
            BubbleEdge = new Pen(edgeBrush, 2.2);
            BubbleEdge.Freeze();

            BubbleText = Brushes.White;
        }

        private static DrawingBrush CreateHatchBrush(Color lineColor, double thickness, double tile)
        {
            var b = new SolidColorBrush(lineColor);
            b.Freeze();

            var p = new Pen(b, thickness)
            {
                StartLineCap = PenLineCap.Square,
                EndLineCap = PenLineCap.Square
            };
            p.Freeze();

            var g = new GeometryGroup();
            g.Children.Add(new LineGeometry(new Point(0, tile), new Point(tile, 0)));
            g.Children.Add(new LineGeometry(new Point(-tile, tile), new Point(tile, -tile)));

            var drawing = new GeometryDrawing(null, p, g);

            var brush = new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, tile, tile),
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None
            };
            brush.Freeze();
            return brush;
        }

        public WalkablesLayer()
        {
            Width = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = true;

            var glowOuter = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)); glowOuter.Freeze();
            var glowInner = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)); glowInner.Freeze();
            var edgeBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)); edgeBrush.Freeze();

            _glowPenWide = new Pen(glowOuter, 10.0) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            _glowPenNarrow = new Pen(glowInner, 6.0) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            _edgePen = new Pen(edgeBrush, 1.8) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            _glowPenWide.Freeze();
            _glowPenNarrow.Freeze();
            _edgePen.Freeze();

            var svc = MapDataService.Instance;
            svc.MapLoaded += (_, __) => { SeedFromService(); Dispatcher.Invoke(InvalidateVisual); };
            svc.MapBytesReset += (_, __) => { SeedFromService(); Dispatcher.Invoke(InvalidateVisual); };
            svc.MapCleared += (_, __) => { ClearCache(); Dispatcher.Invoke(InvalidateVisual); };

            Services.Buildings.BuildingsChangeBus.Instance.Changed +=
                (_, __) => { SeedFromService(); Dispatcher.Invoke(InvalidateVisual); };

            HeightSettings.DisplayModeChanged += (_, __) => Dispatcher.Invoke(InvalidateVisual);

            DataContextChanged += (_, __) => HookVm();
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        private void HookVm()
        {
            if (_vm != null)
                _vm.PropertyChanged -= OnVmChanged;

            _vm = DataContext as MapViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVmChanged;
                _selWalkableId1 = _vm.SelectedWalkableId1;
            }

            InvalidateVisual();
        }

        private void OnVmChanged(object? s, PropertyChangedEventArgs e)
        {
            if (_vm == null) return;

            if (e.PropertyName == nameof(MapViewModel.SelectedWalkableId1))
            {
                _selWalkableId1 = _vm.SelectedWalkableId1;
                Dispatcher.Invoke(InvalidateVisual);
            }
        }

        private void ClearCache()
        {
            _walkables = null;
            _walkableCount = 0;
        }

        private void SeedFromService()
        {
            ClearCache();

            var svc = MapDataService.Instance;
            if (!svc.IsLoaded) return;
            if (!svc.TryGetWalkables(out var walkables, out _)) return;

            _walkables = walkables;
            _walkableCount = (_walkables.Length > 0) ? (_walkables.Length - 1) : 0;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            _bubbleCenter = null;
            _bubbleRadius = 0;

            if (_walkables is null)
            {
                SeedFromService();
                if (_walkables is null) return;
            }

            if (_walkableCount <= 0) return;
            if (_selWalkableId1 <= 0 || _selWalkableId1 > _walkableCount) return;

            var w = _walkables[_selWalkableId1];
            if ((w.X1 | w.Z1 | w.X2 | w.Z2) == 0) return;

            bool hasFaces = w.EndFace4 > w.StartFace4;
            Rect r = ToMapRect(w.X1, w.Z1, w.X2, w.Z2);

            // Cache bubble geometry for hit-testing
            double minDim = r.Width < r.Height ? r.Width : r.Height;
            if (minDim >= 28)
            {
                _bubbleCenter = new Point(r.Left + r.Width * 0.5, r.Top + r.Height * 0.5);
                _bubbleRadius = Math.Clamp(minDim * 0.18, 12, 26);
            }

            dc.DrawRectangle(hasFaces ? FillUsed : FillUnused, null, r);
            dc.DrawRectangle(hasFaces ? HatchUsed : HatchUnused, null, r);
            DrawHeightBubble(dc, this, r, w.Y);

            dc.DrawRectangle(FillHighlight, null, r);
            dc.DrawRectangle(hasFaces ? HatchUsed : HatchUnused, null, r);
            dc.DrawRectangle(null, _glowPenWide, r);
            dc.DrawRectangle(null, _glowPenNarrow, r);
            dc.DrawRectangle(null, _edgePen, r);

            Debug.WriteLine($"[Walkables] Render selected walkable {_selWalkableId1}");
        }

        private static Rect ToMapRect(int x1, int z1, int x2, int z2)
        {
            int minX = x1 < x2 ? x1 : x2;
            int maxX = x1 < x2 ? x2 : x1;
            int minZ = z1 < z2 ? z1 : z2;
            int maxZ = z1 < z2 ? z2 : z1;

            double left = (128 - maxX) * 64.0;
            double right = (128 - minX) * 64.0;
            double top = (128 - maxZ) * 64.0;
            double bottom = (128 - minZ) * 64.0;

            double w = right - left;
            double h = bottom - top;
            if (w < 0) { left += w; w = -w; }
            if (h < 0) { top += h; h = -h; }

            return new Rect(left, top, w, h);
        }

        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            if (_bubbleCenter.HasValue)
            {
                Point pos = hitTestParameters.HitPoint;
                double dx = pos.X - _bubbleCenter.Value.X;
                double dy = pos.Y - _bubbleCenter.Value.Y;
                if (dx * dx + dy * dy <= _bubbleRadius * _bubbleRadius)
                    return new PointHitTestResult(this, pos);
            }
            return null!;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (!_bubbleCenter.HasValue) return;

            Point pos = e.GetPosition(this);
            double dx = pos.X - _bubbleCenter.Value.X;
            double dy = pos.Y - _bubbleCenter.Value.Y;
            if (dx * dx + dy * dy > _bubbleRadius * _bubbleRadius) return;

            // Consume every click on the bubble so it never falls through to the buildings layer.
            e.Handled = true;

            if (e.ClickCount != 2) return;

            var svc = MapDataService.Instance;
            if (!svc.TryGetWalkables(out var walkables, out var roofFaces4)) return;
            if (_selWalkableId1 < 1 || _selWalkableId1 >= walkables.Length) return;

            var dlg = new WalkablePreviewWindow(_selWalkableId1, walkables[_selWalkableId1], roofFaces4)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            dlg.Show();
        }

        private static void DrawHeightBubble(DrawingContext dc, Visual visual, Rect r, int y)
        {
            double minDim = r.Width < r.Height ? r.Width : r.Height;
            if (minDim < 28) return;

            var center = new Point(r.Left + r.Width * 0.5, r.Top + r.Height * 0.5);
            double radius = Math.Clamp(minDim * 0.18, 12, 26);

            dc.DrawEllipse(BubbleFill, BubbleEdge, center, radius, radius);

            var dpi = VisualTreeHelper.GetDpi(visual).PixelsPerDip;
            string txt = (HeightSettings.ShowRawHeights ? y : y / 2).ToString(CultureInfo.InvariantCulture);

            double fontSize = radius * 0.95;
            var ft = new FormattedText(
                txt,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"),
                fontSize,
                BubbleText,
                dpi
            );

            dc.DrawText(ft, new Point(center.X - ft.Width * 0.5, center.Y - ft.Height * 0.52));
        }
    }
}