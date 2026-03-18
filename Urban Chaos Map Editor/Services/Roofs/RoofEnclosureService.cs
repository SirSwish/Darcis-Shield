// /Services/Roofs/RoofEnclosureService.cs
// Detects when wall facets form a closed polygon and automatically sets PAP_HI roof flags
// AND creates a walkable region covering the enclosed area.
using System.Diagnostics;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Textures;

namespace UrbanChaosMapEditor.Services.Roofs
{
    public static class RoofEnclosureService
    {
        /// <summary>
        /// Checks if a building's wall facets form a closed polygon.
        /// If so, sets PAP_HI altitude/Hidden flags AND creates a walkable region.
        /// Call this after adding a wall facet to a building.
        /// </summary>
        public static bool CheckAndApplyRoofEnclosure(
            int buildingId1,
            bool applyRoofTextures = true,
            bool createWalkables = true)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
                return false;

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
                return false;

            var building = snap.Buildings[buildingId1 - 1];

            // Collect exterior Normal + Door facets grouped by height
            var facetsByHeight = new Dictionary<int, List<Edge>>();

            for (int fIdx = building.StartFacet; fIdx < building.EndFacet && fIdx <= snap.Facets.Length; fIdx++)
            {
                if (fIdx < 1) continue;
                var facet = snap.Facets[fIdx - 1];

                if (facet.Type != FacetType.Normal && facet.Type != FacetType.Door)
                    continue;

                if ((facet.Flags & FacetFlags.Inside) != 0)
                    continue;

                if (facet.Building != buildingId1)
                    continue;

                int h = facet.Height;
                if (!facetsByHeight.ContainsKey(h))
                    facetsByHeight[h] = new List<Edge>();
                facetsByHeight[h].Add(new Edge(facet.X0, facet.Z0, facet.X1, facet.Z1));

                Debug.WriteLine($"[RoofEnclosureService] Facet: ({facet.X0},{facet.Z0})->({facet.X1},{facet.Z1}) Height={h} Type={facet.Type}");
            }

            Debug.WriteLine($"[RoofEnclosureService] Building #{buildingId1}: {facetsByHeight.Count} height level(s)");

            // Try each height level (highest first) to find a closed polygon
            List<(byte x, byte z)>? polygon = null;
            int roofHeight = 0;

            foreach (var height in facetsByHeight.Keys.OrderByDescending(h => h))
            {
                var edges = facetsByHeight[height];
                Debug.WriteLine($"[RoofEnclosureService] Trying height={height} with {edges.Count} edges");

                if (edges.Count >= 3 && TryFindClosedPolygon(edges, out var candidatePolygon))
                {
                    polygon = candidatePolygon;
                    roofHeight = height;
                    Debug.WriteLine($"[RoofEnclosureService] Closed polygon found at height={height} with {candidatePolygon.Count} vertices");
                    break;
                }
            }

            if (polygon == null)
            {
                Debug.WriteLine($"[RoofEnclosureService] No closed polygon found at any height level");
                return false;
            }

            foreach (var (x, z) in polygon)
                Debug.WriteLine($"[RoofEnclosureService]   Vertex: ({x}, {z})");

            // Find all tiles inside the polygon
            var interiorTiles = GetInteriorTiles(polygon);

            if (interiorTiles.Count == 0)
            {
                Debug.WriteLine($"[RoofEnclosureService] No interior tiles found");
                return false;
            }

            // Calculate world altitude from the height of the storey that formed the polygon
            int worldAltitude = roofHeight << AltitudeAccessor.PAP_ALT_SHIFT;

            Debug.WriteLine($"[RoofEnclosureService] Found {interiorTiles.Count} interior tiles, applying roof at Height={roofHeight} (world={worldAltitude})");

            // Apply PAP_HI altitude and Hidden flag to interior tiles
            var altAcc = new AltitudeAccessor(svc);
            var texAcc = new TexturesAccessor(svc);
            int currentWorld = texAcc.ReadTextureWorld();

            foreach (var (tx, ty) in interiorTiles)
            {
                int wpfTx = 127 - tx;
                int wpfTy = 127 - ty;

                Debug.WriteLine($"[RoofEnclosureService] Facet coords (X={tx}, Z={ty}) -> WPF coords ({wpfTx},{wpfTy})");

                // Warehouse buildings only need Hidden flag (not RoofExists)
                // Normal buildings need both Hidden + RoofExists
                bool isWarehouseBuilding = (building.Type == (byte)BuildingType.Warehouse);

                altAcc.WriteWorldAltitude(wpfTx, wpfTy, worldAltitude);
                if (isWarehouseBuilding)
                    altAcc.SetFlags(wpfTx, wpfTy, PapFlags.Hidden);
                else
                    altAcc.SetFlags(wpfTx, wpfTy, PapFlags.RoofExists | PapFlags.Hidden);
                if (applyRoofTextures)
                {
                    texAcc.WriteTileTexture(wpfTx, wpfTy, TexturesAccessor.TextureGroup.World, 50, 0, currentWorld);
                }
            }

