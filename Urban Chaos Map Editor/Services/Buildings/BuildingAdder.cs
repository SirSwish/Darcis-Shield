// /Services/BuildingAdder.cs
// Helper class for adding buildings and facets to the map.
//
// File layout within buildings block:
//   Header(48) → Buildings[] → Pad(14) → Facets[] → dstyles[] → paint_mem[] → dstoreys[] → ...
//
// Array sizes in file (each includes unused slot 0):
//   dstyles:   nextStyle   entries × 2 bytes
//   paint_mem: nextPaintMem bytes
//   dstoreys:  nextStorey  entries × 6 bytes

using System.Diagnostics;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using System.IO;

namespace UrbanChaosMapEditor.Services.Buildings
{
    public sealed class BuildingAdder
    {
        private const int HeaderSize = BuildingFormatConstants.HeaderSize;
        private const int DBuildingSize = BuildingFormatConstants.DBuildingSize;
        private const int AfterBuildingsPad = BuildingFormatConstants.AfterBuildingsPad;
        private const int DFacetSize = BuildingFormatConstants.DFacetSize;
        private const int DStyleSize = BuildingFormatConstants.DStyleSize;
        private const int DStoreySize = BuildingFormatConstants.DStoreySize;

        private readonly MapDataService _svc;

        public BuildingAdder(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        #region Add Building

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

            ushort oldNextBuilding = ReadU16(bytes, blockStart + 2);
            ushort oldNextFacet = ReadU16(bytes, blockStart + 4);

            int buildingsOff = blockStart + HeaderSize;
            int oldBuildingsEnd = buildingsOff + (oldNextBuilding - 1) * DBuildingSize;
            int padOff = oldBuildingsEnd;

            int newBuildingId1 = oldNextBuilding;

            var newBuilding = new byte[DBuildingSize];
            WriteU16(newBuilding, 0, oldNextFacet);   // StartFacet
            WriteU16(newBuilding, 2, oldNextFacet);   // EndFacet (same = empty)
            WriteU16(newBuilding, 4, 0);              // Walkable = none
            newBuilding[11] = (byte)type;              // Type

            int afterBuildingsLen = bytes.Length - padOff;

            using var ms = new MemoryStream();

            ms.Write(bytes, 0, blockStart);

            var header = new byte[HeaderSize];
            Buffer.BlockCopy(bytes, blockStart, header, 0, HeaderSize);
            WriteU16(header, 2, (ushort)(oldNextBuilding + 1));
            ms.Write(header, 0, HeaderSize);

            if (oldNextBuilding > 1)
                ms.Write(bytes, buildingsOff, (oldNextBuilding - 1) * DBuildingSize);

            ms.Write(newBuilding, 0, DBuildingSize);
            ms.Write(bytes, padOff, afterBuildingsLen);

            var newBytes = ms.ToArray();

            Debug.WriteLine($"[BuildingAdder] Added building #{newBuildingId1} type={type}. " +
                           $"Old: {bytes.Length} bytes, new: {newBytes.Length} bytes (+{DBuildingSize})");

            _svc.ReplaceBytes(newBytes);

            BuildingsChangeBus.Instance.NotifyBuildingChanged(newBuildingId1, BuildingChangeType.Added);
            BuildingsChangeBus.Instance.NotifyChanged();

            return newBuildingId1;
        }

        #endregion

        #region Add Facets

        private static int CalculateBands(byte height)
        {
            if (height == 0) return 1;
            return (height / 4) + 1;
        }

        private static int CalculateEntriesPerBand(FacetFlags flags)
        {
            bool has2Textured = (flags & FacetFlags.TwoTextured) != 0;
            bool has2Sided = (flags & FacetFlags.TwoSided) != 0;
            bool hasHugFloor = (flags & FacetFlags.HugFloor) != 0;

            if ((has2Textured || has2Sided) && !hasHugFloor)
                return 2;
            return 1;
        }

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

            ushort oldNextBuilding = ReadU16(bytes, blockStart + 2);
            ushort oldNextFacet = ReadU16(bytes, blockStart + 4);
            ushort oldNextStyle = ReadU16(bytes, blockStart + 6);
            ushort oldNextPaintMem = (saveType >= 17) ? ReadU16(bytes, blockStart + 8) : (ushort)1;
            ushort oldNextStorey = (saveType >= 17) ? ReadU16(bytes, blockStart + 10) : (ushort)1;

