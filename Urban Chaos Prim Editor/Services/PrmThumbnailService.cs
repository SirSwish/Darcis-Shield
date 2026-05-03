using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosPrimEditor.Models;

namespace UrbanChaosPrimEditor.Services
{
    public static class PrmThumbnailService
    {
        private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

        public static ImageSource? GetThumbnail(string path)
        {
            return Cache.GetOrAdd(path, CreateThumbnail);
        }

        private static ImageSource? CreateThumbnail(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;

                var parser = new PrmParserService();
                PrmModel model = parser.Decode(Path.GetFileName(path), File.ReadAllBytes(path));
                if (model.Points.Count == 0) return null;

                const int width = 96;
                const int height = 72;
                const double yaw = Math.PI / 4.0;
                const double pitch = -Math.PI / 7.0;

                double cy = Math.Cos(yaw);
                double sy = Math.Sin(yaw);
                double cp = Math.Cos(pitch);
                double sp = Math.Sin(pitch);

                var projected = model.Points
                    .Select(p =>
                    {
                        double x = p.X * cy - p.Z * sy;
                        double z = p.X * sy + p.Z * cy;
                        double y = p.Y * cp - z * sp;
                        return new Point(x, -y);
                    })
                    .ToList();

                double minX = projected.Min(p => p.X);
                double maxX = projected.Max(p => p.X);
                double minY = projected.Min(p => p.Y);
                double maxY = projected.Max(p => p.Y);
                double spanX = Math.Max(1, maxX - minX);
                double spanY = Math.Max(1, maxY - minY);
                double scale = Math.Min((width - 14) / spanX, (height - 14) / spanY);

                Point Map(Point p) => new(
                    7 + (p.X - minX) * scale,
                    7 + (p.Y - minY) * scale);

                var visual = new DrawingVisual();
                using (DrawingContext dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(20, 20, 22)), null, new Rect(0, 0, width, height));

                    var fill = new SolidColorBrush(Color.FromRgb(95, 110, 125));
                    var stroke = new Pen(new SolidColorBrush(Color.FromRgb(210, 220, 230)), 1.0);
                    var pointLookup = model.Points.ToDictionary(p => p.GlobalId);

                    foreach (PrmQuadrangle q in model.Quadrangles.Take(80))
                    {
                        if (!pointLookup.TryGetValue(q.PointAId, out PrmPoint a)) continue;
                        if (!pointLookup.TryGetValue(q.PointBId, out PrmPoint b)) continue;
                        if (!pointLookup.TryGetValue(q.PointCId, out PrmPoint c)) continue;
                        if (!pointLookup.TryGetValue(q.PointDId, out PrmPoint d)) continue;
                        DrawPolygon(dc, fill, stroke, Map(Project(a)), Map(Project(b)), Map(Project(d)), Map(Project(c)));
                    }

                    foreach (PrmTriangle t in model.Triangles.Take(80))
                    {
                        if (!pointLookup.TryGetValue(t.PointAId, out PrmPoint a)) continue;
                        if (!pointLookup.TryGetValue(t.PointBId, out PrmPoint b)) continue;
                        if (!pointLookup.TryGetValue(t.PointCId, out PrmPoint c)) continue;
                        DrawPolygon(dc, fill, stroke, Map(Project(a)), Map(Project(b)), Map(Project(c)));
                    }
                }

                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                bitmap.Freeze();
                return bitmap;

                Point Project(PrmPoint p)
                {
                    double x = p.X * cy - p.Z * sy;
                    double z = p.X * sy + p.Z * cy;
                    double y = p.Y * cp - z * sp;
                    return new Point(x, -y);
                }
            }
            catch
            {
                return null;
            }
        }

        private static void DrawPolygon(DrawingContext dc, Brush fill, Pen stroke, params Point[] points)
        {
            if (points.Length < 3) return;

            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], true, true);
                ctx.PolyLineTo(points.Skip(1).ToList(), true, true);
            }
            geometry.Freeze();
            dc.DrawGeometry(fill, stroke, geometry);
        }
    }
}
