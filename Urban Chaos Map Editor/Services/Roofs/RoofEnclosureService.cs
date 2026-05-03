// /Services/Roofs/RoofEnclosureService.cs
// Detects when wall facets form a closed polygon and automatically sets PAP_HI roof flags
// AND creates a walkable region covering the enclosed area.
using System.Diagnostics;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Textures;

namespace UrbanChaosMapEditor.Services.Roofs
{
    public static class RoofEnclosureService
    {
        // Fixed floor texture used when filling a warehouse's enclosed interior.
        // Warehouses texture their roofs separately, so the PAP tiles inside a warehouse
        // enclosure should be a floor texture rather than the classified roof pattern.
        private const int WarehouseFloorTextureIndex = 287;

        // ── Facet classification helpers ─────────────────────────────────────────
        //
        // Exterior polygon detection must include Doors (Type=Door, OutsideDoor) so that
        // a wall polygon with a 1-tile gap closed by a door still registers as enclosed.
        // Doors are NOT exposed in the polygon list / bulk facet editor though — only
        // the Normal/Wall/NormalFoundation walls are editable there.
        //
        // Interior walls (FacetFlags.Inside set, or FacetType.Inside/OInside) are paired
        // with their mirrored exterior wall by matching coordinates, and are included in
        // the same polygon group so bulk height edits affect both at once.

        private static bool IsExteriorPolygonEdge(DFacetRec f) =>
            (f.Type == FacetType.Normal || f.Type == FacetType.Wall ||
             f.Type == FacetType.NormalFoundation ||
             f.Type == FacetType.Door || f.Type == FacetType.OutsideDoor)
            && (f.Flags & FacetFlags.Inside) == 0;

        private static bool IsExteriorWallForList(DFacetRec f) =>
            (f.Type == FacetType.Normal || f.Type == FacetType.Wall ||
             f.Type == FacetType.NormalFoundation)
            && (f.Flags & FacetFlags.Inside) == 0;

        private static bool IsInteriorWallForList(DFacetRec f)
        {
            // Consider it an interior wall if EITHER the Inside flag is set on a wall
            // type, OR the FacetType itself is one of the explicit inside wall types.
            bool hasInsideFlag = (f.Flags & FacetFlags.Inside) != 0;
            bool isWallType = f.Type == FacetType.Normal ||
                              f.Type == FacetType.Wall ||
                              f.Type == FacetType.NormalFoundation;
            bool isInsideType = f.Type == FacetType.Inside ||
                                f.Type == FacetType.OInside;
            return (hasInsideFlag && (isWallType || isInsideType)) || isInsideType;
        }


        /// <summary>
        /// Returns true if the building's exterior wall facets form a closed polygon
        /// at any height level. Pure read — no side effects.
        /// Used to auto-determine HugFloor when toggling 2SIDED on a facet.
        /// </summary>
        public static bool IsClosedPolygon(int buildingId1)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded) return false;

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
                return false;

            var building = snap.Buildings[buildingId1 - 1];
            var edgesByLevel = new Dictionary<(int height, int blockHeight, int y0), List<Edge>>();

            for (int fIdx = building.StartFacet; fIdx < building.EndFacet && fIdx <= snap.Facets.Length; fIdx++)
            {
                if (fIdx < 1) continue;
                var facet = snap.Facets[fIdx - 1];

                if (!IsExteriorPolygonEdge(facet)) continue;
                if (facet.Building != buildingId1) continue;

                int bh = facet.BlockHeight > 0 ? facet.BlockHeight : 16;
                var key = (height: (int)facet.Height, blockHeight: bh, y0: (int)facet.Y0);
                if (!edgesByLevel.TryGetValue(key, out var bucket))
                    edgesByLevel[key] = bucket = new List<Edge>();

                bucket.Add(new Edge(facet.X0, facet.Z0, facet.X1, facet.Z1));
            }

            if (edgesByLevel.Count == 0) return false;

