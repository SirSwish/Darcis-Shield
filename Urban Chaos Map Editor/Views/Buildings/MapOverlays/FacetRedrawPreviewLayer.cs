// /Views/MapOverlays/FacetRedrawPreviewLayer.cs
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Buildings.MapOverlays
{
    /// <summary>
    /// Renders the preview line during facet redraw mode or multi-draw mode.
    /// Shows a dashed yellow line from the first click point to the current mouse position.
    /// Also renders small circles at the snap points.
    /// </summary>
    public sealed class FacetRedrawPreviewLayer : FrameworkElement
    {
        private MapViewModel? _vm;

        private static readonly Pen PreviewPen;
        private static readonly Pen MultiDrawPen;
        private static readonly Pen DotPen;
        private static readonly Brush DotFill;
        private static readonly Brush MultiDrawDotFill;

        private static readonly Pen LadderPen;
        private static readonly Pen LadderArrowPen;
        private static readonly Brush LadderDotFill;

        private static readonly Pen DoorPen;
        private static readonly Pen DoorArrowPen;
        private static readonly Brush DoorDotFill;

        private static readonly Pen CablePen;
        private static readonly Brush CableDotFill;

        private static readonly Pen WallArrowPen;

        static FacetRedrawPreviewLayer()
        {
            // Dashed yellow line for single redraw preview
            PreviewPen = new Pen(Brushes.Yellow, 4.0)
            {
                DashStyle = new DashStyle(new double[] { 6, 4 }, 0),
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            PreviewPen.Freeze();

            // Orange line for ladder preview
            LadderPen = new Pen(Brushes.Orange, 4.0)
            {
                DashStyle = new DashStyle(new double[] { 6, 4 }, 0),
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            LadderPen.Freeze();

            // Solid orange for arrow
            LadderArrowPen = new Pen(Brushes.Orange, 3.0)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            LadderArrowPen.Freeze();

            // Purple line for door preview
            DoorPen = new Pen(Brushes.Purple, 4.0)
            {
                DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0)
            };
            DoorPen.Freeze();

            DoorArrowPen = new Pen(Brushes.Purple, 3.0)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            DoorArrowPen.Freeze();

            DoorDotFill = new SolidColorBrush(Color.FromRgb(180, 100, 255));
            ((SolidColorBrush)DoorDotFill).Freeze();

            // Red dashed line for cable preview
            CablePen = new Pen(Brushes.Red, 4.0)
            {
                DashStyle = new DashStyle(new double[] { 6, 4 }, 0),
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            CablePen.Freeze();

            CableDotFill = new SolidColorBrush(Color.FromRgb(255, 100, 100));
            ((SolidColorBrush)CableDotFill).Freeze();

            LadderDotFill = new SolidColorBrush(Color.FromRgb(255, 180, 100));
            ((SolidColorBrush)LadderDotFill).Freeze();

            // Cyan line for multi-draw (to distinguish from single redraw)
            MultiDrawPen = new Pen(Brushes.Cyan, 4.0)
            {
                DashStyle = new DashStyle(new double[] { 6, 4 }, 0),
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            MultiDrawPen.Freeze();

            // Dots at endpoints
            DotFill = new SolidColorBrush(Color.FromRgb(255, 255, 100));
            ((SolidColorBrush)DotFill).Freeze();

            MultiDrawDotFill = new SolidColorBrush(Color.FromRgb(100, 255, 255));
            ((SolidColorBrush)MultiDrawDotFill).Freeze();

            DotPen = new Pen(Brushes.Black, 2.0);
            DotPen.Freeze();

            var wallGreen = new SolidColorBrush(Color.FromRgb(102, 255, 0)); wallGreen.Freeze();
            WallArrowPen = new Pen(wallGreen, 3.0)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap   = PenLineCap.Round
            };
            WallArrowPen.Freeze();
        }

        public FacetRedrawPreviewLayer()
        {
            Width = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = false;

            DataContextChanged += (_, __) => HookVm();
        }

        private void HookVm()
        {
            if (_vm != null)
                _vm.PropertyChanged -= OnVmChanged;

            _vm = DataContext as MapViewModel;

            if (_vm != null)
                _vm.PropertyChanged += OnVmChanged;

            InvalidateVisual();
        }

        private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapViewModel.FacetRedrawPreviewLine) ||
                e.PropertyName == nameof(MapViewModel.IsRedrawingFacet) ||
                e.PropertyName == nameof(MapViewModel.MultiDrawPreviewLine) ||
                e.PropertyName == nameof(MapViewModel.IsMultiDrawingFacets) ||
                e.PropertyName == nameof(MapViewModel.LadderPreviewLine) ||
                e.PropertyName == nameof(MapViewModel.IsPlacingLadder) ||
                e.PropertyName == nameof(MapViewModel.CablePreviewLine) ||
                e.PropertyName == nameof(MapViewModel.IsPlacingCable) ||
                e.PropertyName == nameof(MapViewModel.DoorPreviewLine) ||
                e.PropertyName == nameof(MapViewModel.IsPlacingDoor))
            {
                Dispatcher.Invoke(InvalidateVisual);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_vm == null)
                return;

            // Single facet redraw mode
            if (_vm.IsRedrawingFacet && _vm.FacetRedrawPreviewLine != null)
            {
                var line = _vm.FacetRedrawPreviewLine.Value;
                DrawPreviewLine(dc, line.uiX0, line.uiZ0, line.uiX1, line.uiZ1,
                               PreviewPen, DotFill, "Start (X0,Z0)", "End (X1,Z1)");
            }

            // Multi-draw mode
            if (_vm.IsMultiDrawingFacets && _vm.MultiDrawPreviewLine != null)
            {
                var line = _vm.MultiDrawPreviewLine.Value;
                DrawPreviewLine(dc, line.uiX0, line.uiZ0, line.uiX1, line.uiZ1,
                               MultiDrawPen, MultiDrawDotFill, "Start", "End");

                // Facing arrow for Wall / Normal facet types
                var t = _vm.MultiDrawFacetType;
                if (t == FacetType.Wall || t == FacetType.Normal)
                    DrawFacingArrow(dc, line.uiX0, line.uiZ0, line.uiX1, line.uiZ1);
            }

            // Ladder placement mode - with direction arrow
            if (_vm.IsPlacingLadder && _vm.LadderPreviewLine != null)
            {
                var line = _vm.LadderPreviewLine.Value;
                DrawDirectionPreviewLine(dc, line.uiX0, line.uiZ0, line.uiX1, line.uiZ1,
                    LadderPen, LadderArrowPen, LadderDotFill, Brushes.Orange, "Faces →");
            }
            // Door placement mode - with direction arrow
            if (_vm.IsPlacingDoor && _vm.DoorPreviewLine != null)
            {
                var line = _vm.DoorPreviewLine.Value;
                DrawDirectionPreviewLine(dc, line.uiX0, line.uiZ0, line.uiX1, line.uiZ1,
                    DoorPen, DoorArrowPen, DoorDotFill, Brushes.Purple, "Door Faces →");
            }
            // Cable placement mode
            if (_vm.IsPlacingCable && _vm.CablePreviewLine != null)
            {
                var line = _vm.CablePreviewLine.Value;
                DrawPreviewLine(dc, line.uiX0, line.uiZ0, line.uiX1, line.uiZ1,
                               CablePen, CableDotFill, "Cable Start", "Cable End");
            }
        }

        private void DrawPreviewLine(DrawingContext dc, int x0, int z0, int x1, int z1,
                                     Pen linePen, Brush dotBrush, string startLabel, string endLabel)
        {
            var p1 = new Point(x0, z0);
            var p2 = new Point(x1, z1);

            // Draw the preview line
            dc.DrawLine(linePen, p1, p2);

            // Draw dots at both endpoints
            const double dotRadius = 8.0;
            dc.DrawEllipse(dotBrush, DotPen, p1, dotRadius, dotRadius);
            dc.DrawEllipse(dotBrush, DotPen, p2, dotRadius, dotRadius);

            // Draw labels
            var startText = new FormattedText(
                startLabel,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(startText, new Point(p1.X + 12, p1.Y - 8));

            // If line has length, draw end label
            if (p1 != p2)
            {
                var endText = new FormattedText(
                    endLabel,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    12,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(endText, new Point(p2.X + 12, p2.Y - 8));
            }
        }
        /// <summary>
        /// Draws a perpendicular green arrow at the midpoint indicating the primary facing side
        /// of a wall/normal facet (right-hand side of the P0→P1 travel direction).
        /// </summary>
        private void DrawFacingArrow(DrawingContext dc, int x0, int z0, int x1, int z1)
        {
            double dx = x1 - x0, dz = z1 - z0;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1.0) return;

            dx /= len; dz /= len;
            double perpX = dz, perpZ = -dx;

            double midX = (x0 + x1) / 2.0, midZ = (z0 + z1) / 2.0;

            const double arrowLen = 30.0, headSize = 12.0;
            double tipX = midX + perpX * arrowLen, tipZ = midZ + perpZ * arrowLen;

            dc.DrawLine(WallArrowPen, new Point(midX, midZ), new Point(tipX, tipZ));

            double bx = -perpX * headSize * 0.7, bz = -perpZ * headSize * 0.7;
            double sx =    dx  * headSize * 0.5, sz =    dz  * headSize * 0.5;

            dc.DrawLine(WallArrowPen, new Point(tipX, tipZ),
                new Point(tipX + bx + sx, tipZ + bz + sz));
            dc.DrawLine(WallArrowPen, new Point(tipX, tipZ),
                new Point(tipX + bx - sx, tipZ + bz - sz));
        }

        /// <summary>
        /// Draws the ladder preview line with a direction arrow showing which way the ladder faces.
        /// The arrow points to the "right" of the travel direction (start → end).
        /// </summary>
        private void DrawDirectionPreviewLine(DrawingContext dc, int x0, int z0, int x1, int z1,
            Pen linePen, Pen arrowPen, Brush dotBrush, Brush labelBrush, string dirLabel)
        {
            var p1 = new Point(x0, z0);
            var p2 = new Point(x1, z1);

            dc.DrawLine(linePen, p1, p2);

            const double dotRadius = 8.0;
            dc.DrawEllipse(dotBrush, DotPen, p1, dotRadius, dotRadius);
            dc.DrawEllipse(dotBrush, DotPen, p2, dotRadius, dotRadius);

            double midX = (x0 + x1) / 2.0;
            double midZ = (z0 + z1) / 2.0;

            double dx = x1 - x0;
            double dz = z1 - z0;
            double length = Math.Sqrt(dx * dx + dz * dz);

            if (length > 0.1)
            {
                dx /= length;
                dz /= length;

                double perpX = dz;
                double perpZ = -dx;

                const double arrowLength = 30.0;
                const double arrowHeadSize = 12.0;

                double tipX = midX + perpX * arrowLength;
                double tipZ = midZ + perpZ * arrowLength;

                dc.DrawLine(arrowPen, new Point(midX, midZ), new Point(tipX, tipZ));

                double backX = -perpX * arrowHeadSize * 0.7;
                double backZ = -perpZ * arrowHeadSize * 0.7;
                double sideX = dx * arrowHeadSize * 0.5;
                double sideZ = dz * arrowHeadSize * 0.5;

                dc.DrawLine(arrowPen,
                    new Point(tipX, tipZ),
                    new Point(tipX + backX + sideX, tipZ + backZ + sideZ));
                dc.DrawLine(arrowPen,
                    new Point(tipX, tipZ),
                    new Point(tipX + backX - sideX, tipZ + backZ - sideZ));

                var label = new FormattedText(
                    dirLabel,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11,
                    labelBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(label, new Point(tipX + 5, tipZ - 8));
            }

            var startText = new FormattedText(
                "Start",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(startText, new Point(p1.X + 12, p1.Y - 8));

            if (p1 != p2)
            {
                var endText = new FormattedText(
                    "End",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    12,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(endText, new Point(p2.X + 12, p2.Y - 8));
            }
        }
    }
}