            // Determine building type
            int bldRecOff = blockStart + HeaderSize + (buildingId1 - 1) * DBuildingSize;
            byte buildingType = bytes[bldRecOff + 11];
            bool isWarehouse = (buildingType == (byte)BuildingType.Warehouse);
            bool isNormalWall = (template.Type == FacetType.Normal);

            // For warehouse Normal walls: force TwoTextured flag
            if (isWarehouse && isNormalWall)
            {
                template = new FacetTemplate
                {
                    Type = template.Type,
                    Height = template.Height,
                    FHeight = template.FHeight,
                    BlockHeight = template.BlockHeight,
                    Y0 = template.Y0,
                    Y1 = template.Y1,
                    RawStyleId = template.RawStyleId,
                    Flags = template.Flags | FacetFlags.TwoTextured,
                    BuildingId1 = template.BuildingId1,
                    Storey = template.Storey,
                    PaintBytes = template.PaintBytes,
                    InteriorPaintBytes = template.InteriorPaintBytes,
                    InteriorStyleId = template.InteriorStyleId,
                };
            }

            // Only create interior (reversed) facets for warehouse Normal walls
            bool createInteriorFacets = isWarehouse && isNormalWall;

            int bandsPerFacet = CalculateBands(template.Height);
            int entriesPerBand = CalculateEntriesPerBand(template.Flags);

            bool hasPaint = template.PaintBytes != null && template.PaintBytes.Length > 0;
            bool hasInteriorPaint = createInteriorFacets && template.InteriorPaintBytes != null && template.InteriorPaintBytes.Length > 0;

            Debug.WriteLine($"[BuildingAdder.TryAddFacets] buildingId1={buildingId1}, coords={coords.Count}, isWarehouse={isWarehouse}, createInterior={createInteriorFacets}");

            // Calculate file offsets
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

            ushort oldStartFacet = ReadU16(bytes, bldRecOff);
            ushort oldEndFacet = ReadU16(bytes, bldRecOff + 2);
            int insertPosition = oldEndFacet;

            // Allocation cursors
            ushort nextStyleCursor = oldNextStyle;
            ushort nextPaintMemCursor = oldNextPaintMem;
            ushort nextStoreyCursor = oldNextStorey;

            var newFacetList = new List<byte[]>();
            var newDStyleValues = new List<short>();
            var newPaintMemBytes = new List<byte>();
            var newStoreyEntries = new List<byte[]>();

            for (int i = 0; i < coords.Count; i++)
            {
                var (x0, z0, x1, z1) = coords[i];

                ushort outsideStyleIndex = nextStyleCursor;

                for (int band = 0; band < bandsPerFacet; band++)
                {
                    // Outside dstyle
                    short outsideDStyleValue = AllocateDStyleValue(
                        template.RawStyleId, hasPaint, template.PaintBytes,
                        ref nextPaintMemCursor, ref nextStoreyCursor,
                        newPaintMemBytes, newStoreyEntries);
                    newDStyleValues.Add(outsideDStyleValue);

                    if (createInteriorFacets)
                    {
                        // Warehouse 2TEXTURED: interleaved layout — partner (inside) facet owns
                        // the very next slot. Engine reads each side with step = 2 from its own
                        // StyleIndex. Do NOT duplicate outside into the inside slot.
                        ushort interiorRawStyle = template.InteriorStyleId > 0
                            ? template.InteriorStyleId
                            : template.RawStyleId;

                        short insideDStyleValue = AllocateDStyleValue(
                            interiorRawStyle, hasInteriorPaint, template.InteriorPaintBytes,
                            ref nextPaintMemCursor, ref nextStoreyCursor,
                            newPaintMemBytes, newStoreyEntries);
                        newDStyleValues.Add(insideDStyleValue);
                    }
                    else
                    {
                        // 2SIDED (non-warehouse): mirror outside into the back-face slot(s).
                        for (int extra = 1; extra < entriesPerBand; extra++)
                            newDStyleValues.Add(outsideDStyleValue);
                    }

                    nextStyleCursor = (ushort)(oldNextStyle + newDStyleValues.Count);
                }

                // Outside facet
                var outsideFacet = MakeFacetBytes(x0, z0, x1, z1, template, outsideStyleIndex, (ushort)buildingId1);
                newFacetList.Add(outsideFacet);

                // Interior facet (warehouse Normal walls only — reversed coords with Inside flag)
                if (createInteriorFacets)
                {
                    // Inside facet sits one slot after outside in the interleaved block:
                    //   dstyles: [out0, in0, out1, in1, ...] — inside.StyleIndex = outsideStyleIndex + 1
                    //   engine reads each side with step = 2.
                    ushort interiorStyleIndex = (ushort)(outsideStyleIndex + 1);

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
                }
            }

