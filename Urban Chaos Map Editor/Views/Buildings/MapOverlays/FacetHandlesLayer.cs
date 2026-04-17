using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Buildings.MapOverlays
{
    /// <summary>
    /// Interactive overlay for the currently selected facet.
    ///
    /// Three drag modes:
    ///   Handle.P0   – drag the XZ0 endpoint (cyan bulb)
    ///   Handle.P1   – drag the XZ1 endpoint (orange bulb)
    ///   Handle.Line – click-and-drag anywhere on the line body to move the whole facet
    ///
    /// Only claims hit-test at handles or within LineHitDist of the line, so all
    /// other canvas interactions pass through unaffected.
    /// </summary>
    public sealed class FacetHandlesLayer : FrameworkElement
    {
        // ── constants ────────────────────────────────────────────────────────────
        private const double HandleRadius = 7.0;     // canvas-space pixels, endpoint bulbs
        private const double LineHitDist  =  8.0;    // canvas-space pixels, line body hit zone
        private const double TileSize     = 64.0;
        private const int    CoordMax     = 127;

        // ── brushes / pens (frozen for perf) ─────────────────────────────────────
        private static readonly Brush BrushP0;
        private static readonly Brush BrushP1;
        private static readonly Brush BrushHot;
        private static readonly Pen   PenOutline;
        private static readonly Pen   PenOutlineHot;
        private static readonly Pen   PenPreviewLine;
        private static readonly Pen   PenLineDragBody;
        private static readonly Pen   PenFacingArrow;

        static FacetHandlesLayer()
        {
            BrushP0  = new SolidColorBrush(Color.FromArgb(220, 102, 255,   0)); BrushP0.Freeze();
            BrushP1  = new SolidColorBrush(Color.FromArgb(220, 102, 255,   0)); BrushP1.Freeze();
            BrushHot = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)); BrushHot.Freeze();

            PenOutline    = new Pen(Brushes.Black, 2.0);  PenOutline.Freeze();
            PenOutlineHot = new Pen(Brushes.White, 2.5);  PenOutlineHot.Freeze();

            var dashBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)); dashBrush.Freeze();
            PenPreviewLine = new Pen(dashBrush, 2.0) { DashStyle = DashStyles.Dash }; PenPreviewLine.Freeze();

            var bodyBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 100)); bodyBrush.Freeze();
            PenLineDragBody = new Pen(bodyBrush, 4.0); PenLineDragBody.Freeze();

            var arrowBrush = new SolidColorBrush(Color.FromRgb(102, 255, 0)); arrowBrush.Freeze();
            PenFacingArrow = new Pen(arrowBrush, 2.5)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap   = PenLineCap.Round
            };
            PenFacingArrow.Freeze();
        }

        // ── view-model link ──────────────────────────────────────────────────────
        private MapViewModel?           _mapVm;
        private INotifyPropertyChanged? _mapVmNotify;

        // ── selected-facet state ─────────────────────────────────────────────────
        private int   _facetId1;    // 1-based; 0 = nothing selected
        private Point _p0;          // canvas pixel for XZ0
        private Point _p1;          // canvas pixel for XZ1
        private byte  _facetType;   // raw FacetType byte for the selected facet

        // ── interaction state ────────────────────────────────────────────────────
        private enum Handle { None, P0, P1, Line }
        private Handle _dragging = Handle.None;
        private Handle _hovered  = Handle.None;

        // Raw mouse position updated on every move while dragging.
        private Point  _dragCurrent;

        // Used only during Handle.Line drags.
        private Vector _lineDragOffset;  // (click→P0) vector captured at drag-start
        private Vector _lineDelta;       // (P0→P1) pixel vector, constant during drag

        // ── lifecycle ────────────────────────────────────────────────────────────
        public FacetHandlesLayer()
        {
            IsHitTestVisible = false;

            MapDataService.Instance.MapCleared  += (_, _) => Dispatcher.BeginInvoke(ClearFacet);
            MapDataService.Instance.MapLoaded   += (_, _) => Dispatcher.BeginInvoke(ClearFacet);
            BuildingsChangeBus.Instance.Changed += (_, _) => Dispatcher.BeginInvoke(RefreshFromSelection);

            Loaded   += (_, _) => HookMapVm();
            Unloaded += (_, _) => UnhookMapVm();
        }

        private void HookMapVm()
        {
            if (_mapVmNotify != null) return;
            if (Application.Current?.MainWindow?.DataContext is MainWindowViewModel shell)
            {
                _mapVm       = shell.Map;
                _mapVmNotify = shell.Map;
                _mapVmNotify.PropertyChanged += OnVmPropertyChanged;
            }
        }

        private void UnhookMapVm()
        {
            if (_mapVmNotify == null) return;
            _mapVmNotify.PropertyChanged -= OnVmPropertyChanged;
            _mapVmNotify = null;
            _mapVm       = null;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapViewModel.SelectedFacetId) ||
                e.PropertyName == nameof(MapViewModel.IsMultiDrawingFacets) ||
                e.PropertyName == nameof(MapViewModel.ShowBuildings))
                Dispatcher.BeginInvoke(RefreshFromSelection);
        }

        // ── state management ─────────────────────────────────────────────────────
        private void ClearFacet()
        {
            _facetId1        = 0;
            _dragging        = Handle.None;
            _hovered         = Handle.None;
            IsHitTestVisible = false;
            InvalidateVisual();
        }

        private void RefreshFromSelection()
        {
            // Suppress handles entirely while the user is drawing new facets.
            if (_mapVm?.IsMultiDrawingFacets == true)
            {
                ClearFacet();
                return;
            }

            // Hide handles when the buildings layer is switched off.
            if (_mapVm?.ShowBuildings == false)
            {
                ClearFacet();
                return;
            }

            if (_mapVm?.SelectedFacetId is int id && id > 0)
            {
                var snap = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();
                int idx0 = id - 1;
                if (idx0 >= 0 && idx0 < snap.Facets.Length)
                {
                    var f     = snap.Facets[idx0];
                    _facetId1  = id;
                    _p0        = CoordToPixel(f.X0, f.Z0);
                    _p1        = CoordToPixel(f.X1, f.Z1);
                    _facetType = (byte)f.Type;
                    IsHitTestVisible = true;
                    InvalidateVisual();
                    return;
                }
            }
            ClearFacet();
        }

        // ── coordinate helpers ───────────────────────────────────────────────────
        private static Point CoordToPixel(byte cx, byte cz)
            => new((128 - cx) * TileSize, (128 - cz) * TileSize);

        private static (byte cx, byte cz) PixelToCoord(Point p)
        {
            int cx = Math.Clamp((int)Math.Round(128.0 - p.X / TileSize), 0, CoordMax);
            int cz = Math.Clamp((int)Math.Round(128.0 - p.Y / TileSize), 0, CoordMax);
            return ((byte)cx, (byte)cz);
        }

        private static Point SnapPixel(Point p)
        {
            var (cx, cz) = PixelToCoord(p);
            return CoordToPixel(cx, cz);
        }

        // ── drag preview positions ───────────────────────────────────────────────
        /// <summary>Computes the preview P0 position for the current drag state.</summary>
        private Point DragP0()
        {
            if (_dragging == Handle.P0)   return SnapPixel(_dragCurrent);
            if (_dragging == Handle.Line) return SnapPixel(_dragCurrent + _lineDragOffset);
            return _p0;
        }

        /// <summary>Computes the preview P1 position for the current drag state.</summary>
        private Point DragP1()
        {
            if (_dragging == Handle.P1)   return SnapPixel(_dragCurrent);
            if (_dragging == Handle.Line) return DragP0() + _lineDelta;
            return _p1;
        }

        // ── public query (used by MapView to short-circuit facet picking) ─────────
        /// <summary>
        /// Returns true if <paramref name="pos"/> (canvas-space) is on a handle or the line body,
        /// meaning this layer will handle the interaction.
        /// </summary>
        public bool IsHandleAt(Point pos)
            => _facetId1 > 0 && HitHandle(pos) != Handle.None;

        // ── hit testing ──────────────────────────────────────────────────────────
        protected override HitTestResult HitTestCore(PointHitTestParameters p)
        {
            if (_facetId1 > 0 && HitHandle(p.HitPoint) != Handle.None)
                return new PointHitTestResult(this, p.HitPoint);
            return null!;
        }

        /// <summary>
        /// Priority: endpoints first, then line body.
        /// Endpoints must take priority so users can still resize near a short facet.
        /// </summary>
        private Handle HitHandle(Point pos)
        {
            if (Dist(pos, _p0) <= HandleRadius) return Handle.P0;
            if (Dist(pos, _p1) <= HandleRadius) return Handle.P1;
            if (DistToSegment(pos, _p0, _p1) <= LineHitDist) return Handle.Line;
            return Handle.None;
        }

        private static double Dist(Point a, Point b)
        {
            double dx = a.X - b.X, dz = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static double DistToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X, dz = b.Y - a.Y;
            double lenSq = dx * dx + dz * dz;
            if (lenSq == 0.0) return Dist(p, a);
            double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dz) / lenSq, 0.0, 1.0);
            return Dist(p, new Point(a.X + t * dx, a.Y + t * dz));
        }

        // ── mouse interaction ─────────────────────────────────────────────────────
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_facetId1 == 0) return;

            var pos = e.GetPosition(this);

            if (_dragging != Handle.None)
            {
                _dragCurrent = pos;
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Hover
            var prev = _hovered;
            _hovered = HitHandle(pos);
            if (_hovered != prev)
            {
                Cursor = _hovered != Handle.None ? Cursors.SizeAll : null;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_facetId1 == 0) return;

            // Double-clicks are passed through so MapView can open the facet editor.
            if (e.ClickCount >= 2) return;

            var pos = e.GetPosition(this);
            var hit = HitHandle(pos);
            if (hit == Handle.None) return;

            _dragging    = hit;
            _dragCurrent = pos;

            if (hit == Handle.Line)
            {
                _lineDragOffset = _p0 - pos;   // Vector: how far click is from P0
                _lineDelta      = _p1 - _p0;   // Vector: P0→P1 (preserved throughout drag)
            }

            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragging == Handle.None) return;

            _dragCurrent = e.GetPosition(this);

            // Capture final positions BEFORE clearing _dragging — DragP0/DragP1 depend on it.
            Point finalP0 = DragP0();
            Point finalP1 = DragP1();

            ReleaseMouseCapture();
            var target = _dragging;
            _dragging   = Handle.None;

            CommitDrag(target, finalP0, finalP1);
            e.Handled = true;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            if (_dragging == Handle.None) return;

            // Cancel — restore without saving.
            ReleaseMouseCapture();
            _dragging = Handle.None;
            InvalidateVisual();
            e.Handled = true;
        }

        private void CommitDrag(Handle target, Point finalP0, Point finalP1)
        {
            if (_facetId1 == 0) return;

            var (x0, z0) = PixelToCoord(finalP0);
            var (x1, z1) = PixelToCoord(finalP1);

            if (x0 != x1 && z0 != z1 && (FacetType)_facetType != FacetType.Cable)
            {
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Diagonal facets are not allowed — facets must be axis-aligned.";
                InvalidateVisual();
                return;
            }

            var acc = new BuildingsAccessor(MapDataService.Instance);
            bool ok = acc.TryUpdateFacetCoords(_facetId1, x0, z0, x1, z1);

            if (ok)
            {
                _p0 = CoordToPixel(x0, z0);
                _p1 = CoordToPixel(x1, z1);

                string msg = target == Handle.Line
                    ? $"Facet #{_facetId1} moved to ({x0},{z0}) \u2192 ({x1},{z1})"
                    : $"Facet #{_facetId1} endpoint moved: ({x0},{z0}) \u2192 ({x1},{z1})";

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = msg;
            }

            InvalidateVisual();
        }

        // ── rendering ────────────────────────────────────────────────────────────
        protected override void OnRender(DrawingContext dc)
        {
            if (_facetId1 == 0) return;

            Point p0 = DragP0();
            Point p1 = DragP1();

            // While dragging the line body, draw the ghost facet at the new position.
            if (_dragging == Handle.Line)
            {
                dc.DrawLine(PenLineDragBody, p0, p1);
            }
            else if (_dragging != Handle.None)
            {
                // Endpoint drag — dashed preview line between new endpoint and the fixed one.
                dc.DrawLine(PenPreviewLine, p0, p1);
            }

            // Endpoint handles
            bool p0Hot = _dragging == Handle.P0 || _dragging == Handle.Line || _hovered == Handle.P0;
            bool p1Hot = _dragging == Handle.P1 || _dragging == Handle.Line || _hovered == Handle.P1;
            DrawHandle(dc, p0, p0Hot, BrushP0, "0");
            DrawHandle(dc, p1, p1Hot, BrushP1, "1");

            // Facing arrow for Wall (3) and Normal (1) facets
            if (_facetType == 3 || _facetType == 1)
                DrawFacingArrow(dc, p0, p1);
        }

        private static void DrawHandle(DrawingContext dc, Point center, bool hot, Brush normalFill, string label)
        {
            double r    = hot ? HandleRadius * 1.35 : HandleRadius;
            Brush  fill = hot ? BrushHot : normalFill;
            Pen    pen  = hot ? PenOutlineHot : PenOutline;
            dc.DrawEllipse(fill, pen, center, r, r);

            var ft = new FormattedText(
                label,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                r * 1.1,
                Brushes.Black,
                96.0);
            dc.DrawText(ft, new Point(center.X - ft.Width / 2.0, center.Y - ft.Height / 2.0));
        }

        /// <summary>
        /// Draws a perpendicular arrow at the midpoint of p0→p1 indicating the primary facing
        /// direction of the wall (right-hand side of the travel direction).
        /// </summary>
        private static void DrawFacingArrow(DrawingContext dc, Point p0, Point p1)
        {
            double dx = p1.X - p0.X;
            double dz = p1.Y - p0.Y;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1.0) return;

            dx /= len; dz /= len;

            // Right-hand perpendicular (clockwise 90°) = primary facing side
            double perpX = dz, perpZ = -dx;

            double midX = (p0.X + p1.X) / 2.0;
            double midZ = (p0.Y + p1.Y) / 2.0;

            const double arrowLen  = 24.0;
            const double headSize  = 10.0;

            double tipX = midX + perpX * arrowLen;
            double tipZ = midZ + perpZ * arrowLen;

            dc.DrawLine(PenFacingArrow, new Point(midX, midZ), new Point(tipX, tipZ));

            double bx = -perpX * headSize * 0.7, bz = -perpZ * headSize * 0.7;
            double sx =    dx  * headSize * 0.5, sz =    dz  * headSize * 0.5;

            dc.DrawLine(PenFacingArrow, new Point(tipX, tipZ),
                new Point(tipX + bx + sx, tipZ + bz + sz));
            dc.DrawLine(PenFacingArrow, new Point(tipX, tipZ),
                new Point(tipX + bx - sx, tipZ + bz - sz));
        }
    }
}
