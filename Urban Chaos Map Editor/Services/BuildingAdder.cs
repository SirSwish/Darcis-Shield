// /Services/BuildingAdder.cs
// Helper class for adding buildings and facets to the map
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Services.DataServices;
using UrbanChaosMapEditor.Views.Dialogs.Buildings;

namespace UrbanChaosMapEditor.Services
{
    /// <summary>
    /// Handles adding new buildings and facets to the map.
    /// </summary>
    public sealed class BuildingAdder
    {
        private const int HeaderSize = 48;
        private const int DBuildingSize = 24;
        private const int AfterBuildingsPad = 14;
        private const int DFacetSize = 26;
        private const int DStyleSize = 2; // Each dstyles entry is a short (2 bytes)

        private readonly MapDataService _svc;

        public BuildingAdder(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        /// <summary>
        /// Adds a new empty building to the map.
        /// Returns the new building's 1-based ID, or -1 on failure.
        /// </summary>
        public int TryAddBuilding(BuildingType type)
        {
            if (!_svc.IsLoaded)
                return -1;

            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null)
                return -1;

            var bytes = _svc.GetBytesCopy();
            int blockStart = snap.StartOffset;
            int saveType = BitConverter.ToInt32(bytes, 0);

            // Read current header counters
            ushort oldNextBuilding = ReadU16(bytes, blockStart + 2);
            ushort oldNextFacet = ReadU16(bytes, blockStart + 4);

            // Calculate offsets
            int buildingsOff = blockStart + HeaderSize;
            int oldBuildingsEnd = buildingsOff + (oldNextBuilding - 1) * DBuildingSize;
            int padOff = oldBuildingsEnd;
            int facetsOff = padOff + AfterBuildingsPad;

            // The new building ID (1-based)
            int newBuildingId1 = oldNextBuilding;

            // Create new building record (24 bytes)
            // StartFacet = EndFacet = NextDFacet (empty range)
            var newBuilding = new byte[DBuildingSize];
            WriteU16(newBuilding, 0, oldNextFacet);  // StartFacet
            WriteU16(newBuilding, 2, oldNextFacet);  // EndFacet (same = empty)
            // WorldX at +4-7 = 0 (already zero)
            // WorldY at +8-10 = 0 (already zero)
            newBuilding[11] = (byte)type;             // Type (IMPORTANT: offset 11, not 21!)
            // WorldZ at +12-15 = 0 (already zero)
            WriteU16(newBuilding, 16, 0);            // Walkable = none
            newBuilding[18] = 0;                      // Counter0
            newBuilding[19] = 0;                      // Counter1
            // Bytes 20-21 are padding/unknown
            newBuilding[22] = 0;                      // Ware
            // Byte 23 is padding

            // Everything from the pad onwards stays the same, just shifted
            int afterBuildingsLen = bytes.Length - padOff;

            // Build new file
            using var ms = new System.IO.MemoryStream();

            // 1. Copy file header + tiles (everything up to building block header)
            ms.Write(bytes, 0, blockStart);

            // 2. Write building block header with updated NextDBuilding
            var header = new byte[HeaderSize];
            Buffer.BlockCopy(bytes, blockStart, header, 0, HeaderSize);
            WriteU16(header, 2, (ushort)(oldNextBuilding + 1)); // Increment NextDBuilding
            ms.Write(header, 0, HeaderSize);

            // 3. Write existing buildings
            if (oldNextBuilding > 1)
                ms.Write(bytes, buildingsOff, (oldNextBuilding - 1) * DBuildingSize);

            // 4. Write new building
            ms.Write(newBuilding, 0, DBuildingSize);

            // 5. Copy everything from pad onwards (pad, facets, styles, paint, storeys, indoors, walkables, objects, tail)
            ms.Write(bytes, padOff, afterBuildingsLen);

            var newBytes = ms.ToArray();

            Debug.WriteLine($"[BuildingAdder] Added building #{newBuildingId1} type={type} (byte value {(byte)type} at offset 11). " +
                           $"Old file: {bytes.Length} bytes, new file: {newBytes.Length} bytes " +
                           $"(should be +{DBuildingSize} bytes)");

            if (newBytes.Length != bytes.Length + DBuildingSize)
            {
                Debug.WriteLine($"[BuildingAdder] WARNING: Size mismatch! Expected {bytes.Length + DBuildingSize}, got {newBytes.Length}");
            }

            _svc.ReplaceBytes(newBytes);

            BuildingsChangeBus.Instance.NotifyBuildingChanged(newBuildingId1, BuildingChangeType.Added);
            BuildingsChangeBus.Instance.NotifyChanged();

            return newBuildingId1;
        }