            // Notify altitude + texture changes
            if (interiorTiles.Count > 0)
            {
                var wpfTiles = interiorTiles.Select(t => (wpfTx: 127 - t.tx, wpfTy: 127 - t.ty)).ToList();
                var minTx = wpfTiles.Min(t => t.wpfTx);
                var minTy = wpfTiles.Min(t => t.wpfTy);
                var maxTx = wpfTiles.Max(t => t.wpfTx);
                var maxTy = wpfTiles.Max(t => t.wpfTy);
                AltitudeChangeBus.Instance.NotifyRegion(minTx, minTy, maxTx, maxTy);
                TexturesChangeBus.Instance.NotifyChanged();
            }

            // ============================================================
            // AUTO-CREATE WALKABLE for the enclosed roof area
            // ============================================================
            byte minX = (byte)polygon.Min(p => p.x);
            byte maxX = (byte)polygon.Max(p => p.x);
            byte minZ = (byte)polygon.Min(p => p.z);
            byte maxZ = (byte)polygon.Max(p => p.z);

            if (createWalkables && maxX > minX && maxZ > minZ)
            {
                bool walkableExists = false;
                if (svc.TryGetWalkables(out var existingWalkables, out _))
                {
                    for (int i = 1; i < existingWalkables.Length; i++)
                    {
                        var w = existingWalkables[i];
                        if (w.Building == buildingId1 &&
                            w.X1 == minX && w.Z1 == minZ &&
                            w.X2 == maxX && w.Z2 == maxZ)
                        {
                            walkableExists = true;
                            Debug.WriteLine($"[RoofEnclosureService] Walkable already exists for this area, skipping creation");
                            break;
                        }
                    }
                }

                if (!walkableExists)
                {
                    var template = new WalkableTemplate
                    {
                        BuildingId1 = buildingId1,
                        X1 = minX,
                        Z1 = minZ,
                        X2 = maxX,
                        Z2 = maxZ,
                        WorldY = roofHeight * 64,
                        StoreyY = 0
                    };

                    var walkableAdder = new WalkableAdder(svc);
                    var walkResult = walkableAdder.TryAddWalkable(template);

                    if (walkResult.Success)
                    {
                        Debug.WriteLine($"[RoofEnclosureService] Auto-created walkable #{walkResult.WalkableId1} " +
                            $"for Building #{buildingId1}: ({minX},{minZ})->({maxX},{maxZ}) WorldY={roofHeight * 64}");
                        RoofsChangeBus.Instance.NotifyChanged();
                    }
                    else
                    {
                        Debug.WriteLine($"[RoofEnclosureService] Failed to create walkable: {walkResult.Error}");
                    }
                }
            }

            Debug.WriteLine($"[RoofEnclosureService] Successfully applied roof to {interiorTiles.Count} tiles");
            return true;
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
                if (facet.Type != FacetType.Normal) continue;
                // Skip interior (back-to-front) facets — they duplicate the exterior edges
                if ((facet.Flags & FacetFlags.Inside) != 0)
                    continue;
                if (facet.Building != buildingId1) continue;
                wallEdges.Add(new Edge(facet.X0, facet.Z0, facet.X1, facet.Z1));
            }

            if (!TryFindClosedPolygon(wallEdges, out var polygon))
                return;

            var interiorTiles = GetInteriorTiles(polygon);
            if (interiorTiles.Count == 0)
                return;

            var altAcc = new AltitudeAccessor(svc);
            foreach (var (tx, ty) in interiorTiles)
            {
                int wpfTx = 127 - tx;
                int wpfTy = 127 - ty;
                altAcc.ClearRoofTile(wpfTx, wpfTy);
            }

            if (interiorTiles.Count > 0)
            {
                var wpfTiles = interiorTiles.Select(t => (wpfTx: 127 - t.tx, wpfTy: 127 - t.ty)).ToList();
                var minTx = wpfTiles.Min(t => t.wpfTx);
                var minTy = wpfTiles.Min(t => t.wpfTy);
                var maxTx = wpfTiles.Max(t => t.wpfTx);
                var maxTy = wpfTiles.Max(t => t.wpfTy);
                AltitudeChangeBus.Instance.NotifyRegion(minTx, minTy, maxTx, maxTy);
            }
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
                    if (currentVertex.x == startVertex.x && currentVertex.z == startVertex.z && polygon.Count > 2)
                    {
                        if (polygon.Count > 1 && polygon[^1] == startVertex)
                            polygon.RemoveAt(polygon.Count - 1);
                        return usedEdges.Count == edges.Count;
                    }
                    return false;
                }

                usedEdges.Add(nextEdgeIdx);
                var edge = edges[nextEdgeIdx];
                var nextVertex = edge.GetOtherEnd(currentVertex.x, currentVertex.z);

                if (nextVertex == null)
                {
                    Debug.WriteLine($"[RoofEnclosureService] Edge doesn't connect to current vertex");
                    return false;
                }

                currentVertex = nextVertex.Value;

                if (currentVertex.x == startVertex.x && currentVertex.z == startVertex.z)
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
                    {
                        result.Add((tx, ty));
                    }
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

        #endregion
    }
}