// Services/Buildings/CableAdder.cs
// Adds cable facets to the map. Cables MUST be inserted within a building's
// [StartFacet..EndFacet) range ï¿½ orphan cables are silently ignored by the engine.
//
// Unlike regular facets, cables do NOT use dstyles: the StyleIndex and Building
// fields are repurposed for step_angle1 and step_angle2 (cable curve parameters).

using System;
using System.Diagnostics;
using System.IO;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosEditor.Shared.Constants;

namespace UrbanChaosMapEditor.Services.Buildings
{
    public static class CableAdder
    {
        // DFacet byte offsets (26 bytes total)

        /// <summary>
        /// Adds a new cable facet within the specified building's facet range.
        /// The cable is inserted at the building's EndFacet position and all
        /// subsequent buildings' facet ranges are shifted up by 1.
        /// </summary>
        /// <param name="buildingId1">1-based building ID to own the cable</param>
        /// <param name="x0">Start X coordinate (0-127)</param>
        /// <param name="z0">Start Z coordinate (0-127)</param>
        /// <param name="y0">Start Y world coordinate</param>
        /// <param name="x1">End X coordinate (0-127)</param>
        /// <param name="z1">End Z coordinate (0-127)</param>
        /// <param name="y1">End Y world coordinate</param>
        /// <param name="segments">Number of cable segments (auto-calculated if 0)</param>
        /// <param name="stepAngle1">First step angle (auto-calculated if null)</param>
        /// <param name="stepAngle2">Second step angle (auto-calculated if null)</param>
        /// <param name="fHeight">Texture style mode (default 0; working cables use 4)</param>
        /// <returns>Tuple of (success, newFacetId, errorMessage)</returns>
        public static (bool Success, int NewFacetId, string? Error) TryAddCable(
            int buildingId1,
            byte x0, byte z0, short y0,
            byte x1, byte z1, short y1,
            int segments = 0,
            short? stepAngle1 = null,
            short? stepAngle2 = null,
            byte fHeight = 0)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
                return (false, 0, "No map loaded");

            if (buildingId1 <= 0)
                return (false, 0, "A building must be selected. Cables must belong to a building.");

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            if (snap.StartOffset < 0)
                return (false, 0, "Could not find building region");

            if (buildingId1 > snap.Buildings.Length)
                return (false, 0, $"Building #{buildingId1} not found.");

            // Calculate length for auto-segments
            double dx = (x1 - x0) * 256.0;
            double dz = (z1 - z0) * 256.0;
            double dy = y1 - y0;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // Auto-calculate segments if not specified
            if (segments <= 0)
                segments = CalculateSegmentCount(length);

            // Auto-calculate step angles if not specified
            short step1 = stepAngle1 ?? CalculateStepAngles(length, segments).step1;
            short step2 = stepAngle2 ?? CalculateStepAngles(length, segments).step2;

            // Clamp values
            segments = Math.Clamp(segments, 2, 255);

            // Get current file bytes
            var bytes = svc.GetBytesCopy();
            int blockStart = snap.StartOffset;

            // Read current header counters
            ushort oldNextBuilding = ReadU16(bytes, blockStart + 2);
            ushort oldNextFacet = ReadU16(bytes, blockStart + 4);
            ushort oldNextStyle = ReadU16(bytes, blockStart + 6);

            // Calculate offsets
            int buildingsOff = blockStart + BuildingFormatConstants.HeaderSize;
            int totalBuildings = Math.Max(0, oldNextBuilding - 1);
            int padOff = buildingsOff + totalBuildings * BuildingFormatConstants.DBuildingSize;
            int facetsOff = padOff + BuildingFormatConstants.AfterBuildingsPad;
            int totalFacets = Math.Max(0, oldNextFacet - 1);
            int stylesOff = facetsOff + totalFacets * BuildingFormatConstants.DFacetSize;
            int oldStylesSize = Math.Max(0, oldNextStyle - 1) * BuildingFormatConstants.DStyleSize;
            int afterStylesOff = stylesOff + oldStylesSize;

