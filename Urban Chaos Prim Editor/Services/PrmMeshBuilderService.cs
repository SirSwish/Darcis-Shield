// Services/PrmMeshBuilderService.cs
// Converts a parsed PrmModel into a native WPF Model3DGroup.
//
// Two build paths:
//   - Build()         → frozen, view-only scene (legacy, kept for any read-only use).
//   - BuildEditable() → unfrozen scene with extra per-point sphere markers and a
//                       lookup so the view layer can hit-test markers back to point ids.
//
// Pipeline (shared):
//   - Compute per-face textureId and local UVs via PrmUvService (legacy
//     viewer logic: average UVs → tile origin → (raw - base)/32.
//   - Group geometry by textureId so each material is a single GeometryModel3D.
//   - Quads split along the engine perimeter order 0,1,3,2 with UVs
//     preserved per-vertex.
//   - Material: BitmapImage/TGA -> ImageBrush -> DiffuseMaterial.
//     Falls back to a flat gray DiffuseMaterial when texture is missing/unloadable.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using UrbanChaosPrimEditor.Models;

namespace UrbanChaosPrimEditor.Services
{
    /// <summary>Result of an editable scene build — group plus marker lookups.</summary>
    public sealed class PrmEditScene
    {
        public Model3DGroup Group { get; init; } = new();
        public Dictionary<GeometryModel3D, int> MarkerToPointId { get; init; } = new();
        public Dictionary<int, GeometryModel3D> PointIdToMarker { get; init; } = new();
        public Dictionary<GeometryModel3D, SelectedFaceHint> FaceHitToFace { get; init; } = new();
    }

    /// <summary>Identifies which face to draw a highlight outline around.</summary>
    public readonly record struct SelectedFaceHint(bool IsTriangle, int Index);

    public sealed class PrmMeshBuilderService
    {
        private readonly PrimTextureResolverService? _textureResolver;
        private readonly Dictionary<TextureSurfaceKey, Material> _materialCache = new();
        private readonly Dictionary<int, (int Width, int Height)?> _textureSizeCache = new();
        private static readonly Material FallbackMaterial = CreateFallbackMaterial();

        private readonly record struct TextureSurfaceKey(int TextureId, int X, int Y, int Width, int Height);

        // Shared marker geometry — built once and reused for every point.
        private static readonly MeshGeometry3D MarkerMesh = CreateMarkerMesh();

        // Marker materials.
        private static readonly Material MarkerMaterialNormal   = CreateMarkerMaterial(Color.FromRgb(220, 220, 220), Color.FromRgb(120, 120, 120));
        private static readonly Material MarkerMaterialSelected = CreateMarkerMaterial(Color.FromRgb( 80, 220, 255), Color.FromRgb( 60, 180, 220));
        private static readonly Material MarkerMaterialFaceBuild = CreateMarkerMaterial(Color.FromRgb(255, 220,  60), Color.FromRgb(220, 180,  40));
        private static readonly Material GridMaterial          = CreateGuideMaterial(Color.FromRgb(70, 70, 70));
        private static readonly Material FaceHighlightMaterial  = CreateFaceHighlightMaterial();
        private static readonly Material FaceHitTestMaterial = CreateFaceHitTestMaterial();
        private static readonly Material AxisXMaterial = CreateGuideMaterial(Color.FromRgb(230, 70, 70));
        private static readonly Material AxisYMaterial = CreateGuideMaterial(Color.FromRgb(80, 220, 120));
        private static readonly Material AxisZMaterial = CreateGuideMaterial(Color.FromRgb(80, 150, 255));
        private static readonly Material OriginMaterial = CreateGuideMaterial(Color.FromRgb(235, 235, 235));