            foreach (var edges in edgesByLevel.Values)
            {
                var components = SplitIntoConnectedComponents(edges);
                foreach (var component in components)
                {
                    if (TryFindClosedPolygon(component, out _))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a building's wall facets form one or more closed polygons.
        /// If so, sets PAP_HI altitude/Hidden flags AND creates walkable region(s).
        /// Call this after adding a wall facet to a building.
        /// </summary>
        public static bool CheckAndApplyRoofEnclosure(
            int buildingId1,
            bool applyRoofTextures = true,
            bool createWalkables = true,
            bool forceRecalculate = false)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
                return false;

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
                return false;

            var building = snap.Buildings[buildingId1 - 1];

            // Collect exterior Normal + Door facets grouped by (height, blockHeight, y0).
            // BlockHeight scales the per-band world height, so it must be part of the key —
            // walls with the same Height/Y0 but different BlockHeight sit at different altitudes.
            var facetsByHeight = new Dictionary<(int height, int blockHeight, int y0), List<Edge>>();

            for (int fIdx = building.StartFacet; fIdx < building.EndFacet && fIdx <= snap.Facets.Length; fIdx++)
            {
                if (fIdx < 1) continue;
                var facet = snap.Facets[fIdx - 1];

                if (!IsExteriorPolygonEdge(facet)) continue;
                if (facet.Building != buildingId1) continue;

                int bh = facet.BlockHeight > 0 ? facet.BlockHeight : 16;
                var key = (height: (int)facet.Height, blockHeight: bh, y0: (int)facet.Y0);
                if (!facetsByHeight.TryGetValue(key, out var bucket))
                    facetsByHeight[key] = bucket = new List<Edge>();

                bucket.Add(new Edge(facet.X0, facet.Z0, facet.X1, facet.Z1));

                Debug.WriteLine($"[RoofEnclosureService] Facet: ({facet.X0},{facet.Z0})->({facet.X1},{facet.Z1}) Height={facet.Height} BlockHeight={bh} Y0={facet.Y0} Type={facet.Type}");
            }

            Debug.WriteLine($"[RoofEnclosureService] Building #{buildingId1}: {facetsByHeight.Count} height+blockHeight+y0 level(s)");

            var altAcc = new AltitudeAccessor(svc);
            var texAcc = new TexturesAccessor(svc);
            int currentWorld = texAcc.ReadTextureWorld();

            bool anyApplied = false;
            bool anyAltitudeChanged = false;
            bool anyTextureChanged = false;
            bool anyWalkableChanged = false;

            var changedWpfTiles = new List<(int wpfTx, int wpfTy)>();
            var claimedInteriorTiles = new HashSet<(int tx, int ty)>();

            // Try each level highest effective top first.
            // worldAlt = height × blockHeight × 4 + y0  (rawAlt = worldAlt >> PAP_ALT_SHIFT)
            // H=4, BH=16, Y0=0 → worldAlt=256 → rawAlt=32 (1 standard storey)
            foreach (var (height, blockHeight, y0) in facetsByHeight.Keys.OrderByDescending(k => k.height * k.blockHeight * 4 + k.y0))
            {
                var edges = facetsByHeight[(height, blockHeight, y0)];
                Debug.WriteLine($"[RoofEnclosureService] Trying height={height} blockHeight={blockHeight} y0={y0} with {edges.Count} edges");

                var components = SplitIntoConnectedComponents(edges);
                Debug.WriteLine($"[RoofEnclosureService] Height={height} split into {components.Count} connected component(s)");

                foreach (var component in components)
                {
                    Debug.WriteLine($"[RoofEnclosureService] Trying component with {component.Count} edges");

                    if (component.Count < 4)
                        continue;

                    if (!TryFindClosedPolygon(component, out var polygon))
                    {
                        Debug.WriteLine($"[RoofEnclosureService] Component is not a closed polygon");
                        continue;
                    }

                    Debug.WriteLine($"[RoofEnclosureService] Closed polygon found at height={height} with {polygon.Count} vertices");
                    foreach (var (x, z) in polygon)
                        Debug.WriteLine($"[RoofEnclosureService]   Vertex: ({x}, {z})");

                    // Compute enclosure bounds early so we can use them as a persistent identity for the polygon.
                    byte minX = (byte)polygon.Min(p => p.x);
                    byte maxX = (byte)polygon.Max(p => p.x);
                    byte minZ = (byte)polygon.Min(p => p.z);
                    byte maxZ = (byte)polygon.Max(p => p.z);

                    // rawAlt = (height × blockHeight × 4 + y0) >> PAP_ALT_SHIFT
                    // Each BlockHeight unit = 4 world pixels ("4 px each" tooltip).
                    // 1 standard storey (H=4, BH=16): (4×16×4 + 0) >> 3 = 256 >> 3 = 32 raw.
                    // Y0 is the wall's base world Y, already in game pixel units.
                    int worldAltitude = height * blockHeight * 4 + y0;
                    byte newStoredY = (byte)Math.Clamp(worldAltitude >> 5, 0, 255);

                    bool isWarehouseBuilding = (building.Type == (byte)BuildingType.Warehouse);

                    // Decide whether to skip / replace an existing walkable over this footprint.
                    //   - forceRecalculate (bulk facet height edit): always update to new Y
                    //   - Existing is same altitude: already applied, nothing new to do
                    //   - Existing is LOWER: a higher polygon now encloses the same footprint,
                    //     replace — update walkable Y and every RF4 Y inside it
                    //   - Existing is HIGHER: keep existing, this is an older lower polygon
                    //   - No existing walkable: create new
                    bool existingFound = TryFindExistingWalkable(svc, buildingId1, minX, minZ, maxX, maxZ,
                        out int existingWalkableId1, out byte existingStoredY);

                    bool replaceExistingWithHigher = existingFound && newStoredY > existingStoredY;
                    bool wantUpdateExisting = existingFound && (forceRecalculate || replaceExistingWithHigher);

                    if (existingFound && !wantUpdateExisting)
                    {
                        Debug.WriteLine($"[RoofEnclosureService] Polygon already processed for Building #{buildingId1}: " +
                            $"({minX},{minZ})->({maxX},{maxZ}) existingY={existingStoredY} newY={newStoredY}; reapplying roof tiles/textures");
                    }

                    var interiorTiles = GetInteriorTiles(polygon);
                    if (interiorTiles.Count == 0)
                    {
                        Debug.WriteLine($"[RoofEnclosureService] No interior tiles found for this polygon");
                        continue;
                    }

                    // Fast lookup set for smart texture classification (built from full polygon extent).
                    var interiorTileSet = new HashSet<(int, int)>(interiorTiles);
                    bool oneWide = IsOneWidePolygon(interiorTileSet);

                    int appliedCount = 0;
                    // Track tiles where we actually applied PAP/texture for this polygon —
                    // used so warehouse RF4 placement matches the interior footprint exactly,
                    // not the (possibly larger) walkable bounding box.
                    var appliedTilesForPolygon = new List<(int tx, int ty)>();

                    foreach (var (tx, ty) in interiorTiles)
                    {
                        // Higher roofs claim tiles first. Do not let a lower roof overwrite them.
                        if (!claimedInteriorTiles.Add((tx, ty)))
                        {
                            Debug.WriteLine($"[RoofEnclosureService] Skipping tile ({tx},{ty}) because it is already claimed by a higher roof");
                            continue;
                        }

                        int wpfTx = 127 - tx;
                        int wpfTy = 127 - ty;

                        Debug.WriteLine($"[RoofEnclosureService] Facet coords (X={tx}, Z={ty}) -> WPF coords ({wpfTx},{wpfTy})");

                        if (isWarehouseBuilding)
                        {
                            // Warehouses keep PAP altitude at 0 — only the Hidden flag is set so
                            // the tile reads as "no roof here from the PAP layer". The real roof
                            // sits at walkable/RF4 altitude instead.
                            altAcc.SetFlags(wpfTx, wpfTy, PapFlags.Hidden);
                        }
                        else
                        {
                            altAcc.WriteWorldAltitude(wpfTx, wpfTy, worldAltitude);
                            altAcc.SetFlags(wpfTx, wpfTy, PapFlags.RoofExists | PapFlags.Hidden);
                        }

                        changedWpfTiles.Add((wpfTx, wpfTy));
                        anyAltitudeChanged = true;
                        appliedCount++;
                        appliedTilesForPolygon.Add((tx, ty));

                        if (applyRoofTextures)
                        {
                            if (isWarehouseBuilding)
                            {
                                // Warehouses have their own roof texturing path (via RF4 entries)
                                // so the tile underneath gets a flat floor texture, not a
                                // classified roof pattern.
                                texAcc.WriteTileTexture(wpfTx, wpfTy,
                                    TexturesAccessor.TextureGroup.Shared,
                                    WarehouseFloorTextureIndex, 0, currentWorld);
                            }
                            else
                            {
                                var (texNum, rot) = ClassifyRoofTile(tx, ty, interiorTileSet, oneWide);
                                texAcc.WriteTileTexture(wpfTx, wpfTy, TexturesAccessor.TextureGroup.Shared, texNum, rot, currentWorld);
                            }
                            anyTextureChanged = true;
                        }
                    }

                    // Auto-create walkable (and warehouse RF4s) for this specific enclosure
                    if (createWalkables && maxX > minX && maxZ > minZ)
                    {
                        int walkableWorldY = worldAltitude;

                        var template = new WalkableTemplate
                        {
                            BuildingId1 = buildingId1,
                            X1 = minX,
                            Z1 = minZ,
                            X2 = maxX,
                            Z2 = maxZ,
                            WorldY = walkableWorldY,
                            StoreyY = (byte)Math.Clamp(walkableWorldY >> 6, 0, 255)
                        };

                        var walkableAdder = new WalkableAdder(svc);
                        var rf4Adder = new RoofFace4Adder(svc);

                        bool updated = wantUpdateExisting && walkableAdder.TryUpdateExistingWalkableY(
                            buildingId1, minX, minZ, maxX, maxZ, walkableWorldY, template.StoreyY);

                        if (updated)
                        {
                            anyWalkableChanged = true;
                            Debug.WriteLine($"[RoofEnclosureService] Updated existing walkable #{existingWalkableId1} Y for Building #{buildingId1}: " +
                                $"({minX},{minZ})->({maxX},{maxZ}) WorldY={walkableWorldY} (replaceHigher={replaceExistingWithHigher})");

                            // Walkable Y changed → keep any RF4 tiles aligned with the new height.
                            if (isWarehouseBuilding && existingWalkableId1 > 0)
                            {
                                int touched = rf4Adder.TryUpdateWalkableRoofFace4Y(existingWalkableId1, (short)walkableWorldY);
                                if (touched > 0)
                                    Debug.WriteLine($"[RoofEnclosureService] Updated {touched} RF4 Y values to match walkable #{existingWalkableId1}");
                            }

                            RoofsChangeBus.Instance.NotifyChanged();
                        }
                        else if (!existingFound)
                        {
                            var walkResult = walkableAdder.TryAddWalkable(template);

                            if (walkResult.Success)
                            {
                                anyWalkableChanged = true;
                                Debug.WriteLine($"[RoofEnclosureService] Auto-created walkable #{walkResult.WalkableId1} " +
                                    $"for Building #{buildingId1}: ({minX},{minZ})->({maxX},{maxZ}) Height={height} Y0={y0} WorldY={walkableWorldY}");

                                // For warehouses, auto-fill the interior with flat RF4 tiles at
                                // the walkable's height. Only the polygon-interior footprint
                                // gets RF4 tiles — not the full walkable bounding box — so that
                                // non-rectangular shapes mirror the floor-texture footprint.
                                // Houses etc. keep the PAP-roof mechanism and don't get auto
                                // RF4 entries.
                                if (isWarehouseBuilding && walkResult.WalkableId1 > 0)
                                {
                                    int rf4Created = 0;
                                    foreach (var (tx, tz) in appliedTilesForPolygon)
                                    {
                                        byte rx = (byte)(tx - minX);
                                        byte rz = (byte)(tz - minZ);
                                        var rf4Result = rf4Adder.TryAddRoofFace4(
                                            walkResult.WalkableId1, rx, rz,
                                            (short)walkableWorldY, 0, 0, 0, 0x08);
                                        if (rf4Result.IsSuccess) rf4Created++;
                                        else Debug.WriteLine($"[RoofEnclosureService] Failed to add warehouse RF4 at rx={rx} rz={rz}: {rf4Result.ErrorMessage}");
                                    }
                                    Debug.WriteLine($"[RoofEnclosureService] Auto-created {rf4Created}/{appliedTilesForPolygon.Count} RF4 tiles (interior footprint) for warehouse walkable #{walkResult.WalkableId1}");
                                }

                                RoofsChangeBus.Instance.NotifyChanged();
                            }
                            else
                            {
                                Debug.WriteLine($"[RoofEnclosureService] Failed to create walkable: {walkResult.Error}");
                            }
                        }
                    }

                    Debug.WriteLine($"[RoofEnclosureService] Successfully applied roof to {appliedCount} tiles for this polygon");
                    anyApplied = true;
                }
            }

            if (anyAltitudeChanged && changedWpfTiles.Count > 0)
            {
                int minTx = changedWpfTiles.Min(t => t.wpfTx);
                int minTy = changedWpfTiles.Min(t => t.wpfTy);
                int maxTx = changedWpfTiles.Max(t => t.wpfTx);
                int maxTy = changedWpfTiles.Max(t => t.wpfTy);
                AltitudeChangeBus.Instance.NotifyRegion(minTx, minTy, maxTx, maxTy);
            }

            if (anyTextureChanged)
            {
                TexturesChangeBus.Instance.NotifyChanged();
            }

            if (anyWalkableChanged)
            {
                BuildingsChangeBus.Instance.NotifyBuildingChanged(buildingId1, BuildingChangeType.Modified);
                BuildingsChangeBus.Instance.NotifyChanged();
            }

            if (!anyApplied)
            {
                Debug.WriteLine($"[RoofEnclosureService] No new closed polygon found at any height level");
                return false;
            }

            return true;
        }

        // ── Polygon group info (read-only, no side-effects) ──────────────────────

        /// <summary>
        /// A closed-polygon group of qualifying wall facets at a specific height/altitude level.
        /// </summary>
        public readonly record struct PolygonGroupInfo(
            int Height,
            int BlockHeight,
            int Y0,
            IReadOnlyList<int> FacetIds);

        /// <summary>
        /// Returns all connected groups of wall (Normal/Door, exterior) facets for the given
        /// building, each annotated with whether the group forms a closed polygon.
        /// Pure read — no map data is modified.
        /// </summary>
        public static IReadOnlyList<PolygonGroupInfo> GetPolygonGroups(int buildingId1)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded) return Array.Empty<PolygonGroupInfo>();

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();
            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
                return Array.Empty<PolygonGroupInfo>();

