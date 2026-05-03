using System.Windows.Media;
using System.Windows.Media.Media3D;
using UrbanChaosLightEditor.Services;
using UrbanChaosMapEditor.Services.Viewport3D;

namespace UrbanChaosLightEditor.Services.Viewport3D;

public static class Light3DOverlayBuilder
{
    private const double EngineToViewY = 0.25;
    private const double StoreyWorld = 64.0;
    private const double TileWorld = 64.0;
    private const int ConeSegments = 10;
    private const int MaxRealtimeSpotLights = 8;
    private const double MarkerRadius = 4.0;

    private readonly record struct LightCandidate(
        double X,
        double Y,
        double Z,
        double ReachRadius,
        double BeamHeight,
        Color Color,
        double DistanceSq);

    public static Model3DGroup Build(ViewportCullRegion? cull = null)
    {
        var group = new Model3DGroup();
        if (!LightsDataService.Instance.IsLoaded)
            return group;

        var accessor = new LightsAccessor(LightsDataService.Instance);
        var entries = accessor.ReadAllEntries();
        var candidates = new List<LightCandidate>(entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            var light = entries[i];
            if (light.Used != 1)
                continue;

            double x = LightsAccessor.WorldXToUiX(light.X);
            double z = LightsAccessor.WorldZToUiZ(light.Z);
            double y = Math.Max(StoreyWorld, light.Y * EngineToViewY);
            double reachRadius = RangeToGroundRadius(light.Range);
            double beamHeight = StoreyWorld;

            if (cull.HasValue && !cull.Value.IntersectsBounds(
                x - reachRadius,
                z - reachRadius,
                x + reachRadius,
                z + reachRadius))
            {
                continue;
            }

            byte r = unchecked((byte)(light.Red + 128));
            byte g = unchecked((byte)(light.Green + 128));
            byte b = unchecked((byte)(light.Blue + 128));
            var color = Color.FromRgb(r, g, b);

            candidates.Add(new LightCandidate(
                x,
                y,
                z,
                reachRadius,
                beamHeight,
                color,
                cull?.DistanceSquaredToPoint(x, z) ?? 0.0));
        }

        AddMergedConeVisual(group, candidates);
        AddMergedMarkerVisual(group, candidates);

        foreach (var light in candidates.OrderBy(l => l.DistanceSq).Take(MaxRealtimeSpotLights))
            AddSpotLight(group, light.X, light.Y, light.Z, light.ReachRadius, light.BeamHeight, light.Color);

        return group;
    }

    private static double RangeToGroundRadius(byte range)
    {
        // Legacy range is a byte. At one storey up, 255 reaches one tile in
        // each horizontal direction, i.e. the four texture squares around it.
        return Math.Max(6.0, range / 255.0 * TileWorld);
    }

    private static void AddSpotLight(Model3DGroup group, double x, double y, double z, double groundRadius, double beamHeight, Color color)
    {
        double outerAngle = Math.Clamp(2.0 * Math.Atan2(groundRadius, beamHeight) * 180.0 / Math.PI, 8.0, 88.0);
        double innerAngle = Math.Max(4.0, outerAngle * 0.55);

        var light = new SpotLight
        {
            Color = color,
            Position = new Point3D(x, y, z),
            Direction = new Vector3D(0, -1, 0),
            Range = Math.Sqrt(beamHeight * beamHeight + groundRadius * groundRadius),
            InnerConeAngle = innerAngle,
            OuterConeAngle = outerAngle,
            ConstantAttenuation = 0.45,
            LinearAttenuation = 0.012,
            QuadraticAttenuation = 0.0
        };

        group.Children.Add(light);
    }

    private const byte ConeDiffuseAlpha = 58;
    private const byte ConeEmissiveAlpha = 72;

    private static void AddMergedConeVisual(Model3DGroup group, IReadOnlyList<LightCandidate> lights)
    {
        if (lights.Count == 0)
            return;

        var byColor = new Dictionary<Color, List<LightCandidate>>();
        foreach (var light in lights)
        {
            if (!byColor.TryGetValue(light.Color, out var bucket))
            {
                bucket = new List<LightCandidate>();
                byColor[light.Color] = bucket;
            }
            bucket.Add(light);
        }

        foreach (var (color, bucket) in byColor)
        {
            var mesh = new MeshGeometry3D();
            foreach (var light in bucket)
                AppendCone(mesh, light.X, light.Y, light.Z, light.BeamHeight, light.ReachRadius);

            mesh.Freeze();

            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(ConeDiffuseAlpha, color.R, color.G, color.B))));
            material.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(ConeEmissiveAlpha, color.R, color.G, color.B))));
            material.Freeze();

            group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        }
    }

    private static void AppendCone(MeshGeometry3D mesh, double x, double y, double z, double height, double radius)
    {
        var apex = new Point3D(x, y, z);
        double baseY = Math.Max(0.0, y - height);
        int apexIndex = mesh.Positions.Count;
        mesh.Positions.Add(apex);
        mesh.TextureCoordinates.Add(new System.Windows.Point(0.5, 0));

        for (int i = 0; i <= ConeSegments; i++)
        {
            double angle = 2.0 * Math.PI * i / ConeSegments;
            mesh.Positions.Add(new Point3D(
                x + Math.Cos(angle) * radius,
                baseY,
                z + Math.Sin(angle) * radius));
            mesh.TextureCoordinates.Add(new System.Windows.Point((double)i / ConeSegments, 1));
        }

        for (int i = 1; i <= ConeSegments; i++)
        {
            mesh.TriangleIndices.Add(apexIndex);
            mesh.TriangleIndices.Add(apexIndex + i);
            mesh.TriangleIndices.Add(apexIndex + i + 1);
        }
    }

    private static void AddMergedMarkerVisual(Model3DGroup group, IReadOnlyList<LightCandidate> lights)
    {
        if (lights.Count == 0)
            return;

        var mesh = new MeshGeometry3D();
        foreach (var light in lights)
            AppendOctahedron(mesh, light.X, light.Y, light.Z, MarkerRadius);

        mesh.Freeze();
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 244, 170))));
        material.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(255, 226, 90))));
        material.Freeze();
        group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
    }

    private static void AppendOctahedron(MeshGeometry3D mesh, double cx, double cy, double cz, double r)
    {
        int baseIdx = mesh.Positions.Count;
        mesh.Positions.Add(new Point3D(cx, cy + r, cz));
        mesh.Positions.Add(new Point3D(cx + r, cy, cz));
        mesh.Positions.Add(new Point3D(cx, cy, cz + r));
        mesh.Positions.Add(new Point3D(cx - r, cy, cz));
        mesh.Positions.Add(new Point3D(cx, cy, cz - r));
        mesh.Positions.Add(new Point3D(cx, cy - r, cz));

        for (int i = 0; i < 6; i++)
            mesh.TextureCoordinates.Add(new System.Windows.Point(0, 0));

        int[] indices =
        {
            0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1,
            5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4
        };

        foreach (int index in indices)
            mesh.TriangleIndices.Add(baseIdx + index);
    }
}
