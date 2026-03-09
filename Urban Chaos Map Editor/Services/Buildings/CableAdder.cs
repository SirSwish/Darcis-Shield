// /Services/CableAdder.cs
using System.Diagnostics;
using System.IO;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Buildings
{
    /// <summary>
    /// Service for adding new cable facets to the map.
    /// Based on the original create_cable_dfacet() logic.
    /// 
    /// IMPORTANT: Cables are added at the END of the facets array (after all building facets).
    /// This requires rebuilding the file to insert the new facet bytes and shift everything after.
    /// </summary>
    public static class CableAdder
    {
        // DFacet byte offsets (26 bytes total)
        private const int OFF_TYPE = 0;
        private const int OFF_HEIGHT = 1;      // segments for cables
        private const int OFF_X0 = 2;
        private const int OFF_X1 = 3;
        private const int OFF_Y0 = 4;          // 2 bytes, signed
        private const int OFF_Y1 = 6;          // 2 bytes, signed
        private const int OFF_Z0 = 8;
        private const int OFF_Z1 = 9;
        private const int OFF_FLAGS = 10;      // 2 bytes
        private const int OFF_STYLE = 12;      // 2 bytes (step_angle1 for cables)
        private const int OFF_BUILDING = 14;   // 2 bytes (step_angle2 for cables)
        private const int OFF_STOREY = 16;     // 2 bytes
        private const int OFF_FHEIGHT = 18;    // mode for cables

        private const int DFACET_SIZE = 26;
        private const int HEADER_SIZE = 48;
        private const int DBUILDING_SIZE = 24;
        private const int AFTER_BUILDINGS_PAD = 14;

        /// <summary>
        /// Attempts to add a new cable facet to the map.
        /// </summary>
        /// <param name="x0">Start X coordinate (0-127 building units)</param>
        /// <param name="z0">Start Z coordinate (0-127 building units)</param>
        /// <param name="y0">Start Y world coordinate</param>
        /// <param name="x1">End X coordinate (0-127 building units)</param>
        /// <param name="z1">End Z coordinate (0-127 building units)</param>
        /// <param name="y1">End Y world coordinate</param>
        /// <param name="segments">Number of cable segments (auto-calculated if 0)</param>
        /// <param name="stepAngle1">First step angle (auto-calculated if null)</param>
        /// <param name="stepAngle2">Second step angle (auto-calculated if null)</param>
        /// <param name="fHeight">Texture style mode (default 2)</param>
        /// <returns>Tuple of (success, newFacetId, errorMessage)</returns>
        public static (bool Success, int NewFacetId, string? Error) TryAddCable(
            byte x0, byte z0, short y0,
            byte x1, byte z1, short y1,
            int segments = 0,
            short? stepAngle1 = null,
            short? stepAngle2 = null,
            byte fHeight = 2)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
                return (false, 0, "No map loaded");

            // Use BuildingsAccessor to get a proper snapshot
            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            if (snap.StartOffset < 0)
                return (false, 0, "Could not find building region");

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

            // Calculate offsets
            int buildingsOff = blockStart + HEADER_SIZE;
            int totalBuildings = Math.Max(0, oldNextBuilding - 1);
            int facetsOff = buildingsOff + totalBuildings * DBUILDING_SIZE + AFTER_BUILDINGS_PAD;
            int totalFacets = Math.Max(0, oldNextFacet - 1);
            int stylesOff = facetsOff + totalFacets * DFACET_SIZE;

            // The new facet ID (1-based) - cables go at the end
            int newFacetId = oldNextFacet;

            // Create the new cable facet record (26 bytes)
            var newFacet = new byte[DFACET_SIZE];

            newFacet[OFF_TYPE] = (byte)FacetType.Cable; // 9
            newFacet[OFF_HEIGHT] = (byte)segments;
            newFacet[OFF_X0] = x0;
            newFacet[OFF_X1] = x1;

            // Y0 and Y1 are signed 16-bit little-endian
            WriteS16(newFacet, OFF_Y0, y0);
            WriteS16(newFacet, OFF_Y1, y1);

            newFacet[OFF_Z0] = z0;
            newFacet[OFF_Z1] = z1;

            // Flags - MUST be Unclimbable (0x0100 = 256) for cables to render correctly
            WriteU16(newFacet, OFF_FLAGS, (ushort)FacetFlags.Unclimbable);

            // StyleIndex = step_angle1 (as unsigned representation of signed value)
            WriteU16(newFacet, OFF_STYLE, unchecked((ushort)step1));

            // Building = step_angle2 (as unsigned representation of signed value)
            WriteU16(newFacet, OFF_BUILDING, unchecked((ushort)step2));

            // Storey = 0 for cables
            WriteU16(newFacet, OFF_STOREY, 0);

            // FHeight = mode/texture style
            newFacet[OFF_FHEIGHT] = fHeight;

            // Remaining bytes (19-25) are already 0

            // Build the new file with the cable inserted at the end of facets
            using var ms = new MemoryStream();

            // 1. Copy everything up to and including the building block header
            ms.Write(bytes, 0, blockStart + HEADER_SIZE);

            // 2. Go back and update the header's NextDFacet counter
            ms.Position = blockStart + 4;
            WriteU16ToStream(ms, (ushort)(oldNextFacet + 1));
            ms.Position = ms.Length;

            // 3. Write existing buildings (unchanged)
            if (totalBuildings > 0)
            {
                ms.Write(bytes, buildingsOff, totalBuildings * DBUILDING_SIZE);
            }

            // 4. Write the padding between buildings and facets
            ms.Write(bytes, buildingsOff + totalBuildings * DBUILDING_SIZE, AFTER_BUILDINGS_PAD);

            // 5. Write existing facets (unchanged)
            if (totalFacets > 0)
            {
                ms.Write(bytes, facetsOff, totalFacets * DFACET_SIZE);
            }

            // 6. Write the NEW cable facet at the end
            ms.Write(newFacet, 0, DFACET_SIZE);

            // 7. Copy everything AFTER the old facets (styles, paint, storeys, indoors, walkables, objects, tail)
            int afterFacetsLen = bytes.Length - stylesOff;
            if (afterFacetsLen > 0)
            {
                ms.Write(bytes, stylesOff, afterFacetsLen);
            }

            var newBytes = ms.ToArray();

            // Validate the new file size
            int expectedSize = bytes.Length + DFACET_SIZE;
            if (newBytes.Length != expectedSize)
            {
                Debug.WriteLine($"[CableAdder] ERROR: Size mismatch! Expected {expectedSize}, got {newBytes.Length}");
                return (false, 0, $"File size mismatch: expected {expectedSize}, got {newBytes.Length}");
            }

            Debug.WriteLine($"[CableAdder] Created cable facet #{newFacetId} at end of facets array");
            Debug.WriteLine($"[CableAdder]   ({x0},{z0},{y0}) -> ({x1},{z1},{y1}), segments={segments}");
            Debug.WriteLine($"[CableAdder]   step1={step1} (0x{unchecked((ushort)step1):X4}), step2={step2} (0x{unchecked((ushort)step2):X4})");
            Debug.WriteLine($"[CableAdder]   flags=0x{(ushort)FacetFlags.Unclimbable:X4}");
            Debug.WriteLine($"[CableAdder]   Old file: {bytes.Length} bytes, new file: {newBytes.Length} bytes (+{DFACET_SIZE})");

            // Replace the entire file
            svc.ReplaceBytes(newBytes);

            // Notify change bus
            BuildingsChangeBus.Instance.NotifyChanged();

            return (true, newFacetId, null);
        }

        /// <summary>
        /// Calculates step angles for cable catenary curve based on the original game algorithm.
        /// step1 is positive (controls curve at start), step2 is negative (controls curve at end).
        /// The magnitudes should be roughly equal for a symmetric catenary.
        /// </summary>
        public static (short step1, short step2) CalculateStepAngles(double length, int segments)
        {
            if (segments <= 0) segments = 1;

            // Based on reverse-engineering the original create_cable_dfacet:
            // The step angles control the "sag" of the cable catenary curve.
            // step1 is positive (angle at start), step2 is NEGATIVE (angle at end)
            // This creates the characteristic "hanging" catenary shape.

            // From game data analysis:
            // Working cable: segments=15, step1=48 (0x0030), step2=-48 (0xFFD0)
            // Formula: step1 ˜ 1024 / segments, step2 = -step1

            // Calculate base step from segments
            int baseStep = 1024 / segments;

            // Adjust based on cable length - longer cables need more sag
            double lengthFactor = Math.Clamp(length / 1000.0, 0.5, 2.0);
            baseStep = (int)(baseStep * lengthFactor);

            // Clamp to reasonable range (based on game data, values are typically 20-100)
            baseStep = Math.Clamp(baseStep, 20, 150);

            // step1 is positive, step2 is NEGATIVE (this is critical!)
            short step1 = (short)baseStep;
            short step2 = (short)(-baseStep);

            return (step1, step2);
        }

        /// <summary>
        /// Calculates the optimal number of segments based on cable length.
        /// </summary>
        public static int CalculateSegmentCount(double length)
        {
            // Based on analysis of game data:
            // Short cables (~500 units): 4-6 segments
            // Medium cables (~1000 units): 8-10 segments
            // Long cables (~2000+ units): 12-16 segments

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

        private static void WriteU16ToStream(Stream s, ushort val)
        {
            s.WriteByte((byte)(val & 0xFF));
            s.WriteByte((byte)((val >> 8) & 0xFF));
        }

        #endregion
    }
}