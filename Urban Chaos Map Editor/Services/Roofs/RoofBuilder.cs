// /Services/RoofBuilder.cs
using System.Diagnostics;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;

namespace UrbanChaosMapEditor.Services.Roofs
{
    /// <summary>
    /// Detects closed shapes formed by facets and sets the altitude of interior cells
    /// to create flat roof surfaces.
    /// 
    /// Based on the original Urban Chaos building.cpp build_easy_roof() and build_roof_grid()
    /// functions, this class:
    /// 1. Finds facets that form closed rectangles/polygons
    /// 2. Identifies which PAP_HI cells are inside the closed shape
    /// 3. Sets their Alt field to match the top of the enclosing facets
    /// </summary>
    public sealed class RoofBuilder
    {
        private readonly MapDataService _mapData;
        private readonly BuildingsAccessor _buildings;
        private readonly AltitudeAccessor _altitude;

        /// <summary>
        /// Represents an edge (facet segment) with direction information.
        /// </summary>
        public readonly struct Edge
        {
            public int X1 { get; }
            public int Z1 { get; }
            public int X2 { get; }
            public int Z2 { get; }
            public int FacetId { get; }
            public int TopAltitude { get; } // World altitude at top of facet

            public Edge(int x1, int z1, int x2, int z2, int facetId, int topAlt)
            {
                X1 = x1; Z1 = z1; X2 = x2; Z2 = z2;
                FacetId = facetId;
                TopAltitude = topAlt;
            }

            public bool IsHorizontal => Z1 == Z2;
            public bool IsVertical => X1 == X2;

            public override string ToString() =>
                $"Edge[({X1},{Z1})->({X2},{Z2}) facet#{FacetId} alt={TopAltitude}]";
        }

        /// <summary>
        /// Result of closed shape detection.
        /// </summary>
        public sealed class ClosedShapeResult
        {
            public bool IsClosedShape { get; init; }
            public List<Edge> Edges { get; init; } = new();
            public int MinX { get; init; }
            public int MaxX { get; init; }
            public int MinZ { get; init; }
            public int MaxZ { get; init; }
            public int TopAltitude { get; init; }
            public List<(int tx, int ty)> InteriorCells { get; init; } = new();
            public string? ErrorMessage { get; init; }
        }

        public RoofBuilder(MapDataService mapData, BuildingsAccessor buildings, AltitudeAccessor altitude)
        {
            _mapData = mapData ?? throw new ArgumentNullException(nameof(mapData));
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _altitude = altitude ?? throw new ArgumentNullException(nameof(altitude));
        }

        /// <summary>
        /// Analyze a set of facets to determine if they form a closed shape.
        /// Facet coordinates are in block units (0-127).
        /// </summary>
        public ClosedShapeResult AnalyzeClosedShape(IEnumerable<int> facetIds)
        {
            var buildingData = _buildings.ReadSnapshot();
            if (buildingData.Facets.Length == 0)
            {
                return new ClosedShapeResult
                {
                    IsClosedShape = false,
                    ErrorMessage = "No facets loaded"
                };
            }

            var edges = new List<Edge>();
            int minX = int.MaxValue, maxX = int.MinValue;
            int minZ = int.MaxValue, maxZ = int.MinValue;
            int topAltitude = 0;

            foreach (int facetId1 in facetIds)
            {
                int facetIdx0 = facetId1 - 1; // Convert to 0-based index
                if (facetIdx0 < 0 || facetIdx0 >= buildingData.Facets.Length)
                {
                    Debug.WriteLine($"[RoofBuilder] Facet #{facetId1} out of range");
                    continue;
                }

                var facet = buildingData.Facets[facetIdx0];

                // Only consider normal wall-type facets
                if (facet.Type != FacetType.Normal && facet.Type != FacetType.Wall)
                {
                    Debug.WriteLine($"[RoofBuilder] Skipping facet #{facetId1} type={facet.Type}");
                    continue;
                }

                // Calculate top altitude of this facet
                // Top = Y0 + Height * 16 + FHeight
                int facetTop = facet.Y0 + (facet.Height * 16) + facet.FHeight;

                // Update max top altitude (all facets should have same height for a proper closed shape)
                if (facetTop > topAltitude)
                    topAltitude = facetTop;

                // Create edge from facet coordinates
                int x1 = facet.X0;
                int z1 = facet.Z0;
                int x2 = facet.X1;
                int z2 = facet.Z1;

                edges.Add(new Edge(x1, z1, x2, z2, facetId1, facetTop));

                // Update bounds
                minX = Math.Min(minX, Math.Min(x1, x2));
                maxX = Math.Max(maxX, Math.Max(x1, x2));
                minZ = Math.Min(minZ, Math.Min(z1, z2));
                maxZ = Math.Max(maxZ, Math.Max(z1, z2));
            }

            if (edges.Count < 3)
            {
                return new ClosedShapeResult
                {
                    IsClosedShape = false,
                    Edges = edges,
                    ErrorMessage = "Need at least 3 edges to form a closed shape"
                };
            }

            // Check if edges form a closed polygon by verifying connectivity
            bool isClosed = CheckClosedPolygon(edges);

            // Find interior cells using scanline algorithm
            var interiorCells = new List<(int tx, int ty)>();
            if (isClosed)
            {
                interiorCells = FindInteriorCells(edges, minX, maxX, minZ, maxZ);
            }

            return new ClosedShapeResult
            {
                IsClosedShape = isClosed,
                Edges = edges,
                MinX = minX,
                MaxX = maxX,
                MinZ = minZ,
                MaxZ = maxZ,
                TopAltitude = topAltitude,
                InteriorCells = interiorCells,
                ErrorMessage = isClosed ? null : "Edges do not form a closed polygon"
            };
        }

