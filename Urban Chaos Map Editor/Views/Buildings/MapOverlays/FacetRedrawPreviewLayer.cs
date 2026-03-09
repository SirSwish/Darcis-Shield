// /Views/MapOverlays/FacetRedrawPreviewLayer.cs
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
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
                e.PropertyName == nameof(MapViewModel.IsPlacingLadder))
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
            }

            // Ladder placement mode - with direction arrow
            if (_vm.IsPlacingLadder && _vm.LadderPreviewLine != null)
            {
                var line = _vm.LadderPreviewLine.Value;
                DrawLadderPreviewLine(dc, line.uiX0, line.uiZ0, line.uiX1, line.uiZ1);
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
        /// Draws the ladder preview line with a direction arrow showing which way the ladder faces.
        /// The arrow points to the "right" of the travel direction (start ? end).
        /// </summary>
        private void DrawLadderPreviewLine(DrawingContext dc, int x0, int z0, int x1, int z1)
        {
            var p1 = new Point(x0, z0);
            var p2 = new Point(x1, z1);

            // Draw the main ladder line (orange dashed)
            dc.DrawLine(LadderPen, p1, p2);

            // Draw dots at both endpoints
            const double dotRadius = 8.0;
            dc.DrawEllipse(LadderDotFill, DotPen, p1, dotRadius, dotRadius);
            dc.DrawEllipse(LadderDotFill, DotPen, p2, dotRadius, dotRadius);

            // Calculate midpoint
            double midX = (x0 + x1) / 2.0;
            double midZ = (z0 + z1) / 2.0;

            // Calculate direction vector from start to end
            double dx = x1 - x0;
            double dz = z1 - z0;
            double length = Math.Sqrt(dx * dx + dz * dz);

            if (length > 0.1) // Only draw arrow if line has some length
            {
                // Normalize direction
                dx /= length;
                dz /= length;

                // Perpendicular vector (rotate 90 degrees CLOCKWISE)
                // This gives us "right" relative to the direction of travel
                double perpX = dz;
                double perpZ = -dx;

                // Arrow properties
                const double arrowLength = 30.0;
                const double arrowHeadSize = 12.0;

                // Arrow tip position (perpendicular from midpoint)
                double tipX = midX + perpX * arrowLength;
                double tipZ = midZ + perpZ * arrowLength;

                // Draw arrow shaft
                dc.DrawLine(LadderArrowPen, new Point(midX, midZ), new Point(tipX, tipZ));

                // Draw arrowhead
                // Two lines from tip going back at 45 degrees
                double backX = -perpX * arrowHeadSize * 0.7;
                double backZ = -perpZ * arrowHeadSize * 0.7;
                double sideX = dx * arrowHeadSize * 0.5;
                double sideZ = dz * arrowHeadSize * 0.5;

                dc.DrawLine(LadderArrowPen,
                    new Point(tipX, tipZ),
                    new Point(tipX + backX + sideX, tipZ + backZ + sideZ));
                dc.DrawLine(LadderArrowPen,
                    new Point(tipX, tipZ),
                    new Point(tipX + backX - sideX, tipZ + backZ - sideZ));

                // Draw label showing direction
                var dirLabel = new FormattedText(
                    "Faces ?",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11,
                    Brushes.Orange,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(dirLabel, new Point(tipX + 5, tipZ - 8));
            }

            // Draw start/end labels
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