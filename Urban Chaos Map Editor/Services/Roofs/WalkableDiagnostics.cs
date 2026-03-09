// /Services/Diagnostics/WalkableDiagnostics.cs
// Helper to diagnose walkable and building structure issues
using System.Text;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;

namespace UrbanChaosMapEditor.Services.Roofs
{
    /// <summary>
    /// Diagnostic helper for analyzing walkable and building structure data.
    /// </summary>
    public static class WalkableDiagnostics
    {
        /// <summary>
        /// Backward-compatible alias for GenerateFullReport.
        /// </summary>
        public static string GenerateReport(MapDataService svc)
        {
            return GenerateFullReport(svc);
        }

        /// <summary>
        /// Generate a comprehensive diagnostic report including walkables, storeys, and building structure.
        /// </summary>
        public static string GenerateFullReport(MapDataService svc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== FULL MAP DIAGNOSTICS REPORT ===");
            sb.AppendLine();

            if (!svc.IsLoaded)
            {
                sb.AppendLine("ERROR: No map loaded.");
                return sb.ToString();
            }

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            // Header info
            sb.AppendLine($"SaveType: {snap.SaveType}");
            sb.AppendLine($"NextDBuilding: {snap.NextDBuilding}");
            sb.AppendLine($"NextDFacet: {snap.NextDFacet}");
            sb.AppendLine($"NextDStyle: {snap.NextDStyle}");
            sb.AppendLine($"NextPaintMem: {snap.NextPaintMem}");
            sb.AppendLine($"NextDStorey: {snap.NextDStorey}");
            sb.AppendLine();

            // DStorey analysis
            sb.AppendLine("=== DSTOREY RECORDS ===");
            if (snap.Storeys == null || snap.Storeys.Length <= 1)
            {
                sb.AppendLine("No DStorey records found (or only sentinel).");
            }
            else
            {
                sb.AppendLine($"Total DStoreys: {snap.Storeys.Length - 1} (excluding sentinel)");
                sb.AppendLine();
                // DStoreyRec has: StyleIndex, PaintIndex, Count, Padding
                sb.AppendLine("ID   | StyleIdx | PaintIdx | Count | Pad");
                sb.AppendLine("-----|----------|----------|-------|----");

                for (int i = 1; i < snap.Storeys.Length; i++)
                {
                    var s = snap.Storeys[i];
                    sb.AppendLine($"{i,4} | {s.StyleIndex,8} | {s.PaintIndex,8} | {s.Count,5} | {s.Padding}");
                }
            }
            sb.AppendLine();

            // Building analysis
            sb.AppendLine("=== BUILDING RECORDS ===");
            if (snap.Buildings == null || snap.Buildings.Length == 0)
            {
                sb.AppendLine("No buildings found.");
            }
            else
            {
                sb.AppendLine($"Total Buildings: {snap.Buildings.Length}");
                sb.AppendLine();
                sb.AppendLine("ID  | Type | Facets       | Walkable | WorldPos              | Counters");
                sb.AppendLine("----|------|--------------|----------|----------------------|----------");

                for (int i = 0; i < snap.Buildings.Length; i++)
                {
                    var b = snap.Buildings[i];
                    int bId = i + 1;
                    string facetRange = $"{b.StartFacet}-{b.EndFacet}";
                    string worldPos = $"({b.WorldX},{b.WorldY},{b.WorldZ})";
                    sb.AppendLine($"{bId,3} | {b.Type,4} | {facetRange,12} | {b.Walkable,8} | {worldPos,20} | {b.Counter0},{b.Counter1}");
                }
            }
            sb.AppendLine();

            // Facet analysis per building
            sb.AppendLine("=== FACETS BY BUILDING ===");
            if (snap.Buildings != null && snap.Facets != null)
            {
                for (int i = 0; i < snap.Buildings.Length; i++)
                {
                    var b = snap.Buildings[i];
                    int bId = i + 1;

                    sb.AppendLine($"\n--- Building #{bId} (Type={b.Type}) ---");
                    sb.AppendLine($"Facet range: {b.StartFacet} to {b.EndFacet}");
                    sb.AppendLine();
                    sb.AppendLine("  Facet# | Type        | Coords              | H   | Y0    | Y1    | Storey | Flags");
                    sb.AppendLine("  -------|-------------|---------------------|-----|-------|-------|--------|-------");

                    int facetCount = 0;
                    for (int f = b.StartFacet; f < b.EndFacet && f <= snap.Facets.Length; f++)
                    {
                        if (f < 1) continue;
                        var facet = snap.Facets[f - 1]; // 0-indexed array

                        string coords = $"({facet.X0},{facet.Z0})->({facet.X1},{facet.Z1})";
                        string typeName = facet.Type.ToString().PadRight(11);

                        sb.AppendLine($"  {f,6} | {typeName} | {coords,19} | {facet.Height,3} | {facet.Y0,5} | {facet.Y1,5} | {facet.Storey,6} | 0x{(ushort)facet.Flags:X4}");
                        facetCount++;

                        if (facetCount >= 20)
                        {
                            int remaining = b.EndFacet - f - 1;
                            if (remaining > 0)
                                sb.AppendLine($"  ... and {remaining} more facets");
                            break;
                        }
                    }
                }
            }
            sb.AppendLine();

            // Walkables analysis
            sb.AppendLine("=== WALKABLE RECORDS ===");
            if (!acc.TryGetWalkables(out var walkables, out var roofFaces))
            {
                sb.AppendLine("Failed to read walkable data.");
            }
            else
            {
                sb.AppendLine($"Total Walkables: {walkables.Length - 1} (excluding sentinel)");
                sb.AppendLine($"Total RoofFace4: {roofFaces.Length - 1} (excluding sentinel)");
                sb.AppendLine();

                for (int i = 1; i < walkables.Length; i++)
                {
                    var w = walkables[i];
                    sb.AppendLine($"Walkable #{i}:");
                    sb.AppendLine($"  Building: {w.Building}");
                    sb.AppendLine($"  Bounds: ({w.X1},{w.Z1}) -> ({w.X2},{w.Z2})");
                    sb.AppendLine($"  Y: {w.Y} (world: {w.Y << 5})");
                    sb.AppendLine($"  StoreyY: {w.StoreyY} (suggests DY={w.StoreyY << 6})");
                    sb.AppendLine($"  Next: {w.Next}");
                    sb.AppendLine($"  StartPoint: {w.StartPoint}, EndPoint: {w.EndPoint} (count: {w.EndPoint - w.StartPoint})");
                    sb.AppendLine($"  StartFace3: {w.StartFace3}, EndFace3: {w.EndFace3}");
                    sb.AppendLine($"  StartFace4: {w.StartFace4}, EndFace4: {w.EndFace4}");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate a comparison report between two specific buildings.
        /// </summary>
        public static string CompareBuildingsReport(MapDataService svc, int buildingId1_A, int buildingId1_B)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== BUILDING COMPARISON: #{buildingId1_A} vs #{buildingId1_B} ===");
            sb.AppendLine();

            if (!svc.IsLoaded)
            {
                sb.AppendLine("ERROR: No map loaded.");
                return sb.ToString();
            }

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null)
            {
                sb.AppendLine("ERROR: No buildings found.");
                return sb.ToString();
            }

            int idxA = buildingId1_A - 1;
            int idxB = buildingId1_B - 1;

            if (idxA < 0 || idxA >= snap.Buildings.Length)
            {
                sb.AppendLine($"ERROR: Building #{buildingId1_A} not found.");
                return sb.ToString();
            }
            if (idxB < 0 || idxB >= snap.Buildings.Length)
            {
                sb.AppendLine($"ERROR: Building #{buildingId1_B} not found.");
                return sb.ToString();
            }

            var bA = snap.Buildings[idxA];
            var bB = snap.Buildings[idxB];

            sb.AppendLine("Property            | Building A          | Building B");
            sb.AppendLine("--------------------|---------------------|--------------------");
            sb.AppendLine($"Type                | {bA.Type,19} | {bB.Type,19}");
            sb.AppendLine($"StartFacet          | {bA.StartFacet,19} | {bB.StartFacet,19}");
            sb.AppendLine($"EndFacet            | {bA.EndFacet,19} | {bB.EndFacet,19}");
            sb.AppendLine($"Facet Count         | {bA.EndFacet - bA.StartFacet,19} | {bB.EndFacet - bB.StartFacet,19}");
            sb.AppendLine($"Walkable            | {bA.Walkable,19} | {bB.Walkable,19}");
            sb.AppendLine($"WorldX              | {bA.WorldX,19} | {bB.WorldX,19}");
            sb.AppendLine($"WorldY              | {bA.WorldY,19} | {bB.WorldY,19}");
            sb.AppendLine($"WorldZ              | {bA.WorldZ,19} | {bB.WorldZ,19}");
            sb.AppendLine($"Counter0            | {bA.Counter0,19} | {bB.Counter0,19}");
            sb.AppendLine($"Counter1            | {bA.Counter1,19} | {bB.Counter1,19}");
            sb.AppendLine($"Ware                | {bA.Ware,19} | {bB.Ware,19}");
            sb.AppendLine();

            // Compare facets
            sb.AppendLine("=== FACET COMPARISON ===");
            sb.AppendLine();

            int maxFacets = Math.Max(bA.EndFacet - bA.StartFacet, bB.EndFacet - bB.StartFacet);
            maxFacets = Math.Min(maxFacets, 20); // Limit output

            sb.AppendLine("Idx | A: Type/H/Y0/Y1/Storey/Flags       | B: Type/H/Y0/Y1/Storey/Flags");
            sb.AppendLine("----|-------------------------------------|-------------------------------------");

            for (int i = 0; i < maxFacets; i++)
            {
                string strA = "---";
                string strB = "---";

                int fIdxA = bA.StartFacet + i;
                int fIdxB = bB.StartFacet + i;

                if (fIdxA < bA.EndFacet && fIdxA >= 1 && fIdxA <= snap.Facets.Length)
                {
                    var f = snap.Facets[fIdxA - 1];
                    strA = $"{f.Type}/{f.Height}/{f.Y0}/{f.Y1}/{f.Storey}/0x{(ushort)f.Flags:X4}";
                }

                if (fIdxB < bB.EndFacet && fIdxB >= 1 && fIdxB <= snap.Facets.Length)
                {
                    var f = snap.Facets[fIdxB - 1];
                    strB = $"{f.Type}/{f.Height}/{f.Y0}/{f.Y1}/{f.Storey}/0x{(ushort)f.Flags:X4}";
                }

                sb.AppendLine($"{i,3} | {strA,-35} | {strB,-35}");
            }

            // Compare associated walkables
            sb.AppendLine();
            sb.AppendLine("=== ASSOCIATED WALKABLES ===");

            if (acc.TryGetWalkables(out var walkables, out _))
            {
                sb.AppendLine($"\nWalkables for Building #{buildingId1_A}:");
                bool foundA = false;
                for (int i = 1; i < walkables.Length; i++)
                {
                    if (walkables[i].Building == buildingId1_A)
                    {
                        var w = walkables[i];
                        sb.AppendLine($"  Walkable #{i}: Bounds=({w.X1},{w.Z1})->({w.X2},{w.Z2}) Y={w.Y} StoreyY={w.StoreyY} Points={w.StartPoint}-{w.EndPoint}");
                        foundA = true;
                    }
                }
                if (!foundA) sb.AppendLine("  (none)");

                sb.AppendLine($"\nWalkables for Building #{buildingId1_B}:");
                bool foundB = false;
                for (int i = 1; i < walkables.Length; i++)
                {
                    if (walkables[i].Building == buildingId1_B)
                    {
                        var w = walkables[i];
                        sb.AppendLine($"  Walkable #{i}: Bounds=({w.X1},{w.Z1})->({w.X2},{w.Z2}) Y={w.Y} StoreyY={w.StoreyY} Points={w.StartPoint}-{w.EndPoint}");
                        foundB = true;
                    }
                }
                if (!foundB) sb.AppendLine("  (none)");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Quick summary for a single building.
        /// </summary>
        public static string BuildingSummary(MapDataService svc, int buildingId1)
        {
            var sb = new StringBuilder();

            if (!svc.IsLoaded)
                return "ERROR: No map loaded.";

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
                return $"ERROR: Building #{buildingId1} not found.";

            var b = snap.Buildings[buildingId1 - 1];

            sb.AppendLine($"Building #{buildingId1}");
            sb.AppendLine($"  Type: {b.Type}");
            sb.AppendLine($"  Facets: {b.StartFacet} to {b.EndFacet} ({b.EndFacet - b.StartFacet} total)");
            sb.AppendLine($"  Walkable head: {b.Walkable}");
            sb.AppendLine($"  World position: ({b.WorldX}, {b.WorldY}, {b.WorldZ})");
            sb.AppendLine();

            // List facets
            sb.AppendLine("  Facets:");
            for (int f = b.StartFacet; f < b.EndFacet && f <= snap.Facets.Length; f++)
            {
                if (f < 1) continue;
                var facet = snap.Facets[f - 1];
                sb.AppendLine($"    #{f}: {facet.Type} H={facet.Height} Y0={facet.Y0} Y1={facet.Y1} Storey={facet.Storey} Flags=0x{(ushort)facet.Flags:X4}");
            }

            // List walkables
            if (acc.TryGetWalkables(out var walkables, out _))
            {
                sb.AppendLine();
                sb.AppendLine("  Walkables:");
                bool found = false;
                for (int i = 1; i < walkables.Length; i++)
                {
                    if (walkables[i].Building == buildingId1)
                    {
                        var w = walkables[i];
                        sb.AppendLine($"    Walkable #{i}: ({w.X1},{w.Z1})->({w.X2},{w.Z2}) Y={w.Y} StoreyY={w.StoreyY}");
                        sb.AppendLine($"      Points: {w.StartPoint}-{w.EndPoint}, Face4: {w.StartFace4}-{w.EndFace4}");
                        found = true;
                    }
                }
                if (!found) sb.AppendLine("    (none)");
            }

            return sb.ToString();
        }
    }
}