        public PrmMeshBuilderService(PrimTextureResolverService? textureResolver)
        {
            _textureResolver = textureResolver;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Build a frozen view-only scene (no markers).</summary>
        public Model3DGroup Build(PrmModel prm)
        {
            ArgumentNullException.ThrowIfNull(prm);

            var group = new Model3DGroup();
            BuildMeshGeometry(prm, group, freezeMeshes: true);
            CenterModel(group);
            group.Freeze();
            return group;
        }

        /// <summary>
        /// Build an unfrozen scene that includes per-point sphere markers.
        /// Returns the group along with marker→pointId lookups for hit-testing.
        /// </summary>
        public PrmEditScene BuildEditable(
            PrmModel prm,
            int? selectedPointId,
            IReadOnlyCollection<int>? faceBuildIds,
            SelectedFaceHint? selectedFace = null)
        {
            ArgumentNullException.ThrowIfNull(prm);

            var group = new Model3DGroup();
            BuildMeshGeometry(prm, group, freezeMeshes: false);

            // Determine a good marker size from the model bounds before centering.
            Rect3D bounds = group.Bounds;
            double radius = bounds.IsEmpty
                ? 200.0
                : Math.Sqrt(bounds.SizeX * bounds.SizeX + bounds.SizeY * bounds.SizeY + bounds.SizeZ * bounds.SizeZ) / 2.0;
            AddEditorGuides(group, radius);
            double markerSize = Math.Clamp(radius * 0.025, 2.0, 20.0);
            double edgeThickness = markerSize * 0.55;

            var faceBuildSet = faceBuildIds is null ? new HashSet<int>() : new HashSet<int>(faceBuildIds);
            var markerToPointId = new Dictionary<GeometryModel3D, int>(prm.Points.Count);
            var pointIdToMarker = new Dictionary<int, GeometryModel3D>(prm.Points.Count);
            var faceHitToFace = new Dictionary<GeometryModel3D, SelectedFaceHint>();

            AddFaceHitSurfaces(group, prm, faceHitToFace);

            foreach (PrmPoint p in prm.Points)
            {
                Material material = MarkerMaterialNormal;
                if (p.GlobalId == selectedPointId)
                    material = MarkerMaterialSelected;
                else if (faceBuildSet.Contains(p.GlobalId))
                    material = MarkerMaterialFaceBuild;

                var transform = new Transform3DGroup();
                transform.Children.Add(new ScaleTransform3D(markerSize, markerSize, markerSize));
                transform.Children.Add(new TranslateTransform3D(p.X, p.Y, p.Z));

                var marker = new GeometryModel3D(MarkerMesh, material)
                {
                    BackMaterial = material,
                    Transform    = transform,
                };

                group.Children.Add(marker);
                markerToPointId[marker] = p.GlobalId;
                pointIdToMarker[p.GlobalId] = marker;
            }

            if (selectedFace is { } face)
                AddFaceHighlight(group, prm, face.IsTriangle, face.Index, edgeThickness);

            CenterModel(group);

            return new PrmEditScene
            {
                Group           = group,
                MarkerToPointId = markerToPointId,
                PointIdToMarker = pointIdToMarker,
                FaceHitToFace   = faceHitToFace,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Mesh building (shared)
        // ─────────────────────────────────────────────────────────────────────

        private void BuildMeshGeometry(PrmModel prm, Model3DGroup group, bool freezeMeshes)
        {
            var pointLookup = prm.Points.ToDictionary(p => p.GlobalId);
            var meshesBySurface = new Dictionary<TextureSurfaceKey, MeshGeometry3D>();

            for (int faceIndex = 0; faceIndex < prm.Triangles.Count; faceIndex++)
            {
                PrmTriangle tri = prm.Triangles[faceIndex];

                if (!pointLookup.TryGetValue(tri.PointAId, out PrmPoint a)) continue;
                if (!pointLookup.TryGetValue(tri.PointBId, out PrmPoint b)) continue;
                if (!pointLookup.TryGetValue(tri.PointCId, out PrmPoint c)) continue;

                var mapping = PrmUvService.CalculateTriangle(
                    tri.UA, tri.VA,
                    tri.UB, tri.VB,
                    tri.UC, tri.VC,
                    tri.TexturePage);

                LogTriangleMapping(faceIndex, tri, mapping);

                Point[] uvs = { mapping.UV0, mapping.UV1, mapping.UV2 };
                TextureSurfaceKey surfaceKey = NormalizeFaceTextureSurface(mapping.TextureId, uvs);
                MeshGeometry3D mesh = GetOrCreateMesh(meshesBySurface, surfaceKey);

                AddTriangle(mesh,
                    a, b, c,
                    uvs[0], uvs[1], uvs[2]);
            }

            for (int faceIndex = 0; faceIndex < prm.Quadrangles.Count; faceIndex++)
            {
                PrmQuadrangle quad = prm.Quadrangles[faceIndex];

                if (!pointLookup.TryGetValue(quad.PointAId, out PrmPoint a)) continue;
                if (!pointLookup.TryGetValue(quad.PointBId, out PrmPoint b)) continue;
                if (!pointLookup.TryGetValue(quad.PointCId, out PrmPoint c)) continue;
                if (!pointLookup.TryGetValue(quad.PointDId, out PrmPoint d)) continue;

                var mapping = PrmUvService.CalculateQuad(
                    quad.UA, quad.VA,
                    quad.UB, quad.VB,
                    quad.UC, quad.VC,
                    quad.UD, quad.VD,
                    quad.TexturePage);

                LogQuadMapping(faceIndex, quad, mapping);

                // Engine quad perimeter is 0,1,3,2, so split on that diagonal.
                Point uv0 = mapping.UV0;
                Point uv1 = mapping.UV1;
                Point uv2 = mapping.UV2;
                Point uv3 = mapping.UV3!.Value;
                Point[] uvs = { uv0, uv1, uv2, uv3 };
                TextureSurfaceKey surfaceKey = NormalizeFaceTextureSurface(mapping.TextureId, uvs);
                MeshGeometry3D mesh = GetOrCreateMesh(meshesBySurface, surfaceKey);
                uv0 = uvs[0];
                uv1 = uvs[1];
                uv2 = uvs[2];
                uv3 = uvs[3];

                AddTriangle(mesh,
                    a, b, d,
                    uv0, uv1, uv3);

                AddTriangle(mesh,
                    a, d, c,
                    uv0, uv3, uv2);
            }

            foreach ((TextureSurfaceKey surfaceKey, MeshGeometry3D mesh) in meshesBySurface)
            {
                if (mesh.Positions.Count == 0) continue;

                Material material = GetOrCreateMaterial(surfaceKey);
                if (freezeMeshes) mesh.Freeze();
                var geom = new GeometryModel3D(mesh, material) { BackMaterial = material };
                if (freezeMeshes) geom.Freeze();
                group.Children.Add(geom);
            }
        }

        // ── Mesh helpers ──────────────────────────────────────────────────────

        private static MeshGeometry3D GetOrCreateMesh(Dictionary<TextureSurfaceKey, MeshGeometry3D> dict, TextureSurfaceKey surfaceKey)
        {
            if (!dict.TryGetValue(surfaceKey, out MeshGeometry3D? mesh))
            {
                mesh = new MeshGeometry3D();
                dict[surfaceKey] = mesh;
            }
            return mesh;
        }

        private static void AddTriangle(
                             MeshGeometry3D mesh,
                             PrmPoint a,
                             PrmPoint b,
                             PrmPoint c,
                             Point uva,
                             Point uvb,
                             Point uvc)
        {
            int start = mesh.Positions.Count;

            mesh.Positions.Add(ToPoint3D(a));
            mesh.Positions.Add(ToPoint3D(b));
            mesh.Positions.Add(ToPoint3D(c));

            mesh.TextureCoordinates.Add(uva);
            mesh.TextureCoordinates.Add(uvb);
            mesh.TextureCoordinates.Add(uvc);

            mesh.TriangleIndices.Add(start + 2);
            mesh.TriangleIndices.Add(start + 1);
            mesh.TriangleIndices.Add(start);
        }

        private TextureSurfaceKey NormalizeFaceTextureSurface(int textureId, Point[] uvs)
        {
            (int Width, int Height)? size = GetTextureSize(textureId);
            if (size is not { } textureSize)
                return new TextureSurfaceKey(textureId, 0, 0, 0, 0);

            double minU = uvs.Min(uv => uv.X);
            double maxU = uvs.Max(uv => uv.X);
            double minV = uvs.Min(uv => uv.Y);
            double maxV = uvs.Max(uv => uv.Y);

            minU = SnapUvEdge(minU);
            maxU = SnapUvEdge(maxU);
            minV = SnapUvEdge(minV);
            maxV = SnapUvEdge(maxV);

            if (NearlyEqual(minU, maxU) || NearlyEqual(minV, maxV))
                return new TextureSurfaceKey(textureId, 0, 0, textureSize.Width, textureSize.Height);

            for (int i = 0; i < uvs.Length; i++)
            {
                double u = (SnapUvEdge(uvs[i].X) - minU) / (maxU - minU);
                double v = (SnapUvEdge(uvs[i].Y) - minV) / (maxV - minV);
                uvs[i] = new Point(u, v);
            }

            int x = ClampPixel((int)Math.Floor(minU * textureSize.Width), textureSize.Width);
            int y = ClampPixel((int)Math.Floor(minV * textureSize.Height), textureSize.Height);
            int right = ClampPixel((int)Math.Ceiling(maxU * textureSize.Width), textureSize.Width);
            int bottom = ClampPixel((int)Math.Ceiling(maxV * textureSize.Height), textureSize.Height);

            if (right <= x) right = Math.Min(textureSize.Width, x + 1);
            if (bottom <= y) bottom = Math.Min(textureSize.Height, y + 1);

            return new TextureSurfaceKey(textureId, x, y, right - x, bottom - y);
        }

        private (int Width, int Height)? GetTextureSize(int textureId)
        {
            if (_textureSizeCache.TryGetValue(textureId, out (int Width, int Height)? cached))
                return cached;

            (int Width, int Height)? size = null;

            if (_textureResolver is not null)
            {
                string? path = _textureResolver.ResolveTexturePath(textureId);
                if (path is not null && File.Exists(path))
                {
                    BitmapSource? bitmap = LoadBitmap(path);
                    if (bitmap is not null)
                        size = (bitmap.PixelWidth, bitmap.PixelHeight);
                }
            }

            _textureSizeCache[textureId] = size;
            return size;
        }

        private static bool NearlyEqual(double a, double b)
        {
            return Math.Abs(a - b) < 0.00001;
        }

        private static double SnapUvEdge(double value)
        {
            if (Math.Abs(value) < 0.00001)
                return 0.0;

            if (Math.Abs(value - (31.0 / 32.0)) < 0.00001 || Math.Abs(value - 1.0) < 0.00001)
                return 1.0;

            return value;
        }

        private static int ClampPixel(int value, int max)
        {
            return Math.Clamp(value, 0, max);
        }

        private static Point3D ToPoint3D(PrmPoint p)
        {
            // The game negates Y when placing prims in the world, so the raw PRM
            // data is already in the game's effective orientation. Pass Y through
            // unchanged so the viewer matches what the player sees in-game.
            return new Point3D(p.X, p.Y, p.Z);
        }

        private static void AddEditorGuides(Model3DGroup group, double radius)
        {
            double extent = Math.Max(100.0, Math.Ceiling(radius * 1.25 / 50.0) * 50.0);
            double spacing = extent <= 300.0 ? 50.0 : 100.0;
            double thin = Math.Clamp(extent / 500.0, 0.5, 3.0);
            double thick = thin * 3.0;

            for (double p = -extent; p <= extent + 0.001; p += spacing)
            {
                if (Math.Abs(p) < 0.001) continue;
                AddBox(group, new Point3D(0, 0, p), new Size3D(extent * 2.0, thin, thin), GridMaterial);
                AddBox(group, new Point3D(p, 0, 0), new Size3D(thin, thin, extent * 2.0), GridMaterial);
            }

            AddBox(group, new Point3D(0, 0, 0), new Size3D(extent * 2.0, thick, thick), AxisXMaterial);
            AddBox(group, new Point3D(0, extent / 2.0, 0), new Size3D(thick, extent, thick), AxisYMaterial);
            AddBox(group, new Point3D(0, 0, 0), new Size3D(thick, thick, extent * 2.0), AxisZMaterial);
            AddBox(group, new Point3D(0, 0, 0), new Size3D(thick * 3.0, thick * 3.0, thick * 3.0), OriginMaterial);
        }

        private static void AddBox(Model3DGroup group, Point3D center, Size3D size, Material material)
        {
            double hx = Math.Max(0.1, size.X / 2.0);
            double hy = Math.Max(0.1, size.Y / 2.0);
            double hz = Math.Max(0.1, size.Z / 2.0);

            var mesh = new MeshGeometry3D();
            mesh.Positions.Add(new Point3D(center.X - hx, center.Y - hy, center.Z - hz));
            mesh.Positions.Add(new Point3D(center.X + hx, center.Y - hy, center.Z - hz));
            mesh.Positions.Add(new Point3D(center.X + hx, center.Y + hy, center.Z - hz));
            mesh.Positions.Add(new Point3D(center.X - hx, center.Y + hy, center.Z - hz));
            mesh.Positions.Add(new Point3D(center.X - hx, center.Y - hy, center.Z + hz));
            mesh.Positions.Add(new Point3D(center.X + hx, center.Y - hy, center.Z + hz));
            mesh.Positions.Add(new Point3D(center.X + hx, center.Y + hy, center.Z + hz));
            mesh.Positions.Add(new Point3D(center.X - hx, center.Y + hy, center.Z + hz));

            int[] indices =
            {
                0,2,1, 0,3,2,
                4,5,6, 4,6,7,
                0,1,5, 0,5,4,
                3,7,6, 3,6,2,
                1,2,6, 1,6,5,
                0,4,7, 0,7,3,
            };

            foreach (int index in indices)
                mesh.TriangleIndices.Add(index);

            mesh.Freeze();
            var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
            model.Freeze();
            group.Children.Add(model);
        }

        private static void CenterModel(Model3DGroup group)
        {
            Rect3D bounds = group.Bounds;
            if (bounds.IsEmpty) return;

            var center = new Point3D(
                bounds.X + bounds.SizeX / 2.0,
                bounds.Y + bounds.SizeY / 2.0,
                bounds.Z + bounds.SizeZ / 2.0);
            group.Transform = new TranslateTransform3D(-center.X, -center.Y, -center.Z);
        }

        // ── Marker geometry / materials ───────────────────────────────────────

        private static MeshGeometry3D CreateMarkerMesh()
        {
            // Unit-radius octahedron — scaled per-instance via TransformGroup.
            var m = new MeshGeometry3D();
            m.Positions.Add(new Point3D( 1,  0,  0));
            m.Positions.Add(new Point3D(-1,  0,  0));
            m.Positions.Add(new Point3D( 0,  1,  0));
            m.Positions.Add(new Point3D( 0, -1,  0));
            m.Positions.Add(new Point3D( 0,  0,  1));
            m.Positions.Add(new Point3D( 0,  0, -1));

            int[] tri =
            {
                0,2,4,  2,1,4,  1,3,4,  3,0,4,
                2,0,5,  1,2,5,  3,1,5,  0,3,5,
            };
            foreach (int i in tri) m.TriangleIndices.Add(i);
            m.Freeze();
            return m;
        }

        private static Material CreateMarkerMaterial(Color diffuse, Color emissive)
        {
            var diffuseBrush = new SolidColorBrush(diffuse); diffuseBrush.Freeze();
            var emissiveBrush = new SolidColorBrush(emissive); emissiveBrush.Freeze();

            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(diffuseBrush));
            group.Children.Add(new EmissiveMaterial(emissiveBrush));
            group.Freeze();
            return group;
        }

        // ── Face highlight ────────────────────────────────────────────────────

        private static Material CreateFaceHighlightMaterial()
        {
            var diffuse  = new SolidColorBrush(Color.FromRgb(255, 165,   0)); diffuse.Freeze();
            var emissive = new SolidColorBrush(Color.FromRgb(255, 120,   0)); emissive.Freeze();
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(diffuse));
            group.Children.Add(new EmissiveMaterial(emissive));
            group.Freeze();
            return group;
        }

        private static Material CreateFaceHitTestMaterial()
        {
            var brush = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
            brush.Freeze();
            var material = new DiffuseMaterial(brush);
            material.Freeze();
            return material;
        }

        private static void AddFaceHitSurfaces(
            Model3DGroup group,
            PrmModel prm,
            Dictionary<GeometryModel3D, SelectedFaceHint> faceHitToFace)
        {
            var pointLookup = prm.Points.ToDictionary(p => p.GlobalId);

            for (int i = 0; i < prm.Triangles.Count; i++)
            {
                PrmTriangle t = prm.Triangles[i];
                if (!pointLookup.TryGetValue(t.PointAId, out PrmPoint a)) continue;
                if (!pointLookup.TryGetValue(t.PointBId, out PrmPoint b)) continue;
                if (!pointLookup.TryGetValue(t.PointCId, out PrmPoint c)) continue;

                var mesh = new MeshGeometry3D();
                mesh.Positions.Add(ToPoint3D(a));
                mesh.Positions.Add(ToPoint3D(b));
                mesh.Positions.Add(ToPoint3D(c));
                mesh.TriangleIndices.Add(2);
                mesh.TriangleIndices.Add(1);
                mesh.TriangleIndices.Add(0);

                var model = new GeometryModel3D(mesh, FaceHitTestMaterial) { BackMaterial = FaceHitTestMaterial };
                group.Children.Add(model);
                faceHitToFace[model] = new SelectedFaceHint(true, i);
            }

            for (int i = 0; i < prm.Quadrangles.Count; i++)
            {
                PrmQuadrangle q = prm.Quadrangles[i];
                if (!pointLookup.TryGetValue(q.PointAId, out PrmPoint a)) continue;
                if (!pointLookup.TryGetValue(q.PointBId, out PrmPoint b)) continue;
                if (!pointLookup.TryGetValue(q.PointCId, out PrmPoint c)) continue;
                if (!pointLookup.TryGetValue(q.PointDId, out PrmPoint d)) continue;

                var mesh = new MeshGeometry3D();
                mesh.Positions.Add(ToPoint3D(a));
                mesh.Positions.Add(ToPoint3D(b));
                mesh.Positions.Add(ToPoint3D(c));
                mesh.Positions.Add(ToPoint3D(d));
                mesh.TriangleIndices.Add(3);
                mesh.TriangleIndices.Add(1);
                mesh.TriangleIndices.Add(0);
                mesh.TriangleIndices.Add(2);
                mesh.TriangleIndices.Add(3);
                mesh.TriangleIndices.Add(0);

                var model = new GeometryModel3D(mesh, FaceHitTestMaterial) { BackMaterial = FaceHitTestMaterial };
                group.Children.Add(model);
                faceHitToFace[model] = new SelectedFaceHint(false, i);
            }
        }

        /// <summary>Draws an orange outline around the given face using thick edge tubes.</summary>
        private void AddFaceHighlight(Model3DGroup group, PrmModel prm, bool isTriangle, int faceIndex, double edgeThickness)
        {
            var pointLookup = prm.Points.ToDictionary(p => p.GlobalId);

            Point3D[] verts;
            if (isTriangle)
            {
                if (faceIndex < 0 || faceIndex >= prm.Triangles.Count) return;
                PrmTriangle t = prm.Triangles[faceIndex];
                if (!pointLookup.TryGetValue(t.PointAId, out PrmPoint a)) return;
                if (!pointLookup.TryGetValue(t.PointBId, out PrmPoint b)) return;
                if (!pointLookup.TryGetValue(t.PointCId, out PrmPoint c)) return;
                verts = [ToPoint3D(a), ToPoint3D(b), ToPoint3D(c)];
            }
            else
            {
                if (faceIndex < 0 || faceIndex >= prm.Quadrangles.Count) return;
                PrmQuadrangle q = prm.Quadrangles[faceIndex];
                if (!pointLookup.TryGetValue(q.PointAId, out PrmPoint a)) return;
                if (!pointLookup.TryGetValue(q.PointBId, out PrmPoint b)) return;
                if (!pointLookup.TryGetValue(q.PointCId, out PrmPoint c)) return;
                if (!pointLookup.TryGetValue(q.PointDId, out PrmPoint d)) return;
                // Engine perimeter order: A→B→D→C
                verts = [ToPoint3D(a), ToPoint3D(b), ToPoint3D(d), ToPoint3D(c)];
            }

            for (int i = 0; i < verts.Length; i++)
                AddEdgeLine(group, verts[i], verts[(i + 1) % verts.Length], edgeThickness, FaceHighlightMaterial);
        }

        /// <summary>Adds a square-cross-section tube between <paramref name="start"/> and <paramref name="end"/>.</summary>
        private static void AddEdgeLine(Model3DGroup group, Point3D start, Point3D end, double thickness, Material material)
        {
            Vector3D dir = end - start;
            double len = dir.Length;
            if (len < 0.001) return;
            dir.Normalize();

            // Build two axes perpendicular to the edge direction.
            Vector3D arbitrary = Math.Abs(dir.Y) < 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0);
            Vector3D right = Vector3D.CrossProduct(dir, arbitrary); right.Normalize();
            Vector3D up    = Vector3D.CrossProduct(right, dir);    up.Normalize();

            double h = thickness / 2.0;

            // 8 corners: 4 at start, 4 at end.
            Point3D s0 = start + h * right + h * up;
            Point3D s1 = start - h * right + h * up;
            Point3D s2 = start - h * right - h * up;
            Point3D s3 = start + h * right - h * up;
            Point3D e0 = end   + h * right + h * up;
            Point3D e1 = end   - h * right + h * up;
            Point3D e2 = end   - h * right - h * up;
            Point3D e3 = end   + h * right - h * up;

            var mesh = new MeshGeometry3D();
            mesh.Positions.Add(s0); mesh.Positions.Add(s1); mesh.Positions.Add(s2); mesh.Positions.Add(s3); // 0-3
            mesh.Positions.Add(e0); mesh.Positions.Add(e1); mesh.Positions.Add(e2); mesh.Positions.Add(e3); // 4-7

            int[] idx =
            {
                // 4 long sides
                0,4,5,  0,5,1,
                1,5,6,  1,6,2,
                2,6,7,  2,7,3,
                3,7,4,  3,4,0,
                // start cap
                0,1,2,  0,2,3,
                // end cap
                4,6,5,  4,7,6,
            };
            foreach (int i in idx) mesh.TriangleIndices.Add(i);

            var geom = new GeometryModel3D(mesh, material) { BackMaterial = material };
            group.Children.Add(geom);
        }