            var building = snap.Buildings[buildingId1 - 1];
            var result = new List<PolygonGroupInfo>();

            // Collect qualifying exterior edge facets (Normal/Wall/NormalFoundation +
            // Door/OutsideDoor) with 1-based IDs, grouped by (height, blockHeight, y0).
            // Doors count as polygon edges for closure but are filtered out of the
            // FacetIds shown in the polygon list — bulk facet edits only operate on walls.
            var byLevel = new Dictionary<(int h, int bh, int y0), List<(Edge edge, int id1)>>();

            for (int fIdx = (int)building.StartFacet; fIdx < (int)building.EndFacet && fIdx <= snap.Facets.Length; fIdx++)
            {
                if (fIdx < 1) continue;
                var f = snap.Facets[fIdx - 1];

                if (!IsExteriorPolygonEdge(f)) continue;
                if (f.Building != buildingId1) continue;

                int bh = f.BlockHeight > 0 ? f.BlockHeight : 16;
                var key = (h: (int)f.Height, bh, y0: (int)f.Y0);
                if (!byLevel.TryGetValue(key, out var bucket))
                    byLevel[key] = bucket = new List<(Edge, int)>();

                bucket.Add((new Edge(f.X0, f.Z0, f.X1, f.Z1), fIdx));
            }

