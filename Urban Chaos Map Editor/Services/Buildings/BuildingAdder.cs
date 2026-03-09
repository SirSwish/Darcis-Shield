// /Services/BuildingAdder.cs
// Helper class for adding buildings and facets to the map.
//
// KEY CHANGES from previous version:
// 1. Fixed dstyles size calculation: nextStyle * 2 (was (nextStyle-1) * 2, off by 2 bytes)
// 2. Support painted textures at creation time via DStorey + paint_mem
// 3. Warehouse buildings auto-create interior facets (reversed, FACET_FLAG_INSIDE)
//    with dual dstyle entries per wall (outside + inside), matching the C engine
// 4. Correctly inserts into all three arrays: dstyles, paint_mem, dstoreys
//
// File layout within buildings block:
//   Header(48) ? Buildings[] ? Pad(14) ? Facets[] ? dstyles[] ? paint_mem[] ? dstoreys[] ? ...
//
// Array sizes in file (each includes unused slot 0):
//   dstyles:   nextStyle   entries × 2 bytes
//   paint_mem: nextPaintMem bytes
//   dstoreys:  nextStorey  entries × 6 bytes

using System.Diagnostics;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using System.IO;

namespace UrbanChaosMapEditor.Services.Buildings
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
        private const int DStyleSize = 2;       // Each dstyles entry is a signed short
        private const int DStoreySize = 6;      // U16 Style + U16 PaintIndex + S8 Count + U8 Padding

        private readonly MapDataService _svc;

        public BuildingAdder(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        #region Add Building

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

            // Read current header counters
            ushort oldNextBuilding = ReadU16(bytes, blockStart + 2);
            ushort oldNextFacet = ReadU16(bytes, blockStart + 4);

            // Calculate offsets
            int buildingsOff = blockStart + HeaderSize;
            int oldBuildingsEnd = buildingsOff + (oldNextBuilding - 1) * DBuildingSize;
            int padOff = oldBuildingsEnd;

            // The new building ID (1-based)
            int newBuildingId1 = oldNextBuilding;

            // Create new building record (24 bytes)
            var newBuilding = new byte[DBuildingSize];
            WriteU16(newBuilding, 0, oldNextFacet);   // StartFacet
            WriteU16(newBuilding, 2, oldNextFacet);   // EndFacet (same = empty)
            newBuilding[11] = (byte)type;              // Type
            WriteU16(newBuilding, 16, 0);              // Walkable = none

            // Everything from the pad onwards stays the same, just shifted
            int afterBuildingsLen = bytes.Length - padOff;

            using var ms = new MemoryStream();

            // 1. Copy file header + tiles (everything up to building block header)
            ms.Write(bytes, 0, blockStart);

            // 2. Write building block header with updated NextDBuilding
            var header = new byte[HeaderSize];
            Buffer.BlockCopy(bytes, blockStart, header, 0, HeaderSize);
            WriteU16(header, 2, (ushort)(oldNextBuilding + 1));
            ms.Write(header, 0, HeaderSize);

            // 3. Write existing buildings
            if (oldNextBuilding > 1)
                ms.Write(bytes, buildingsOff, (oldNextBuilding - 1) * DBuildingSize);

            // 4. Write new building
            ms.Write(newBuilding, 0, DBuildingSize);

            // 5. Copy everything from pad onwards
            ms.Write(bytes, padOff, afterBuildingsLen);

            var newBytes = ms.ToArray();

            Debug.WriteLine($"[BuildingAdder] Added building #{newBuildingId1} type={type}. " +
                           $"Old: {bytes.Length} bytes, new: {newBytes.Length} bytes (+{DBuildingSize})");

            if (newBytes.Length != bytes.Length + DBuildingSize)
            {
                Debug.WriteLine($"[BuildingAdder] WARNING: Size mismatch! Expected {bytes.Length + DBuildingSize}, got {newBytes.Length}");
            }

            _svc.ReplaceBytes(newBytes);

            BuildingsChangeBus.Instance.NotifyBuildingChanged(newBuildingId1, BuildingChangeType.Added);
            BuildingsChangeBus.Instance.NotifyChanged();

            return newBuildingId1;
        }

        #endregion

        #region Add Facets

        /// <summary>
        /// Calculates how many vertical bands (storey levels) a facet has.
        /// Each band consumes dstyles entries.
        /// From the C code: while (height >= 0) { height -= 4; }
        /// For Height=4: needs entries at [base] and [base+1]
        /// </summary>
        private static int CalculateBands(byte height)
        {
            if (height == 0) return 1;
            return (height / 4) + 1;
        }

        /// <summary>
        /// Calculates how many dstyles entries per band based on facet flags.
        /// If 2TEXTURED or 2SIDED (and not HUG_FLOOR), use 2 entries per band.
        /// </summary>
        private static int CalculateEntriesPerBand(FacetFlags flags)
        {
            bool has2Textured = (flags & FacetFlags.TwoTextured) != 0;
            bool has2Sided = (flags & FacetFlags.TwoSided) != 0;
            bool hasHugFloor = (flags & FacetFlags.HugFloor) != 0;

            if ((has2Textured || has2Sided) && !hasHugFloor)
                return 2;
            return 1;
        }

        /// <summary>
        /// Adds multiple facets to a building.
        /// Correctly allocates dstyles[], DStorey[], and paint_mem[] entries.
        /// For warehouse buildings, automatically creates reversed interior facets.
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
            ushort oldNextPaintMem = (saveType >= 17) ? ReadU16(bytes, blockStart + 8) : (ushort)1;
            ushort oldNextStorey = (saveType >= 17) ? ReadU16(bytes, blockStart + 10) : (ushort)1;

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] buildingId1={buildingId1}, coords={coords.Count}");
            Debug.WriteLine($"[BuildingAdder.TryAddFacets] template: Type={template.Type}, Height={template.Height}, RawStyleId={template.RawStyleId}");
            Debug.WriteLine($"[BuildingAdder.TryAddFacets] Header: NextBuilding={oldNextBuilding}, NextFacet={oldNextFacet}, NextStyle={oldNextStyle}, NextPaintMem={oldNextPaintMem}, NextStorey={oldNextStorey}");

            // Determine if this is a warehouse building
            int bldRecOff = blockStart + HeaderSize + (buildingId1 - 1) * DBuildingSize;
            byte buildingType = bytes[bldRecOff + 11];
            bool isWarehouse = (buildingType == (byte)BuildingType.Warehouse);

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] BuildingType={buildingType} isWarehouse={isWarehouse}");

            // Calculate bands and entries per facet
            int bandsPerFacet = CalculateBands(template.Height);
            int entriesPerBand = CalculateEntriesPerBand(template.Flags);
            int dstylesPerFacet = bandsPerFacet * entriesPerBand;

            // For warehouses: each wall gets +1 extra dstyle for interior face per band
            // (the C code does: dstyles[next_dstyle++] = TextureStyle2 for each wall of a warehouse)
            int warehouseExtraDStylesPerFacet = isWarehouse ? bandsPerFacet : 0;
            int totalDStylesPerWall = dstylesPerFacet + warehouseExtraDStylesPerFacet;

            // Determine if we're creating painted textures
            bool hasPaint = template.PaintBytes != null && template.PaintBytes.Length > 0;
            bool hasInteriorPaint = isWarehouse && template.InteriorPaintBytes != null && template.InteriorPaintBytes.Length > 0;

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] bands={bandsPerFacet}, entriesPerBand={entriesPerBand}, " +
                           $"dstylesPerFacet={dstylesPerFacet}, warehouseExtra={warehouseExtraDStylesPerFacet}, " +
                           $"hasPaint={hasPaint}, hasInteriorPaint={hasInteriorPaint}");

            int facetCount = coords.Count;
            int interiorFacetCount = isWarehouse ? facetCount : 0;
            int totalNewFacets = facetCount + interiorFacetCount;
            int totalNewDStyles = coords.Count * totalDStylesPerWall;

            // Calculate how many DStorey + paint_mem entries we need
            // Each painted band needs one DStorey entry + N paint bytes (N = columns per facet)
            int paintBandsPerFacet = hasPaint ? bandsPerFacet : 0;
            int interiorPaintBandsPerFacet = hasInteriorPaint ? bandsPerFacet : 0;
            int totalNewStoreys = coords.Count * (paintBandsPerFacet + interiorPaintBandsPerFacet);

            // For paint_mem, each painted band stores one byte per column
            // Columns = wall width in grid units + 1 (from the C code: count = panelsAcross + 1)
            // We'll use the paint bytes as provided in the template
            int paintBytesPerBand = hasPaint ? template.PaintBytes!.Length : 0;
            int interiorPaintBytesPerBand = hasInteriorPaint ? template.InteriorPaintBytes!.Length : 0;
            int totalNewPaintBytes = coords.Count * (paintBandsPerFacet * paintBytesPerBand + interiorPaintBandsPerFacet * interiorPaintBytesPerBand);

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] totalNewFacets={totalNewFacets}, totalNewDStyles={totalNewDStyles}, totalNewStoreys={totalNewStoreys}, totalNewPaintBytes={totalNewPaintBytes}");

            // Calculate file offsets
            // IMPORTANT: dstyles size = nextStyle * 2 (includes slot 0)
            int buildingsOff = blockStart + HeaderSize;
            int padOff = buildingsOff + (oldNextBuilding - 1) * DBuildingSize;
            int facetsOff = padOff + AfterBuildingsPad;
            int stylesOff = facetsOff + (oldNextFacet - 1) * DFacetSize;
            int existingStylesSize = oldNextStyle * DStyleSize;
            int paintMemOff = stylesOff + existingStylesSize;
            int existingPaintSize = oldNextPaintMem;
            int storeysOff = paintMemOff + existingPaintSize;
            int existingStoreysSize = oldNextStorey * DStoreySize;
            int afterStoreysOff = storeysOff + existingStoreysSize;

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] stylesOff=0x{stylesOff:X}, paintMemOff=0x{paintMemOff:X}, storeysOff=0x{storeysOff:X}, afterStoreysOff=0x{afterStoreysOff:X}");

            // Get the target building's current facet range
            ushort oldStartFacet = ReadU16(bytes, bldRecOff);
            ushort oldEndFacet = ReadU16(bytes, bldRecOff + 2);
            int insertPosition = oldEndFacet; // 1-based position where new facets start

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] Building #{buildingId1}: StartFacet={oldStartFacet}, EndFacet={oldEndFacet}");

            // Track allocation cursors
            ushort nextStyleCursor = oldNextStyle;         // next available dstyles index
            ushort nextPaintMemCursor = oldNextPaintMem;   // next available paint_mem offset
            ushort nextStoreyCursor = oldNextStorey;       // next available DStorey index

            // Build new data: facets, dstyles, paint_mem, dstoreys
            var newFacetList = new List<byte[]>();
            var newDStyleValues = new List<short>();         // in order, appended after existing
            var newPaintMemBytes = new List<byte>();         // in order, appended after existing
            var newStoreyEntries = new List<byte[]>();       // in order, appended after existing

            for (int i = 0; i < coords.Count; i++)
            {
                var (x0, z0, x1, z1) = coords[i];

                // --- Allocate dstyles for this facet's outside face ---
                ushort outsideStyleIndex = nextStyleCursor;

                for (int band = 0; band < bandsPerFacet; band++)
                {
                    short dstyleValue;

                    if (hasPaint)
                    {
                        // Create DStorey + paint_mem for this band
                        ushort paintIndex = nextPaintMemCursor;
                        int storeyId = nextStoreyCursor;

                        // Write paint bytes for this band
                        for (int col = 0; col < paintBytesPerBand; col++)
                            newPaintMemBytes.Add(template.PaintBytes![col]);
                        nextPaintMemCursor += (ushort)paintBytesPerBand;

                        // Write DStorey entry
                        var storey = new byte[DStoreySize];
                        WriteU16(storey, 0, template.RawStyleId);           // Style (base/fallback)
                        WriteU16(storey, 2, paintIndex);                     // PaintIndex
                        storey[4] = unchecked((byte)(sbyte)paintBytesPerBand); // Count
                        storey[5] = 0;                                        // Padding
                        newStoreyEntries.Add(storey);
                        nextStoreyCursor++;

                        // dstyle = negative storey reference
                        dstyleValue = (short)(-storeyId);
                    }
                    else
                    {
                        // Positive raw style
                        dstyleValue = (short)template.RawStyleId;
                    }

                    newDStyleValues.Add(dstyleValue);

                    // If 2-sided/2-textured, add the second entry per band
                    for (int extra = 1; extra < entriesPerBand; extra++)
                    {
                        newDStyleValues.Add(dstyleValue); // same style for second side
                    }
                }

                // --- For warehouses: allocate interior dstyles ---
                ushort interiorStyleIndex = 0;
                if (isWarehouse)
                {
                    interiorStyleIndex = nextStyleCursor;
                    // The interior style is at outsideStyleIndex + 1 for each band
                    // But we've already allocated the outside styles above.
                    // Actually, for the C code pattern: outside and inside are consecutive:
                    //   dstyles[next_dstyle++] = TextureStyle;    // outside
                    //   dstyles[next_dstyle++] = TextureStyle2;   // inside (warehouse only)
                    // And the interior facet uses style_index+1.
                    //
                    // We already added outside styles. Now add interior styles.
                    interiorStyleIndex = (ushort)(outsideStyleIndex + entriesPerBand);
                    // Wait - the C pattern interleaves them per band. Let me reconsider.
                    //
                    // Actually looking at the C code more carefully:
                    // For each wall segment, the pattern is:
                    //   style_index = next_dstyle;
                    //   dstyles[next_dstyle++] = TextureStyle;    // outside (index = style_index)
                    //   if warehouse: dstyles[next_dstyle++] = TextureStyle2;  // inside (index = style_index+1)
                    //   facet outside uses: style_index
                    //   facet inside uses: style_index+1
                    //
                    // But for multi-band walls with connect_wall, each connect adds more dstyles.
                    // For a simple single-storey wall (no connect), it's just:
                    //   [outside_style, inside_style] then facets ref [style_index, style_index+1]
                    //
                    // However, we already wrote all outside band styles above.
                    // For the warehouse pattern, the interior dstyle needs to be at
                    // outsideStyleIndex + 1 (for the first band).
                    // But we wrote bandsPerFacet * entriesPerBand outside entries...
                    //
                    // Let me re-think the allocation order to match the C pattern.
                    // The C code doesn't have multi-band walls in the same way we do.
                    // In the C code, connect_wall stacks multiple storeys vertically,
                    // each with its own style_index pair.
                    //
                    // For our simplified approach: we'll add the interior dstyles right after
                    // each outside dstyle, making pairs [outside, inside] per band.

                    // PROBLEM: We already pushed outside entries. Let me redo the approach.
                }

                // OK, let me restart the dstyle allocation with the correct interleaving.
                // For cleanliness, I'll clear what we just did and redo it properly.
                // Actually, let me restructure this loop to handle it correctly from the start.

                // ---- Actually, we need to rethink. Let me break and redo. ----
                // The issue is that for warehouses, the dstyles pattern is:
                //   [outside_band0, inside_band0, outside_band1, inside_band1, ...]
                // And the outside facet references style_index (the first entry),
                // while the inside facet references style_index+1.
                //
                // But for non-warehouses, it's just:
                //   [band0, band1, band2, ...]
                //
                // So I need to build the dstyle list differently.
                // Let me just restart this method with a cleaner approach.
                break; // Will redo below
            }

            // --- CLEAN REDO of facet + dstyle allocation ---
            newFacetList.Clear();
            newDStyleValues.Clear();
            newPaintMemBytes.Clear();
            newStoreyEntries.Clear();
            nextStyleCursor = oldNextStyle;
            nextPaintMemCursor = oldNextPaintMem;
            nextStoreyCursor = oldNextStorey;

            for (int i = 0; i < coords.Count; i++)
            {
                var (x0, z0, x1, z1) = coords[i];

                // The StyleIndex that the outside facet will reference
                ushort outsideStyleIndex = nextStyleCursor;

                // Build dstyles for this wall segment (interleaved for warehouses)
                for (int band = 0; band < bandsPerFacet; band++)
                {
                    // --- Outside dstyle entry ---
                    short outsideDStyleValue = AllocateDStyleValue(
                        template.RawStyleId, hasPaint, template.PaintBytes,
                        ref nextPaintMemCursor, ref nextStoreyCursor,
                        newPaintMemBytes, newStoreyEntries);
                    newDStyleValues.Add(outsideDStyleValue);

                    // If 2-sided/2-textured, add second entry per band for outside
                    for (int extra = 1; extra < entriesPerBand; extra++)
                        newDStyleValues.Add(outsideDStyleValue);

                    // --- Warehouse interior dstyle entry ---
                    if (isWarehouse)
                    {
                        ushort interiorRawStyle = template.InteriorStyleId > 0
                            ? template.InteriorStyleId
                            : template.RawStyleId;

                        short insideDStyleValue = AllocateDStyleValue(
                            interiorRawStyle, hasInteriorPaint, template.InteriorPaintBytes,
                            ref nextPaintMemCursor, ref nextStoreyCursor,
                            newPaintMemBytes, newStoreyEntries);
                        newDStyleValues.Add(insideDStyleValue);
                    }

                    nextStyleCursor = (ushort)(oldNextStyle + newDStyleValues.Count);
                }

                // Create outside facet
                var outsideFacet = MakeFacetBytes(x0, z0, x1, z1, template, outsideStyleIndex, (ushort)buildingId1);
                newFacetList.Add(outsideFacet);

                Debug.WriteLine($"[BuildingAdder] Facet {i} outside: ({x0},{z0})->({x1},{z1}) StyleIndex={outsideStyleIndex}");

                // For warehouses: create reversed interior facet
                if (isWarehouse)
                {
                    // Interior style is at outsideStyleIndex + entriesPerBand (after the outside entries for band 0)
                    ushort interiorStyleIndex = (ushort)(outsideStyleIndex + entriesPerBand);

                    // Reversed coordinates, with FACET_FLAG_INSIDE
                    var interiorTemplate = new FacetTemplate
                    {
                        Type = template.Type,
                        Height = template.Height,
                        FHeight = template.FHeight,
                        BlockHeight = template.BlockHeight,
                        Y0 = template.Y0,
                        Y1 = template.Y1,
                        RawStyleId = template.InteriorStyleId > 0 ? template.InteriorStyleId : template.RawStyleId,
                        Flags = template.Flags | FacetFlags.Inside,
                        BuildingId1 = template.BuildingId1,
                        Storey = template.Storey,
                    };

                    var interiorFacet = MakeFacetBytes(x1, z1, x0, z0, interiorTemplate, interiorStyleIndex, (ushort)buildingId1);
                    newFacetList.Add(interiorFacet);

                    Debug.WriteLine($"[BuildingAdder] Facet {i} interior: ({x1},{z1})->({x0},{z0}) StyleIndex={interiorStyleIndex}");
                }
            }

            // Recalculate totals based on what we actually allocated
            totalNewFacets = newFacetList.Count;
            totalNewDStyles = newDStyleValues.Count;
            totalNewPaintBytes = newPaintMemBytes.Count;
            int totalNewStoreysCount = newStoreyEntries.Count;

            Debug.WriteLine($"[BuildingAdder] FINAL: {totalNewFacets} facets, {totalNewDStyles} dstyles, {totalNewStoreysCount} storeys, {totalNewPaintBytes} paint bytes");

            // === Build new file ===
            using var ms2 = new MemoryStream();

            // 1. Copy everything up to building block header
            ms2.Write(bytes, 0, blockStart);

            // 2. Write updated header
            var hdr = new byte[HeaderSize];
            Buffer.BlockCopy(bytes, blockStart, hdr, 0, HeaderSize);
            WriteU16(hdr, 4, (ushort)(oldNextFacet + totalNewFacets));
            WriteU16(hdr, 6, (ushort)(oldNextStyle + totalNewDStyles));
            if (saveType >= 17)
            {
                WriteU16(hdr, 8, (ushort)(oldNextPaintMem + totalNewPaintBytes));
                WriteU16(hdr, 10, (ushort)(oldNextStorey + totalNewStoreysCount));
            }
            ms2.Write(hdr, 0, HeaderSize);

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
                    WriteU16(bldBytes, 2, (ushort)(end + totalNewFacets));
                    Debug.WriteLine($"[BuildingAdder] Building #{bldId1} (target): EndFacet {end} -> {end + totalNewFacets}");
                }
                else if (start >= insertPosition)
                {
                    WriteU16(bldBytes, 0, (ushort)(start + totalNewFacets));
                    WriteU16(bldBytes, 2, (ushort)(end + totalNewFacets));
                }

                ms2.Write(bldBytes, 0, DBuildingSize);
            }

            // 4. Write pad
            ms2.Write(bytes, padOff, AfterBuildingsPad);

            // 5. Write facets (before + new + after)
            int facetsBefore = insertPosition - 1;
            if (facetsBefore > 0)
                ms2.Write(bytes, facetsOff, facetsBefore * DFacetSize);

            foreach (var fb in newFacetList)
                ms2.Write(fb, 0, DFacetSize);

            int facetsAfter = oldNextFacet - 1 - facetsBefore;
            if (facetsAfter > 0)
                ms2.Write(bytes, facetsOff + facetsBefore * DFacetSize, facetsAfter * DFacetSize);

            // 6. Write existing dstyles (CORRECT: nextStyle * 2 bytes, not (nextStyle-1) * 2)
            if (existingStylesSize > 0)
                ms2.Write(bytes, stylesOff, existingStylesSize);

            // 7. Write new dstyles (appended at end)
            foreach (short val in newDStyleValues)
            {
                ms2.WriteByte((byte)(val & 0xFF));
                ms2.WriteByte((byte)((val >> 8) & 0xFF));
            }

            // 8. Write existing paint_mem
            if (existingPaintSize > 0)
                ms2.Write(bytes, paintMemOff, existingPaintSize);

            // 9. Write new paint_mem bytes
            if (newPaintMemBytes.Count > 0)
                ms2.Write(newPaintMemBytes.ToArray(), 0, newPaintMemBytes.Count);

            // 10. Write existing DStoreys
            if (existingStoreysSize > 0)
                ms2.Write(bytes, storeysOff, existingStoreysSize);

            // 11. Write new DStorey entries
            foreach (var se in newStoreyEntries)
                ms2.Write(se, 0, DStoreySize);

            // 12. Copy everything after storeys (indoors, walkables, objects, tail)
            int tailSize = bytes.Length - afterStoreysOff;
            if (tailSize > 0)
                ms2.Write(bytes, afterStoreysOff, tailSize);

            var newBytes = ms2.ToArray();

            int expectedSize = bytes.Length
                + totalNewFacets * DFacetSize
                + totalNewDStyles * DStyleSize
                + totalNewPaintBytes
                + totalNewStoreysCount * DStoreySize;

            Debug.WriteLine($"[BuildingAdder] File: {bytes.Length} -> {newBytes.Length} (expected {expectedSize})");

            if (newBytes.Length != expectedSize)
            {
                Debug.WriteLine($"[BuildingAdder] ERROR: Size mismatch!");
                return AddFacetsResult.Fail($"File size mismatch: expected {expectedSize}, got {newBytes.Length}");
            }

            _svc.ReplaceBytes(newBytes);

            BuildingsChangeBus.Instance.NotifyBuildingChanged(buildingId1, BuildingChangeType.Modified);
            BuildingsChangeBus.Instance.NotifyChanged();

            Debug.WriteLine($"[BuildingAdder] Successfully added {totalNewFacets} facets to building #{buildingId1}");

            return AddFacetsResult.Success(totalNewFacets);
        }

        /// <summary>
        /// Allocates a dstyle value for a single band.
        /// If painted, creates a DStorey entry + paint_mem bytes and returns the negative storey reference.
        /// If not painted, returns the positive raw style id.
        /// </summary>
        private static short AllocateDStyleValue(
            ushort rawStyleId,
            bool hasPaint,
            byte[]? paintBytes,
            ref ushort nextPaintMemCursor,
            ref ushort nextStoreyCursor,
            List<byte> newPaintMemBytes,
            List<byte[]> newStoreyEntries)
        {
            if (!hasPaint || paintBytes == null || paintBytes.Length == 0)
            {
                // Positive raw style — no DStorey needed
                return (short)rawStyleId;
            }

            // Create painted texture entry
            ushort paintIndex = nextPaintMemCursor;
            int storeyId = nextStoreyCursor;
            int count = paintBytes.Length;

            // Append paint bytes
            for (int c = 0; c < count; c++)
                newPaintMemBytes.Add(paintBytes[c]);
            nextPaintMemCursor += (ushort)count;

            // Create DStorey entry (6 bytes)
            var storey = new byte[DStoreySize];
            WriteU16(storey, 0, rawStyleId);                        // Style (fallback)
            WriteU16(storey, 2, paintIndex);                         // PaintIndex
            storey[4] = unchecked((byte)(sbyte)count);               // Count
            storey[5] = 0;                                            // Padding
            newStoreyEntries.Add(storey);
            nextStoreyCursor++;

            // Return negative storey reference
            return (short)(-storeyId);
        }

        /// <summary>
        /// Builds a 26-byte DFacet record.
        /// </summary>
        private static byte[] MakeFacetBytes(byte x0, byte z0, byte x1, byte z1,
            FacetTemplate template, ushort styleIndex, ushort buildingId)
        {
            var fb = new byte[DFacetSize];
            fb[0] = (byte)template.Type;
            fb[1] = template.Height;
            fb[2] = x0;
            fb[3] = x1;
            WriteS16(fb, 4, template.Y0);
            WriteS16(fb, 6, template.Y1);
            fb[8] = z0;
            fb[9] = z1;
            WriteU16(fb, 10, (ushort)template.Flags);
            WriteU16(fb, 12, styleIndex);
            WriteU16(fb, 14, buildingId);
            WriteU16(fb, 16, (ushort)template.Storey);
            fb[18] = template.FHeight;
            fb[19] = template.BlockHeight;
            // bytes 20-25: Open, Dfcache, Shake, CutHole, Counter0, Counter1 = 0
            return fb;
        }

        #endregion

        #region Byte Helpers

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

        // Kept for backward compatibility — not used in new code
        private static void WriteU16(Span<byte> b, int off, ushort val)
        {
            b[off] = (byte)(val & 0xFF);
            b[off + 1] = (byte)((val >> 8) & 0xFF);
        }

        #endregion
    }

    /// <summary>
    /// Result of adding facets to a building.
    /// </summary>
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