        // ── Texture material pipeline ─────────────────────────────────────────

        private static Material CreateGuideMaterial(Color color)
        {
            var diffuseBrush = new SolidColorBrush(color) { Opacity = 0.75 };
            diffuseBrush.Freeze();
            var emissiveBrush = new SolidColorBrush(color) { Opacity = 0.35 };
            emissiveBrush.Freeze();

            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(diffuseBrush));
            group.Children.Add(new EmissiveMaterial(emissiveBrush));
            group.Freeze();
            return group;
        }

        private Material GetOrCreateMaterial(TextureSurfaceKey surfaceKey)
        {
            if (_materialCache.TryGetValue(surfaceKey, out Material? cached))
                return cached;

            Material material = CreateMaterial(surfaceKey);
            _materialCache[surfaceKey] = material;
            return material;
        }

        private Material CreateMaterial(TextureSurfaceKey surfaceKey)
        {
            if (_textureResolver is null)
                return FallbackMaterial;

            string? path = _textureResolver.ResolveTexturePath(surfaceKey.TextureId);
            if (path is null || !File.Exists(path))
                return FallbackMaterial;

            BitmapSource? bitmap = LoadBitmap(path);
            if (bitmap is null)
                return FallbackMaterial;

            bitmap = CropSurface(bitmap, surfaceKey);

            var brush = new ImageBrush(bitmap)
            {
                Stretch        = Stretch.Fill,
                TileMode       = TileMode.None,
                ViewboxUnits   = BrushMappingMode.RelativeToBoundingBox,
                Viewbox        = new Rect(0, 0, 1, 1),
                ViewportUnits  = BrushMappingMode.RelativeToBoundingBox,
                Viewport       = new Rect(0, 0, 1, 1),
            };
            brush.Freeze();

            var diffuse = new DiffuseMaterial(brush);
            diffuse.Freeze();
            return diffuse;
        }

