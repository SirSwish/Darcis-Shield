using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Roofs.MapOverlays
{
    /// <summary>
    /// Draws DWalkable coverage on the map.
    /// - Base solid fill + fully-opaque diagonal hatch overlay (no outline in normal pass)
    /// - Height bubble (w.Y) in the center of each walkable rect (if large enough)
    /// - Optional glow stack highlight
    ///
    /// BEHAVIOUR:
    /// - If SelectedBuildingId > 0: only draw walkables for that building.
    /// - If no building selected: draw all walkables.
    /// - If SelectedWalkableId1 > 0: highlight only that walkable (within the selected building).
    /// </summary>
    public sealed class WalkablesLayer : FrameworkElement
    {
        // Pens (kept, but not used for normal pass outlines)
        private static readonly Pen PenUsed;
        private static readonly Pen PenUnused;

        // Highlight pens (glow stack)
        private readonly Pen _glowPenWide;
        private readonly Pen _glowPenNarrow;
        private readonly Pen _edgePen;

        // Fills / hatch
        private static readonly Brush FillUsed;
        private static readonly Brush HatchUsed;
        private static readonly Brush FillUnused;
        private static readonly Brush HatchUnused;
        private static readonly Brush FillHighlight;

        // Optional dim wash (to make selected walkable pop)
        private static readonly Brush DimWash;

        // Bubble (height label)
        private static readonly Brush BubbleFill;
        private static readonly Pen BubbleEdge;
        private static readonly Brush BubbleText;

        // Cached parsed data (per map load)
        private DWalkableRec[]? _walkables;     // index 0 is sentinel, real data starts at 1
        private RoofFace4Rec[]? _roofFaces4;    // not rendered yet, but cached for future
        private int _walkableCount;             // number of valid entries (excluding sentinel)

        // VM selection
        private MapViewModel? _vm;
        private int _selBuildingId;
        private int _selWalkableId1;            // <-- NEW

        static WalkablesLayer()
        {
            static Pen Make(Pen p)
            {
                p.LineJoin = PenLineJoin.Round;
                p.StartLineCap = PenLineCap.Round;
                p.EndLineCap = PenLineCap.Round;
                p.Freeze();
                return p;
            }

            // Pens kept (mostly used for optional selection glow/edge look if you want)
            var usedBrush = new SolidColorBrush(Color.FromArgb(255, 0, 210, 180));
            usedBrush.Freeze();
            PenUsed = Make(new Pen(usedBrush, 5.0));

            var unusedBrush = new SolidColorBrush(Color.FromArgb(230, 140, 140, 140));
            unusedBrush.Freeze();
            var dashed = new Pen(unusedBrush, 4.0) { DashStyle = DashStyles.Dash, DashCap = PenLineCap.Round };
            PenUnused = Make(dashed);

            // ---- Base fills (strong coverage) ----
            FillUsed = new SolidColorBrush(Color.FromArgb(210, 0, 210, 180));      // bluey-green, strong
            FillUsed.Freeze();

            FillUnused = new SolidColorBrush(Color.FromArgb(120, 120, 120, 120));  // subtle
            FillUnused.Freeze();

            // ---- Fully-opaque diagonal hatch overlay ----
            HatchUsed = CreateHatchBrush(Color.FromArgb(255, 0, 120, 100), 3.2, 14.0);
            HatchUnused = CreateHatchBrush(Color.FromArgb(255, 90, 90, 90), 2.4, 14.0);

            // ---- Highlight wash (under glow stack) ----
            FillHighlight = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
            FillHighlight.Freeze();

            // ---- Dim wash (used when a specific walkable is selected) ----
            DimWash = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)); // darken others a bit
            DimWash.Freeze();

            // ---- Bubble style ----
            BubbleFill = new SolidColorBrush(Color.FromArgb(230, 10, 10, 10)); // near-black
            BubbleFill.Freeze();

            var edgeBrush = new SolidColorBrush(Color.FromArgb(255, 245, 245, 245));
            edgeBrush.Freeze();
            BubbleEdge = new Pen(edgeBrush, 2.2);
            BubbleEdge.Freeze();

            BubbleText = Brushes.White; // already frozen
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

            // One tile worth of diagonal lines
            var g = new GeometryGroup();
            g.Children.Add(new LineGeometry(new Point(0, tile), new Point(tile, 0)));
            g.Children.Add(new LineGeometry(new Point(-tile, tile), new Point(tile, -tile))); // seamless tiling

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
            IsHitTestVisible = false;

            // Glow pens (same vibe as BuildingLayer)
            var glowOuter = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)); glowOuter.Freeze();
            var glowInner = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)); glowInner.Freeze();
            var edgeBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)); edgeBrush.Freeze();

            _glowPenWide = new Pen(glowOuter, 10.0) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            _glowPenNarrow = new Pen(glowInner, 6.0) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            _edgePen = new Pen(edgeBrush, 1.8) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            _glowPenWide.Freeze(); _glowPenNarrow.Freeze(); _edgePen.Freeze();

            // lifecycle hooks
            var svc = MapDataService.Instance;
            svc.MapLoaded += (_, __) => { SeedFromService(); Dispatcher.Invoke(InvalidateVisual); };
            svc.MapBytesReset += (_, __) => { SeedFromService(); Dispatcher.Invoke(InvalidateVisual); };
            svc.MapCleared += (_, __) => { ClearCache(); Dispatcher.Invoke(InvalidateVisual); };

            DataContextChanged += (_, __) => HookVm();
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        private void HookVm()
        {
            if (_vm != null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as MapViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVmChanged;
                _selBuildingId = _vm.SelectedBuildingId;
                _selWalkableId1 = _vm.SelectedWalkableId1; // <-- NEW
            }
            InvalidateVisual();
        }

        private void OnVmChanged(object? s, PropertyChangedEventArgs e)
        {
            if (_vm == null) return;

            if (e.PropertyName == nameof(MapViewModel.SelectedBuildingId))
            {
                _selBuildingId = _vm.SelectedBuildingId;

                // If building changed, clear walkable selection (optional but sane)
                if (_selBuildingId <= 0)
                    _selWalkableId1 = 0;

                Dispatcher.Invoke(InvalidateVisual);
                return;
            }

            // NEW: listen for selected walkable changes
            if (e.PropertyName == nameof(MapViewModel.SelectedWalkableId1))
            {
                _selWalkableId1 = _vm.SelectedWalkableId1;
                Dispatcher.Invoke(InvalidateVisual);
                return;
            }
        }

        private void ClearCache()
        {
            _walkables = null;
            _roofFaces4 = null;
            _walkableCount = 0;
        }

        /// <summary>
        /// Pull parsed walkables/rooffaces from the service (preferred).
        /// </summary>
        private void SeedFromService()
        {
            ClearCache();

            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
            {
                Debug.WriteLine("[Walkables] Seed: no map loaded.");
                return;
            }

            if (!svc.TryGetWalkables(out var walkables, out var roofFaces4))
            {
                Debug.WriteLine("[Walkables] Seed: walkables not available.");
                return;
            }

            _walkables = walkables;
            _roofFaces4 = roofFaces4;
            _walkableCount = (_walkables.Length > 0) ? (_walkables.Length - 1) : 0;

            Debug.WriteLine($"[Walkables] Seed OK. walkables={_walkableCount} roofFace4={_roofFaces4?.Length ?? 0}");
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_walkables is null)
            {
                SeedFromService();
                if (_walkables is null) return;
            }
            if (_walkableCount <= 0) return;

            bool filterToSelectedBuilding = _selBuildingId > 0;

            // NEW: when a walkable is selected (and a building is selected), show ONLY that walkable.
            bool showOnlySelectedWalkable = filterToSelectedBuilding && _selWalkableId1 > 0;

            int drawn = 0;

            // -------- Normal pass: draw coverage (fill + hatch; no outline) ----------
            if (showOnlySelectedWalkable)
            {
                int i = _selWalkableId1;

                if (i >= 1 && i <= _walkableCount)
                {
                    var w = _walkables[i];

                    // Safety: only draw if it actually belongs to the selected building.
                    if (w.Building == _selBuildingId && (w.X1 | w.Z1 | w.X2 | w.Z2) != 0)
                    {
                        bool hasFaces = w.EndFace4 > w.StartFace4;
                        Rect r = ToMapRect(w.X1, w.Z1, w.X2, w.Z2);

                        dc.DrawRectangle(hasFaces ? FillUsed : FillUnused, null, r);
                        dc.DrawRectangle(hasFaces ? HatchUsed : HatchUnused, null, r);
                        DrawHeightBubble(dc, this, r, w.Y);

                        drawn = 1;

                        // Optional: keep your glow stack to make it scream "selected"
                        dc.DrawRectangle(FillHighlight, null, r);
                        dc.DrawRectangle(HatchUsed, null, r);
                        dc.DrawRectangle(null, _glowPenWide, r);
                        dc.DrawRectangle(null, _glowPenNarrow, r);
                        dc.DrawRectangle(null, _edgePen, r);
                    }
                }
            }
            else
            {
                for (int i = 1; i <= _walkableCount; i++)
                {
                    var w = _walkables[i];

                    // If a building is selected, only draw that building's walkables.
                    if (filterToSelectedBuilding && w.Building != _selBuildingId)
                        continue;

                    // Skip empty rects
                    if ((w.X1 | w.Z1 | w.X2 | w.Z2) == 0)
                        continue;

                    bool hasFaces = w.EndFace4 > w.StartFace4;
                    Rect r = ToMapRect(w.X1, w.Z1, w.X2, w.Z2);

                    dc.DrawRectangle(hasFaces ? FillUsed : FillUnused, null, r);
                    dc.DrawRectangle(hasFaces ? HatchUsed : HatchUnused, null, r);
                    DrawHeightBubble(dc, this, r, w.Y);

                    drawn++;
                }

                // Existing behavior: highlight all walkables for selected building
                if (_selBuildingId > 0)
                {
                    for (int i = 1; i <= _walkableCount; i++)
                    {
                        var w = _walkables[i];
                        if (w.Building != _selBuildingId) continue;
                        if ((w.X1 | w.Z1 | w.X2 | w.Z2) == 0) continue;

                        Rect r = ToMapRect(w.X1, w.Z1, w.X2, w.Z2);

                        dc.DrawRectangle(FillHighlight, null, r);
                        dc.DrawRectangle(HatchUsed, null, r);
                        dc.DrawRectangle(null, _glowPenWide, r);
                        dc.DrawRectangle(null, _glowPenNarrow, r);
                        dc.DrawRectangle(null, _edgePen, r);
                    }
                }
            }

            Debug.WriteLine($"[Walkables] Render drew {drawn} rects. selBld={_selBuildingId} selWalk={_selWalkableId1}");
        }

        /// <summary>
        /// Convert a walkable rect in map coords (X/Z in 0..127-ish) to screen pixels,
        /// matching BuildingLayer’s coordinate inversion: px = (128 - x) * 64.
        /// </summary>
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

        private static void DrawHeightBubble(DrawingContext dc, Visual visual, Rect r, int y)
        {
            // Skip if too small to be readable
            double minDim = r.Width < r.Height ? r.Width : r.Height;
            if (minDim < 28) return;

            var center = new Point(r.Left + r.Width * 0.5, r.Top + r.Height * 0.5);
            double radius = Math.Clamp(minDim * 0.18, 12, 26);

            dc.DrawEllipse(BubbleFill, BubbleEdge, center, radius, radius);

            var dpi = VisualTreeHelper.GetDpi(visual).PixelsPerDip;
            string txt = y.ToString(CultureInfo.InvariantCulture);

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