            int totalNewFacets = newFacetList.Count;
            int totalNewDStyles = newDStyleValues.Count;
            int totalNewPaintBytes = newPaintMemBytes.Count;
            int totalNewStoreysCount = newStoreyEntries.Count;

            Debug.WriteLine($"[BuildingAdder] FINAL: {totalNewFacets} facets, {totalNewDStyles} dstyles, {totalNewStoreysCount} storeys, {totalNewPaintBytes} paint bytes");

            // === Build new file ===
            using var ms2 = new MemoryStream();

            ms2.Write(bytes, 0, blockStart);

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
                }
                else if (start >= insertPosition)
                {
                    WriteU16(bldBytes, 0, (ushort)(start + totalNewFacets));
                    WriteU16(bldBytes, 2, (ushort)(end + totalNewFacets));
                }

                ms2.Write(bldBytes, 0, DBuildingSize);
            }

            ms2.Write(bytes, padOff, AfterBuildingsPad);

            int facetsBefore = insertPosition - 1;
            if (facetsBefore > 0)
                ms2.Write(bytes, facetsOff, facetsBefore * DFacetSize);

            foreach (var fb in newFacetList)
                ms2.Write(fb, 0, DFacetSize);

            int facetsAfter = oldNextFacet - 1 - facetsBefore;
            if (facetsAfter > 0)
                ms2.Write(bytes, facetsOff + facetsBefore * DFacetSize, facetsAfter * DFacetSize);

            if (existingStylesSize > 0)
                ms2.Write(bytes, stylesOff, existingStylesSize);

            foreach (short val in newDStyleValues)
            {
                ms2.WriteByte((byte)(val & 0xFF));
                ms2.WriteByte((byte)((val >> 8) & 0xFF));
            }

            if (existingPaintSize > 0)
                ms2.Write(bytes, paintMemOff, existingPaintSize);

            if (newPaintMemBytes.Count > 0)
                ms2.Write(newPaintMemBytes.ToArray(), 0, newPaintMemBytes.Count);

            if (existingStoreysSize > 0)
                ms2.Write(bytes, storeysOff, existingStoreysSize);

            foreach (var se in newStoreyEntries)
                ms2.Write(se, 0, DStoreySize);

            int tailSize = bytes.Length - afterStoreysOff;
            if (tailSize > 0)
                ms2.Write(bytes, afterStoreysOff, tailSize);

            var newBytes = ms2.ToArray();

            int expectedSize = bytes.Length
                + totalNewFacets * DFacetSize
                + totalNewDStyles * DStyleSize
                + totalNewPaintBytes
                + totalNewStoreysCount * DStoreySize;

            if (newBytes.Length != expectedSize)
            {
                Debug.WriteLine($"[BuildingAdder] ERROR: Size mismatch! expected {expectedSize}, got {newBytes.Length}");
                return AddFacetsResult.Fail($"File size mismatch: expected {expectedSize}, got {newBytes.Length}");
            }

            _svc.ReplaceBytes(newBytes);

            BuildingsChangeBus.Instance.NotifyBuildingChanged(buildingId1, BuildingChangeType.Modified);
            BuildingsChangeBus.Instance.NotifyChanged();

            return AddFacetsResult.Success(totalNewFacets);
        }

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
                return (short)rawStyleId;

            ushort paintIndex = nextPaintMemCursor;
            int storeyId = nextStoreyCursor;
            int count = paintBytes.Length;

            for (int c = 0; c < count; c++)
                newPaintMemBytes.Add(paintBytes[c]);
            nextPaintMemCursor += (ushort)count;

            var storey = new byte[DStoreySize];
            WriteU16(storey, 0, rawStyleId);
            WriteU16(storey, 2, paintIndex);
            storey[4] = unchecked((byte)(sbyte)count);
            storey[5] = 0;
            newStoreyEntries.Add(storey);
            nextStoreyCursor++;

            return (short)(-storeyId);
        }

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

        private static void WriteU16(Span<byte> b, int off, ushort val)
        {
            b[off] = (byte)(val & 0xFF);
            b[off + 1] = (byte)((val >> 8) & 0xFF);
        }

        #endregion
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