        /// <summary>
        /// Check if edges form a closed polygon by verifying all vertices have even degree.
        /// </summary>
        private bool CheckClosedPolygon(List<Edge> edges)
        {
            // Build vertex degree map
            var vertexDegree = new Dictionary<(int, int), int>();

            foreach (var edge in edges)
            {
                var v1 = (edge.X1, edge.Z1);
                var v2 = (edge.X2, edge.Z2);

                vertexDegree[v1] = vertexDegree.GetValueOrDefault(v1, 0) + 1;
                vertexDegree[v2] = vertexDegree.GetValueOrDefault(v2, 0) + 1;
            }

            // For a closed polygon, every vertex should have even degree (typically 2)
            // But complex shapes like L-shapes or inner rectangles may have degree 4 at corners
            foreach (var kvp in vertexDegree)
            {
                if (kvp.Value % 2 != 0)
                {
                    Debug.WriteLine($"[RoofBuilder] Vertex {kvp.Key} has odd degree {kvp.Value}");
                    return false;
                }
            }

            // Also need at least 4 vertices for a closed shape (triangle minimum)
            if (vertexDegree.Count < 3)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Find cells that are inside the closed polygon using scanline algorithm.
        /// This uses a ray-casting approach: for each cell, count how many edges
        /// are crossed when casting a ray to the right. Odd count = inside.
        /// </summary>
        private List<(int tx, int ty)> FindInteriorCells(List<Edge> edges, int minX, int maxX, int minZ, int maxZ)
        {
            var interior = new List<(int tx, int ty)>();

            // For each cell row (Z coordinate)
            for (int z = minZ; z <= maxZ; z++)
            {
                // For each cell in this row
                for (int x = minX; x <= maxX; x++)
                {
                    // Check if cell center is inside the polygon
                    // Cell center is at (x + 0.5, z + 0.5) in block coordinates
                    // But since we're working with integers, we check (x, z) as the cell position

                    if (IsPointInsidePolygon(x, z, edges))
                    {
                        // Also check we're not on an edge
                        if (!IsPointOnEdge(x, z, edges))
                        {
                            interior.Add((x, z));
                        }
                    }
                }
            }

            return interior;
        }

        /// <summary>
        /// Ray-casting algorithm to determine if a point is inside a polygon.
        /// Casts a ray to the right (+X direction) and counts edge crossings.
        /// </summary>
        private bool IsPointInsidePolygon(int px, int pz, List<Edge> edges)
        {
            int crossings = 0;

            foreach (var edge in edges)
            {
                // Check if the edge crosses our horizontal ray at Y=pz
                int z1 = edge.Z1;
                int z2 = edge.Z2;
                int x1 = edge.X1;
                int x2 = edge.X2;

                // Ensure z1 <= z2
                if (z1 > z2)
                {
                    (z1, z2) = (z2, z1);
                    (x1, x2) = (x2, x1);
                }

                // Skip if edge is entirely above or below the ray
                if (pz < z1 || pz >= z2)
                    continue;

                // Skip horizontal edges
                if (z1 == z2)
                    continue;

                // Calculate X at intersection
                double t = (double)(pz - z1) / (z2 - z1);
                double intersectX = x1 + t * (x2 - x1);

                // Count if intersection is to the right of point
                if (intersectX > px)
                {
                    crossings++;
                }
            }

            // Odd crossings = inside
            return (crossings % 2) == 1;
        }

        /// <summary>
        /// Check if a point lies on any edge of the polygon.
        /// </summary>
        private bool IsPointOnEdge(int px, int pz, List<Edge> edges)
        {
            foreach (var edge in edges)
            {
                int minX = Math.Min(edge.X1, edge.X2);
                int maxX = Math.Max(edge.X1, edge.X2);
                int minZ = Math.Min(edge.Z1, edge.Z2);
                int maxZ = Math.Max(edge.Z1, edge.Z2);

                // Check if point is within edge bounds
                if (px >= minX && px <= maxX && pz >= minZ && pz <= maxZ)
                {
                    // For horizontal or vertical edges, point is on edge
                    if (edge.IsHorizontal && pz == edge.Z1 && px >= minX && px <= maxX)
                        return true;
                    if (edge.IsVertical && px == edge.X1 && pz >= minZ && pz <= maxZ)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Build a roof by setting the altitude of interior cells.
        /// Returns the number of cells modified.
        /// </summary>
        public int BuildRoof(IEnumerable<int> facetIds)
        {
            var result = AnalyzeClosedShape(facetIds);

            if (!result.IsClosedShape)
            {
                Debug.WriteLine($"[RoofBuilder] Cannot build roof: {result.ErrorMessage}");
                return 0;
            }

            Debug.WriteLine($"[RoofBuilder] Building roof for {result.Edges.Count} edges, {result.InteriorCells.Count} interior cells");
            Debug.WriteLine($"[RoofBuilder] Bounds: ({result.MinX},{result.MinZ})->({result.MaxX},{result.MaxZ})");
            Debug.WriteLine($"[RoofBuilder] Top altitude: {result.TopAltitude}");

            int modified = 0;
            foreach (var (tx, ty) in result.InteriorCells)
            {
                // Validate tile coordinates
                if (tx < 0 || tx >= MapConstants.TilesPerSide || ty < 0 || ty >= MapConstants.TilesPerSide)
                {
                    Debug.WriteLine($"[RoofBuilder] Skipping out-of-bounds cell ({tx},{ty})");
                    continue;
                }

                // Use SetRoofTile to set BOTH altitude AND roof flags
                _altitude.SetRoofTile(tx, ty, result.TopAltitude);
                modified++;
                Debug.WriteLine($"[RoofBuilder] Set roof tile ({tx},{ty}) altitude to {result.TopAltitude}");
            }

            if (modified > 0)
            {
                AltitudeChangeBus.Instance.NotifyAll();
            }

            return modified;
        }

        /// <summary>
        /// Analyze all buildings and automatically build roofs for any that form closed shapes.
        /// Returns total number of cells modified.
        /// </summary>
        public int BuildAllRoofs()
        {
            var buildingData = _buildings.ReadSnapshot();
            int totalModified = 0;

            foreach (var building in buildingData.Buildings)
            {
                if (building.StartFacet == 0 || building.EndFacet == 0)
                    continue;

                // Get all facet IDs for this building
                var facetIds = Enumerable.Range(building.StartFacet, building.EndFacet - building.StartFacet);

                int modified = BuildRoof(facetIds);
                totalModified += modified;
            }

            return totalModified;
        }

        /// <summary>
        /// Analyze facets around a specific coordinate to find potential closed shapes.
        /// Useful for the UI to detect shapes when the user clicks near facets.
        /// </summary>
        public ClosedShapeResult FindClosedShapeNearPoint(int blockX, int blockZ, int searchRadius = 5)
        {
            var buildingData = _buildings.ReadSnapshot();
            var nearbyFacetIds = new HashSet<int>();

            // Find all facets within search radius
            for (int i = 0; i < buildingData.Facets.Length; i++)
            {
                var facet = buildingData.Facets[i];

                // Check if facet is near the point
                int minFX = Math.Min(facet.X0, facet.X1);
                int maxFX = Math.Max(facet.X0, facet.X1);
                int minFZ = Math.Min(facet.Z0, facet.Z1);
                int maxFZ = Math.Max(facet.Z0, facet.Z1);

                if (blockX >= minFX - searchRadius && blockX <= maxFX + searchRadius &&
                    blockZ >= minFZ - searchRadius && blockZ <= maxFZ + searchRadius)
                {
                    nearbyFacetIds.Add(i + 1); // Convert to 1-based ID
                }
            }

            if (nearbyFacetIds.Count == 0)
            {
                return new ClosedShapeResult
                {
                    IsClosedShape = false,
                    ErrorMessage = "No facets found near this point"
                };
            }

            // Try to find a closed shape from these facets
            return AnalyzeClosedShape(nearbyFacetIds);
        }

        /// <summary>
        /// Get a summary of the closed shape analysis for debugging/display.
        /// </summary>
        public string GetAnalysisSummary(ClosedShapeResult result)
        {
            if (!result.IsClosedShape)
            {
                return $"Not a closed shape: {result.ErrorMessage ?? "unknown reason"}";
            }

            return $"Closed shape: {result.Edges.Count} edges, " +
                   $"bounds ({result.MinX},{result.MinZ})->({result.MaxX},{result.MaxZ}), " +
                   $"{result.InteriorCells.Count} interior cells, " +
                   $"top altitude: {result.TopAltitude}";
        }
    }
}