// Services/Viewport3D/SkyboxBuilder.cs
// Resolves the per-world sky texture and builds a Model3DGroup that wraps the
// scene like a skybox. The camera sits inside, so we draw inward-facing geometry
// and disable lighting (emissive material) so the sky always renders at full colour.

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using UrbanChaosEditor.Shared.Constants;

namespace UrbanChaosMapEditor.Services.Viewport3D
{
    public static class SkyboxBuilder
    {
        // The skybox is centred on the camera and is large enough that the scene
        // (8192 x 8192 with vertical extent of a few hundred) never reaches its edge.
        public const double SkyboxRadius = 20000.0;

        // Y at which the bottom edge of the side faces sits, in skybox-local space.
        // Raised above -SkyboxRadius so the bottom of sky.tga renders in the sky region
        // rather than at the ground horizon.
        public const double SkyboxSideBottomY = 4000.0;

        private const string TexturesAsm = ApplicationConstants.SharedAssemblyName;

        /// <summary>
        /// Resolves the sky bitmap for the given world / variant.
        /// Tries (in order): embedded PNG, custom-textures disk PNG, custom-textures disk TGA.
        /// </summary>
        public static BitmapSource? ResolveSkyBitmap(int worldNumber, string? variant)
        {
            if (worldNumber <= 0) return null;

            string setName = variant?.ToLowerInvariant() ?? "release";

            // 1. Embedded PNG
            string packUri = $"pack://application:,,,/{TexturesAsm};component/Assets/Textures/{setName}/world{worldNumber}/sky.png";
            try
            {
                var sri = Application.GetResourceStream(new Uri(packUri));
                if (sri?.Stream != null)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = sri.Stream;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Skybox] Embedded sky lookup failed for world {worldNumber}: {ex.Message}");
            }

            // 2/3. CustomTextures on disk (png / tga)
            string customRoot = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "CustomTextures", $"world{worldNumber}");

            if (Directory.Exists(customRoot))
            {
                foreach (var name in new[] { "sky.png", "sky.tga", "sky.bmp" })
                {
                    string p = Path.Combine(customRoot, name);
                    if (!File.Exists(p)) continue;
                    try
                    {
                        if (name.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                        {
                            // Reuse the Prim Editor's TGA loader to avoid a second implementation.
                            var bmp = UrbanChaosPrimEditor.Services.TgaLoader.Load(p);
                            if (bmp != null) return bmp;
                        }
                        else
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.UriSource = new Uri(p, UriKind.Absolute);
                            bmp.EndInit();
                            bmp.Freeze();
                            return bmp;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Skybox] Failed loading {p}: {ex.Message}");
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Builds a skybox Model3D for the given sky bitmap.
        /// Geometry: large inward-facing cube. Lighting: emissive only, so it renders
        /// at full colour regardless of scene lights. Returns null if texture is missing.
        /// </summary>
        public static Model3D? Build(BitmapSource? skyBitmap)
        {
            if (skyBitmap == null) return null;

            var brush = new ImageBrush(skyBitmap)
            {
                Stretch = Stretch.Fill,
                TileMode = TileMode.None,
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new Rect(0, 0, 1, 1)
            };
            brush.Freeze();

            // Emissive so scene lighting has no effect on the sky.
            var matGroup = new MaterialGroup();
            matGroup.Children.Add(new EmissiveMaterial(brush));
            matGroup.Freeze();

            double r = SkyboxRadius;
            double yb = SkyboxSideBottomY;

            // 8 cube corners centred at origin on X/Z; bottom corners use yb so the
            // side faces (and the texture's bottom edge) start above the horizon.
            var p000 = new Point3D(-r, yb, -r);
            var p100 = new Point3D(+r, yb, -r);
            var p110 = new Point3D(+r, +r, -r);
            var p010 = new Point3D(-r, +r, -r);
            var p001 = new Point3D(-r, yb, +r);
            var p101 = new Point3D(+r, yb, +r);
            var p111 = new Point3D(+r, +r, +r);
            var p011 = new Point3D(-r, +r, +r);

            var group = new Model3DGroup();

            // 6 inward-facing quads with the sky texture mapped over each face.
            // Winding order is chosen so the visible side faces the camera (inward).
            // -Z face (looking +Z): p100, p000, p010, p110
            AddFace(group, matGroup, p100, p000, p010, p110);
            // +Z face (looking -Z): p001, p101, p111, p011
            AddFace(group, matGroup, p001, p101, p111, p011);
            // -X face (looking +X): p000, p001, p011, p010
            AddFace(group, matGroup, p000, p001, p011, p010);
            // +X face (looking -X): p101, p100, p110, p111
            AddFace(group, matGroup, p101, p100, p110, p111);
            // -Y face (looking +Y) — ground side. Skip; terrain covers it.
            // +Y face (looking -Y) — top of dome.
            AddFace(group, matGroup, p010, p011, p111, p110);

            group.Freeze();
            return group;
        }

        private static void AddFace(
            Model3DGroup group,
            Material material,
            Point3D a, Point3D b, Point3D c, Point3D d)
        {
            var mesh = new MeshGeometry3D();
            mesh.Positions.Add(a);
            mesh.Positions.Add(b);
            mesh.Positions.Add(c);
            mesh.Positions.Add(d);

            mesh.TextureCoordinates.Add(new Point(0, 1));
            mesh.TextureCoordinates.Add(new Point(1, 1));
            mesh.TextureCoordinates.Add(new Point(1, 0));
            mesh.TextureCoordinates.Add(new Point(0, 0));

            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(3);

            mesh.Freeze();

            var geom = new GeometryModel3D(mesh, material) { BackMaterial = material };
            geom.Freeze();
            group.Children.Add(geom);
        }
    }
}
