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
        /// <summary>
        /// Checks if a building's wall facets form one or more closed polygons.
        /// If so, sets PAP_HI altitude/Hidden flags AND creates walkable region(s).
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

            // Collect exterior Normal + Door facets grouped by (height, y0).
            // Y0 is the vertical offset of the polygon — it must be included in the
            // walkable WorldY so the floor sits at the correct altitude.
            var facetsByHeight = new Dictionary<(int height, int y0), List<Edge>>();

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

                var key = (height: facet.Height, y0: facet.Y0);
                if (!facetsByHeight.ContainsKey(key))
                    facetsByHeight[key] = new List<Edge>();

                facetsByHeight[key].Add(new Edge(facet.X0, facet.Z0, facet.X1, facet.Z1));

                Debug.WriteLine($"[RoofEnclosureService] Facet: ({facet.X0},{facet.Z0})->({facet.X1},{facet.Z1}) Height={facet.Height} Y0={facet.Y0} Type={facet.Type}");
            }

            Debug.WriteLine($"[RoofEnclosureService] Building #{buildingId1}: {facetsByHeight.Count} height+y0 level(s)");

            var altAcc = new AltitudeAccessor(svc);
            var texAcc = new TexturesAccessor(svc);
            int currentWorld = texAcc.ReadTextureWorld();

            bool anyApplied = false;
            bool anyAltitudeChanged = false;
            bool anyTextureChanged = false;

            var changedWpfTiles = new List<(int wpfTx, int wpfTy)>();
            var claimedInteriorTiles = new HashSet<(int tx, int ty)>();

            // Try each height+y0 level (highest effective top first)
            foreach (var (height, y0) in facetsByHeight.Keys.OrderByDescending(k => k.height * 64 + k.y0))
            {
                var edges = facetsByHeight[(height, y0)];
                Debug.WriteLine($"[RoofEnclosureService] Trying height={height} y0={y0} with {edges.Count} edges");

                var components = SplitIntoConnectedComponents(edges);
                Debug.WriteLine($"[RoofEnclosureService] Height={height} split into {components.Count} connected component(s)");

                foreach (var component in components)
                {
                    Debug.WriteLine($"[RoofEnclosureService] Trying component with {component.Count} edges");

                    if (component.Count < 3)
                        continue;

                    if (!TryFindClosedPolygon(component, out var polygon))
                    {
                        Debug.WriteLine($"[RoofEnclosureService] Component is not a closed polygon");
                        continue;
                    }

                    Debug.WriteLine($"[RoofEnclosureService] Closed polygon found at height={height} with {polygon.Count} vertices");
                    foreach (var (x, z) in polygon)
                        Debug.WriteLine($"[RoofEnclosureService]   Vertex: ({x}, {z})");

                    var interiorTiles = GetInteriorTiles(polygon);
                    if (interiorTiles.Count == 0)
                    {
                        Debug.WriteLine($"[RoofEnclosureService] No interior tiles found for this polygon");
                        continue;
                    }

                    int worldAltitude = (height << AltitudeAccessor.PAP_ALT_SHIFT) + (y0 >> AltitudeAccessor.PAP_ALT_SHIFT);
                    Debug.WriteLine($"[RoofEnclosureService] Found {interiorTiles.Count} interior tiles, applying roof at Height={height} Y0={y0} (world={worldAltitude})");

                    int appliedCount = 0;

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

                        bool isWarehouseBuilding = (building.Type == (byte)BuildingType.Warehouse);

                        altAcc.WriteWorldAltitude(wpfTx, wpfTy, worldAltitude);
                        if (isWarehouseBuilding)
                            altAcc.SetFlags(wpfTx, wpfTy, PapFlags.Hidden);
                        else
                            altAcc.SetFlags(wpfTx, wpfTy, PapFlags.RoofExists | PapFlags.Hidden);

                        changedWpfTiles.Add((wpfTx, wpfTy));
                        anyAltitudeChanged = true;
                        appliedCount++;

                        if (applyRoofTextures)
                        {
                            texAcc.WriteTileTexture(wpfTx, wpfTy, TexturesAccessor.TextureGroup.World, 50, 0, currentWorld);
                            anyTextureChanged = true;
                        }
                    }

                    // Auto-create walkable for this specific enclosure
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
                            // WorldY = top of the enclosed polygon = facet height contribution + Y offset.
                            // Without y0, a polygon raised off the ground (y0 > 0) would create a
                            // walkable floor too low by exactly the y0 amount.
                            int walkableWorldY = height * 64 + y0;

                            var template = new WalkableTemplate
                            {
                                BuildingId1 = buildingId1,
                                X1 = minX,
                                Z1 = minZ,
                                X2 = maxX,
                                Z2 = maxZ,
                                WorldY = walkableWorldY,
                                StoreyY = 0
                            };

                            var walkableAdder = new WalkableAdder(svc);
                            var walkResult = walkableAdder.TryAddWalkable(template);

                            if (walkResult.Success)
                            {
                                Debug.WriteLine($"[RoofEnclosureService] Auto-created walkable #{walkResult.WalkableId1} " +
                                    $"for Building #{buildingId1}: ({minX},{minZ})->({maxX},{maxZ}) Height={height} Y0={y0} WorldY={walkableWorldY}");
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

            if (!anyApplied)
            {
                Debug.WriteLine($"[RoofEnclosureService] No closed polygon found at any height level");
                return false;
            }

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
                if ((facet.Flags & FacetFlags.Inside) != 0)
                    continue;
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

        #endregion
    }
}