// /Services/FacetPainter.cs
// Service for applying paint data to existing facets by updating dstyles[], DStorey[], and paint_mem[].
//
// This handles painting for both regular and warehouse buildings.
//
// For regular buildings:
//   - Each band's dstyle is at: styleStart + band * entriesPerBand
//   - entriesPerBand is 1 (normal) or 2 (2-sided/2-textured)
//
// For warehouse buildings (interleaved outside/inside dstyles):
//   - Each band's outside dstyle is at: styleStart + band * (entriesPerBand + 1)
//   - The inside dstyle is at: styleStart + band * (entriesPerBand + 1) + entriesPerBand
//   - In practice, warehouses are single-band (Height=4), so there's no multi-band stepping.
//
// Painting converts a positive dstyle (raw TMA style) into a negative DStorey reference:
//   dstyles[idx] = -storeyIndex
//   DStorey { Style = baseStyle, PaintIndex = offset, Count = columns }
//   paint_mem[offset..offset+Count] = per-column texture tile bytes
//
// File layout: Header(48) ? Buildings[] ? Pad(14) ? Facets[] ? dstyles[] ? paint_mem[] ? dstoreys[] ? ...

using System.Diagnostics;
using System.IO;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Buildings
{
    /// <summary>
    /// Handles applying paint data to facets by updating dstyles[], DStorey[], and paint_mem[].
    /// </summary>
    public sealed class FacetPainter
    {
        private const int HeaderSize = BuildingFormatConstants.HeaderSize;
        private const int DBuildingSize = BuildingFormatConstants.DBuildingSize;
        private const int AfterBuildingsPad = BuildingFormatConstants.AfterBuildingsPad;
        private const int DFacetSize = BuildingFormatConstants.DFacetSize;
        private const int DStyleSize = BuildingFormatConstants.DStyleSize;
        private const int DStoreySize = BuildingFormatConstants.DStoreySize;    // U16 Style + U16 PaintIndex + S8 Count + U8 Padding

        private readonly MapDataService _svc;

        public FacetPainter(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        /// <summary>
        /// Applies paint data to a facet.
        /// </summary>
        /// <param name="facetIndex1">1-based facet index</param>
        /// <param name="columnsCount">Number of columns (paint bytes per band)</param>
        /// <param name="bandsCount">Number of vertical bands</param>
        /// <param name="paintData">Paint bytes per band (key=band index 0=bottom, value=byte[] per column)</param>
        /// <param name="baseStyles">Base TMA style per band (fallback when paint byte is 0)</param>
        public FacetPaintResult ApplyPaint(
            int facetIndex1,
            int columnsCount,
            int bandsCount,
            Dictionary<int, byte[]> paintData,
            Dictionary<int, short> baseStyles,
            int faceOffset = 1)
        {
            if (!_svc.IsLoaded)
                return FacetPaintResult.Fail("No map loaded.");

            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.Facets == null || facetIndex1 < 1 || facetIndex1 > snap.Facets.Length)
                return FacetPaintResult.Fail($"Facet #{facetIndex1} not found.");

            var facet = snap.Facets[facetIndex1 - 1];

            Debug.WriteLine($"[FacetPainter] ===== ApplyPaint START =====");
            Debug.WriteLine($"[FacetPainter] Facet #{facetIndex1}: StyleIndex={facet.StyleIndex}, Flags=0x{(ushort)facet.Flags:X4}");
            Debug.WriteLine($"[FacetPainter] Grid: {columnsCount} columns - {bandsCount} bands");

            var bytes = _svc.GetBytesCopy();
            int blockStart = snap.StartOffset;
            int saveType = BitConverter.ToInt32(bytes, 0);

            if (saveType < 17)
                return FacetPaintResult.Fail("Map version does not support paint data (saveType < 17).");

            // Read current header counters
            ushort nextBuilding = ReadU16(bytes, blockStart + 2);
            ushort nextFacet = ReadU16(bytes, blockStart + 4);
            ushort nextStyle = ReadU16(bytes, blockStart + 6);
            ushort nextPaintMem = ReadU16(bytes, blockStart + 8);
            ushort nextStorey = ReadU16(bytes, blockStart + 10);

            Debug.WriteLine($"[FacetPainter] Header: NextStyle={nextStyle}, NextPaintMem={nextPaintMem}, NextStorey={nextStorey}");

            // Calculate file offsets
            // IMPORTANT: dstyles has nextStyle entries (including slot 0) = nextStyle * 2 bytes
            int buildingsOff = blockStart + HeaderSize;
            int padOff = buildingsOff + (nextBuilding - 1) * DBuildingSize;
            int facetsOff = padOff + AfterBuildingsPad;
            int stylesOff = facetsOff + (nextFacet - 1) * DFacetSize;
            int paintMemOff = stylesOff + nextStyle * DStyleSize;
            int storeysOff = paintMemOff + nextPaintMem;

            Debug.WriteLine($"[FacetPainter] Offsets: styles=0x{stylesOff:X}, paint=0x{paintMemOff:X}, storeys=0x{storeysOff:X}");

            // Determine band stepping
            bool twoTextured = (facet.Flags & FacetFlags.TwoTextured) != 0;
            bool twoSided = (facet.Flags & FacetFlags.TwoSided) != 0;
            bool hugFloor = (facet.Flags & FacetFlags.HugFloor) != 0;

            // 2TEXTURED = warehouse-style paired opposing facets, emitted as two separate DFacetRec
            //   entries (outside + inside). Each facet's StyleIndex already points directly at its
            //   own style stream. The two streams are interleaved in dstyles (outside at even slots,
            //   inside at odd), so each facet steps by 2 across multi-band walls to skip the partner.
            //
            // 2SIDED = single-channel. The game source does use StyleIndex+1 for the back-face render
            //   but the editor treats 2SIDED as single-channel: we only paint StyleIndex+0.
            //   HugFloor is auto-set based on whether the building forms a closed polygon.
            //
            // Plain / 2SIDED + HugFloor = single-channel, step of 1.

            int entriesPerBand = twoTextured ? 2 : 1;
            int styleIndexStep = entriesPerBand;

            // 2TEXTURED: each paired facet has its own StyleIndex — no sub-slot offset needed.
            // All other cases (including 2SIDED): stream starts at facet.StyleIndex directly.
            bool isDualFace = false;
            int facetStyleStart = facet.StyleIndex;

            Debug.WriteLine($"[FacetPainter] twoTextured={twoTextured}, twoSided={twoSided}, hugFloor={hugFloor}, isDualFace={isDualFace}");
            Debug.WriteLine($"[FacetPainter] styleIndexStep={styleIndexStep}, facetStyleStart={facetStyleStart}");

            // Dump current dstyle values
            for (int band = 0; band < bandsCount; band++)
            {
                int dstyleIdx = facetStyleStart + band * styleIndexStep;
                if (dstyleIdx >= 0 && dstyleIdx < nextStyle)
                {
                    int fileOff = stylesOff + dstyleIdx * DStyleSize;
                    short val = (short)(bytes[fileOff] | (bytes[fileOff + 1] << 8));
                    Debug.WriteLine($"[FacetPainter]   Band {band}: dstyles[{dstyleIdx}] = {val}");
                }
            }

            // Determine which bands need painting
            var bandsToPaint = new List<int>();
            for (int band = 0; band < bandsCount; band++)
            {
                if (paintData.ContainsKey(band) && paintData[band].Any(b => (b & 0x7F) != 0))
                {
                    bandsToPaint.Add(band);
                }
            }

            // Determine which bands need clearing: currently have a DStorey reference (negative dstyle)
            // but have no paint data — must restore them to their positive base style value.
            var bandsToClear = new List<int>();
            for (int band = 0; band < bandsCount; band++)
            {
                if (bandsToPaint.Contains(band)) continue;
                int dstyleIdx = facetStyleStart + band * styleIndexStep;
                if (dstyleIdx < 0 || dstyleIdx >= nextStyle) continue;
                int fileOff = stylesOff + dstyleIdx * DStyleSize;
                short currentVal = (short)(bytes[fileOff] | (bytes[fileOff + 1] << 8));
                if (currentVal < 0)
                    bandsToClear.Add(band);
            }

            if (bandsToPaint.Count == 0 && bandsToClear.Count == 0)
            {
                Debug.WriteLine("[FacetPainter] No bands need painting or clearing.");
                return FacetPaintResult.Success(0, 0);
            }

            Debug.WriteLine($"[FacetPainter] Bands to paint: {bandsToPaint.Count}, bands to clear: {bandsToClear.Count}");

            // Calculate new data sizes
            int newStoreysCount = bandsToPaint.Count;
            int newPaintBytesCount = bandsToPaint.Count * columnsCount;

            Debug.WriteLine($"[FacetPainter] Will allocate {newStoreysCount} DStoreys, {newPaintBytesCount} paint bytes");

            // Build new DStorey entries and paint_mem bytes
            var newStoreys = new List<byte[]>();
            var newPaintBytes = new List<byte>();
            var dstylesUpdates = new Dictionary<int, short>(); // dstyles index -> new value

            ushort currentStoreyId = nextStorey;
            ushort currentPaintMemIndex = nextPaintMem;

            foreach (int band in bandsToPaint)
            {
                int dstyleIndex = facetStyleStart + band * styleIndexStep;
                var rawBytes = paintData.ContainsKey(band) ? paintData[band] : Array.Empty<byte>();
                Debug.WriteLine($"[FacetPainter] Band {band}: dstyleIndex={dstyleIndex}, rawInput=[{string.Join(",", rawBytes.Select(b => $"0x{b:X2}"))}]");

                if (dstyleIndex < 0 || dstyleIndex >= nextStyle)
                {
                    Debug.WriteLine($"[FacetPainter]   WARNING: dstyleIndex {dstyleIndex} out of range, skipping");
                    continue;
                }

                // Get base style for this band
                short baseStyle = baseStyles.ContainsKey(band) ? baseStyles[band] : (short)1;

                // Create DStorey entry (6 bytes)
                var storeyBytes = new byte[DStoreySize];
                WriteU16(storeyBytes, 0, (ushort)baseStyle);           // Style (base/fallback)
                WriteU16(storeyBytes, 2, currentPaintMemIndex);        // PaintIndex
                storeyBytes[4] = unchecked((byte)(sbyte)columnsCount); // Count
                storeyBytes[5] = 0;                                     // Padding
                newStoreys.Add(storeyBytes);

                Debug.WriteLine($"[FacetPainter]   DStorey #{currentStoreyId}: Style={baseStyle}, PaintIndex={currentPaintMemIndex}, Count={columnsCount}");

                // Write paint bytes.
                // 2SIDED walls: stored forward — game reads Side A with pos = col.
                // Indoor (Inside flag) walls: engine reads them forward too, so store forward
                //   to compensate for otherwise coming out mirrored on the inner face.
                // All other walls (single-channel, non-2SIDED, non-Inside): stored reversed —
                //   game reads with pos = N-1-col.
                bool isInside = (facet.Flags & FacetFlags.Inside) != 0;
                bool storeForward = twoSided || isInside;
                var bandPaintBytes = paintData[band];
                if (storeForward)
                {
                    for (int col = 0; col < columnsCount; col++)
                        newPaintBytes.Add(col < bandPaintBytes.Length ? bandPaintBytes[col] : (byte)0);
                }
                else
                {
                    for (int col = columnsCount - 1; col >= 0; col--)
                        newPaintBytes.Add(col < bandPaintBytes.Length ? bandPaintBytes[col] : (byte)0);
                }

                // Update dstyles to point to this DStorey (negative value)
                dstylesUpdates[dstyleIndex] = (short)(-currentStoreyId);

                Debug.WriteLine($"[FacetPainter]   Will set dstyles[{dstyleIndex}] = -{currentStoreyId}");

                currentStoreyId++;
                currentPaintMemIndex += (ushort)columnsCount;
            }

            // Add clearing updates: restore negative dstyle entries to positive base style values.
            // These bands had DStorey references from a prior paint; clearing removes the paint
            // by pointing dstyles[] back at the raw TMA style (positive value).
            foreach (int band in bandsToClear)
            {
                int dstyleIndex = facetStyleStart + band * styleIndexStep;
                short baseStyle = baseStyles.ContainsKey(band) ? baseStyles[band] : (short)1;
                dstylesUpdates[dstyleIndex] = baseStyle;
                Debug.WriteLine($"[FacetPainter] Band {band}: clearing dstyles[{dstyleIndex}] -> {baseStyle}");
            }

            // === Build new file ===
            using var ms = new MemoryStream();

            // 1. Copy everything up to building block header
            ms.Write(bytes, 0, blockStart);

            // 2. Write updated header
            var header = new byte[HeaderSize];
            Buffer.BlockCopy(bytes, blockStart, header, 0, HeaderSize);
            WriteU16(header, 8, (ushort)(nextPaintMem + newPaintBytesCount));
            WriteU16(header, 10, (ushort)(nextStorey + newStoreysCount));
            ms.Write(header, 0, HeaderSize);

            // 3. Copy buildings
            int buildingsSize = (nextBuilding - 1) * DBuildingSize;
            if (buildingsSize > 0)
                ms.Write(bytes, buildingsOff, buildingsSize);

            // 4. Copy padding
            ms.Write(bytes, padOff, AfterBuildingsPad);

            // 5. Copy facets (unchanged)
            int facetsSize = (nextFacet - 1) * DFacetSize;
            if (facetsSize > 0)
                ms.Write(bytes, facetsOff, facetsSize);

            // 6. Write dstyles with in-place updates for painted bands
            int stylesSize = nextStyle * DStyleSize;
            var stylesData = new byte[stylesSize];
            Buffer.BlockCopy(bytes, stylesOff, stylesData, 0, stylesSize);

            foreach (var kvp in dstylesUpdates)
            {
                int index = kvp.Key;
                short value = kvp.Value;
                if (index >= 0 && (index * DStyleSize + 1) < stylesData.Length)
                {
                    WriteS16(stylesData, index * DStyleSize, value);
                    Debug.WriteLine($"[FacetPainter] Updated dstyles[{index}] = {value}");
                }
            }
            ms.Write(stylesData, 0, stylesSize);

            // 7. Copy existing paint_mem (includes slot 0)
            int existingPaintMemSize = nextPaintMem;
            if (existingPaintMemSize > 0)
                ms.Write(bytes, paintMemOff, existingPaintMemSize);

            // 8. Write new paint bytes
            if (newPaintBytes.Count > 0)
                ms.Write(newPaintBytes.ToArray(), 0, newPaintBytes.Count);

            // 9. Copy existing DStoreys (includes slot 0)
            int existingStoreysSize = nextStorey * DStoreySize;
            if (existingStoreysSize > 0)
                ms.Write(bytes, storeysOff, existingStoreysSize);

            // 10. Write new DStoreys
            foreach (var storeyBytes in newStoreys)
                ms.Write(storeyBytes, 0, DStoreySize);

            // 11. Copy everything after storeys (indoors, walkables, objects, tail)
            int afterStoreysOff = storeysOff + existingStoreysSize;
            int tailSize = bytes.Length - afterStoreysOff;
            if (tailSize > 0)
                ms.Write(bytes, afterStoreysOff, tailSize);

            var newBytes = ms.ToArray();

            int expectedGrowth = (newStoreysCount * DStoreySize) + newPaintBytesCount;
            int actualGrowth = newBytes.Length - bytes.Length;

            Debug.WriteLine($"[FacetPainter] File: {bytes.Length} -> {newBytes.Length} (growth: expected={expectedGrowth}, actual={actualGrowth})");

            if (actualGrowth != expectedGrowth)
            {
                return FacetPaintResult.Fail($"File size mismatch: expected growth {expectedGrowth}, got {actualGrowth}");
            }

            _svc.ReplaceBytes(newBytes);

            // Verification
            Debug.WriteLine($"[FacetPainter] ===== VERIFICATION =====");
            var verifyBytes = _svc.GetBytesCopy();
            ushort vNextStyle = ReadU16(verifyBytes, blockStart + 6);
            ushort vNextPaint = ReadU16(verifyBytes, blockStart + 8);
            ushort vNextStorey = ReadU16(verifyBytes, blockStart + 10);
            Debug.WriteLine($"[FacetPainter] Header AFTER: NextStyle={vNextStyle}, NextPaintMem={vNextPaint}, NextStorey={vNextStorey}");

            // Verify dstyle values
            for (int band = 0; band < bandsCount; band++)
            {
                int dstyleIdx = facetStyleStart + band * styleIndexStep;
                if (dstyleIdx >= 0 && dstyleIdx < vNextStyle)
                {
                    int fileOff = stylesOff + dstyleIdx * DStyleSize;
                    short val = (short)(verifyBytes[fileOff] | (verifyBytes[fileOff + 1] << 8));
                    Debug.WriteLine($"[FacetPainter]   Band {band}: dstyles[{dstyleIdx}] = {val}");
                }
            }

            // Verify new DStoreys and their paint bytes
            int newStoreysOff = stylesOff + vNextStyle * DStyleSize + vNextPaint;
            int vPaintMemOff = stylesOff + vNextStyle * DStyleSize;
            for (int i = 1; i < vNextStorey; i++)
            {
                int sOff = newStoreysOff + i * DStoreySize;
                if (sOff + DStoreySize <= verifyBytes.Length)
                {
                    ushort style = (ushort)(verifyBytes[sOff] | (verifyBytes[sOff + 1] << 8));
                    ushort pidx = (ushort)(verifyBytes[sOff + 2] | (verifyBytes[sOff + 3] << 8));
                    sbyte cnt = (sbyte)verifyBytes[sOff + 4];
                    Debug.WriteLine($"[FacetPainter]   DStorey[{i}]: Style={style}, PaintIndex={pidx}, Count={cnt}");

                    // Read back the actual paint bytes at PaintIndex
                    if (cnt > 0)
                    {
                        var paintBytesVerify = new System.Text.StringBuilder();
                        for (int p = 0; p < cnt; p++)
                        {
                            int pOff = vPaintMemOff + pidx + p;
                            byte pb = (pOff >= 0 && pOff < verifyBytes.Length) ? verifyBytes[pOff] : (byte)0xFF;
                            paintBytesVerify.Append($"[{p}]=0x{pb:X2} ");
                        }
                        Debug.WriteLine($"[FacetPainter]     paint_mem @ {pidx}: {paintBytesVerify}");
                    }
                }
            }

            Debug.WriteLine($"[FacetPainter] ===== ApplyPaint COMPLETE =====");

            BuildingsChangeBus.Instance.NotifyBuildingChanged(facet.Building, BuildingChangeType.Modified);
            BuildingsChangeBus.Instance.NotifyChanged();

            return FacetPaintResult.Success(newStoreysCount, newPaintBytesCount);
        }

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

        #endregion
    }

    public sealed class FacetPaintResult
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public int StoreysAllocated { get; }
        public int PaintBytesAllocated { get; }

        private FacetPaintResult(bool success, string? error, int storeys, int paintBytes)
        {
            IsSuccess = success;
            ErrorMessage = error;
            StoreysAllocated = storeys;
            PaintBytesAllocated = paintBytes;
        }

        public static FacetPaintResult Success(int storeys, int paintBytes) =>
            new(true, null, storeys, paintBytes);

        public static FacetPaintResult Fail(string error) =>
            new(false, error, 0, 0);
    }
}