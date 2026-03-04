// /Services/RoofEnclosureService.cs
// Detects when wall facets form a closed polygon and automatically sets PAP_HI roof flags
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Services.DataServices;

namespace UrbanChaosMapEditor.Services
{
    /// <summary>
    /// Service that detects when a building's wall facets form a closed polygon
    /// and automatically sets the PAP_HI altitude and Hidden flags for interior tiles.
    /// This enables ledge grabbing on the resulting roof surface.
    /// </summary>
    public static class RoofEnclosureService
    {
        /// <summary>
        /// Checks if a building's wall facets form a closed polygon.
        /// If so, sets PAP_HI altitude and Hidden flag for interior tiles.
        /// Call this after adding a wall facet to a building.
        /// </summary>
        /// <param name="buildingId1">1-based building ID</param>
        /// <returns>True if an enclosure was detected and roof was applied</returns>
        public static bool CheckAndApplyRoofEnclosure(int buildingId1)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
                return false;

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
                return false;

            var building = snap.Buildings[buildingId1 - 1];

            // Get all wall facets (Normal type) for this building
            var wallEdges = new List<Edge>();
            int roofHeight = 0;

            for (int fIdx = building.StartFacet; fIdx < building.EndFacet && fIdx <= snap.Facets.Length; fIdx++)
            {
                if (fIdx < 1) continue;
                var facet = snap.Facets[fIdx - 1];

                // Only consider Normal wall facets (not ladders, doors, fences, etc.)
                if (facet.Type != FacetType.Normal)
                    continue;

                // Also skip if it's assigned to a different building (shouldn't happen, but safety check)
                if (facet.Building != buildingId1)
                    continue;

                Debug.WriteLine($"[RoofEnclosureService] Wall facet: ({facet.X0},{facet.Z0})->({facet.X1},{facet.Z1}) Height={facet.Height} BlockHeight={facet.BlockHeight}");
                wallEdges.Add(new Edge(facet.X0, facet.Z0, facet.X1, facet.Z1));

                // Track the maximum roof height
                // The Height field is the raw altitude value for PAP_HI.Alt
                // World altitude = Height << PAP_ALT_SHIFT (i.e. Height * 8)
                int facetHeight = facet.Height;
                if (facetHeight > roofHeight)
                    roofHeight = facetHeight;
            }

            Debug.WriteLine($"[RoofEnclosureService] Building #{buildingId1}: Found {wallEdges.Count} wall edges, max height={roofHeight}");

            if (wallEdges.Count < 3)
            {
                Debug.WriteLine($"[RoofEnclosureService] Not enough edges for a polygon (need at least 3)");
                return false;
            }

            // Try to find a closed polygon from these edges
            if (!TryFindClosedPolygon(wallEdges, out var polygon))
            {
                Debug.WriteLine($"[RoofEnclosureService] No closed polygon found");
                return false;
            }

            Debug.WriteLine($"[RoofEnclosureService] Found closed polygon with {polygon.Count} vertices:");
            foreach (var (x, z) in polygon)
            {
                Debug.WriteLine($"[RoofEnclosureService]   Vertex: ({x}, {z})");
            }

            // Find all tiles inside the polygon
            var interiorTiles = GetInteriorTiles(polygon);

            if (interiorTiles.Count == 0)
            {
                Debug.WriteLine($"[RoofEnclosureService] No interior tiles found");
                return false;
            }

            Debug.WriteLine($"[RoofEnclosureService] Found {interiorTiles.Count} interior tiles, applying roof at Height={roofHeight} (raw Alt={roofHeight}, world={roofHeight << AltitudeAccessor.PAP_ALT_SHIFT})");

            // Apply PAP_HI altitude and Hidden flag to interior tiles
            // Height is the raw altitude value, convert to world altitude for SetRoofTile
            int worldAltitude = roofHeight << AltitudeAccessor.PAP_ALT_SHIFT;

            var altAcc = new AltitudeAccessor(svc);
            var texAcc = new TexturesAccessor(svc);
            int currentWorld = texAcc.ReadTextureWorld();