            // Get the target building's current facet range
            int buildingRecOff = buildingsOff + (buildingId1 - 1) * BuildingFormatConstants.DBuildingSize;
            ushort oldEndFacet = ReadU16(bytes, buildingRecOff + 2);

            // Insert at the building's current EndFacet position (1-based)
            int insertPosition = oldEndFacet;

            Debug.WriteLine($"[CableAdder] Inserting cable into Building #{buildingId1}, insertPos={insertPosition}");

            // Create the new cable facet record (26 bytes)
            var newFacet = new byte[BuildingFormatConstants.DFacetSize];

            newFacet[BuildingFormatConstants.DFacetOffsetType] = (byte)FacetType.Cable; // 9
            newFacet[BuildingFormatConstants.DFacetOffsetHeight] = (byte)segments;
            newFacet[BuildingFormatConstants.DFacetOffsetX0] = x0;
            newFacet[BuildingFormatConstants.DFacetOffsetX1] = x1;
            WriteS16(newFacet, BuildingFormatConstants.DFacetOffsetY0, y0);
            WriteS16(newFacet, BuildingFormatConstants.DFacetOffsetY1, y1);
            newFacet[BuildingFormatConstants.DFacetOffsetZ0] = z0;
            newFacet[BuildingFormatConstants.DFacetOffsetZ1] = z1;

            // Flags ï¿½ bit 8 (0x0100) is set on all working cables in the original maps
            WriteU16(newFacet, BuildingFormatConstants.DFacetOffsetFlags, 0x0100);

            // StyleIndex = step_angle1 (repurposed, NOT a real style index)
            WriteU16(newFacet, BuildingFormatConstants.DFacetOffsetStyle, unchecked((ushort)step1));

            // Building = step_angle2 (repurposed, NOT a real building ID)
            WriteU16(newFacet, BuildingFormatConstants.DFacetOffsetBuilding, unchecked((ushort)step2));

            // Storey = 0 for cables
            WriteU16(newFacet, BuildingFormatConstants.DFacetOffsetStorey, 0);

            // FHeight
            newFacet[BuildingFormatConstants.DFacetOffsetFHeight] = fHeight;

            // Build new file with cable inserted at the correct position
            using var ms = new MemoryStream();

            // 1. Copy everything up to building block start
            ms.Write(bytes, 0, blockStart);

            // 2. Write building block header with updated NextDFacet
            var header = new byte[BuildingFormatConstants.HeaderSize];
            Buffer.BlockCopy(bytes, blockStart, header, 0, BuildingFormatConstants.HeaderSize);
            WriteU16(header, 4, (ushort)(oldNextFacet + 1));  // +1 facet
            // NextDStyle stays the same ï¿½ cables don't use dstyles
            ms.Write(header, 0, BuildingFormatConstants.HeaderSize);

            // 3. Write buildings with updated facet ranges
            for (int bldIdx = 0; bldIdx < totalBuildings; bldIdx++)
            {
                int srcOff = buildingsOff + bldIdx * BuildingFormatConstants.DBuildingSize;
                var bldBytes = new byte[BuildingFormatConstants.DBuildingSize];
                Buffer.BlockCopy(bytes, srcOff, bldBytes, 0, BuildingFormatConstants.DBuildingSize);

                int bldId1 = bldIdx + 1;
                ushort start = ReadU16(bldBytes, 0);
                ushort end = ReadU16(bldBytes, 2);

                if (bldId1 == buildingId1)
                {
                    // Target building ï¿½ expand EndFacet by 1
                    WriteU16(bldBytes, 2, (ushort)(end + 1));
                    Debug.WriteLine($"[CableAdder] Building #{bldId1} (target): EndFacet {end} -> {end + 1}");
                }
                else if (start >= insertPosition)
                {
                    // Building comes after insertion point ï¿½ shift its range
                    WriteU16(bldBytes, 0, (ushort)(start + 1));
                    WriteU16(bldBytes, 2, (ushort)(end + 1));
                    Debug.WriteLine($"[CableAdder] Building #{bldId1} (after): range ({start},{end}) -> ({start + 1},{end + 1})");
                }

                ms.Write(bldBytes, 0, BuildingFormatConstants.DBuildingSize);
            }

