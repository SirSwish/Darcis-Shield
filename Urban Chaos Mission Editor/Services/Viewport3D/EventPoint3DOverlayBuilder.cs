using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using UrbanChaosMissionEditor.ViewModels;

namespace UrbanChaosMissionEditor.Services.Viewport3D;

public static class EventPoint3DOverlayBuilder
{
    private const double EngineToViewY = 0.25;

    public static Model3DGroup Build(IEnumerable<EventPointViewModel> eventPoints)
    {
        var group = new Model3DGroup();

        foreach (var ep in eventPoints)
        {
            if (!ep.Used || !ep.IsVisible)
                continue;

            double x = ep.PixelX;
            double z = ep.PixelZ;
            double y = Math.Max(8.0, ep.WorldY * EngineToViewY);
            var color = ep.PointColor;

            AddSphere(group, x, y + 10.0, z, 10.0, color);

            if (ep.Radius > 0)
                AddGroundRing(group, x, z, Math.Max(8.0, ep.Radius / 4.0), Color.FromArgb(185, color.R, color.G, color.B));

            AddLabel(group, $"EP {ep.Index}", x, y + 34.0, z);
        }

        return group;
    }

    private static void AddSphere(Model3DGroup group, double x, double y, double z, double radius, Color color)
    {
        var mesh = new MeshGeometry3D();
        AppendSphere(mesh, x, y, z, radius, 8, 14);

        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        material.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(140, color.R, color.G, color.B))));

        group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
    }

    private static void AddGroundRing(Model3DGroup group, double cx, double cz, double radius, Color color)
    {
        const int segments = 64;
        const double y = 2.0;
        double halfWidth = Math.Max(1.5, Math.Min(5.0, radius / 40.0));
        var mesh = new MeshGeometry3D();

        for (int i = 0; i < segments; i++)
        {
            double a0 = i * Math.PI * 2.0 / segments;
            double a1 = (i + 1) * Math.PI * 2.0 / segments;
            AddRingSegment(mesh, cx, cz, y, radius, halfWidth, a0, a1);
        }

        var material = new DiffuseMaterial(new SolidColorBrush(color));
        group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
    }

    private static void AddRingSegment(MeshGeometry3D mesh, double cx, double cz, double y, double radius, double halfWidth, double a0, double a1)
    {
        double r0 = Math.Max(0.5, radius - halfWidth);
        double r1 = radius + halfWidth;
        int baseIdx = mesh.Positions.Count;

        mesh.Positions.Add(new Point3D(cx + Math.Cos(a0) * r0, y, cz + Math.Sin(a0) * r0));
        mesh.Positions.Add(new Point3D(cx + Math.Cos(a1) * r0, y, cz + Math.Sin(a1) * r0));
        mesh.Positions.Add(new Point3D(cx + Math.Cos(a1) * r1, y, cz + Math.Sin(a1) * r1));
        mesh.Positions.Add(new Point3D(cx + Math.Cos(a0) * r1, y, cz + Math.Sin(a0) * r1));

        mesh.TextureCoordinates.Add(new Point(0, 0));
        mesh.TextureCoordinates.Add(new Point(1, 0));
        mesh.TextureCoordinates.Add(new Point(1, 1));
        mesh.TextureCoordinates.Add(new Point(0, 1));

        mesh.TriangleIndices.Add(baseIdx + 0);
        mesh.TriangleIndices.Add(baseIdx + 1);
        mesh.TriangleIndices.Add(baseIdx + 2);
        mesh.TriangleIndices.Add(baseIdx + 0);
        mesh.TriangleIndices.Add(baseIdx + 2);
        mesh.TriangleIndices.Add(baseIdx + 3);
    }

    private static void AddLabel(Model3DGroup group, string text, double x, double y, double z)
    {
        const double width = 72.0;
        const double height = 26.0;
        const double quadZ = -2.0;

        var label = new TextBlock
        {
            Text = text,
            Width = 128,
            Height = 48,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.Bold,
            FontSize = 22,
            TextAlignment = TextAlignment.Center,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(220, 20, 20, 24)),
            Padding = new Thickness(4)
        };
        label.Measure(new Size(label.Width, label.Height));
        label.Arrange(new Rect(0, 0, label.Width, label.Height));

        var material = new DiffuseMaterial(new VisualBrush(label) { Stretch = Stretch.Fill });
        var mesh = new MeshGeometry3D();

        // Front-facing quad: visible from +Z, text reads left-to-right.
        int frontBase = mesh.Positions.Count;
        mesh.Positions.Add(new Point3D(x - width / 2, y - height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x + width / 2, y - height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x + width / 2, y + height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x - width / 2, y + height / 2, z + quadZ));

        mesh.TextureCoordinates.Add(new Point(0, 1));
        mesh.TextureCoordinates.Add(new Point(1, 1));
        mesh.TextureCoordinates.Add(new Point(1, 0));
        mesh.TextureCoordinates.Add(new Point(0, 0));

        mesh.TriangleIndices.Add(frontBase + 0);
        mesh.TriangleIndices.Add(frontBase + 1);
        mesh.TriangleIndices.Add(frontBase + 2);
        mesh.TriangleIndices.Add(frontBase + 0);
        mesh.TriangleIndices.Add(frontBase + 2);
        mesh.TriangleIndices.Add(frontBase + 3);

        // Back-facing quad: reversed winding makes -Z the visible side, and the U
        // coordinate is mirrored so the text still reads left-to-right.
        int backBase = mesh.Positions.Count;
        mesh.Positions.Add(new Point3D(x - width / 2, y - height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x + width / 2, y - height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x + width / 2, y + height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x - width / 2, y + height / 2, z + quadZ));

        mesh.TextureCoordinates.Add(new Point(1, 1));
        mesh.TextureCoordinates.Add(new Point(0, 1));
        mesh.TextureCoordinates.Add(new Point(0, 0));
        mesh.TextureCoordinates.Add(new Point(1, 0));

        mesh.TriangleIndices.Add(backBase + 0);
        mesh.TriangleIndices.Add(backBase + 2);
        mesh.TriangleIndices.Add(backBase + 1);
        mesh.TriangleIndices.Add(backBase + 0);
        mesh.TriangleIndices.Add(backBase + 3);
        mesh.TriangleIndices.Add(backBase + 2);

        group.Children.Add(new GeometryModel3D(mesh, material));
    }

    private static void AppendSphere(MeshGeometry3D mesh, double cx, double cy, double cz, double r, int stacks, int slices)
    {
        int baseIdx = mesh.Positions.Count;
        for (int i = 0; i <= stacks; i++)
        {
            double v = (double)i / stacks;
            double phi = v * Math.PI;
            double y = Math.Cos(phi);
            double sphi = Math.Sin(phi);
            for (int j = 0; j <= slices; j++)
            {
                double u = (double)j / slices;
                double theta = u * Math.PI * 2.0;
                double x = sphi * Math.Cos(theta);
                double z = sphi * Math.Sin(theta);
                mesh.Positions.Add(new Point3D(cx + x * r, cy + y * r, cz + z * r));
                mesh.TextureCoordinates.Add(new Point(u, v));
            }
        }

        int ring = slices + 1;
        for (int i = 0; i < stacks; i++)
        {
            for (int j = 0; j < slices; j++)
            {
                int a = baseIdx + i * ring + j;
                int b = a + 1;
                int c = a + ring;
                int d = c + 1;
                mesh.TriangleIndices.Add(a);
                mesh.TriangleIndices.Add(c);
                mesh.TriangleIndices.Add(b);
                mesh.TriangleIndices.Add(b);
                mesh.TriangleIndices.Add(c);
                mesh.TriangleIndices.Add(d);
            }
        }
    }
}