        private static BitmapSource CropSurface(BitmapSource bitmap, TextureSurfaceKey surfaceKey)
        {
            if (surfaceKey.Width <= 0 || surfaceKey.Height <= 0)
                return bitmap;

            if (surfaceKey.X == 0 &&
                surfaceKey.Y == 0 &&
                surfaceKey.Width == bitmap.PixelWidth &&
                surfaceKey.Height == bitmap.PixelHeight)
            {
                return bitmap;
            }

            int width = Math.Min(surfaceKey.Width, bitmap.PixelWidth - surfaceKey.X);
            int height = Math.Min(surfaceKey.Height, bitmap.PixelHeight - surfaceKey.Y);
            if (width <= 0 || height <= 0)
                return bitmap;

            var cropped = new CroppedBitmap(bitmap, new Int32Rect(surfaceKey.X, surfaceKey.Y, width, height));
            cropped.Freeze();
            return cropped;
        }

        private static BitmapSource? LoadBitmap(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".tga")
                return TgaLoader.Load(path);

            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource   = new Uri(path, UriKind.Absolute);
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                return null;
            }
        }

        private static Material CreateFallbackMaterial()
        {
            var brush = new SolidColorBrush(Color.FromRgb(160, 160, 160));
            brush.Freeze();
            var mat = new DiffuseMaterial(brush);
            mat.Freeze();
            return mat;
        }

        private void LogTriangleMapping(
            int faceIndex,
            PrmTriangle tri,
            PrmUvService.PrmFaceTextureMapping mapping)
        {
            string resolvedPath = ResolveTexturePathForLog(mapping.TextureId);
            Debug.WriteLine(
                $"[PRM UV] Face={faceIndex} Type=Triangle " +
                $"TexturePage={tri.TexturePage} " +
                $"RawUVs=({tri.UA},{tri.VA})({tri.UB},{tri.VB})({tri.UC},{tri.VC}) " +
                $"Avg=({mapping.AverageU:0.###},{mapping.AverageV:0.###}) " +
                $"Tile=({mapping.TileU},{mapping.TileV}) Base=({mapping.BaseU},{mapping.BaseV}) " +
                $"Page={mapping.Page} Texture={mapping.TextureId} " +
                $"ResolvedPath={resolvedPath} " +
                $"FinalUVs={FormatUv(mapping.UV0)}{FormatUv(mapping.UV1)}{FormatUv(mapping.UV2)}");
        }

        private void LogQuadMapping(
            int faceIndex,
            PrmQuadrangle quad,
            PrmUvService.PrmFaceTextureMapping mapping)
        {
            string resolvedPath = ResolveTexturePathForLog(mapping.TextureId);
            Debug.WriteLine(
                $"[PRM UV] Face={faceIndex} Type=Quad " +
                $"TexturePage={quad.TexturePage} " +
                $"RawUVs=({quad.UA},{quad.VA})({quad.UB},{quad.VB})({quad.UC},{quad.VC})({quad.UD},{quad.VD}) " +
                $"Avg=({mapping.AverageU:0.###},{mapping.AverageV:0.###}) " +
                $"Tile=({mapping.TileU},{mapping.TileV}) Base=({mapping.BaseU},{mapping.BaseV}) " +
                $"Page={mapping.Page} Texture={mapping.TextureId} " +
                $"ResolvedPath={resolvedPath} " +
                $"FinalUVs={FormatUv(mapping.UV0)}{FormatUv(mapping.UV1)}{FormatUv(mapping.UV2)}{FormatUv(mapping.UV3!.Value)}");
        }

        private string ResolveTexturePathForLog(int textureId)
        {
            if (_textureResolver is null)
                return "<no resolver>";

            return _textureResolver.ResolveTexturePath(textureId) ?? "<missing>";
        }

        private static string FormatUv(Point uv)
        {
            return $"({uv.X:0.#####},{uv.Y:0.#####})";
        }
    }
}