            foreach (var (key, items) in byLevel.OrderByDescending(kv => kv.Key.h * kv.Key.bh * 4 + kv.Key.y0))
            {
                // Build adjacency: vertex → indices of edges that touch it
                var adj = new Dictionary<(byte x, byte z), List<int>>();
                for (int i = 0; i < items.Count; i++)
                {
                    var e = items[i].edge;
                    foreach (var v in new[] { e.Start, e.End })
                    {
                        if (!adj.TryGetValue(v, out var list))
                            adj[v] = list = new List<int>();
                        list.Add(i);
                    }
                }

                // BFS to find connected components, tracking member facet IDs
                var visited = new HashSet<int>();
                for (int start = 0; start < items.Count; start++)
                {
                    if (!visited.Add(start)) continue;

                    var compIndices = new List<int> { start };
                    var queue = new Queue<int>();
                    queue.Enqueue(start);

                    while (queue.Count > 0)
                    {
                        int cur = queue.Dequeue();
                        foreach (var v in new[] { items[cur].edge.Start, items[cur].edge.End })
                        {
                            if (!adj.TryGetValue(v, out var nbrs)) continue;
                            foreach (int nbr in nbrs)
                            {
                                if (visited.Add(nbr))
                                {
                                    compIndices.Add(nbr);
                                    queue.Enqueue(nbr);
                                }
                            }
                        }
                    }

                    var edges = compIndices.Select(i => items[i].edge).ToList();

                    // Need at least 4 axis-aligned walls to form a closed polygon.
                    if (edges.Count < 4) continue;
                    if (!TryFindClosedPolygon(edges, out _)) continue;

                    // Build the set of exterior wall IDs that belong to this polygon —
                    // doors are used only for closure and are dropped from the bulk-edit list.
                    var idsForList = new List<int>();
                    foreach (int i in compIndices)
                    {
                        int id1 = items[i].id1;
                        if (IsExteriorWallForList(snap.Facets[id1 - 1]))
                            idsForList.Add(id1);
                    }

                    // Pair every exterior edge in this polygon to its mirrored interior wall
                    // (same X/Z endpoints, forward or reversed) at the same level. These share
                    // height with the exterior walls — bulk editing one must edit the other.
                    // Build a fast lookup of the component's edge endpoints (normalised).
                    var componentEdgeSet = new HashSet<((byte x, byte z) a, (byte x, byte z) b)>();
                    foreach (var e in edges)
                    {
                        var a = e.Start; var b = e.End;
                        var pair = Compare(a, b) <= 0 ? (a, b) : (b, a);
                        componentEdgeSet.Add(pair);
                    }

                    for (int iIdx = (int)building.StartFacet; iIdx < (int)building.EndFacet && iIdx <= snap.Facets.Length; iIdx++)
                    {
                        if (iIdx < 1) continue;
                        var ifacet = snap.Facets[iIdx - 1];
                        if (ifacet.Building != buildingId1) continue;
                        if (!IsInteriorWallForList(ifacet)) continue;

                        int ibh = ifacet.BlockHeight > 0 ? ifacet.BlockHeight : 16;
                        if ((int)ifacet.Height != key.h || ibh != key.bh || (int)ifacet.Y0 != key.y0)
                            continue;

                        var ia = ((byte)ifacet.X0, (byte)ifacet.Z0);
                        var ib = ((byte)ifacet.X1, (byte)ifacet.Z1);
                        var ipair = Compare(ia, ib) <= 0 ? (ia, ib) : (ib, ia);

                        if (componentEdgeSet.Contains(ipair))
                            idsForList.Add(iIdx);
                    }

                    if (idsForList.Count == 0) continue;

                    var facetIds = idsForList.Distinct().OrderBy(id => id).ToList();
                    result.Add(new PolygonGroupInfo(key.h, key.bh, key.y0, facetIds));
                }
            }

