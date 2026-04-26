// Services/Viewport3D/SceneBuilder.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using UrbanChaosEditor.Shared.Services.Textures;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Heights;
using UrbanChaosMapEditor.Services.Prims;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.Services.Styles;
using UrbanChaosMapEditor.Services.Textures;

namespace UrbanChaosMapEditor.Services.Viewport3D
{
    /// <summary>
    /// Builds Model3DGroups for the 3D viewport (terrain heightmap, facet walls/roofs, prim markers).
    /// Coord convention (matches the 2D map canvas):
    ///   X = 0..8192 left-to-right, Z = 0..8192 top-to-bottom, Y = up.
    /// Height byte (-127..127) is scaled by HeightScale to world Y.
    /// Facet endpoints use the same axis flip as the 2D overlay: pixelX = (128 - X) * 64.
    /// </summary>
    public sealed class SceneBuilder
    {
        // --- Tunable constants ---
        // Viewport Y convention: 1 storey = 64 view units (so 1 quarter-storey = 16).
        // The game stores Y in "engine" units where 1 storey = 256 (quarter-storey * 64,
        // and terrain / PAP_HI alt bytes are shifted by PAP_ALT_SHIFT=3, i.e. raw*8).
        // Every Y value read from the file must therefore be multiplied by EngineToViewY
        // (= 64/256 = 1/4) to land in view units.
        public const double StoreyWorld = 64.0;                     // one storey in view Y units
        public const double QuarterStoreyWorld = StoreyWorld / 4.0; // 16
        public const double EngineToViewY = 0.25;                   // engine-Y -> view-Y
        public const double HeightScale = 8.0 * EngineToViewY;      // terrain byte -> view Y (raw<<3 then scaled) = 2.0

        private readonly HeightsAccessor _heights;
        private readonly TexturesAccessor _textures;
        private readonly BuildingsAccessor _buildings;
        private readonly PrimsAccessor _prims;
        private readonly AltitudeAccessor _altitudes;

        // Cached procedural ladder tile (generated once, shared across all ladder facets).
        private static BitmapSource? _ladderTile;

        public SceneBuilder()
        {
            var data = MapDataService.Instance;
            _heights = new HeightsAccessor(data);
            _textures = new TexturesAccessor(data);
            _buildings = new BuildingsAccessor(data);
            _prims = new PrimsAccessor(data);
            _altitudes = new AltitudeAccessor(data);
        }

