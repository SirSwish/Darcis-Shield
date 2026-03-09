using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using UrbanChaosMapEditor.Models.Buildings;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class CableFacetPreviewWindow : Window
    {
        private readonly DFacetRec _facet;
        private readonly int _facetId;

        public CableFacetPreviewWindow(DFacetRec facet, int facetId)
        {
            InitializeComponent();

            _facet = facet;
            _facetId = facetId;

            Title = $"Cable Preview – Facet #{facetId}";

            short step1 = unchecked((short)_facet.StyleIndex);
            short step2 = unchecked((short)_facet.Building);
            int segments = Math.Max(1, (int)_facet.Height);   // <-- cast to int
            int sagBase = _facet.FHeight * 64;

            HeaderTextBlock.Text = $"Facet #{facetId}  (Cable)";
            DetailsTextBlock.Text =
                $"Segments: {segments}   FHeight: {_facet.FHeight} (sag base={sagBase})   " +
                $"step1: {step1}   step2: {step2}\n" +
                $"Endpoints (x,z,y): ({_facet.X0},{_facet.Z0},{_facet.Y0}) ? ({_facet.X1},{_facet.Z1},{_facet.Y1})";

            PreviewCanvas.Loaded += (_, __) => Redraw();
            PreviewCanvas.SizeChanged += (_, __) => Redraw();
        }

        private void Redraw()
        {
            double w = PreviewCanvas.ActualWidth;
            double h = PreviewCanvas.ActualHeight;

            PreviewCanvas.Children.Clear();

            if (w <= 10 || h <= 10)
                return;

            if (_facet.Height == 0)
                return;

            int segments = Math.Max(1, (int)_facet.Height);   // <-- cast to int

            // World endpoints (match cable_draw: x/z in tiles * 256, Y already in world units)
            double x1 = _facet.X0 * 256.0;
            double y1 = _facet.Y0;
            double z1 = _facet.Z0 * 256.0;

            double x2 = _facet.X1 * 256.0;
            double y2 = _facet.Y1;
            double z2 = _facet.Z1 * 256.0;

            double dx = (x2 - x1) / segments;
            double dy = (y2 - y1) / segments;
            double dz = (z2 - z1) / segments;

            int angle = -512;
            short dangle1 = unchecked((short)_facet.StyleIndex); // step_angle1
            short dangle2 = unchecked((short)_facet.Building);   // step_angle2
            int sagBase = _facet.FHeight * 64;                   // same as cable_draw: sag = FHeight * 64

            double[] baseY = new double[segments + 1];
            double[] sagY = new double[segments + 1];

            double cx = x1;
            double cy = y1;
            double cz = z1;

            for (int i = 0; i <= segments; i++)
            {
                // Base cable line (no sag)
                baseY[i] = cy;

                // Approximate COS((angle+2048)&2047) using 2048-step table -> Math.Cos
                int ang = angle + 2048;
                int wrapped = ang & 2047; // 0..2047
                double rad = wrapped * (2.0 * Math.PI / 2048.0);
                double sagOffset = Math.Cos(rad) * sagBase;

                // Engine does cy - sagy
                sagY[i] = cy - sagOffset;

                // Advance along the segment
                cx += dx;
                cy += dy;
                cz += dz;

                angle += dangle1;
                if (angle >= -30)
                {
                    // Same switch behaviour as cable_draw
                    dangle1 = dangle2;
                }
            }

            // Normalise Y to fit the canvas
            double minY = sagY.Concat(baseY).Min();
            double maxY = sagY.Concat(baseY).Max();
            if (Math.Abs(maxY - minY) < 1e-3)
            {
                maxY = minY + 1.0;
            }

            double margin = 20.0;
            double plotW = Math.Max(1.0, w - 2 * margin);
            double plotH = Math.Max(1.0, h - 2 * margin);

            var basePoints = new PointCollection();
            var sagPoints = new PointCollection();

            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                double sx = margin + t * plotW;

                double nyBase = (baseY[i] - minY) / (maxY - minY);
                double nySag = (sagY[i] - minY) / (maxY - minY);

                // Higher Y in world ? smaller screen Y (top of plot)
                double syBase = margin + (1.0 - nyBase) * plotH;
                double sySag = margin + (1.0 - nySag) * plotH;

                basePoints.Add(new Point(sx, syBase));
                sagPoints.Add(new Point(sx, sySag));
            }

            // Base line (no sag) – dashed grey
            var baseLine = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Points = basePoints
            };

            // Sagged cable – red
            var cableLine = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(255, 80, 80)),
                StrokeThickness = 2,
                Points = sagPoints
            };

            PreviewCanvas.Children.Add(baseLine);
            PreviewCanvas.Children.Add(cableLine);

            // Optional: endpoints markers
            if (sagPoints.Count >= 2)
            {
                var start = sagPoints[0];
                var end = sagPoints[sagPoints.Count - 1];

                var startMarker = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = Brushes.Lime,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                };
                Canvas.SetLeft(startMarker, start.X - 3);
                Canvas.SetTop(startMarker, start.Y - 3);
                PreviewCanvas.Children.Add(startMarker);

                var endMarker = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = Brushes.Lime,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                };
                Canvas.SetLeft(endMarker, end.X - 3);
                Canvas.SetTop(endMarker, end.Y - 3);
                PreviewCanvas.Children.Add(endMarker);
            }
        }
    }
}