            return result;

            static int Compare((byte x, byte z) a, (byte x, byte z) b)
            {
                int cmp = a.x.CompareTo(b.x);
                return cmp != 0 ? cmp : a.z.CompareTo(b.z);
            }
        }

        /// <summary>
        /// Clears roof tiles for a building's enclosed area.
        /// Call this before deleting facets that might break an enclosure.
        /// </summary>
        public static void ClearRoofEnclosure(int buildingId1)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
                return;

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
                return;

            var building = snap.Buildings[buildingId1 - 1];

            var wallEdges = new List<Edge>();
            for (int fIdx = building.StartFacet; fIdx < building.EndFacet && fIdx <= snap.Facets.Length; fIdx++)
            {
                if (fIdx < 1) continue;
                var facet = snap.Facets[fIdx - 1];
                if (!IsExteriorPolygonEdge(facet)) continue;
                if (facet.Building != buildingId1) continue;
                wallEdges.Add(new Edge(facet.X0, facet.Z0, facet.X1, facet.Z1));
            }

            var components = SplitIntoConnectedComponents(wallEdges);
            var allInteriorTiles = new List<(int tx, int ty)>();

            foreach (var component in components)
            {
                if (!TryFindClosedPolygon(component, out var polygon))
                    continue;

                var interiorTiles = GetInteriorTiles(polygon);
                if (interiorTiles.Count > 0)
                    allInteriorTiles.AddRange(interiorTiles);
            }

            if (allInteriorTiles.Count == 0)
                return;

            var altAcc = new AltitudeAccessor(svc);
            foreach (var (tx, ty) in allInteriorTiles.Distinct())
            {
                int wpfTx = 127 - tx;
                int wpfTy = 127 - ty;
                altAcc.ClearRoofTile(wpfTx, wpfTy);
            }

            var wpfTiles = allInteriorTiles
                .Distinct()
                .Select(t => (wpfTx: 127 - t.tx, wpfTy: 127 - t.ty))
                .ToList();

            if (wpfTiles.Count > 0)
            {
                int minTx = wpfTiles.Min(t => t.wpfTx);
                int minTy = wpfTiles.Min(t => t.wpfTy);
                int maxTx = wpfTiles.Max(t => t.wpfTx);
                int maxTy = wpfTiles.Max(t => t.wpfTy);
                AltitudeChangeBus.Instance.NotifyRegion(minTx, minTy, maxTx, maxTy);
            }
        }

        /// <summary>
        /// Returns the 2D tile footprint for a building by looking only at its exterior wall-like
        /// facets projected onto X/Z space. This ignores Height / BlockHeight / Y0 completely.
        /// 
        /// It does NOT rely on walkables, and it does NOT require a component to be a perfect
        /// "closed polygon" in the roof-enclosure sense. Instead, it rasterises the wall edges
        /// as barriers and flood-fills from outside; any tile cell not reachable from outside
        /// is treated as interior footprint.
        /// </summary>
        public static IReadOnlyList<(int tx, int ty)> GetBuildingFootprintTiles(int buildingId1)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
                return Array.Empty<(int tx, int ty)>();

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
                return Array.Empty<(int tx, int ty)>();

            var building = snap.Buildings[buildingId1 - 1];

            var wallEdges = new List<Edge>();

            for (int fIdx = building.StartFacet; fIdx < building.EndFacet && fIdx <= snap.Facets.Length; fIdx++)
            {
                if (fIdx < 1) continue;

                var facet = snap.Facets[fIdx - 1];

                if (!IsExteriorPolygonEdge(facet)) continue;
                if (facet.Building != buildingId1) continue;

                wallEdges.Add(new Edge(facet.X0, facet.Z0, facet.X1, facet.Z1));
            }

            if (wallEdges.Count == 0)
                return Array.Empty<(int tx, int ty)>();

            var components = SplitIntoConnectedComponents(wallEdges);
            var result = new HashSet<(int tx, int ty)>();

            foreach (var component in components)
            {
                foreach (var tile in GetInteriorTilesFromEdgeBarriers(component))
                    result.Add(tile);
            }

            return result
                .OrderBy(t => t.ty)
                .ThenBy(t => t.tx)
                .ToArray();
        }

        /// <summary>
        /// Finds enclosed tile cells for one connected wall component by treating every wall edge
        /// as a blocked boundary between neighbouring tile cells, then flood-filling from outside.
        /// Any candidate tile cell inside the component bounds that cannot be reached from outside
        /// is considered interior.
        /// </summary>
        private static List<(int tx, int ty)> GetInteriorTilesFromEdgeBarriers(List<Edge> edges)
        {
            var result = new List<(int tx, int ty)>();
            if (edges == null || edges.Count == 0)
                return result;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minZ = int.MaxValue, maxZ = int.MinValue;

            // verticalWalls: boundary at x between cells (x-1,z) and (x,z), for one z row
            var verticalWalls = new HashSet<(int x, int z)>();

            // horizontalWalls: boundary at z between cells (x,z-1) and (x,z), for one x column
            var horizontalWalls = new HashSet<(int x, int z)>();

            foreach (var e in edges)
            {
                minX = Math.Min(minX, Math.Min(e.X0, e.X1));
                maxX = Math.Max(maxX, Math.Max(e.X0, e.X1));
                minZ = Math.Min(minZ, Math.Min(e.Z0, e.Z1));
                maxZ = Math.Max(maxZ, Math.Max(e.Z0, e.Z1));

                if (e.X0 == e.X1)
                {
                    int x = e.X0;
                    int z0 = Math.Min(e.Z0, e.Z1);
                    int z1 = Math.Max(e.Z0, e.Z1);

                    for (int z = z0; z < z1; z++)
                        verticalWalls.Add((x, z));
                }
                else if (e.Z0 == e.Z1)
                {
                    int z = e.Z0;
                    int x0 = Math.Min(e.X0, e.X1);
                    int x1 = Math.Max(e.X0, e.X1);

                    for (int x = x0; x < x1; x++)
                        horizontalWalls.Add((x, z));
                }
                else
                {
                    // Unexpected non-axis-aligned segment. Ignore it for now.
                    Debug.WriteLine($"[RoofEnclosureService] Non-axis-aligned edge ignored: ({e.X0},{e.Z0})->({e.X1},{e.Z1})");
                }
            }

            if (minX >= maxX || minZ >= maxZ)
                return result;

            // Expand by 1 cell all around so we have a guaranteed outside start cell.
            int minCellX = minX - 1;
            int maxCellX = maxX;
            int minCellZ = minZ - 1;
            int maxCellZ = maxZ;

            var visited = new HashSet<(int x, int z)>();
            var queue = new Queue<(int x, int z)>();

            queue.Enqueue((minCellX, minCellZ));
            visited.Add((minCellX, minCellZ));

            while (queue.Count > 0)
            {
                var (cx, cz) = queue.Dequeue();

                // Left
                TryVisit(cx - 1, cz, canMove: cx > minCellX && !verticalWalls.Contains((cx, cz)));

                // Right
                TryVisit(cx + 1, cz, canMove: cx < maxCellX && !verticalWalls.Contains((cx + 1, cz)));

                // Down
                TryVisit(cx, cz - 1, canMove: cz > minCellZ && !horizontalWalls.Contains((cx, cz)));

                // Up
                TryVisit(cx, cz + 1, canMove: cz < maxCellZ && !horizontalWalls.Contains((cx, cz + 1)));

                void TryVisit(int nx, int nz, bool canMove)
                {
                    if (!canMove)
                        return;

                    if (nx < minCellX || nx > maxCellX || nz < minCellZ || nz > maxCellZ)
                        return;

                    if (visited.Add((nx, nz)))
                        queue.Enqueue((nx, nz));
                }
            }

            // Any tile cell inside the real component bounds that the outside flood-fill could not
            // reach is interior footprint.
            for (int tx = minX; tx < maxX; tx++)
            {
                for (int ty = minZ; ty < maxZ; ty++)
                {
                    if (!visited.Contains((tx, ty)))
                        result.Add((tx, ty));
                }
            }

            return result;
        }

        private static bool PolygonAlreadyProcessed(
            MapDataService svc,
            int buildingId1,
            byte minX,
            byte minZ,
            byte maxX,
            byte maxZ)
        {
            return TryFindExistingWalkable(svc, buildingId1, minX, minZ, maxX, maxZ, out _, out _);
        }

        /// <summary>
        /// Finds an existing walkable for this building whose footprint matches the given
        /// bounds. Returns true with the walkable's 1-based ID and stored byte Y, so
        /// callers can compare altitudes and decide whether to replace the existing
        /// enclosure with a higher one.
        /// </summary>
        private static bool TryFindExistingWalkable(
            MapDataService svc,
            int buildingId1,
            byte minX,
            byte minZ,
            byte maxX,
            byte maxZ,
            out int walkableId1,
            out byte storedY)
        {
            walkableId1 = 0;
            storedY = 0;

            if (!svc.TryGetWalkables(out var existingWalkables, out _))
                return false;

            for (int i = 1; i < existingWalkables.Length; i++)
            {
                var w = existingWalkables[i];
                if (w.Building == buildingId1 &&
                    w.X1 == minX && w.Z1 == minZ &&
                    w.X2 == maxX && w.Z2 == maxZ)
                {
                    walkableId1 = i;
                    storedY = w.Y;
                    return true;
                }
            }

            return false;
        }

        #region Polygon Detection

        private readonly struct Edge
        {
            public readonly byte X0, Z0, X1, Z1;

            public Edge(byte x0, byte z0, byte x1, byte z1)
            {
                X0 = x0; Z0 = z0; X1 = x1; Z1 = z1;
            }

            public (byte x, byte z) Start => (X0, Z0);
            public (byte x, byte z) End => (X1, Z1);

            public (byte x, byte z)? GetOtherEnd(byte x, byte z)
            {
                if (X0 == x && Z0 == z) return (X1, Z1);
                if (X1 == x && Z1 == z) return (X0, Z0);
                return null;
            }

            public bool HasVertex(byte x, byte z) =>
                (X0 == x && Z0 == z) || (X1 == x && Z1 == z);
        }

        private static List<List<Edge>> SplitIntoConnectedComponents(List<Edge> edges)
        {
            var result = new List<List<Edge>>();
            if (edges.Count == 0)
                return result;

            var adjacency = new Dictionary<(byte x, byte z), List<int>>();
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];

                if (!adjacency.TryGetValue(e.Start, out var startList))
                {
                    startList = new List<int>();
                    adjacency[e.Start] = startList;
                }
                startList.Add(i);

                if (!adjacency.TryGetValue(e.End, out var endList))
                {
                    endList = new List<int>();
                    adjacency[e.End] = endList;
                }
                endList.Add(i);
            }

            var visitedEdges = new HashSet<int>();

            for (int i = 0; i < edges.Count; i++)
            {
                if (visitedEdges.Contains(i))
                    continue;

                var component = new List<Edge>();
                var queue = new Queue<int>();
                queue.Enqueue(i);
                visitedEdges.Add(i);

                while (queue.Count > 0)
                {
                    int edgeIdx = queue.Dequeue();
                    var edge = edges[edgeIdx];
                    component.Add(edge);

                    foreach (var vertex in new[] { edge.Start, edge.End })
                    {
                        if (!adjacency.TryGetValue(vertex, out var touchingEdges))
                            continue;

                        foreach (int nextIdx in touchingEdges)
                        {
                            if (visitedEdges.Add(nextIdx))
                                queue.Enqueue(nextIdx);
                        }
                    }
                }

                result.Add(component);
            }

            return result;
        }

        private static bool TryFindClosedPolygon(List<Edge> edges, out List<(byte x, byte z)> polygon)
        {
            polygon = new List<(byte x, byte z)>();

            if (edges.Count < 3)
                return false;

            var adjacency = new Dictionary<(byte, byte), List<int>>();
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                var start = e.Start;
                var end = e.End;

                if (!adjacency.ContainsKey(start))
                    adjacency[start] = new List<int>();
                adjacency[start].Add(i);

                if (!adjacency.ContainsKey(end))
                    adjacency[end] = new List<int>();
                adjacency[end].Add(i);
            }

            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count != 2)
                {
                    Debug.WriteLine($"[RoofEnclosureService] Vertex ({kvp.Key.Item1},{kvp.Key.Item2}) has {kvp.Value.Count} edges (need exactly 2)");
                    return false;
                }
            }

            var usedEdges = new HashSet<int>();
            var startVertex = edges[0].Start;
            var currentVertex = startVertex;

            polygon.Add(currentVertex);

            int maxIterations = edges.Count + 1;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                var edgesAtVertex = adjacency[currentVertex];

                int nextEdgeIdx = -1;
                foreach (var eIdx in edgesAtVertex)
                {
                    if (!usedEdges.Contains(eIdx))
                    {
                        nextEdgeIdx = eIdx;
                        break;
                    }
                }

                if (nextEdgeIdx < 0)
                {
                    if (currentVertex.Item1 == startVertex.Item1 &&
                        currentVertex.Item2 == startVertex.Item2 &&
                        polygon.Count > 2)
                    {
                        if (polygon.Count > 1 && polygon[^1] == startVertex)
                            polygon.RemoveAt(polygon.Count - 1);

                        return usedEdges.Count == edges.Count;
                    }

                    return false;
                }

                usedEdges.Add(nextEdgeIdx);
                var edge = edges[nextEdgeIdx];
                var nextVertex = edge.GetOtherEnd(currentVertex.Item1, currentVertex.Item2);

                if (nextVertex == null)
                {
                    Debug.WriteLine($"[RoofEnclosureService] Edge doesn't connect to current vertex");
                    return false;
                }

                currentVertex = nextVertex.Value;

                if (currentVertex.Item1 == startVertex.Item1 &&
                    currentVertex.Item2 == startVertex.Item2)
                {
                    return usedEdges.Count == edges.Count;
                }

                polygon.Add(currentVertex);
            }

            return false;
        }

        #endregion

        #region Interior Tile Detection

        private static List<(int tx, int ty)> GetInteriorTiles(List<(byte x, byte z)> polygon)
        {
            var result = new List<(int tx, int ty)>();

            if (polygon.Count < 3)
                return result;

            int minX = polygon.Min(p => p.x);
            int maxX = polygon.Max(p => p.x);
            int minZ = polygon.Min(p => p.z);
            int maxZ = polygon.Max(p => p.z);

            Debug.WriteLine($"[RoofEnclosureService] Polygon bounds: X=[{minX},{maxX}] Z=[{minZ},{maxZ}]");

            for (int tx = minX; tx < maxX; tx++)
            {
                for (int ty = minZ; ty < maxZ; ty++)
                {
                    double centerX = tx + 0.5;
                    double centerZ = ty + 0.5;
                    bool isInside = IsPointInPolygon(centerX, centerZ, polygon);

                    if (isInside)
                        result.Add((tx, ty));
                }
            }

            return result;
        }

        private static bool IsPointInPolygon(double testX, double testZ, List<(byte x, byte z)> polygon)
        {
            int n = polygon.Count;
            bool inside = false;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = polygon[i].x;
                double zi = polygon[i].z;
                double xj = polygon[j].x;
                double zj = polygon[j].z;

                if (((zi > testZ) != (zj > testZ)) &&
                    (testX < (xj - xi) * (testZ - zi) / (zj - zi) + xi))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Classifies a roof tile and returns the texture number and rotation to use.
        ///
        /// Coordinate note: tile coords are in game/facet space. The WPF render flips
        /// both axes (wpfTx = 127-tx, wpfTy = 127-ty), so on-screen directions are:
        ///   Screen Right  = lower tx  (tx - 1)
        ///   Screen Left   = higher tx (tx + 1)
        ///   Screen Bottom = lower ty  (ty - 1)
        ///   Screen Top    = higher ty (ty + 1)
        ///
        /// Textures:
        ///   tex420 – centre (fully surrounded, no concave diagonal)
        ///   tex414 – edge (one side open); rot 0=right, 1=bottom, 2=left, 3=top
        ///   tex415 – outer corner (two adjacent sides open);
        ///            rot 0=bottom-right, 1=top-right, 2=top-left, 3=bottom-left
        ///   tex416 – concave inner-corner dot (all cardinals present, one diagonal missing);
        ///            rot 0=top-right missing, 1=bottom-right, 2=bottom-left, 3=top-left
        /// </summary>
        private static bool IsOneWidePolygon(HashSet<(int, int)> tileSet)
        {
            foreach (var (tx, ty) in tileSet)
            {
                int count = (tileSet.Contains((tx - 1, ty)) ? 1 : 0)
                          + (tileSet.Contains((tx + 1, ty)) ? 1 : 0)
                          + (tileSet.Contains((tx, ty - 1)) ? 1 : 0)
                          + (tileSet.Contains((tx, ty + 1)) ? 1 : 0);
                if (count >= 3)
                    return false;
            }
            return true;
        }

        private static (int texNum, int rotation) ClassifyRoofTile(int tx, int ty, HashSet<(int, int)> tileSet, bool isOneWide)
        {
            // Special case 1:
            // A closed polygon that contains exactly one tile should use the centre tile.
            if (tileSet.Count == 1)
                return (420, 0);

            bool hasRight = tileSet.Contains((tx - 1, ty));
            bool hasLeft = tileSet.Contains((tx + 1, ty));
            bool hasBottom = tileSet.Contains((tx, ty - 1));
            bool hasTop = tileSet.Contains((tx, ty + 1));

            int neighbourCount = (hasRight ? 1 : 0) + (hasLeft ? 1 : 0)
                               + (hasBottom ? 1 : 0) + (hasTop ? 1 : 0);

            // Special case 2:
            // Exact 2x2 enclosure has a known rotation quirk on the top row.
            if (tileSet.Count == 4)
            {
                int minX = tileSet.Min(t => t.Item1);
                int maxX = tileSet.Max(t => t.Item1);
                int minY = tileSet.Min(t => t.Item2);
                int maxY = tileSet.Max(t => t.Item2);

                bool exact2x2 = (maxX - minX == 1) && (maxY - minY == 1);

                if (exact2x2)
                {
                    bool isLeft = tx == maxX;
                    bool isRight = tx == minX;
                    bool isTopRow = ty == maxY;
                    bool isBottom = ty == minY;

                    if (isRight && isBottom) return (415, 2); // bottom-right
                    if (isLeft && isBottom) return (415, 1); // bottom-left
                    if (isRight && isTopRow) return (415, 3); // top-right
                    if (isLeft && isTopRow) return (415, 0); // top-left
                }
            }

            if (isOneWide)
            {
                if (neighbourCount <= 1)
                {
                    // End piece — open end faces away from the single neighbour.
                    if (hasRight) return (418, 1);
                    if (hasLeft) return (418, 3);
                    if (hasTop) return (418, 2);
                    if (hasBottom) return (418, 0);
                    return (418, 3); // isolated single tile (guarded above, but kept as fallback)
                }

                if (neighbourCount == 2)
                {
                    // Straight piece — two opposite neighbours.
                    if (hasRight && hasLeft) return (417, 0); // horizontal run
                    if (hasTop && hasBottom) return (417, 1); // vertical run

                    // Bend/corner piece — two adjacent neighbours.
                    if (hasRight && hasTop) return (419, 1);
                    if (hasRight && hasBottom) return (419, 1);
                    if (hasLeft && hasBottom) return (419, 2);
                    return (419, 2); // hasLeft && hasTop
                }

                return (417, 0);
            }

            int missing = (!hasRight ? 1 : 0) + (!hasLeft ? 1 : 0)
                        + (!hasBottom ? 1 : 0) + (!hasTop ? 1 : 0);

            if (missing == 0)
            {
                bool hasTopRight = tileSet.Contains((tx - 1, ty + 1));
                bool hasBottomRight = tileSet.Contains((tx - 1, ty - 1));
                bool hasBottomLeft = tileSet.Contains((tx + 1, ty - 1));
                bool hasTopLeft = tileSet.Contains((tx + 1, ty + 1));

                int missingDiag = (!hasTopRight ? 1 : 0) + (!hasBottomRight ? 1 : 0)
                                + (!hasBottomLeft ? 1 : 0) + (!hasTopLeft ? 1 : 0);

                if (missingDiag == 1)
                {
                    if (!hasTopRight) return (416, 3);
                    if (!hasBottomRight) return (416, 2);
                    if (!hasBottomLeft) return (416, 1);
                    return (416, 0); // !hasTopLeft
                }

                return (420, 0);
            }

            if (missing == 1)
            {
                if (!hasRight) return (414, 3);
                if (!hasBottom) return (414, 2);
                if (!hasLeft) return (414, 1);
                return (414, 0); // !hasTop
            }

            if (missing == 2)
            {
                if (!hasRight && !hasBottom) return (415, 2); // bottom-right corner
                if (!hasRight && !hasTop) return (415, 3); // top-right corner
                if (!hasLeft && !hasTop) return (415, 0); // top-left corner
                if (!hasLeft && !hasBottom) return (415, 1); // bottom-left corner
            }

            return (420, 0);
        }

        #endregion
    }
}