            foreach (var (tx, ty) in interiorTiles)
            {
                // tx = facet X, ty = facet Z
                // To make visual tile match building position:
                // wpfTx = 127 - facet X
                // wpfTy = 127 - facet Z  
                int wpfTx = 127 - tx;  // 127 - facet X
                int wpfTy = 127 - ty;  // 127 - facet Z

                Debug.WriteLine($"[RoofEnclosureService] Facet coords (X={tx}, Z={ty}) -> WPF coords ({wpfTx},{wpfTy})");

                // Read current values before modification
                var oldFlags = altAcc.ReadFlags(wpfTx, wpfTy);
                var oldAlt = altAcc.ReadAltRaw(wpfTx, wpfTy);

                // SetRoofTile sets altitude AND the RoofExists + Hidden flags
                altAcc.SetRoofTile(wpfTx, wpfTy, worldAltitude);

                // DEBUG: Also set a distinctive texture so we can see which tiles are being modified
                // Using texture 50 from world textures (should be visually different)
                Debug.WriteLine($"[RoofEnclosureService] WRITING TEXTURE 50 to WPF tile ({wpfTx},{wpfTy})");
                texAcc.WriteTileTexture(wpfTx, wpfTy, TexturesAccessor.TextureGroup.World, 50, 0, currentWorld);
                Debug.WriteLine($"[RoofEnclosureService] TEXTURE WRITTEN to ({wpfTx},{wpfTy})");

                // Read new values after modification
                var newFlags = altAcc.ReadFlags(wpfTx, wpfTy);
                var newAlt = altAcc.ReadAltRaw(wpfTx, wpfTy);
                int newWorldAlt = altAcc.ReadWorldAltitude(wpfTx, wpfTy);

                Debug.WriteLine($"[RoofEnclosureService] PAP_HI WPF tile ({wpfTx},{wpfTy}): " +
                    $"Alt {oldAlt}->{newAlt} (world: {newWorldAlt}), " +
                    $"Flags 0x{(ushort)oldFlags:X4}->0x{(ushort)newFlags:X4}");
            }

            // Notify that altitude changed (using WPF coordinates)
            if (interiorTiles.Count > 0)
            {
                // Transform: wpfTx = 127 - facetX, wpfTy = 127 - facetZ
                var wpfTiles = interiorTiles.Select(t => (wpfTx: 127 - t.tx, wpfTy: 127 - t.ty)).ToList();
                var minTx = wpfTiles.Min(t => t.wpfTx);
                var minTy = wpfTiles.Min(t => t.wpfTy);
                var maxTx = wpfTiles.Max(t => t.wpfTx);
                var maxTy = wpfTiles.Max(t => t.wpfTy);
                AltitudeChangeBus.Instance.NotifyRegion(minTx, minTy, maxTx, maxTy);

                // Also notify texture change for debug visualization
                TexturesChangeBus.Instance.NotifyChanged();
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

            // Get wall edges
            var wallEdges = new List<Edge>();
            for (int fIdx = building.StartFacet; fIdx < building.EndFacet && fIdx <= snap.Facets.Length; fIdx++)
            {
                if (fIdx < 1) continue;
                var facet = snap.Facets[fIdx - 1];
                if (facet.Type != FacetType.Normal) continue;
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
                // Transform: wpfTx = 127 - facetX, wpfTy = 127 - facetZ
                int wpfTx = 127 - tx;
                int wpfTy = 127 - ty;
                altAcc.ClearRoofTile(wpfTx, wpfTy);
            }

            if (interiorTiles.Count > 0)
            {
                // Transform: wpfTx = 127 - facetX, wpfTy = 127 - facetZ
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

            // Get the other endpoint given one endpoint
            public (byte x, byte z)? GetOtherEnd(byte x, byte z)
            {
                if (X0 == x && Z0 == z) return (X1, Z1);
                if (X1 == x && Z1 == z) return (X0, Z0);
                return null;
            }

            public bool HasVertex(byte x, byte z) =>
                (X0 == x && Z0 == z) || (X1 == x && Z1 == z);
        }

        /// <summary>
        /// Attempts to find a closed polygon from the given edges.
        /// Returns the polygon as an ordered list of vertices.
        /// </summary>
        private static bool TryFindClosedPolygon(List<Edge> edges, out List<(byte x, byte z)> polygon)
        {
            polygon = new List<(byte x, byte z)>();

            if (edges.Count < 3)
                return false;

            // Build adjacency map: vertex -> list of edges that touch it
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

            // For a valid closed polygon, every vertex must have exactly 2 edges
            // (entry and exit). If any vertex has != 2 edges, we can't form a simple closed polygon.
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count != 2)
                {
                    Debug.WriteLine($"[RoofEnclosureService] Vertex ({kvp.Key.Item1},{kvp.Key.Item2}) has {kvp.Value.Count} edges (need exactly 2)");
                    return false;
                }
            }

