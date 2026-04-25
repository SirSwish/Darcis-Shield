// Services/PrmMeshBuilderService.cs
// Converts a parsed PrmModel into a native WPF Model3DGroup.
//
// Pipeline per the spec:
//   - Compute per-face textureId via PrimTextureResolverService.
//   - Group geometry by textureId so each material is a single GeometryModel3D.
//   - Quads split into two triangles using indices (0,1,2) and (0,2,3).
//   - UVs normalized as u = U/256, v = V/256.
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
    public sealed class PrmMeshBuilderService
    {
        private readonly PrimTextureResolverService? _textureResolver;
        private readonly Dictionary<int, Material> _materialCache = new();
        private static readonly Material FallbackMaterial = CreateFallbackMaterial();

        public PrmMeshBuilderService(PrimTextureResolverService? textureResolver)
        {
            _textureResolver = textureResolver;
        }

        public Model3DGroup Build(PrmModel prm)
        {
            ArgumentNullException.ThrowIfNull(prm);

            Debug.WriteLine($"[PrmMeshBuilder] Building '{prm.FileName}'  " +
                            $"pts={prm.Points.Count}  tris={prm.Triangles.Count}  quads={prm.Quadrangles.Count}");
            Debug.WriteLine($"[PrmMeshBuilder] TextureResolver: {(_textureResolver is null ? "NULL — no texture dir set" : _textureResolver.PrimTextureDirectory)}");

            var group = new Model3DGroup();
            var pointLookup = prm.Points.ToDictionary(p => p.GlobalId);
            var meshesByTextureId = new Dictionary<int, MeshGeometry3D>();

            foreach (PrmTriangle tri in prm.Triangles)
            {
                if (!pointLookup.TryGetValue(tri.PointAId, out PrmPoint a)) continue;
                if (!pointLookup.TryGetValue(tri.PointBId, out PrmPoint b)) continue;
                if (!pointLookup.TryGetValue(tri.PointCId, out PrmPoint c)) continue;

                var mapping = PrmUvService.GetTriangleTexture(
                    tri.UA, tri.VA,
                    tri.UB, tri.VB,
                    tri.UC, tri.VC,
                    tri.TexturePage);

                MeshGeometry3D mesh = GetOrCreateMesh(meshesByTextureId, mapping.TextureId);

                AddTriangle(mesh,
                    a, b, c,
                    PrmUvService.ToLocalUv(tri.UA, tri.VA, mapping),
                    PrmUvService.ToLocalUv(tri.UB, tri.VB, mapping),
                    PrmUvService.ToLocalUv(tri.UC, tri.VC, mapping));
            }

            foreach (PrmQuadrangle quad in prm.Quadrangles)
            {
                if (!pointLookup.TryGetValue(quad.PointAId, out PrmPoint a)) continue;
                if (!pointLookup.TryGetValue(quad.PointBId, out PrmPoint b)) continue;
                if (!pointLookup.TryGetValue(quad.PointCId, out PrmPoint c)) continue;
                if (!pointLookup.TryGetValue(quad.PointDId, out PrmPoint d)) continue;

                var mapping = PrmUvService.GetQuadTexture(
                    quad.UA, quad.VA,
                    quad.UB, quad.VB,
                    quad.UC, quad.VC,
                    quad.UD, quad.VD,
                    quad.TexturePage);

                MeshGeometry3D mesh = GetOrCreateMesh(meshesByTextureId, mapping.TextureId);

                AddTriangle(mesh,
                    a, b, c,
                    PrmUvService.ToLocalUv(quad.UA, quad.VA, mapping),
                    PrmUvService.ToLocalUv(quad.UB, quad.VB, mapping),
                    PrmUvService.ToLocalUv(quad.UC, quad.VC, mapping));

                AddTriangle(mesh,
                    a, c, d,
                    PrmUvService.ToLocalUv(quad.UA, quad.VA, mapping),
                    PrmUvService.ToLocalUv(quad.UC, quad.VC, mapping),
                    PrmUvService.ToLocalUv(quad.UD, quad.VD, mapping));
            }

            foreach ((int textureId, MeshGeometry3D mesh) in meshesByTextureId)
            {
                if (mesh.Positions.Count == 0) continue;

                Material material = GetOrCreateMaterial(textureId);
                mesh.Freeze();
                var geom = new GeometryModel3D(mesh, material) { BackMaterial = material };
                geom.Freeze();
                group.Children.Add(geom);
            }

            CenterModel(group);
            return group;
        }

        // ── Mesh helpers ──────────────────────────────────────────────────────

        private static MeshGeometry3D GetOrCreateMesh(Dictionary<int, MeshGeometry3D> dict, int textureId)
        {
            if (!dict.TryGetValue(textureId, out MeshGeometry3D? mesh))
            {
                mesh = new MeshGeometry3D();
                dict[textureId] = mesh;
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

            mesh.TriangleIndices.Add(start);
            mesh.TriangleIndices.Add(start + 1);
            mesh.TriangleIndices.Add(start + 2);
        }

        private static Point3D ToPoint3D(PrmPoint p)
        {
            // The game negates Y when placing prims in the world, so the raw PRM
            // data is already in the game's effective orientation. Pass Y through
            // unchanged so the viewer matches what the player sees in-game.
            return new Point3D(p.X, p.Y, p.Z);
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

        // ── Material pipeline ─────────────────────────────────────────────────

        private Material GetOrCreateMaterial(int textureId)
        {
            if (_materialCache.TryGetValue(textureId, out Material? cached))
                return cached;

            Material material = CreateMaterial(textureId);
            _materialCache[textureId] = material;
            return material;
        }

        private Material CreateMaterial(int textureId)
        {
            if (_textureResolver is null)
            {
                Debug.WriteLine($"[PrmMeshBuilder]   texId={textureId}  → FALLBACK (no resolver — set game root first)");
                return FallbackMaterial;
            }

            string expectedPath = _textureResolver.BuildExpectedTexturePath(textureId);
            string? path = _textureResolver.ResolveTexturePath(textureId);

            if (path is null || !File.Exists(path))
            {
                Debug.WriteLine($"[PrmMeshBuilder]   texId={textureId}  → FALLBACK (file not found)  expected: {expectedPath}");
                return FallbackMaterial;
            }

            BitmapSource? bitmap = LoadBitmap(path);
            if (bitmap is null)
            {
                Debug.WriteLine($"[PrmMeshBuilder]   texId={textureId}  → FALLBACK (bitmap load failed)  path: {path}");
                return FallbackMaterial;
            }

            Debug.WriteLine($"[PrmMeshBuilder]   texId={textureId}  → OK  {path}  ({bitmap.PixelWidth}×{bitmap.PixelHeight})");

            var brush = new ImageBrush(bitmap)
            {
                ViewportUnits = BrushMappingMode.Absolute,
                TileMode = TileMode.Tile,
                Stretch = Stretch.Fill,
            };
            brush.Freeze();

            var diffuse = new DiffuseMaterial(brush);
            diffuse.Freeze();
            return diffuse;
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
                img.UriSource = new Uri(path, UriKind.Absolute);
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
    }
}
