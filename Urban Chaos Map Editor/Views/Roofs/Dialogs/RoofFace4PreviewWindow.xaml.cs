using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using UrbanChaosMapEditor.Models.Buildings;

namespace UrbanChaosMapEditor.Views.Roofs.Dialogs
{
    public partial class RoofFace4PreviewWindow : Window
    {
        private readonly int _index;
        private readonly RoofFace4Rec _rf;

        public RoofFace4PreviewWindow(int index, RoofFace4Rec rf)
        {
            InitializeComponent();

            _index = index;
            _rf = rf;

            Title = $"RoofFace4 Preview – #{index}";

            bool anyDy = _rf.DY0 != 0 || _rf.DY1 != 0 || _rf.DY2 != 0;
            bool rzSlopeBit = (_rf.RZ & 0x80) != 0;

            HeaderTextBlock.Text = $"RoofFace4 #{index}";
            DetailsTextBlock.Text =
                $"Y={_rf.Y}   DY=({_rf.DY0},{_rf.DY1},{_rf.DY2})   " +
                $"DrawFlags=0x{_rf.DrawFlags:X2}   RX={_rf.RX}   RZ=0x{_rf.RZ:X2}   Next={_rf.Next}\n" +
                $"Sloped? {(anyDy || rzSlopeBit ? "Yes" : "No")}";

            PreviewCanvas.Loaded += (_, __) => Redraw();
            PreviewCanvas.SizeChanged += (_, __) => Redraw();
        }

        private void Redraw()
        {
            PreviewCanvas.Children.Clear();

            double w = PreviewCanvas.ActualWidth;
            double h = PreviewCanvas.ActualHeight;
            if (w < 40 || h < 40) return;

            // We don’t know the engine’s exact corner mapping for DY[0..2],
            // but a useful visualization is: base Y at corner0 and Y+DY at the other 3 corners.
            // This makes pitch / slope *obvious* even if corner order isn’t perfect.

            double margin = 40;
            double box = Math.Min(w, h) - margin * 2;
            if (box < 40) box = 40;

            double left = (w - box) * 0.5;
            double top = (h - box) * 0.5;

            // “Corner heights”
            double y0 = _rf.Y;
            double y1 = _rf.Y + _rf.DY0;
            double y2 = _rf.Y + _rf.DY1;
            double y3 = _rf.Y + _rf.DY2;

            double minY = Math.Min(Math.Min(y0, y1), Math.Min(y2, y3));
            double maxY = Math.Max(Math.Max(y0, y1), Math.Max(y2, y3));
            if (Math.Abs(maxY - minY) < 0.0001) { maxY = minY + 1; }

            // Map heights to a vertical offset (higher world Y -> higher on canvas)
            double HeightToOffset(double yy)
            {
                double t = (yy - minY) / (maxY - minY); // 0..1
                // Make it visible but not extreme:
                return (1.0 - t) * (box * 0.35);
            }

            Point p0 = new(left, top + HeightToOffset(y0));
            Point p1 = new(left + box, top + HeightToOffset(y1));
            Point p2 = new(left + box, top + box + HeightToOffset(y2));
            Point p3 = new(left, top + box + HeightToOffset(y3));

            // Outline quad
            var poly = new Polygon
            {
                Points = new PointCollection { p0, p1, p2, p3 },
                Stroke = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 210, 180))
            };
            PreviewCanvas.Children.Add(poly);

            // Diagonal for clarity
            var diag = new Line
            {
                X1 = p0.X,
                Y1 = p0.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 3 }
            };
            PreviewCanvas.Children.Add(diag);

            // Corner markers + labels
            AddCorner(PreviewCanvas, p0, $"Y={y0}", Brushes.Lime);
            AddCorner(PreviewCanvas, p1, $"Y={y1}", Brushes.Lime);
            AddCorner(PreviewCanvas, p2, $"Y={y2}", Brushes.Lime);
            AddCorner(PreviewCanvas, p3, $"Y={y3}", Brushes.Lime);

            // Caption
            var caption = new System.Windows.Controls.TextBlock
            {
                Text = "DY sketch (approx corner mapping)",
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                FontSize = 12
            };
            Canvas.SetLeft(caption, 12);
            Canvas.SetTop(caption, 10);
            PreviewCanvas.Children.Add(caption);
        }

        private static void AddCorner(System.Windows.Controls.Canvas c, Point p, string label, Brush dotBrush)
        {
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = dotBrush,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            Canvas.SetLeft(dot, p.X - 4);
            Canvas.SetTop(dot, p.Y - 4);
            c.Children.Add(dot);

            var tb = new System.Windows.Controls.TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                FontSize = 12
            };
            Canvas.SetLeft(tb, p.X + 6);
            Canvas.SetTop(tb, p.Y - 10);
            c.Children.Add(tb);
        }
    }
}