        // =====================================================================
        // TERRAIN
        // =====================================================================
        public Model3D? BuildTerrain()
        {
            if (!MapDataService.Instance.IsLoaded) return null;

            int N = MapConstants.TilesPerSide;  // 128
            int tile = MapConstants.TileSize;   // 64

            // Read all corner heights once. Corners form an (N+1) x (N+1) grid.
            // In UC the stored height belongs to the SE corner of each tile, so
            // corner (cx, cz) reads from tile (cx-1, cz-1), clamped to valid range.
            double[,] cornerY = new double[N + 1, N + 1];
            for (int cz = 0; cz <= N; cz++)
            {
                for (int cx = 0; cx <= N; cx++)
                {
                    int tx = Math.Max(0, Math.Min(cx - 1, N - 1));
                    int ty = Math.Max(0, Math.Min(cz - 1, N - 1));
                    sbyte h;
                    try { h = _heights.ReadHeight(tx, ty); }
                    catch { h = 0; }
                    cornerY[cx, cz] = h * HeightScale;
                }
            }

            // Batch quads by their texture key. Each bucket becomes one MeshGeometry3D
            // with a DiffuseMaterial using that texture. Quads with rotation use
            // rotated UVs within the same bucket.
            var buckets = new Dictionary<string, MeshGeometry3D>(StringComparer.OrdinalIgnoreCase);
            var materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            MeshGeometry3D? fallbackMesh = null;

            var cache = TextureCacheService.Instance;
            for (int ty = 0; ty < N; ty++)
            {
                for (int tx = 0; tx < N; tx++)
                {
                    string key;
                    int rot;
                    try { (key, rot) = _textures.GetTileTextureKeyAndRotation(tx, ty); }
                    catch { key = string.Empty; rot = 0; }

                    // Cell altitude (PAP_HI.Alt): per-tile elevation for roofs / raised floor cells.
                    // Engine Y = rawAlt << PAP_ALT_SHIFT (== raw*8). Scale to view-Y so 1 storey
                    // (rawAlt=32, engine=256) renders as 64 view units. Applied en bloc to all four
                    // corners so adjacent tiles with differing altitudes render as separated planes,
                    // matching the game's roof / raised-cell semantics.
                    double cellAltWorld = 0.0;
                    try
                    {
                        int papAlt = _altitudes.ReadAltRaw(tx, ty);
                        cellAltWorld = papAlt * QuarterStoreyWorld;   // 1 quarter-storey = 16
                    }
                    catch
                    {
                        cellAltWorld = 0.0;
                    }

                    // Quad corners in 2D-canvas XZ.
                    double x0 = tx * tile;
                    double x1 = x0 + tile;
                    double z0 = ty * tile;
                    double z1 = z0 + tile;

                    // If this cell has a roof, suppress terrain vertex heights entirely —
                    // render it as a perfectly flat quad at the cell altitude.
                    PapFlags papFlags = PapFlags.None;
                    try { papFlags = _altitudes.ReadFlags(tx, ty); } catch { }

                    double y00, y10, y01, y11;
                    if ((papFlags & PapFlags.RoofExists) != 0)
                    {
                        y00 = y10 = y01 = y11 = cellAltWorld;
                    }
                    else
                    {
                        y00 = cornerY[tx, ty]     + cellAltWorld;
                        y10 = cornerY[tx + 1, ty] + cellAltWorld;
                        y01 = cornerY[tx, ty + 1] + cellAltWorld;
                        y11 = cornerY[tx + 1, ty + 1] + cellAltWorld;
                    }

                    BitmapSource? bmp = null;
                    if (!string.IsNullOrEmpty(key))
                        cache.TryGetRelative(key, out bmp);

                    MeshGeometry3D mesh;
                    if (bmp != null)
                    {
                        if (!buckets.TryGetValue(key, out mesh!))
                        {
                            mesh = new MeshGeometry3D();
                            buckets[key] = mesh;
                            var brush = new ImageBrush(bmp)
                            {
                                TileMode = TileMode.None,
                                Stretch = Stretch.Fill,
                                ViewportUnits = BrushMappingMode.Absolute
                            };
                            brush.Freeze();
                            materials[key] = new DiffuseMaterial(brush);
                        }
                    }
                    else
                    {
                        fallbackMesh ??= new MeshGeometry3D();
                        mesh = fallbackMesh;
                    }

                    AddTileQuad(mesh, x0, z0, x1, z1, y00, y10, y01, y11, (rot + 180) % 360);
                }
            }

            var group = new Model3DGroup();
            foreach (var kv in buckets)
            {
                kv.Value.Freeze();
                group.Children.Add(new GeometryModel3D(kv.Value, materials[kv.Key])
                {
                    BackMaterial = materials[kv.Key]
                });
            }
            if (fallbackMesh != null)
            {
                fallbackMesh.Freeze();
                var gray = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(120, 120, 110)));
                group.Children.Add(new GeometryModel3D(fallbackMesh, gray) { BackMaterial = gray });
            }
            group.Freeze();
            return group;
        }

        /// <summary>Emit two triangles for one tile quad with rotated UVs.</summary>
        private static void AddTileQuad(
            MeshGeometry3D mesh,
            double x0, double z0, double x1, double z1,
            double y00, double y10, double y01, double y11,
            int rotationDeg)
        {
            int baseIdx = mesh.Positions.Count;
            mesh.Positions.Add(new Point3D(x0, y00, z0)); // 0 (x0,z0)
            mesh.Positions.Add(new Point3D(x1, y10, z0)); // 1 (x1,z0)
            mesh.Positions.Add(new Point3D(x1, y11, z1)); // 2 (x1,z1)
            mesh.Positions.Add(new Point3D(x0, y01, z1)); // 3 (x0,z1)

            // Base UVs aligned to corners 0..3.
            Point uv0 = new(0, 0), uv1 = new(1, 0), uv2 = new(1, 1), uv3 = new(0, 1);
            switch (rotationDeg % 360)
            {
                case 0:
                    (uv0, uv1, uv2, uv3) = (uv2, uv3, uv0, uv1);
                    break;

                case 90:
                    (uv0, uv1, uv2, uv3) = (uv1, uv2, uv3, uv0);
                    break;

                case 180:
                    break;

                case 270:
                    (uv0, uv1, uv2, uv3) = (uv3, uv0, uv1, uv2);
                    break;
            }
            mesh.TextureCoordinates.Add(uv0);
            mesh.TextureCoordinates.Add(uv1);
            mesh.TextureCoordinates.Add(uv2);
            mesh.TextureCoordinates.Add(uv3);

            // Two triangles, counter-clockwise when viewed from +Y so the top faces up.
            mesh.TriangleIndices.Add(baseIdx + 0);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx + 1);
            mesh.TriangleIndices.Add(baseIdx + 0);
            mesh.TriangleIndices.Add(baseIdx + 3);
            mesh.TriangleIndices.Add(baseIdx + 2);
        }

        // =====================================================================
        // FACETS (walls / roofs / etc.)
        // =====================================================================
        public Model3D? BuildFacets()
        {
            if (!MapDataService.Instance.IsLoaded) return null;

            BuildingArrays snap;
            try { snap = _buildings.ReadSnapshot(); }
            catch { return null; }
            if (snap.Facets == null || snap.Facets.Length == 0) return null;

            // Textured buckets: one mesh per unique (tileId, flip) combination.
            var texBuckets = new Dictionary<string, MeshGeometry3D>(StringComparer.Ordinal);
            var texMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);

            // Flat-color fallback buckets (roofs/floors, plus any wall whose tile couldn't be resolved).
            var flatMeshes = new Dictionary<FacetType, MeshGeometry3D>();
            var colors = new Dictionary<FacetType, Color>
            {
                [FacetType.Normal] = Color.FromRgb(210, 200, 180),
                [FacetType.Wall] = Color.FromRgb(200, 180, 150),
                [FacetType.Inside] = Color.FromRgb(180, 160, 140),
                [FacetType.OInside] = Color.FromRgb(180, 160, 140),
                [FacetType.Partition] = Color.FromRgb(170, 150, 140),
                [FacetType.Door] = Color.FromRgb(100, 70, 40),
                [FacetType.InsideDoor] = Color.FromRgb(110, 80, 50),
                [FacetType.OutsideDoor] = Color.FromRgb(90, 60, 30),
                [FacetType.Roof] = Color.FromRgb(120, 70, 70),
                [FacetType.RoofQuad] = Color.FromRgb(130, 80, 80),
                [FacetType.FloorPoints] = Color.FromRgb(100, 100, 100),
                [FacetType.Fence] = Color.FromRgb(160, 140, 100),
                [FacetType.FenceBrick] = Color.FromRgb(150, 120, 110),
                [FacetType.FenceFlat] = Color.FromRgb(160, 140, 100),
                [FacetType.Skylight] = Color.FromRgb(160, 200, 240),
                [FacetType.Staircase] = Color.FromRgb(160, 140, 110),
                [FacetType.FireEscape] = Color.FromRgb(120, 120, 140),
                [FacetType.Ladder] = Color.FromRgb(140, 110, 80),
                [FacetType.Cable] = Color.FromRgb(40, 40, 40),
                [FacetType.Trench] = Color.FromRgb(80, 70, 60),
                [FacetType.JustCollision] = Color.FromRgb(255, 0, 255),
                [FacetType.NormalFoundation] = Color.FromRgb(180, 170, 150),
            };
            Color defaultColor = Color.FromRgb(190, 190, 190);

            int tile = MapConstants.TileSize;
            int N = MapConstants.TilesPerSide;

            TryResolveVariantAndWorld(out string? variant, out int worldNum);
            bool canTexture = worldNum > 0 && !string.IsNullOrEmpty(variant);

            // Facets are stored 1-based; index 0 is a sentinel.
            for (int i = 0; i < snap.Facets.Length; i++)
            {
                var f = snap.Facets[i];
                if (f.Type == FacetType.Cable) continue; // skip cables in first pass

                // Convert facet byte endpoints to world XZ using the same flip as the 2D overlay.
                double x0 = (N - f.X0) * tile;
                double z0 = (N - f.Z0) * tile;
                double x1 = (N - f.X1) * tile;
                double z1 = (N - f.Z1) * tile;

                // Base Y from the stored endpoint Y. Y0/Y1 are in engine units
                // (1 quarter-storey = 64, so 1 storey = 256); convert to view-Y.
                double y0 = f.Y0 * EngineToViewY;
                double y1 = f.Y1 * EngineToViewY;

                // Buildings render flat: only fences follow terrain contours.
                // Without this, uneven terrain bakes different Y0/Y1 into each endpoint,
                // sloping roofs and wall bases across the terrain shape.
                if (!IsFenceFacetType(f.Type))
                    y1 = y0;

                // Wall vertical extent: Height byte is in quarter-storeys.
                double vertical = Math.Max(
    QuarterStoreyWorld / 4.0,
    (f.Height * (f.BlockHeight / 4.0) * QuarterStoreyWorld)/4);

                switch (f.Type)
                {
                    case FacetType.Roof:
                    case FacetType.RoofQuad:
                    case FacetType.FloorPoints:
                    case FacetType.Skylight:
                    case FacetType.Inside:
                    case FacetType.OInside:
                        {
                            if (!flatMeshes.TryGetValue(f.Type, out var mesh))
                            {
                                mesh = new MeshGeometry3D();
                                flatMeshes[f.Type] = mesh;
                            }
                            AddFacetFloorStrip(mesh, x0, z0, y0, x1, z1, y1);
                            break;
                        }

                    case FacetType.Ladder:
                        {
                            // Ladders use a procedural texture (rails + rungs) instead of
                            // the wall texture looked up from the style index.
                            const string ladderKey = "12|0|ladder|0|0";
                            if (!texBuckets.TryGetValue(ladderKey, out var ladderMesh))
                            {
                                ladderMesh = new MeshGeometry3D();
                                texBuckets[ladderKey] = ladderMesh;
                                var bmp = GetOrBuildLadderTile();
                                var brush = new ImageBrush(bmp)
                                {
                                    TileMode = TileMode.None,
                                    Stretch = Stretch.Fill
                                };
                                brush.Freeze();
                                texMaterials[ladderKey] = new DiffuseMaterial(brush);
                            }

                            // Offset the ladder geometry slightly along the wall's outward normal
                            // so it always renders in front of the wall behind it.
                            const double LadderBias = 3.0;
                            double wdx = x1 - x0, wdz = z1 - z0;
                            double wlen = Math.Sqrt(wdx * wdx + wdz * wdz);
                            double ox = wlen > 1e-6 ? ( wdz / wlen) * LadderBias : 0.0;
                            double oz = wlen > 1e-6 ? (-wdx / wlen) * LadderBias : 0.0;

                            // Split into one textured panel per storey (matches 2D preview tiling).
                            int ladderPanelsDown = Math.Max(1, f.Height / 4);
                            for (int row = 0; row < ladderPanelsDown; row++)
                            {
                                double fracA = (double)row / ladderPanelsDown;
                                double fracB = (double)(row + 1) / ladderPanelsDown;
                                AddTexturedWallPanel(
                                    ladderMesh,
                                    x0 + ox, z0 + oz, y0 + fracA * vertical,
                                    x1 + ox, z1 + oz, y1 + fracA * vertical,
                                    x0 + ox, z0 + oz, y0 + fracB * vertical,
                                    x1 + ox, z1 + oz, y1 + fracB * vertical);
                            }
                            break;
                        }

                    default:
                        {
                            bool textured = false;
                            if (canTexture)
                            {
                                textured = TryAddTexturedWall(
                                    f, snap, x0, z0, y0, x1, z1, y1, vertical,
                                    worldNum, variant!, texBuckets, texMaterials);
                            }
                            if (!textured)
                            {
                                if (!flatMeshes.TryGetValue(f.Type, out var mesh))
                                {
                                    mesh = new MeshGeometry3D();
                                    flatMeshes[f.Type] = mesh;
                                }
                                AddWallQuad(mesh, x0, z0, y0, x1, z1, y1, vertical);
                            }
                            break;
                        }
                }
            }

            var group = new Model3DGroup();

            // Textured opaque walls first.
            // Transparent ladder geometry is deferred until after all opaque draws so its
            // alpha-zero pixels don't write to the depth buffer before the wall behind renders.
            GeometryModel3D? ladderModel = null;
            foreach (var kv in texBuckets)
            {
                kv.Value.Freeze();
                var mat = texMaterials[kv.Key];

                // key format: "{facetType}|{world}|{variant}|{tileId}|{flip}"
                int pipe = kv.Key.IndexOf('|');
                int facetTypeInt = pipe > 0 ? int.Parse(kv.Key[..pipe]) : -1;
                var facetType = (FacetType)facetTypeInt;

                var model = new GeometryModel3D(kv.Value, mat);
                if (ShouldRenderBackface(facetType))
                    model.BackMaterial = mat;

                if (facetType == FacetType.Ladder)
                    ladderModel = model;   // added last, below
                else
                    group.Children.Add(model);
            }

            // Flat-color fallbacks.
            foreach (var kv in flatMeshes)
            {
                kv.Value.Freeze();
                Color c = colors.TryGetValue(kv.Key, out var cc) ? cc : defaultColor;
                var mat = new DiffuseMaterial(new SolidColorBrush(c));

                var model = new GeometryModel3D(kv.Value, mat);
                if (ShouldRenderBackface(kv.Key))
                {
                    var backMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(
                        (byte)(c.R * 0.7), (byte)(c.G * 0.7), (byte)(c.B * 0.7))));
                    model.BackMaterial = backMat;
                }

                group.Children.Add(model);
            }

            // Ladder last: transparent facets must follow all opaque geometry.
            if (ladderModel != null)
                group.Children.Add(ladderModel);

            group.Freeze();
            return group;
        }

        private static bool IsFenceFacetType(FacetType type)
        {
            return type == FacetType.Fence
                || type == FacetType.FenceBrick
                || type == FacetType.FenceFlat;
        }

        private static bool IsGateFacetType(FacetType type)
        {
            return type == FacetType.OutsideDoor;
        }

        /// <summary>True for types that need black/key-colour rendered as transparent.</summary>
        private static bool NeedsTransparency(FacetType type)
            => IsFenceFacetType(type) || IsGateFacetType(type);

        private static BitmapSource MakeFenceKeyColorsTransparent(BitmapSource source)
        {
            BitmapSource bmp = source;

            if (bmp.Format != PixelFormats.Bgra32)
            {
                bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
                bmp.Freeze();
            }

            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            bmp.CopyPixels(pixels, stride, 0);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i + 0];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];

                bool matchColor1 = (r == 34 && g == 51 && b == 34);
                bool matchColor2 = (r == 68 && g == 68 && b == 85);

                if (matchColor1 || matchColor2)
                {
                    pixels[i + 3] = 0;   // transparent
                }
                else
                {
                    pixels[i + 3] = 255; // opaque
                }
            }

            var result = BitmapSource.Create(
                width,
                height,
                bmp.DpiX,
                bmp.DpiY,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);

            result.Freeze();
            return result;
        }

        private static BitmapSource MakeDarkPixelsTransparent(
    BitmapSource source,
    byte threshold = 32)
        {
            BitmapSource bmp = source;

            if (bmp.Format != PixelFormats.Bgra32)
            {
                bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
                bmp.Freeze();
            }

            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            bmp.CopyPixels(pixels, stride, 0);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i + 0];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                byte a = pixels[i + 3];

                bool isDark =
                    r <= threshold &&
                    g <= threshold &&
                    b <= threshold;

                bool isKeyColor1 = (r == 34 && g == 51 && b == 34);
                bool isKeyColor2 = (r == 68 && g == 68 && b == 85);

                if (isDark || isKeyColor1 || isKeyColor2)
                {
                    pixels[i + 3] = 0; // transparent
                }
                else
                {
                    pixels[i + 3] = a == 0 ? (byte)255 : a;
                }
            }

            var result = BitmapSource.Create(
                width,
                height,
                bmp.DpiX,
                bmp.DpiY,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);

            result.Freeze();
            return result;
        }

        /// <summary>
        /// Split the wall into panelsAcross x panelsDown sub-quads. Resolve each cell's
        /// tile/flip using the same chain as the 2D FacetPreview (dstyles / paint_mem / DStorey),
        /// bucket by (tileId, flip) so each unique texture becomes one mesh + one material.
        /// Returns true if at least one sub-quad was emitted.
        /// </summary>
        private bool TryAddTexturedWall(
            DFacetRec f, BuildingArrays snap,
            double x0, double z0, double yBase0,
            double x1, double z1, double yBase1,
            double verticalExtent,
            int worldNumber, string variant,
            Dictionary<string, MeshGeometry3D> texBuckets,
            Dictionary<string, Material> texMaterials)
        {
            if (snap.Styles == null || snap.Styles.Length == 0) return false;

            int dxCells = Math.Abs(f.X1 - f.X0);
            int dzCells = Math.Abs(f.Z1 - f.Z0);
            int panelsAcross = Math.Max(dxCells, dzCells);
            if (panelsAcross <= 0) return false;

            // Fences always use a single panel stretched across the full height —
            // one texture covers the whole fence regardless of how many storeys tall it is.
            int panelsDown = IsFenceFacetType(f.Type)
                ? 1
                : Math.Max(1, f.Height / 4);

            int blockHeight = f.BlockHeight;
            if (blockHeight <= 0) blockHeight = 4;   // safe default
            if (blockHeight > 4) blockHeight = 4;

            bool twoTextured = (f.Flags & FacetFlags.TwoTextured) != 0;
            bool twoSided = (f.Flags & FacetFlags.TwoSided) != 0;
            bool isInside = (f.Flags & FacetFlags.Inside) != 0;
            bool readForward = twoSided || isInside;
            int step = twoTextured ? 2 : 1;
            int count = panelsAcross + 1;

            bool addedAny = false;

            for (int row = 0; row < panelsDown; row++)
            {
                int styleIdxForRow = f.StyleIndex + row * step;
                if (styleIdxForRow < 0 || styleIdxForRow >= snap.Styles.Length) continue;
                short dval = snap.Styles[styleIdxForRow];

                double yRow0 = row * verticalExtent / panelsDown;
                double yRow1 = (row + 1) * verticalExtent / panelsDown;

                for (int col = 0; col < panelsAcross; col++)
                {
                    int pos = readForward ? (panelsAcross - 1 - col) : col;
                    if (!TryResolveTileIdForCell(dval, pos, count, snap, out int tileId, out byte flip))
                        continue;
                    if (tileId < 0) continue;

                    int page = tileId / 64;
                    int idxInPage = tileId % 64;
                    byte tx = (byte)(idxInPage % 8);
                    byte ty = (byte)(idxInPage / 8);

                    string key = $"{(int)f.Type}|{worldNumber}|{variant}|{tileId}|{flip}";
                    if (!texBuckets.TryGetValue(key, out var bucketMesh))
                    {
                        if (!TextureResolver.TryResolve(page, tx, ty, flip, worldNumber, variant, out var bmp)
                            || bmp == null)
                        {
                            continue;
                        }
                        bucketMesh = new MeshGeometry3D();
                        texBuckets[key] = bucketMesh;
                        BitmapSource materialBmp = bmp;

                        if (NeedsTransparency(f.Type))
                        {
                            materialBmp = MakeDarkPixelsTransparent(bmp, 32);
                        }

                        var brush = new ImageBrush(materialBmp)
                        {
                            TileMode = TileMode.None,
                            Stretch = Stretch.Fill
                        };
                        brush.Freeze();
                        texMaterials[key] = new DiffuseMaterial(brush);
                    }

                    double tA = (double)col / panelsAcross;
                    double tB = (double)(col + 1) / panelsAcross;
                    double xA = x0 + (x1 - x0) * tA;
                    double xB = x0 + (x1 - x0) * tB;
                    double zA = z0 + (z1 - z0) * tA;
                    double zB = z0 + (z1 - z0) * tB;
                    double yBaseA = yBase0 + (yBase1 - yBase0) * tA;
                    double yBaseB = yBase0 + (yBase1 - yBase0) * tB;

                    // Only special-case shortened block heights.
                    // Normal/full-height facets keep the original UV logic.
                    if (blockHeight >= 1 && blockHeight < 4)
                    {
                        double visibleV = blockHeight / 4.0;

                        AddTexturedWallPanelCropped(
                            bucketMesh,
                            xA, zA, yBaseA + yRow0,
                            xB, zB, yBaseB + yRow0,
                            xA, zA, yBaseA + yRow1,
                            xB, zB, yBaseB + yRow1,
                            visibleV);
                    }
                    else
                    {
                        AddTexturedWallPanel(
                            bucketMesh,
                            xA, zA, yBaseA + yRow0,
                            xB, zB, yBaseB + yRow0,
                            xA, zA, yBaseA + yRow1,
                            xB, zB, yBaseB + yRow1);
                    }
                    addedAny = true;
                }
            }

            return addedAny;
        }

        /// <summary>Emit a single textured sub-panel (one wall cell) with UVs covering [0,1].</summary>
        private static void AddTexturedWallPanel(
            MeshGeometry3D mesh,
            double xA, double zA, double yBotA,
            double xB, double zB, double yBotB,
            double xAT, double zAT, double yTopA,
            double xBT, double zBT, double yTopB)
        {
            int baseIdx = mesh.Positions.Count;
            mesh.Positions.Add(new Point3D(xA, yBotA, zA));    // 0 bottom-A
            mesh.Positions.Add(new Point3D(xB, yBotB, zB));    // 1 bottom-B
            mesh.Positions.Add(new Point3D(xBT, yTopB, zBT));   // 2 top-B
            mesh.Positions.Add(new Point3D(xAT, yTopA, zAT));   // 3 top-A

            // Flip U horizontally so the texture is not mirrored.
            mesh.TextureCoordinates.Add(new Point(1, 1));
            mesh.TextureCoordinates.Add(new Point(0, 1));
            mesh.TextureCoordinates.Add(new Point(0, 0));
            mesh.TextureCoordinates.Add(new Point(1, 0));

            // Keep whichever triangle winding fixed the inside-out issue.
            mesh.TriangleIndices.Add(baseIdx + 0);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx + 1);
            mesh.TriangleIndices.Add(baseIdx + 0);
            mesh.TriangleIndices.Add(baseIdx + 3);
            mesh.TriangleIndices.Add(baseIdx + 2);
        }

        private static void AddTexturedWallPanelCropped(
    MeshGeometry3D mesh,
    double xA, double zA, double yBotA,
    double xB, double zB, double yBotB,
    double xAT, double zAT, double yTopA,
    double xBT, double zBT, double yTopB,
    double visibleV)
        {
            int baseIdx = mesh.Positions.Count;
            mesh.Positions.Add(new Point3D(xA, yBotA, zA));
            mesh.Positions.Add(new Point3D(xB, yBotB, zB));
            mesh.Positions.Add(new Point3D(xBT, yTopB, zBT));
            mesh.Positions.Add(new Point3D(xAT, yTopA, zAT));

            // Keep the SAME U handedness as your currently-correct wall textures.
            // Only crop the V range from the top.
            mesh.TextureCoordinates.Add(new Point(1, visibleV));
            mesh.TextureCoordinates.Add(new Point(0, visibleV));
            mesh.TextureCoordinates.Add(new Point(0, 0));
            mesh.TextureCoordinates.Add(new Point(1, 0));

            mesh.TriangleIndices.Add(baseIdx + 0);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx + 1);
            mesh.TriangleIndices.Add(baseIdx + 0);
            mesh.TriangleIndices.Add(baseIdx + 3);
            mesh.TriangleIndices.Add(baseIdx + 2);
        }

        // ---------------- Texture resolution helpers (mirror FacetPreviewWindow) ----------------

        private static bool TryResolveTileIdForCell(
            short dstyleValue, int pos, int count, BuildingArrays snap,
            out int tileId, out byte flip)
        {
            tileId = -1;
            flip = 0;

            if (dstyleValue >= 0)
                return ResolveRawTileId(dstyleValue, pos, count, out tileId, out flip);

            int storeyId = -dstyleValue;
            if (snap.Storeys == null || storeyId < 1 || storeyId >= snap.Storeys.Length) return false;

            var ds = snap.Storeys[storeyId];
            return ResolvePaintedTileId(ds, pos, count, snap.PaintMem, out tileId, out flip);
        }

        private static bool ResolveRawTileId(int rawStyleId, int pos, int count, out int tileId, out byte flip)
        {
            tileId = -1;
            flip = 0;

            var tma = StyleDataService.Instance.TmaSnapshot;
            if (tma == null) return false;

            int styleId = rawStyleId <= 0 ? 1 : rawStyleId;
            int idx = StyleDataService.MapRawStyleIdToTmaIndex(styleId);
            if (idx < 0 || idx >= tma.TextureStyles.Count) return false;

            var entries = tma.TextureStyles[idx].Entries;
            if (entries == null || entries.Count == 0) return false;

            int pieceIndex = pos == 0 ? 2 : (pos == count - 2 ? 0 : 1);
            if (pieceIndex >= entries.Count) pieceIndex = entries.Count - 1;

            var e = entries[pieceIndex];
            tileId = e.Page * 64 + e.Ty * 8 + e.Tx;
            flip = e.Flip;
            return true;
        }

        private static bool ResolvePaintedTileId(
            BuildingArrays.DStoreyRec ds, int pos, int count, byte[] paintMem,
            out int tileId, out byte flip)
        {
            tileId = -1;
            flip = 0;
            int baseStyle = ds.StyleIndex;

            if (paintMem == null || paintMem.Length == 0 || ds.Count == 0)
                return ResolveRawTileId(baseStyle, pos, count, out tileId, out flip);

            int paintStart = ds.PaintIndex;
            int paintCount = ds.Count;

            if (paintStart < 0 || paintStart + paintCount > paintMem.Length || pos >= paintCount)
                return ResolveRawTileId(baseStyle, pos, count, out tileId, out flip);

            byte raw = paintMem[paintStart + pos];
            flip = (byte)(((raw & 0x80) != 0) ? 1 : 0);
            int val = raw & 0x7F;

            if (val == 0)
                return ResolveRawTileId(baseStyle, pos, count, out tileId, out flip);

            tileId = val;
            return true;
        }

        private static bool TryResolveVariantAndWorld(out string? variant, out int world)
        {
            variant = null;
            world = 0;

            try
            {
                var app = Application.Current;
                var shell = app?.MainWindow?.DataContext;
                if (shell == null) return false;

                var mapProp = shell.GetType().GetProperty("Map");
                var map = mapProp?.GetValue(shell);
                if (map == null) return false;

                var mapType = map.GetType();
                var useBetaProp = mapType.GetProperty("UseBetaTextures");
                var worldProp = mapType.GetProperty("TextureWorld");

                if (useBetaProp?.GetValue(map) is bool useBeta &&
                    worldProp?.GetValue(map) is int w && w > 0)
                {
                    variant = useBeta ? "Beta" : "Release";
                    world = w;
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        private static void AddWallQuad(
            MeshGeometry3D mesh,
            double x0, double z0, double yBase0,
            double x1, double z1, double yBase1,
            double verticalExtent)
        {
            int baseIdx = mesh.Positions.Count;
            // Bottom: at yBase; Top: yBase + verticalExtent (scene Y is up).
            mesh.Positions.Add(new Point3D(x0, yBase0, z0));                    // 0 bottom-left
            mesh.Positions.Add(new Point3D(x1, yBase1, z1));                    // 1 bottom-right
            mesh.Positions.Add(new Point3D(x1, yBase1 + verticalExtent, z1));   // 2 top-right
            mesh.Positions.Add(new Point3D(x0, yBase0 + verticalExtent, z0));   // 3 top-left

            mesh.TextureCoordinates.Add(new Point(0, 1));
            mesh.TextureCoordinates.Add(new Point(1, 1));
            mesh.TextureCoordinates.Add(new Point(1, 0));
            mesh.TextureCoordinates.Add(new Point(0, 0));

            mesh.TriangleIndices.Add(baseIdx + 0);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx + 1);
            mesh.TriangleIndices.Add(baseIdx + 0);
            mesh.TriangleIndices.Add(baseIdx + 3);
            mesh.TriangleIndices.Add(baseIdx + 2);
        }

        private static void AddFacetFloorStrip(
            MeshGeometry3D mesh,
            double x0, double z0, double y0,
            double x1, double z1, double y1)
        {
            // Generate a thin strip: the facet's span + a small width perpendicular to it.
            double dx = x1 - x0, dz = z1 - z0;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-3) return;
            double nx = -dz / len;
            double nz = dx / len;
            const double halfWidth = 16.0;

            int baseIdx = mesh.Positions.Count;
            mesh.Positions.Add(new Point3D(x0 + nx * halfWidth, y0, z0 + nz * halfWidth));
            mesh.Positions.Add(new Point3D(x1 + nx * halfWidth, y1, z1 + nz * halfWidth));
            mesh.Positions.Add(new Point3D(x1 - nx * halfWidth, y1, z1 - nz * halfWidth));
            mesh.Positions.Add(new Point3D(x0 - nx * halfWidth, y0, z0 - nz * halfWidth));

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

        // =====================================================================
        // PRIMS (red spheres)
        // =====================================================================
        public Model3D? BuildPrims()
        {
            if (!MapDataService.Instance.IsLoaded) return null;

            PrimsAccessor.Snapshot snap;
            try { snap = _prims.ReadSnapshot(); }
            catch { return null; }
            if (snap.Prims == null || snap.Prims.Length == 0) return null;

            // Share one sphere mesh for all prims; place via one merged mesh (cheaper than per-prim visuals).
            var mesh = new MeshGeometry3D();
            const double radius = 18.0;
            const int stacks = 8;
            const int slices = 10;

            foreach (var p in snap.Prims)
            {
                if (p.MapWhoIndex < 0 || p.MapWhoIndex > 1023) continue;

                int gameCol = p.MapWhoIndex / 32;
                int gameRow = p.MapWhoIndex % 32;

                int uiRow = 31 - gameRow;
                int uiCol = 31 - gameCol;

                double cx = uiCol * MapConstants.MapWhoCellSize + (255 - p.X);
                double cz = uiRow * MapConstants.MapWhoCellSize + (255 - p.Z);

                // Prim Y: file value is in engine units (same convention as facet Y0/Y1).
                double cy = p.Y * EngineToViewY;

                AppendSphere(mesh, cx, cy, cz, radius, stacks, slices);
            }

            if (mesh.Positions.Count == 0) return null;
            mesh.Freeze();

            var mat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(230, 40, 40)));
            var group = new Model3DGroup();
            group.Children.Add(new GeometryModel3D(mesh, mat) { BackMaterial = mat });
            group.Freeze();
            return group;
        }

        // =====================================================================
        // CABLES
        // =====================================================================
        public Model3D? BuildCables()
        {
            if (!MapDataService.Instance.IsLoaded) return null;

            BuildingArrays snap;
            try { snap = _buildings.ReadSnapshot(); }
            catch { return null; }

            if (snap.Cables == null || snap.Cables.Length == 0) return null;

            var mesh = new MeshGeometry3D();
            const double HalfWidth = 2.0;

            foreach (var cable in snap.Cables)
            {
                int segments = Math.Max(1, cable.SegmentCount);

                // World endpoints: X/Z are tile*256, Y is raw engine units.
                double x1w = cable.WorldX1;
                double y1w = cable.WorldY1;
                double z1w = cable.WorldZ1;
                double x2w = cable.WorldX2;
                double y2w = cable.WorldY2;
                double z2w = cable.WorldZ2;

                double dx = (x2w - x1w) / segments;
                double dy = (y2w - y1w) / segments;
                double dz = (z2w - z1w) / segments;

                // Sag algorithm mirrors CableFacetPreviewWindow / cable_draw.
                int   angle   = -512;
                short dangle1 = cable.SagAngleDelta1;
                short dangle2 = cable.SagAngleDelta2;
                int   sagBase = cable.SagBase * 64;   // CableFacet.SagBase = FHeight (not yet *64)

                var points = new Point3D[segments + 1];
                double cx = x1w, cy = y1w, cz = z1w;

                for (int i = 0; i <= segments; i++)
                {
                    int    ang        = angle + 2048;
                    int    wrapped    = ang & 2047;
                    double rad        = wrapped * (2.0 * Math.PI / 2048.0);
                    double sagOffset  = Math.Cos(rad) * sagBase;
                    double saggedCy   = cy - sagOffset;

                    // Convert world coords → scene coords (same flip as facets).
                    // WorldX = tileX*256  →  sceneX = (128-tileX)*64 = 8192 - WorldX/4
                    points[i] = new Point3D(
                        8192.0 - cx / 4.0,
                        saggedCy * EngineToViewY,
                        8192.0 - cz / 4.0);

                    cx += dx;
                    cy += dy;
                    cz += dz;

                    angle += dangle1;
                    if (angle >= -30)
                        dangle1 = dangle2;
                }

                for (int i = 0; i < segments; i++)
                    AppendCableSegment(mesh, points[i], points[i + 1], HalfWidth);
            }

            if (mesh.Positions.Count == 0) return null;
            mesh.Freeze();

            var mat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(30, 30, 30)));
            var group = new Model3DGroup();
            group.Children.Add(new GeometryModel3D(mesh, mat) { BackMaterial = mat });
            group.Freeze();
            return group;
        }

        /// <summary>
        /// Emits a thin flat ribbon quad between two cable segment points, extruded
        /// perpendicular to the horizontal direction so it is visible from above.
        /// BackMaterial is set by the caller so it is visible from both sides.
        /// </summary>
        private static void AppendCableSegment(MeshGeometry3D mesh, Point3D a, Point3D b, double halfWidth)
        {
            double ddx = b.X - a.X;
            double ddz = b.Z - a.Z;
            double len = Math.Sqrt(ddx * ddx + ddz * ddz);

            double px = len > 1e-3 ? (-ddz / len) * halfWidth : halfWidth;
            double pz = len > 1e-3 ? ( ddx / len) * halfWidth : 0.0;

            int baseIdx = mesh.Positions.Count;
            mesh.Positions.Add(new Point3D(a.X + px, a.Y, a.Z + pz));  // 0
            mesh.Positions.Add(new Point3D(a.X - px, a.Y, a.Z - pz));  // 1
            mesh.Positions.Add(new Point3D(b.X - px, b.Y, b.Z - pz));  // 2
            mesh.Positions.Add(new Point3D(b.X + px, b.Y, b.Z + pz));  // 3

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

        private static void AppendSphere(
            MeshGeometry3D mesh,
            double cx, double cy, double cz,
            double r, int stacks, int slices)
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


        private static bool ShouldRenderBackface(FacetType type)
        {
            return type == FacetType.Fence
                || type == FacetType.FenceBrick
                || type == FacetType.FenceFlat
                || type == FacetType.Door
                || type == FacetType.InsideDoor
                || type == FacetType.OutsideDoor
                || type == FacetType.Ladder;
        }

        /// <summary>
        /// Generates (or returns the cached) 64×64 procedural ladder tile.
        /// The graphic is 67% of the tile width (centred), matching the 2D FacetPreviewWindow
        /// WidthScale.  Background is fully transparent so the wall behind shows through.
        /// </summary>
        private static BitmapSource GetOrBuildLadderTile()
        {
            if (_ladderTile != null) return _ladderTile;

            const int W = 64;
            const int H = 64;
            const int RailWidth = 4;
            const int RungHeight = 4;
            const int RunsPerPanel = 4;
            const double WidthScale = 0.67;

            // Ladder graphic occupies WidthScale of the tile width, centred.
            int ladderW = (int)(W * WidthScale); // ~43 px
            int xOffset = (W - ladderW) / 2;     // ~10 px on each side

            int stride = W * 4; // BGRA32
            // byte[] default-initialises to 0 → transparent black everywhere.
            byte[] pixels = new byte[H * stride];

            // White vertical rails within the scaled-width zone.
            for (int y = 0; y < H; y++)
            {
                for (int x = xOffset; x < xOffset + RailWidth; x++)
                {
                    int idx = y * stride + x * 4;
                    pixels[idx + 0] = 255; pixels[idx + 1] = 255; pixels[idx + 2] = 255; pixels[idx + 3] = 255;
                }
                for (int x = xOffset + ladderW - RailWidth; x < xOffset + ladderW; x++)
                {
                    int idx = y * stride + x * 4;
                    pixels[idx + 0] = 255; pixels[idx + 1] = 255; pixels[idx + 2] = 255; pixels[idx + 3] = 255;
                }
            }

            // White horizontal rungs, evenly spaced, spanning only the ladder width.
            for (int r = 0; r < RunsPerPanel; r++)
            {
                int rungCenterY = (int)((r + 0.5) * H / RunsPerPanel);
                int rungTop = rungCenterY - RungHeight / 2;
                for (int y = rungTop; y < rungTop + RungHeight; y++)
                {
                    if (y < 0 || y >= H) continue;
                    for (int x = xOffset; x < xOffset + ladderW; x++)
                    {
                        int idx = y * stride + x * 4;
                        pixels[idx + 0] = 255; pixels[idx + 1] = 255; pixels[idx + 2] = 255; pixels[idx + 3] = 255;
                    }
                }
            }

            _ladderTile = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            _ladderTile.Freeze();
            return _ladderTile;
        }

        private void AppendRoofFace4s(
            Model3DGroup group,
            BuildingArrays snap,
            int worldNum,
            string? variant,
            bool canTexture)
        {
            if (snap.Walkables == null || snap.Walkables.Length <= 1) return;
            if (snap.RoofFaces4 == null || snap.RoofFaces4.Length <= 1) return;

            var cache = TextureCacheService.Instance;

            var texMeshes = new Dictionary<string, MeshGeometry3D>(StringComparer.Ordinal);
            var texMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);
            MeshGeometry3D? fallbackMesh = null;

            // DY is stored as a signed byte, ROOF_SHIFT = 3, so DY * 8 = engine height delta.
            // Scale by EngineToViewY to convert to view units: DY * 8 * 0.25 = DY * 2.0.
            const double DyToView = 8.0 * EngineToViewY;

            for (int w = 1; w < snap.Walkables.Length; w++)
            {
                var walk = snap.Walkables[w];
                if (walk.EndFace4 <= walk.StartFace4) continue;

                bool isWarehouse = false;
                int buildingId1 = walk.Building;

                if (buildingId1 >= 1 &&
                    snap.Buildings != null &&
                    buildingId1 <= snap.Buildings.Length)
                {
                    var bld = snap.Buildings[buildingId1 - 1];
                    isWarehouse = (BuildingType)bld.Type == BuildingType.Warehouse;
                }

                for (int i = walk.StartFace4; i < walk.EndFace4 && i < snap.RoofFaces4.Length; i++)
                {
                    if (i < 1) continue;

                    var rf4 = snap.RoofFaces4[i];

                    // Mask off high bits — they are flags, not coordinate bits.
                    int cellX = rf4.RX & 0x7F;
                    int cellZ = rf4.RZ & 0x7F;

                    if (cellX > 127 || cellZ > 127) continue;

                    // Convert game cell coords to WPF tile coords using the same
                    // axis-flip applied by FileIndexForTile in TexturesAccessor.
                    int wpfTx = 127 - cellX;
                    int wpfTy = 127 - cellZ;

                    // Scene-space quad corners: same transform as BuildTerrain.
                    double x0 = wpfTx * 64.0;
                    double z0 = wpfTy * 64.0;
                    double x1 = x0 + 64.0;
                    double z1 = z0 + 64.0;

                    // Corner heights per spec (ROOF_SHIFT = 3):
                    //   NW = Y
                    //   NE = Y + DY0 * 8
                    //   SW = Y + DY2 * 8
                    //   SE = Y + DY1 * 8
                    // Then swapped 180° (NW↔SE, NE↔SW) to correct the rendered orientation.
                    double baseY = rf4.Y * EngineToViewY;
                    double yNW = baseY + rf4.DY1 * DyToView;   // spec SE → rendered NW
                    double yNE = baseY + rf4.DY2 * DyToView;   // spec SW → rendered NE
                    double ySW = baseY + rf4.DY0 * DyToView;   // spec NE → rendered SW
                    double ySE = baseY;                          // spec NW → rendered SE

                    // RX high bit: diagonal split direction only — no effect on heights.
                    bool diagNWSE = (rf4.RX & 0x80) != 0;

                    if (canTexture)
                    {
                        if (isWarehouse)
                        {
                            // Warehouse RF4: use tex000 from the world texture set.
                            string meshKey = $"rf4|ware|tex000|diag={(diagNWSE ? 1 : 0)}";

                            if (!texMeshes.TryGetValue(meshKey, out var mesh))
                            {
                                if (!TextureResolver.TryResolve(0, 0, 0, 0, worldNum, variant!, out var bmp) || bmp == null)
                                {
                                    fallbackMesh ??= new MeshGeometry3D();
                                    AddRf4Quad(fallbackMesh, x0, z0, x1, z1, yNW, yNE, ySE, ySW, 0, diagNWSE);
                                    continue;
                                }

                                mesh = new MeshGeometry3D();
                                texMeshes[meshKey] = mesh;
                                var brush = new ImageBrush(bmp) { TileMode = TileMode.None, Stretch = Stretch.Fill };
                                brush.Freeze();
                                texMaterials[meshKey] = new DiffuseMaterial(brush);
                            }

                            AddRf4Quad(texMeshes[meshKey], x0, z0, x1, z1, yNW, yNE, ySE, ySW, 0, diagNWSE);
                        }
                        else
                        {
                            // Non-warehouse RF4: sample the normal floor/map texture that sits
                            // on this cell — the RF4 tile must visually match the floor beneath it.
                            string texKey;
                            int texRot;
                            try { (texKey, texRot) = _textures.GetTileTextureKeyAndRotation(wpfTx, wpfTy); }
                            catch { texKey = string.Empty; texRot = 0; }

                            if (!string.IsNullOrEmpty(texKey) && cache.TryGetRelative(texKey, out var bmp) && bmp != null)
                            {
                                // BuildTerrain passes (texRot + 180) % 360 to AddTileQuad.
                                // AddRf4Quad uses a different rotation-zero convention, so to
                                // produce the same visible UV result we pass (360 - texRot) % 360.
                                int rf4Rot = (360 - texRot) % 360;
                                string meshKey = $"rf4|floor|{texKey}|rot={rf4Rot}|diag={(diagNWSE ? 1 : 0)}";

                                if (!texMeshes.TryGetValue(meshKey, out var mesh))
                                {
                                    mesh = new MeshGeometry3D();
                                    texMeshes[meshKey] = mesh;
                                    var brush = new ImageBrush(bmp) { TileMode = TileMode.None, Stretch = Stretch.Fill };
                                    brush.Freeze();
                                    texMaterials[meshKey] = new DiffuseMaterial(brush);
                                }

                                AddRf4Quad(mesh, x0, z0, x1, z1, yNW, yNE, ySE, ySW, rf4Rot, diagNWSE);
                            }
                            else
                            {
                                // Floor texture unavailable — neutral grey so tile is still visible.
                                fallbackMesh ??= new MeshGeometry3D();
                                AddRf4Quad(fallbackMesh, x0, z0, x1, z1, yNW, yNE, ySE, ySW, 0, diagNWSE);
                            }
                        }
                    }
                    else
                    {
                        fallbackMesh ??= new MeshGeometry3D();
                        AddRf4Quad(fallbackMesh, x0, z0, x1, z1, yNW, yNE, ySE, ySW, 0, diagNWSE);
                    }
                }
            }

            foreach (var kv in texMeshes)
            {
                kv.Value.Freeze();
                var mat = texMaterials[kv.Key];
                group.Children.Add(new GeometryModel3D(kv.Value, mat) { BackMaterial = mat });
            }

            if (fallbackMesh != null)
            {
                fallbackMesh.Freeze();
                var mat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(140, 140, 140)));
                group.Children.Add(new GeometryModel3D(fallbackMesh, mat) { BackMaterial = mat });
            }
        }
        private static void AddRf4Quad(
    MeshGeometry3D mesh,
    double x0, double z0, double x1, double z1,
    double yNW, double yNE, double ySE, double ySW,
    int rotationDeg,
    bool diagNWSE)
        {
            int baseIdx = mesh.Positions.Count;

            // Corner order:
            // 0 = NW, 1 = NE, 2 = SE, 3 = SW
            mesh.Positions.Add(new Point3D(x0, yNW, z0));
            mesh.Positions.Add(new Point3D(x1, yNE, z0));
            mesh.Positions.Add(new Point3D(x1, ySE, z1));
            mesh.Positions.Add(new Point3D(x0, ySW, z1));

            Point uv0 = new(0, 0), uv1 = new(1, 0), uv2 = new(1, 1), uv3 = new(0, 1);
            switch (rotationDeg % 360)
            {
                case 0: break;
                case 90: (uv0, uv1, uv2, uv3) = (uv1, uv2, uv3, uv0); break;
                case 180: (uv0, uv1, uv2, uv3) = (uv2, uv3, uv0, uv1); break;
                case 270: (uv0, uv1, uv2, uv3) = (uv3, uv0, uv1, uv2); break;
            }

            mesh.TextureCoordinates.Add(uv0);
            mesh.TextureCoordinates.Add(uv1);
            mesh.TextureCoordinates.Add(uv2);
            mesh.TextureCoordinates.Add(uv3);

            // RX high bit:
            // set   => NW-SE diagonal
            // clear => SW-NE diagonal
            if (diagNWSE)
            {
                // diagonal 0 -> 2
                mesh.TriangleIndices.Add(baseIdx + 0);
                mesh.TriangleIndices.Add(baseIdx + 2);
                mesh.TriangleIndices.Add(baseIdx + 1);

                mesh.TriangleIndices.Add(baseIdx + 0);
                mesh.TriangleIndices.Add(baseIdx + 3);
                mesh.TriangleIndices.Add(baseIdx + 2);
            }
            else
            {
                // diagonal 3 -> 1
                mesh.TriangleIndices.Add(baseIdx + 0);
                mesh.TriangleIndices.Add(baseIdx + 3);
                mesh.TriangleIndices.Add(baseIdx + 1);

                mesh.TriangleIndices.Add(baseIdx + 1);
                mesh.TriangleIndices.Add(baseIdx + 3);
                mesh.TriangleIndices.Add(baseIdx + 2);
            }
        }
        public Model3D? BuildRoofTiles()
        {
            if (!MapDataService.Instance.IsLoaded) return null;

            BuildingArrays snap;
            try { snap = _buildings.ReadSnapshot(); }
            catch { return null; }

            TryResolveVariantAndWorld(out string? variant, out int worldNum);
            bool canTexture = worldNum > 0 && !string.IsNullOrEmpty(variant);

            var group = new Model3DGroup();
            AppendRoofFace4s(group, snap, worldNum, variant, canTexture);

            if (group.Children.Count == 0)
                return null;

            group.Freeze();
            return group;
        }
    }

}