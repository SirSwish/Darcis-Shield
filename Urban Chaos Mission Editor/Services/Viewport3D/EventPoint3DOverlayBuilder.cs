using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using UrbanChaosMissionEditor.ViewModels;
using UrbanChaosMapEditor.Services.Viewport3D;

namespace UrbanChaosMissionEditor.Services.Viewport3D;

public static class EventPoint3DOverlayBuilder
{
    private const double EngineToViewY = 0.25;
    private const int MaxLabels = 80;
    private const int RingSegments = 32;
    private const double MarkerRadius = 22.0;

    private readonly record struct EventPointCandidate(
        int Index,
        double X,
        double Y,
        double Z,
        double RingRadius,
        Color Color,
        double DistanceSq);

    public static Model3DGroup Build(IEnumerable<EventPointViewModel> eventPoints, ViewportCullRegion? cull = null)
    {
        var group = new Model3DGroup();
        var candidates = CollectCandidates(eventPoints, cull);
        AddMergedSpheres(group, candidates);
        AddMergedRings(group, candidates);
        return group;
    }

    /// <summary>
    /// Build billboard index labels for each EP. Labels are staggered vertically
    /// (3 height tiers by index % 3) so consecutive / clustered EPs don't stack
    /// their labels on top of each other.
    /// </summary>
    public static Model3DGroup BuildLabels(IEnumerable<EventPointViewModel> eventPoints, ViewportCullRegion? cull = null)
    {
        var group = new Model3DGroup();
        var candidates = CollectCandidates(eventPoints, cull);

        // Tier offsets — chosen so labels don't overlap their own sphere either.
        // The sphere top sits at Y + 10 + MarkerRadius; add a base gap of 6 then stagger.
        double baseY = 10.0 + MarkerRadius + 6.0;
        double[] tierOffsets = { 0.0, 18.0, 36.0 };

        foreach (var ep in candidates.OrderBy(ep => ep.DistanceSq).Take(MaxLabels))
        {
            double tier = tierOffsets[Math.Abs(ep.Index) % tierOffsets.Length];
            AddOutlinedLabel(group, ep.Index.ToString(CultureInfo.InvariantCulture),
                ep.X, ep.Y + baseY + tier, ep.Z);
        }

        return group;
    }

    private static List<EventPointCandidate> CollectCandidates(
        IEnumerable<EventPointViewModel> eventPoints, ViewportCullRegion? cull)
    {
        var candidates = new List<EventPointCandidate>();

        foreach (var ep in eventPoints)
        {
            if (!ep.Used || !ep.IsVisible)
                continue;

            double x = ep.PixelX;
            double z = ep.PixelZ;
            double y = Math.Max(8.0, ep.WorldY * EngineToViewY);
            var color = ep.PointColor;
            double ringRadius = ep.Radius > 0 ? Math.Max(8.0, ep.Radius / 4.0) : 0.0;
            double boundsRadius = Math.Max(MarkerRadius, ringRadius);

            if (cull.HasValue && !cull.Value.IntersectsBounds(
                x - boundsRadius,
                z - boundsRadius,
                x + boundsRadius,
                z + boundsRadius))
            {
                continue;
            }

            candidates.Add(new EventPointCandidate(
                ep.Index,
                x,
                y,
                z,
                ringRadius,
                color,
                cull?.DistanceSquaredToPoint(x, z) ?? 0.0));
        }

        return candidates;
    }