            // 4. Write padding between buildings and facets (copy original bytes)
            ms.Write(bytes, padOff, BuildingFormatConstants.AfterBuildingsPad);

            // 5. Write facets with the cable inserted at the correct position
            int facetsBefore = insertPosition - 1; // 0-based count of facets before insertion
            if (facetsBefore > 0)
            {
                ms.Write(bytes, facetsOff, facetsBefore * BuildingFormatConstants.DFacetSize);
            }

            // Insert the new cable facet
            ms.Write(newFacet, 0, BuildingFormatConstants.DFacetSize);

            // Write remaining facets after insertion point
            int facetsAfter = totalFacets - facetsBefore;
            if (facetsAfter > 0)
            {
                int afterSrcOff = facetsOff + facetsBefore * BuildingFormatConstants.DFacetSize;
                ms.Write(bytes, afterSrcOff, facetsAfter * BuildingFormatConstants.DFacetSize);
            }

            // 6. Copy everything after facets (dstyles, paint, storeys, walkables, etc.)
            int afterFacetsLen = bytes.Length - stylesOff;
            if (afterFacetsLen > 0)
            {
                ms.Write(bytes, stylesOff, afterFacetsLen);
            }

            var newBytes = ms.ToArray();

            // Validate
            int expectedSize = bytes.Length + BuildingFormatConstants.DFacetSize;
            if (newBytes.Length != expectedSize)
            {
                Debug.WriteLine($"[CableAdder] ERROR: Size mismatch! Expected {expectedSize}, got {newBytes.Length}");
                return (false, 0, $"File size mismatch: expected {expectedSize}, got {newBytes.Length}");
            }

            // The new facet's 1-based ID
            int newFacetId = insertPosition;

            Debug.WriteLine($"[CableAdder] Created cable facet #{newFacetId} within Building #{buildingId1}");
            Debug.WriteLine($"[CableAdder]   ({x0},{z0},{y0}) -> ({x1},{z1},{y1}), segments={segments}");
            Debug.WriteLine($"[CableAdder]   step1={step1} (0x{unchecked((ushort)step1):X4}), step2={step2} (0x{unchecked((ushort)step2):X4})");
            Debug.WriteLine($"[CableAdder]   flags=0x0100, fHeight={fHeight}");
            Debug.WriteLine($"[CableAdder]   Old file: {bytes.Length} bytes, new file: {newBytes.Length} bytes (+{BuildingFormatConstants.DFacetSize})");

            // Replace the file
            svc.ReplaceBytes(newBytes);

            // Notify
            BuildingsChangeBus.Instance.NotifyBuildingChanged(buildingId1, BuildingChangeType.Modified);

            return (true, newFacetId, null);
        }

        /// <summary>
        /// Calculates step angles for cable catenary curve.
        /// step1 is positive (curve at start), step2 is negative (curve at end).
        /// </summary>
        public static (short step1, short step2) CalculateStepAngles(double length, int segments)
        {
            if (segments <= 0) segments = 1;

            int baseStep = 1024 / segments;
            double lengthFactor = Math.Clamp(length / 1000.0, 0.5, 2.0);
            baseStep = (int)(baseStep * lengthFactor);
            baseStep = Math.Clamp(baseStep, 20, 150);

            short step1 = (short)baseStep;
            short step2 = (short)(-baseStep);

            return (step1, step2);
        }

        /// <summary>
        /// Calculates the optimal number of segments based on cable length.
        /// </summary>
        public static int CalculateSegmentCount(double length)
        {
            int segments = (int)(length / 150.0);
            return Math.Clamp(segments, 4, 24);
        }

        #region Helpers

        private static ushort ReadU16(byte[] b, int off) =>
            (ushort)(b[off] | (b[off + 1] << 8));

        private static void WriteU16(byte[] b, int off, ushort val)
        {
            b[off] = (byte)(val & 0xFF);
            b[off + 1] = (byte)((val >> 8) & 0xFF);
        }

        private static void WriteS16(byte[] b, int off, short val)
        {
            b[off] = (byte)(val & 0xFF);
            b[off + 1] = (byte)((val >> 8) & 0xFF);
        }

        #endregion
    }
}
