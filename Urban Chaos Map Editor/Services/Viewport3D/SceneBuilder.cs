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
            // Corner (cx, cz) is at tile boundary; its height = height of tile (cx, cz)
            // clamped to valid tile range.
            double[,] cornerY = new double[N + 1, N + 1];
            for (int cz = 0; cz <= N; cz++)
            {
                for (int cx = 0; cx <= N; cx++)
                {
                    int tx = Math.Min(cx, N - 1);
                    int ty = Math.Min(cz, N - 1);
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
                        int engineY = _altitudes.ReadAltRaw(tx, ty) << AltitudeAccessor.PAP_ALT_SHIFT;
                        cellAltWorld = engineY * EngineToViewY;
                    }
                    catch { cellAltWorld = 0.0; }

                    // Quad corners in 2D-canvas XZ.
                    double x0 = tx * tile;
                    double x1 = x0 + tile;
                    double z0 = ty * tile;
                    double z1 = z0 + tile;

                    double y00 = cornerY[tx, ty]     + cellAltWorld;
                    double y10 = cornerY[tx + 1, ty] + cellAltWorld;
                    double y01 = cornerY[tx, ty + 1] + cellAltWorld;
                    double y11 = cornerY[tx + 1, ty + 1] + cellAltWorld;

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

                    AddTileQuad(mesh, x0, z0, x1, z1, y00, y10, y01, y11, rot);
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
                case 0:   break;
                case 90:  (uv0, uv1, uv2, uv3) = (uv1, uv2, uv3, uv0); break;
                case 180: (uv0, uv1, uv2, uv3) = (uv2, uv3, uv0, uv1); break;
                case 270: (uv0, uv1, uv2, uv3) = (uv3, uv0, uv1, uv2); break;
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
                [FacetType.Normal]        = Color.FromRgb(210, 200, 180),
                [FacetType.Wall]          = Color.FromRgb(200, 180, 150),
                [FacetType.Inside]        = Color.FromRgb(180, 160, 140),
                [FacetType.OInside]       = Color.FromRgb(180, 160, 140),
                [FacetType.Partition]     = Color.FromRgb(170, 150, 140),
                [FacetType.Door]          = Color.FromRgb(100,  70,  40),
                [FacetType.InsideDoor]    = Color.FromRgb(110,  80,  50),
                [FacetType.OutsideDoor]   = Color.FromRgb( 90,  60,  30),
                [FacetType.Roof]          = Color.FromRgb(120,  70,  70),
                [FacetType.RoofQuad]      = Color.FromRgb(130,  80,  80),
                [FacetType.FloorPoints]   = Color.FromRgb(100, 100, 100),
                [FacetType.Fence]         = Color.FromRgb(160, 140, 100),
                [FacetType.FenceBrick]    = Color.FromRgb(150, 120, 110),
                [FacetType.FenceFlat]     = Color.FromRgb(160, 140, 100),
                [FacetType.Skylight]      = Color.FromRgb(160, 200, 240),
                [FacetType.Staircase]     = Color.FromRgb(160, 140, 110),
                [FacetType.FireEscape]    = Color.FromRgb(120, 120, 140),
                [FacetType.Ladder]        = Color.FromRgb(140, 110,  80),
                [FacetType.Cable]         = Color.FromRgb( 40,  40,  40),
                [FacetType.Trench]        = Color.FromRgb( 80,  70,  60),
                [FacetType.JustCollision] = Color.FromRgb(255,   0, 255),
                [FacetType.NormalFoundation] = Color.FromRgb(180, 170, 150),
            };
            Color defaultColor = Color.FromRgb(190, 190, 190);

            int tile = MapConstants.TileSize;
            int N = MapConstants.TilesPerSide;

            TryResolveVariantAndWorld(out string? variant, out int worldNum);
            bool canTexture = worldNum > 0 && !string.IsNullOrEmpty(variant);

            // Facets are stored 1-based; index 0 is a sentinel.
            for (int i = 1; i < snap.Facets.Length; i++)
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

                // Wall vertical extent: Height byte is in quarter-storeys.
                double vertical = Math.Max(QuarterStoreyWorld, f.Height * QuarterStoreyWorld);

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

            // Textured walls first.
            foreach (var kv in texBuckets)
            {
                kv.Value.Freeze();
                var mat = texMaterials[kv.Key];
                group.Children.Add(new GeometryModel3D(kv.Value, mat) { BackMaterial = mat });
            }

            // Flat-color fallbacks.
            foreach (var kv in flatMeshes)
            {
                kv.Value.Freeze();
                Color c = colors.TryGetValue(kv.Key, out var cc) ? cc : defaultColor;
                var mat = new DiffuseMaterial(new SolidColorBrush(c));
                var backMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(
                    (byte)(c.R * 0.7), (byte)(c.G * 0.7), (byte)(c.B * 0.7))));
                group.Children.Add(new GeometryModel3D(kv.Value, mat) { BackMaterial = backMat });
            }

            group.Freeze();
            return group;
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

            int panelsDown = Math.Max(1, f.Height / 4);

            bool twoTextured = (f.Flags & FacetFlags.TwoTextured) != 0;
            bool twoSided    = (f.Flags & FacetFlags.TwoSided) != 0;
            bool isInside    = (f.Flags & FacetFlags.Inside) != 0;
            bool readForward = twoSided || isInside;
            int step = twoTextured ? 2 : 1;
            int count = panelsAcross + 1;

            bool addedAny = false;

            for (int row = 0; row < panelsDown; row++)
            {
                int styleIdxForRow = f.StyleIndex + row * step;
                if (styleIdxForRow < 0 || styleIdxForRow >= snap.Styles.Length) continue;
                short dval = snap.Styles[styleIdxForRow];

                double yRow0 = row       * verticalExtent / panelsDown;
                double yRow1 = (row + 1) * verticalExtent / panelsDown;

                for (int col = 0; col < panelsAcross; col++)
                {
                    int pos = readForward ? col : (panelsAcross - 1 - col);
                    if (!TryResolveTileIdForCell(dval, pos, count, snap, out int tileId, out byte flip))
                        continue;
                    if (tileId < 0) continue;

                    int page = tileId / 64;
                    int idxInPage = tileId % 64;
                    byte tx = (byte)(idxInPage % 8);
                    byte ty = (byte)(idxInPage / 8);

                    string key = $"{worldNumber}|{variant}|{tileId}|{flip}";
                    if (!texBuckets.TryGetValue(key, out var bucketMesh))
                    {
                        if (!TextureResolver.TryResolve(page, tx, ty, flip, worldNumber, variant, out var bmp)
                            || bmp == null)
                        {
                            continue;
                        }
                        bucketMesh = new MeshGeometry3D();
                        texBuckets[key] = bucketMesh;
                        var brush = new ImageBrush(bmp)
                        {
                            TileMode = TileMode.None,
                            Stretch = Stretch.Fill
                        };
                        brush.Freeze();
                        texMaterials[key] = new DiffuseMaterial(brush);
                    }

                    double tA = (double)col       / panelsAcross;
                    double tB = (double)(col + 1) / panelsAcross;
                    double xA = x0 + (x1 - x0) * tA;
                    double xB = x0 + (x1 - x0) * tB;
                    double zA = z0 + (z1 - z0) * tA;
                    double zB = z0 + (z1 - z0) * tB;
                    double yBaseA = yBase0 + (yBase1 - yBase0) * tA;
                    double yBaseB = yBase0 + (yBase1 - yBase0) * tB;

                    AddTexturedWallPanel(bucketMesh,
                        xA, zA, yBaseA + yRow0,
                        xB, zB, yBaseB + yRow0,
                        xA, zA, yBaseA + yRow1,
                        xB, zB, yBaseB + yRow1);
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
            mesh.Positions.Add(new Point3D(xA,  yBotA, zA));    // 0 bottom-A
            mesh.Positions.Add(new Point3D(xB,  yBotB, zB));    // 1 bottom-B
            mesh.Positions.Add(new Point3D(xBT, yTopB, zBT));   // 2 top-B
            mesh.Positions.Add(new Point3D(xAT, yTopA, zAT));   // 3 top-A

            mesh.TextureCoordinates.Add(new Point(0, 1));
            mesh.TextureCoordinates.Add(new Point(1, 1));
            mesh.TextureCoordinates.Add(new Point(1, 0));
            mesh.TextureCoordinates.Add(new Point(0, 0));

            mesh.TriangleIndices.Add(baseIdx + 0);
            mesh.TriangleIndices.Add(baseIdx + 1);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx + 0);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx + 3);
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
            mesh.TriangleIndices.Add(baseIdx + 1);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx + 0);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx + 3);
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

                int row = p.MapWhoIndex / 32;
                int col = p.MapWhoIndex % 32;

                // MapWho cells are 4x4 tiles = 256 px.  X/Z bytes (0..255) index within the cell.
                double cx = col * MapConstants.MapWhoCellSize + p.X;
                double cz = row * MapConstants.MapWhoCellSize + p.Z;

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
    }
}