        /// <summary>
        /// Calculates how many vertical bands (storey levels) a facet has.
        /// Each band consumes dstyles entries.
        /// We add +1 because the game's rendering loop increments style_index
        /// before the first draw, effectively needing one extra entry.
        /// </summary>
        private static int CalculateBands(byte height)
        {
            // From the C code: while (height >= 0) { height -= 4; }
            // The loop increments style_index on every iteration including the first (hf=0) no-draw pass
            // So for Height=4: 2 iterations = needs entries at [base] and [base+1] but draws 1 band
            // For Height=8: 3 iterations = needs entries at [base], [base+1], [base+2] but draws 2 bands
            // We need (height / 4) + 1 entries, or equivalently ceil((height+4)/4)
            if (height == 0) return 1;
            return (height / 4) + 1;
        }

        /// <summary>
        /// Calculates how many dstyles entries per band based on facet flags.
        /// </summary>
        private static int CalculateEntriesPerBand(FacetFlags flags)
        {
            // If 2TEXTURED or 2SIDED (and not HUG_FLOOR), use 2 entries per band
            bool has2Textured = (flags & FacetFlags.TwoTextured) != 0;
            bool has2Sided = (flags & FacetFlags.TwoSided) != 0;
            bool hasHugFloor = (flags & FacetFlags.HugFloor) != 0;

            if ((has2Textured || has2Sided) && !hasHugFloor)
                return 2;
            return 1;
        }

        /// <summary>
        /// Adds multiple facets to a building.
        /// Now correctly allocates dstyles[] entries for each facet.
        /// </summary>
        public AddFacetsResult TryAddFacets(int buildingId1, List<(byte x0, byte z0, byte x1, byte z1)> coords, FacetTemplate template)
        {
            if (!_svc.IsLoaded)
                return AddFacetsResult.Fail("No map loaded.");

            if (coords == null || coords.Count == 0)
                return AddFacetsResult.Fail("No facets to add.");

            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
                return AddFacetsResult.Fail($"Building #{buildingId1} not found.");

            var bytes = _svc.GetBytesCopy();
            int blockStart = snap.StartOffset;
            int saveType = BitConverter.ToInt32(bytes, 0);

            // Read current header counters
            ushort oldNextBuilding = ReadU16(bytes, blockStart + 2);
            ushort oldNextFacet = ReadU16(bytes, blockStart + 4);
            ushort oldNextStyle = ReadU16(bytes, blockStart + 6);
            ushort oldNextPaintMem = (saveType >= 17) ? ReadU16(bytes, blockStart + 8) : (ushort)0;
            ushort oldNextStorey = (saveType >= 17) ? ReadU16(bytes, blockStart + 10) : (ushort)0;

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] buildingId1={buildingId1}, coords.Count={coords.Count}");
            Debug.WriteLine($"[BuildingAdder.TryAddFacets] template: Type={template.Type}, Height={template.Height}, RawStyleId={template.RawStyleId}, Flags={template.Flags}");
            Debug.WriteLine($"[BuildingAdder.TryAddFacets] Header counters: NextBuilding={oldNextBuilding}, NextFacet={oldNextFacet}, NextStyle={oldNextStyle}");
            Debug.WriteLine($"[BuildingAdder.TryAddFacets] blockStart=0x{blockStart:X}, saveType={saveType}");

            // Calculate how many dstyles entries each facet needs
            int bandsPerFacet = CalculateBands(template.Height);
            int entriesPerBand = CalculateEntriesPerBand(template.Flags);
            int dstylesPerFacet = bandsPerFacet * entriesPerBand;
            int totalNewDStyles = coords.Count * dstylesPerFacet;

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] bands={bandsPerFacet}, entriesPerBand={entriesPerBand}, dstylesPerFacet={dstylesPerFacet}, totalNewDStyles={totalNewDStyles}");