            // All vertices have exactly 2 edges - try to walk the polygon
            var usedEdges = new HashSet<int>();
            var startVertex = edges[0].Start;
            var currentVertex = startVertex;

            polygon.Add(currentVertex);

            // Walk around the polygon
            int maxIterations = edges.Count + 1;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                var edgesAtVertex = adjacency[currentVertex];

                // Find an unused edge from this vertex
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
                    // No more unused edges - check if we're back at start
                    if (currentVertex.x == startVertex.x && currentVertex.z == startVertex.z && polygon.Count > 2)
                    {
                        // Remove the duplicate start vertex at the end
                        if (polygon.Count > 1 && polygon[^1] == startVertex)
                            polygon.RemoveAt(polygon.Count - 1);
                        return usedEdges.Count == edges.Count; // Success only if all edges used
                    }
                    return false;
                }

                usedEdges.Add(nextEdgeIdx);
                var edge = edges[nextEdgeIdx];
                var nextVertex = edge.GetOtherEnd(currentVertex.x, currentVertex.z);

                if (nextVertex == null)
                {
                    Debug.WriteLine($"[RoofEnclosureService] Edge doesn't connect to current vertex - should never happen");
                    return false;
                }

                currentVertex = nextVertex.Value;

                // Check if we've returned to start
                if (currentVertex.x == startVertex.x && currentVertex.z == startVertex.z)
                {
                    // Complete loop - check if all edges were used
                    return usedEdges.Count == edges.Count;
                }

                polygon.Add(currentVertex);
            }

            return false; // Failed to close the loop
        }

        #endregion

        #region Interior Tile Detection

        /// <summary>
        /// Gets all tiles that are inside the polygon using ray casting algorithm.
        /// </summary>
        private static List<(int tx, int ty)> GetInteriorTiles(List<(byte x, byte z)> polygon)
        {
            var result = new List<(int tx, int ty)>();

            if (polygon.Count < 3)
                return result;

            // Find bounding box (polygon coordinates are in tile space)
            int minX = polygon.Min(p => p.x);
            int maxX = polygon.Max(p => p.x);
            int minZ = polygon.Min(p => p.z);
            int maxZ = polygon.Max(p => p.z);

            Debug.WriteLine($"[RoofEnclosureService] Polygon bounds: X=[{minX},{maxX}] Z=[{minZ},{maxZ}]");
            Debug.WriteLine($"[RoofEnclosureService] Checking tiles from ({minX},{minZ}) to ({maxX - 1},{maxZ - 1})");

            // The polygon vertices are at tile CORNERS (vertices), not tile centers
            // A tile at (tx, ty) occupies the area from (tx, ty) to (tx+1, ty+1)
            // We want tiles whose centers are inside the polygon
            // Tile center = (tx + 0.5, ty + 0.5)

            // However, for building roofs, we typically want the tiles INSIDE the walls
            // The walls are ON the polygon edges, so interior tiles are strictly inside

            // Iterate over potential interior tiles
            for (int tx = minX; tx < maxX; tx++)
            {
                for (int ty = minZ; ty < maxZ; ty++)
                {
                    // Check if tile center is inside polygon
                    // Tile center in vertex-space: (tx + 0.5, ty + 0.5)
                    double centerX = tx + 0.5;
                    double centerZ = ty + 0.5;
                    bool isInside = IsPointInPolygon(centerX, centerZ, polygon);

                    Debug.WriteLine($"[RoofEnclosureService] Tile ({tx},{ty}) center=({centerX},{centerZ}) inside={isInside}");

                    if (isInside)
                    {
                        // Convert to WPF tile coordinates if needed
                        // The facet coordinates appear to already be in the same space as PAP_HI tiles
                        result.Add((tx, ty));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Ray casting algorithm to determine if a point is inside a polygon.
        /// </summary>
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

                // Check if the ray from (testX, testZ) going right crosses this edge
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