    private static void AddMergedSpheres(Model3DGroup group, IReadOnlyList<EventPointCandidate> candidates)
    {
        if (candidates.Count == 0)
            return;

        foreach (var bucket in candidates.GroupBy(ep => ep.Color))
        {
            var mesh = new MeshGeometry3D();
            foreach (var ep in bucket)
                AppendSphere(mesh, ep.X, ep.Y + 10.0, ep.Z, MarkerRadius, 6, 10);

            mesh.Freeze();

            var color = bucket.Key;
            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            material.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(140, color.R, color.G, color.B))));
            material.Freeze();

            group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        }
    }

    private static void AddMergedRings(Model3DGroup group, IReadOnlyList<EventPointCandidate> candidates)
    {
        foreach (var bucket in candidates.Where(ep => ep.RingRadius > 0.0).GroupBy(ep => ep.Color))
        {
            var mesh = new MeshGeometry3D();
            foreach (var ep in bucket)
                AppendGroundRing(mesh, ep.X, ep.Z, ep.RingRadius);

            if (mesh.Positions.Count == 0)
                continue;

            mesh.Freeze();
            var color = bucket.Key;
            var material = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(185, color.R, color.G, color.B)));
            material.Freeze();
            group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        }
    }

    private static void AppendGroundRing(MeshGeometry3D mesh, double cx, double cz, double radius)
    {
        const double y = 2.0;
        double halfWidth = Math.Max(1.5, Math.Min(5.0, radius / 40.0));

        for (int i = 0; i < RingSegments; i++)
        {
            double a0 = i * Math.PI * 2.0 / RingSegments;
            double a1 = (i + 1) * Math.PI * 2.0 / RingSegments;
            AddRingSegment(mesh, cx, cz, y, radius, halfWidth, a0, a1);
        }
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

    /// <summary>
    /// Emit a billboard quad above the sphere with the EP index rendered as white
    /// glyphs with a black outline — no opaque background, so the outline is the
    /// only thing separating the digits from the scene behind.
    /// </summary>
    private static void AddOutlinedLabel(Model3DGroup group, string text, double x, double y, double z)
    {
        // Build the text geometry once, oriented in 2D, then place into the world quad.
        var typeface = new Typeface(
            new FontFamily("Segoe UI"),
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal);

        const double emSize = 28.0;
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            emSize,
            Brushes.White,
            1.0);

        // Rasterise into a fixed-size bitmap. We size the bitmap to the actual text
        // bounds so the brush isn't stretched (which would soften the outline).
        double pad = 6.0;
        int bmpW = (int)Math.Ceiling(formatted.Width + pad * 2);
        int bmpH = (int)Math.Ceiling(formatted.Height + pad * 2);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var geom = formatted.BuildGeometry(new Point(pad, pad));
            var pen = new Pen(Brushes.Black, 1.0) { LineJoin = PenLineJoin.Round };
            pen.Freeze();
            dc.DrawGeometry(Brushes.White, pen, geom);
        }

        var rtb = new RenderTargetBitmap(bmpW, bmpH, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();

        var brush = new ImageBrush(rtb) { Stretch = Stretch.Fill, TileMode = TileMode.None };
        brush.Freeze();
        var diffuse = new DiffuseMaterial(brush);
        var emissive = new EmissiveMaterial(brush);
        var matGroup = new MaterialGroup();
        matGroup.Children.Add(diffuse);
        matGroup.Children.Add(emissive);
        matGroup.Freeze();

        // World-space quad sized proportionally to the bitmap so glyph aspect is preserved.
        // 0.55 view-units per pixel ≈ readable from 200+ units away without dominating.
        double scale = 0.55;
        double width = bmpW * scale;
        double height = bmpH * scale;
        const double quadZ = -2.0;

        var mesh = new MeshGeometry3D();
        // Front-facing quad (visible from +Z): the text reads left-to-right.
        mesh.Positions.Add(new Point3D(x - width / 2, y - height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x + width / 2, y - height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x + width / 2, y + height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x - width / 2, y + height / 2, z + quadZ));
        mesh.TextureCoordinates.Add(new Point(0, 1));
        mesh.TextureCoordinates.Add(new Point(1, 1));
        mesh.TextureCoordinates.Add(new Point(1, 0));
        mesh.TextureCoordinates.Add(new Point(0, 0));
        mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(3);

        // Back-facing quad (visible from -Z): reversed winding, U mirrored so it still reads correctly.
        mesh.Positions.Add(new Point3D(x - width / 2, y - height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x + width / 2, y - height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x + width / 2, y + height / 2, z + quadZ));
        mesh.Positions.Add(new Point3D(x - width / 2, y + height / 2, z + quadZ));
        mesh.TextureCoordinates.Add(new Point(1, 1));
        mesh.TextureCoordinates.Add(new Point(0, 1));
        mesh.TextureCoordinates.Add(new Point(0, 0));
        mesh.TextureCoordinates.Add(new Point(1, 0));
        mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(5);
        mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(7); mesh.TriangleIndices.Add(6);
        mesh.Freeze();

        group.Children.Add(new GeometryModel3D(mesh, matGroup));
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