            // Calculate offsets
            int buildingsOff = blockStart + HeaderSize;
            int padOff = buildingsOff + (oldNextBuilding - 1) * DBuildingSize;
            int facetsOff = padOff + AfterBuildingsPad;
            int stylesOff = facetsOff + (oldNextFacet - 1) * DFacetSize;
            int oldStylesSize = (oldNextStyle - 1) * DStyleSize;
            int afterStylesOff = stylesOff + oldStylesSize;

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] buildingsOff=0x{buildingsOff:X}, padOff=0x{padOff:X}, facetsOff=0x{facetsOff:X}, stylesOff=0x{stylesOff:X}");
            Debug.WriteLine($"[BuildingAdder.TryAddFacets] oldStylesSize={oldStylesSize}, afterStylesOff=0x{afterStylesOff:X}");

            // Get the target building's current facet range
            int buildingRecOff = buildingsOff + (buildingId1 - 1) * DBuildingSize;
            ushort oldStartFacet = ReadU16(bytes, buildingRecOff);
            ushort oldEndFacet = ReadU16(bytes, buildingRecOff + 2);

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] Building #{buildingId1}: StartFacet={oldStartFacet}, EndFacet={oldEndFacet}");

            // New facets will be inserted at the end of this building's range
            int insertPosition = oldEndFacet; // 1-based position where new facets start
            int facetCount = coords.Count;

            // Track which dstyles index each new facet will use
            // New dstyles are appended at the end of the existing dstyles array
            // nextStyleIndex tracks the 1-based "next available" slot
            ushort nextStyleIndex = oldNextStyle;

            // Create new facet records
            var newFacets = new List<byte[]>();
            var facetStyleIndices = new List<ushort>(); // Track the StyleIndex for each facet

            for (int i = 0; i < coords.Count; i++)
            {
                var (x0, z0, x1, z1) = coords[i];
                var facetBytes = new byte[DFacetSize];

                // This facet's StyleIndex points to where its dstyles entries start
                // IMPORTANT: StyleIndex in DFacet is 1-based (same as NextDStyle convention)
                // But we write to 0-based array position (nextStyleIndex - 1)
                // The facet stores the 1-based index, which the game converts to 0-based when reading
                ushort thisStyleIndex = nextStyleIndex;
                facetStyleIndices.Add(thisStyleIndex);

                facetBytes[0] = (byte)template.Type;     // FacetType
                facetBytes[1] = template.Height;          // Height
                facetBytes[2] = x0;                       // X0
                facetBytes[3] = x1;                       // X1
                WriteS16(facetBytes, 4, template.Y0);     // Y0
                WriteS16(facetBytes, 6, template.Y1);     // Y1
                facetBytes[8] = z0;                       // Z0
                facetBytes[9] = z1;                       // Z1
                WriteU16(facetBytes, 10, (ushort)template.Flags); // Flags
                WriteU16(facetBytes, 12, thisStyleIndex);         // StyleIndex - points into dstyles[]
                WriteU16(facetBytes, 14, (ushort)buildingId1);    // Building
                WriteU16(facetBytes, 16, (ushort)template.Storey); // DStorey
                facetBytes[18] = template.FHeight;        // FHeight
                facetBytes[19] = template.BlockHeight;    // BlockHeight
                facetBytes[20] = 0;                       // Open
                facetBytes[21] = 0;                       // Dfcache
                facetBytes[22] = 0;                       // Shake
                facetBytes[23] = 0;                       // CutHole
                facetBytes[24] = 0;                       // Counter0
                facetBytes[25] = 0;                       // Counter1

                newFacets.Add(facetBytes);

                // Advance nextStyleIndex for the next facet
                nextStyleIndex += (ushort)dstylesPerFacet;

                Debug.WriteLine($"[BuildingAdder.TryAddFacets] Created facet {i}: ({x0},{z0})->({x1},{z1}) StyleIndex={thisStyleIndex}");
            }

            // Create new dstyles entries
            // Each entry is a short containing the Raw Style ID
            var newDStyles = new byte[totalNewDStyles * DStyleSize];
            for (int i = 0; i < totalNewDStyles; i++)
            {
                // Write the Raw Style ID (positive value = direct TMA style)
                WriteS16(newDStyles, i * DStyleSize, (short)template.RawStyleId);
            }

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] Created {totalNewDStyles} dstyles entries, each with RawStyleId={template.RawStyleId}");

            // Build new file with inserted facets AND dstyles
            using var ms = new System.IO.MemoryStream();

            // 1. Copy everything up to building block header (file header + tiles)
            ms.Write(bytes, 0, blockStart);

            // 2. Write building block header with updated counters
            var header = new byte[HeaderSize];
            Buffer.BlockCopy(bytes, blockStart, header, 0, HeaderSize);
            WriteU16(header, 4, (ushort)(oldNextFacet + facetCount));      // Increment NextDFacet
            WriteU16(header, 6, (ushort)(oldNextStyle + totalNewDStyles)); // Increment NextDStyle
            ms.Write(header, 0, HeaderSize);

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] Updated header: NextDFacet {oldNextFacet} -> {oldNextFacet + facetCount}, NextDStyle {oldNextStyle} -> {oldNextStyle + totalNewDStyles}");

            // 3. Write buildings with updated facet ranges
            for (int bldIdx = 0; bldIdx < oldNextBuilding - 1; bldIdx++)
            {
                int srcOff = buildingsOff + bldIdx * DBuildingSize;
                var bldBytes = new byte[DBuildingSize];
                Buffer.BlockCopy(bytes, srcOff, bldBytes, 0, DBuildingSize);

                int bldId1 = bldIdx + 1;
                ushort start = ReadU16(bldBytes, 0);
                ushort end = ReadU16(bldBytes, 2);

                if (bldId1 == buildingId1)
                {
                    // This is our target building - expand its range
                    WriteU16(bldBytes, 2, (ushort)(end + facetCount));
                    Debug.WriteLine($"[BuildingAdder.TryAddFacets] Building #{bldId1} (target): EndFacet {end} -> {end + facetCount}");
                }
                else if (start >= insertPosition)
                {
                    // Building comes after insertion point - shift its range
                    WriteU16(bldBytes, 0, (ushort)(start + facetCount));
                    WriteU16(bldBytes, 2, (ushort)(end + facetCount));
                    Debug.WriteLine($"[BuildingAdder.TryAddFacets] Building #{bldId1} (after): range ({start},{end}) -> ({start + facetCount},{end + facetCount})");
                }

                ms.Write(bldBytes, 0, DBuildingSize);
            }

            // 4. Write pad
            ms.Write(bytes, padOff, AfterBuildingsPad);
            Debug.WriteLine($"[BuildingAdder.TryAddFacets] Copied {AfterBuildingsPad} padding bytes from offset 0x{padOff:X}");

            // 5. Write facets with insertions
            int facetsBefore = insertPosition - 1;
            if (facetsBefore > 0)
            {
                ms.Write(bytes, facetsOff, facetsBefore * DFacetSize);
                Debug.WriteLine($"[BuildingAdder.TryAddFacets] Wrote {facetsBefore} facets before insertion point");
            }

            // New facets
            foreach (var fb in newFacets)
                ms.Write(fb, 0, DFacetSize);
            Debug.WriteLine($"[BuildingAdder.TryAddFacets] Wrote {newFacets.Count} new facets");

            // Facets after insertion point
            int facetsAfter = oldNextFacet - 1 - facetsBefore;
            if (facetsAfter > 0)
            {
                int afterSrcOff = facetsOff + facetsBefore * DFacetSize;
                ms.Write(bytes, afterSrcOff, facetsAfter * DFacetSize);
                Debug.WriteLine($"[BuildingAdder.TryAddFacets] Wrote {facetsAfter} facets after insertion point");
            }

            // 6. Write existing dstyles
            if (oldStylesSize > 0)
            {
                ms.Write(bytes, stylesOff, oldStylesSize);
                Debug.WriteLine($"[BuildingAdder.TryAddFacets] Wrote {oldStylesSize} bytes of existing dstyles");
            }

            // 7. Write new dstyles (appended at the end)
            ms.Write(newDStyles, 0, newDStyles.Length);
            Debug.WriteLine($"[BuildingAdder.TryAddFacets] Wrote {newDStyles.Length} bytes of new dstyles");

            // 8. Copy everything after dstyles (paint, storeys, indoors, walkables, objects, tail)
            int afterDStylesLen = bytes.Length - afterStylesOff;
            if (afterDStylesLen > 0)
            {
                ms.Write(bytes, afterStylesOff, afterDStylesLen);
                Debug.WriteLine($"[BuildingAdder.TryAddFacets] Copied {afterDStylesLen} bytes after dstyles (paint, storeys, etc.)");
            }

            var newBytes = ms.ToArray();

            int expectedSize = bytes.Length + (facetCount * DFacetSize) + (totalNewDStyles * DStyleSize);
            Debug.WriteLine($"[BuildingAdder.TryAddFacets] Old file: {bytes.Length} bytes, new file: {newBytes.Length} bytes, expected: {expectedSize}");

            if (newBytes.Length != expectedSize)
            {
                Debug.WriteLine($"[BuildingAdder.TryAddFacets] ERROR: Size mismatch! Expected {expectedSize}, got {newBytes.Length}");
                return AddFacetsResult.Fail($"File size mismatch: expected {expectedSize}, got {newBytes.Length}");
            }

            _svc.ReplaceBytes(newBytes);

            BuildingsChangeBus.Instance.NotifyBuildingChanged(buildingId1, BuildingChangeType.Modified);
            BuildingsChangeBus.Instance.NotifyChanged();

            Debug.WriteLine($"[BuildingAdder] Successfully added {facetCount} facets (with {totalNewDStyles} dstyles entries) to building #{buildingId1}");

            return AddFacetsResult.Success(facetCount);
        }

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

        private static void WriteU16ToStream(System.IO.Stream s, ushort val)
        {
            s.WriteByte((byte)(val & 0xFF));
            s.WriteByte((byte)((val >> 8) & 0xFF));
        }
    }

    public sealed class AddFacetsResult
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public int FacetsAdded { get; }

        private AddFacetsResult(bool success, string? error, int count)
        {
            IsSuccess = success;
            ErrorMessage = error;
            FacetsAdded = count;
        }

        public static AddFacetsResult Success(int count) => new(true, null, count);
        public static AddFacetsResult Fail(string error) => new(false, error, 0);
    }
}