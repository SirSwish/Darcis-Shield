// /Services/DataServices/DStoreyAccessor.cs
// Service for reading and writing DStorey and dstyles data.
//
// File layout (within the buildings block):
//   Header(48) ? Buildings[] ? Pad(14) ? Facets[] ? dstyles[] ? paint_mem[] ? dstoreys[] ? ...
//
// Array sizes in file (each includes unused slot 0):
//   dstyles:   nextStyle   entries × 2 bytes each
//   paint_mem: nextPaint   bytes
//   dstoreys:  nextStorey  entries × 6 bytes each

using System.Diagnostics;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Buildings
{
    /// <summary>
    /// Accessor for DStorey entries, dstyles array, and paint_mem blob.
    /// These are the painted-texture system used by both regular and warehouse buildings.
    /// </summary>
    public class DStoreyAccessor
    {
        private readonly MapDataService _svc;

        // Structure sizes
        private const int DStoreySize = 6;      // U16 Style + U16 PaintIndex + S8 Count + U8 Padding
        private const int DBuildingSize = 24;
        private const int DFacetSize = 26;
        private const int HeaderSize = 48;
        private const int AfterBuildingsPad = 14;

        public DStoreyAccessor(MapDataService svc)
        {
            _svc = svc;
        }

        /// <summary>
        /// Read all section offsets needed for storey operations.
        /// </summary>
        public bool TryGetSectionOffsets(out StoreysSectionOffsets offsets)
        {
            offsets = default;

            if (!_svc.IsLoaded) return false;

            var bytes = _svc.GetBytesCopy();
            int saveType = BitConverter.ToInt32(bytes, 0);

            // Buildings header at 0x18008
            int buildingsHeader = 0x18008;
            if (buildingsHeader + 12 > bytes.Length) return false;

            ushort nextBuilding = ReadU16(bytes, buildingsHeader + 2);
            ushort nextFacet = ReadU16(bytes, buildingsHeader + 4);
            ushort nextStyle = ReadU16(bytes, buildingsHeader + 6);
            ushort nextPaint = (saveType >= 17) ? ReadU16(bytes, buildingsHeader + 8) : (ushort)0;
            ushort nextStorey = (saveType >= 17) ? ReadU16(bytes, buildingsHeader + 10) : (ushort)0;

            int buildingsDataOff = buildingsHeader + HeaderSize;
            int padOff = buildingsDataOff + (nextBuilding - 1) * DBuildingSize;
            int facetsOff = padOff + AfterBuildingsPad;

            // IMPORTANT: dstyles has nextStyle entries (including slot 0), so size = nextStyle * 2
            int stylesOff = facetsOff + (nextFacet - 1) * DFacetSize;
            int paintOff = stylesOff + nextStyle * 2;
            int storeysOff = paintOff + nextPaint;

            offsets = new StoreysSectionOffsets
            {
                BuildingsHeader = buildingsHeader,
                BuildingsData = buildingsDataOff,
                PadOffset = padOff,
                FacetsData = facetsOff,
                StylesData = stylesOff,
                PaintData = paintOff,
                StoreysData = storeysOff,
                NextBuilding = nextBuilding,
                NextFacet = nextFacet,
                NextStyle = nextStyle,
                NextPaint = nextPaint,
                NextStorey = nextStorey,
                SaveType = saveType
            };

            return true;
        }

        /// <summary>
        /// Read all DStorey entries (index 0 is unused sentinel).
        /// </summary>
        public DStoreyRec[] ReadAllStoreys()
        {
            if (!TryGetSectionOffsets(out var offsets))
                return Array.Empty<DStoreyRec>();

            var bytes = _svc.GetBytesCopy();
            var storeys = new DStoreyRec[offsets.NextStorey];

            for (int i = 0; i < offsets.NextStorey; i++)
            {
                int off = offsets.StoreysData + i * DStoreySize;
                if (off + DStoreySize > bytes.Length) break;

                storeys[i] = new DStoreyRec
                {
                    Style = ReadU16(bytes, off),
                    PaintIndex = ReadU16(bytes, off + 2),
                    Count = unchecked((sbyte)bytes[off + 4]),
                    Padding = bytes[off + 5]
                };
            }

            return storeys;
        }

        /// <summary>
        /// Read all dstyles entries (index 0 is unused sentinel).
        /// </summary>
        public short[] ReadAllDStyles()
        {
            if (!TryGetSectionOffsets(out var offsets))
                return Array.Empty<short>();

            var bytes = _svc.GetBytesCopy();
            var styles = new short[offsets.NextStyle];

            for (int i = 0; i < offsets.NextStyle; i++)
            {
                int off = offsets.StylesData + i * 2;
                if (off + 2 > bytes.Length) break;

                styles[i] = ReadS16(bytes, off);
            }

            return styles;
        }

        /// <summary>
        /// Read the raw paint_mem blob (index 0 is unused sentinel).
        /// </summary>
        public byte[] ReadPaintMem()
        {
            if (!TryGetSectionOffsets(out var offsets))
                return Array.Empty<byte>();

            if (offsets.NextPaint <= 0)
                return Array.Empty<byte>();

            var bytes = _svc.GetBytesCopy();
            int size = Math.Min(offsets.NextPaint, bytes.Length - offsets.PaintData);
            if (size <= 0) return Array.Empty<byte>();

            var result = new byte[size];
            Buffer.BlockCopy(bytes, offsets.PaintData, result, 0, size);
            return result;
        }

        /// <summary>
        /// Get comprehensive storey view models with cross-references.
        /// </summary>
        public List<DStoreyViewModel> GetStoreyViewModels()
        {
            if (!TryGetSectionOffsets(out var offsets))
                return new List<DStoreyViewModel>();

            var storeys = ReadAllStoreys();
            var dstyles = ReadAllDStyles();
            var paintMem = ReadPaintMem();
            var bytes = _svc.GetBytesCopy();

            var result = new List<DStoreyViewModel>();

            // Build storey view models (skip slot 0)
            for (int i = 1; i < storeys.Length; i++)
            {
                var s = storeys[i];
                var vm = new DStoreyViewModel
                {
                    Index = i,
                    Style = s.Style,
                    PaintIndex = s.PaintIndex,
                    Count = s.Count,
                    Padding = s.Padding,
                    FileOffset = offsets.StoreysData + i * DStoreySize
                };

                // Extract paint bytes for display
                if (s.Count > 0 && s.PaintIndex >= 0 && s.PaintIndex + s.Count <= paintMem.Length)
                {
                    vm.PaintBytes = new byte[s.Count];
                    Array.Copy(paintMem, s.PaintIndex, vm.PaintBytes, 0, s.Count);
                }

                result.Add(vm);
            }

            // Find dstyles that reference each storey
            for (int i = 0; i < dstyles.Length; i++)
            {
                if (dstyles[i] < 0)
                {
                    int storeyIdx = -dstyles[i];
                    var vm = result.FirstOrDefault(s => s.Index == storeyIdx);
                    if (vm != null)
                    {
                        vm.ReferencedByDStyles.Add(i);
                    }
                }
            }

            // Find facets that use each storey (through dstyles)
            for (int f = 1; f < offsets.NextFacet; f++)
            {
                int facetOff = offsets.FacetsData + (f - 1) * DFacetSize;
                if (facetOff + DFacetSize > bytes.Length) break;

                int styleIndex = ReadU16(bytes, facetOff + 12);
                if (styleIndex < dstyles.Length && dstyles[styleIndex] < 0)
                {
                    int storeyIdx = -dstyles[styleIndex];
                    var vm = result.FirstOrDefault(s => s.Index == storeyIdx);
                    if (vm != null)
                    {
                        vm.UsedByFacets.Add(f);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get storeys referenced by a specific building's facets.
        /// </summary>
        public List<DStoreyViewModel> GetStoreysForBuilding(int buildingId)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return new List<DStoreyViewModel>();

            var bytes = _svc.GetBytesCopy();
            var dstyles = ReadAllDStyles();

            // Find this building's facet range
            int bldOff = offsets.BuildingsData + (buildingId - 1) * DBuildingSize;
            if (bldOff + DBuildingSize > bytes.Length)
                return new List<DStoreyViewModel>();

            ushort startFacet = ReadU16(bytes, bldOff);
            ushort endFacet = ReadU16(bytes, bldOff + 2);

            // Collect storey indices referenced by this building's facets
            var storeyIndices = new HashSet<int>();
            for (int f = startFacet; f < endFacet; f++)
            {
                int facetOff = offsets.FacetsData + (f - 1) * DFacetSize;
                if (facetOff + DFacetSize > bytes.Length) break;

                int styleIndex = ReadU16(bytes, facetOff + 12);
                if (styleIndex < dstyles.Length && dstyles[styleIndex] < 0)
                {
                    storeyIndices.Add(-dstyles[styleIndex]);
                }
            }

            // Return matching storey view models
            return GetStoreyViewModels().Where(s => storeyIndices.Contains(s.Index)).ToList();
        }

        /// <summary>
        /// Get a summary of a building's DStorey/dstyles configuration.
        /// </summary>
        public IndoorBuildingSummary GetBuildingSummary(int buildingId)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return new IndoorBuildingSummary { BuildingId = buildingId };

            var bytes = _svc.GetBytesCopy();

            int bldOff = offsets.BuildingsData + (buildingId - 1) * DBuildingSize;
            if (bldOff + DBuildingSize > bytes.Length)
                return new IndoorBuildingSummary { BuildingId = buildingId };

            var summary = new IndoorBuildingSummary
            {
                BuildingId = buildingId,
                BuildingType = bytes[bldOff + 11],       // Type offset within DBuilding
                FacetStart = ReadU16(bytes, bldOff),
                FacetEnd = ReadU16(bytes, bldOff + 2),
                WalkablePointer = ReadU16(bytes, bldOff + 16)
            };

            var dstyles = ReadAllDStyles();
            var usedDStyleIndices = new HashSet<int>();

            for (int f = summary.FacetStart; f < summary.FacetEnd; f++)
            {
                int facetOff = offsets.FacetsData + (f - 1) * DFacetSize;
                if (facetOff + DFacetSize > bytes.Length) break;

                int styleIndex = ReadU16(bytes, facetOff + 12);
                usedDStyleIndices.Add(styleIndex);
            }

            summary.UsedDStyles = usedDStyleIndices
                .Where(i => i < dstyles.Length)
                .Select(i => new DStyleEntry { Index = i, Value = dstyles[i] })
                .ToList();

            summary.Storeys = GetStoreysForBuilding(buildingId);

            return summary;
        }

        /// <summary>
        /// Add a new DStorey entry with paint_mem bytes.
        /// Returns (success, newStoreyIndex, error).
        /// </summary>
        public (bool Success, int NewIndex, string Error) AddDStorey(
            ushort baseStyle, ushort paintIndex, sbyte count, byte padding = 0)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return (false, 0, "Failed to read section offsets");

            var bytes = _svc.GetBytesCopy();

            int newIndex = offsets.NextStorey;
            int insertOffset = offsets.StoreysData + newIndex * DStoreySize;

            // Create new storey data (6 bytes)
            byte[] newStorey = new byte[DStoreySize];
            WriteU16(newStorey, 0, baseStyle);                       // Style
            WriteU16(newStorey, 2, paintIndex);                      // PaintIndex
            newStorey[4] = unchecked((byte)count);                   // Count (as SBYTE)
            newStorey[5] = padding;                                   // Padding

            // Insert into file (append at end of storeys section)
            var newBytes = new byte[bytes.Length + DStoreySize];
            Array.Copy(bytes, 0, newBytes, 0, insertOffset);
            Array.Copy(newStorey, 0, newBytes, insertOffset, DStoreySize);
            Array.Copy(bytes, insertOffset, newBytes, insertOffset + DStoreySize, bytes.Length - insertOffset);

            // Update nextStorey in header
            WriteU16(newBytes, offsets.BuildingsHeader + 10, (ushort)(offsets.NextStorey + 1));

            _svc.ReplaceBytes(newBytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[DStoreyAccessor] Added DStorey #{newIndex}: Style={baseStyle}, PaintIndex={paintIndex}, Count={count}");

            return (true, newIndex, null);
        }

        /// <summary>
        /// Append paint bytes to paint_mem and return the starting index.
        /// </summary>
        public (bool Success, ushort PaintIndex, string Error) AppendPaintMem(byte[] paintBytes)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return (false, 0, "Failed to read section offsets");

            if (paintBytes == null || paintBytes.Length == 0)
                return (false, 0, "No paint bytes to append");

            var bytes = _svc.GetBytesCopy();

            ushort newPaintIndex = offsets.NextPaint;
            int insertOffset = offsets.PaintData + newPaintIndex;

            // Insert paint bytes into the file
            var newBytes = new byte[bytes.Length + paintBytes.Length];
            Array.Copy(bytes, 0, newBytes, 0, insertOffset);
            Array.Copy(paintBytes, 0, newBytes, insertOffset, paintBytes.Length);
            Array.Copy(bytes, insertOffset, newBytes, insertOffset + paintBytes.Length, bytes.Length - insertOffset);

            // Update nextPaintMem in header
            WriteU16(newBytes, offsets.BuildingsHeader + 8, (ushort)(offsets.NextPaint + paintBytes.Length));

            _svc.ReplaceBytes(newBytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[DStoreyAccessor] Appended {paintBytes.Length} paint bytes at index {newPaintIndex}");

            return (true, newPaintIndex, null);
        }

        /// <summary>
        /// Add a dstyles entry (positive = raw style, negative = storey ref).
        /// Appends at the end of the dstyles array.
        /// </summary>
        public (bool Success, int NewIndex, string Error) AppendDStyle(short value)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return (false, 0, "Failed to read section offsets");

            var bytes = _svc.GetBytesCopy();

            int newIndex = offsets.NextStyle;
            // Insert at end of dstyles section (= start of paint_mem section)
            int insertOffset = offsets.StylesData + newIndex * 2;

            var newBytes = new byte[bytes.Length + 2];
            Array.Copy(bytes, 0, newBytes, 0, insertOffset);
            WriteS16(newBytes, insertOffset, value);
            Array.Copy(bytes, insertOffset, newBytes, insertOffset + 2, bytes.Length - insertOffset);

            // Update nextStyle in header
            WriteU16(newBytes, offsets.BuildingsHeader + 6, (ushort)(offsets.NextStyle + 1));

            _svc.ReplaceBytes(newBytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[DStoreyAccessor] Appended dstyles[{newIndex}] = {value}");

            return (true, newIndex, null);
        }

        /// <summary>
        /// Update a facet's StyleIndex field.
        /// </summary>
        public bool UpdateFacetStyleIndex(int facetId1, ushort newStyleIndex)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return false;

            if (facetId1 < 1 || facetId1 >= offsets.NextFacet)
                return false;

            var bytes = _svc.GetBytesCopy();
            int facetOff = offsets.FacetsData + (facetId1 - 1) * DFacetSize;

            // StyleIndex is at offset 12 within DFacet
            WriteU16(bytes, facetOff + 12, newStyleIndex);

            _svc.ReplaceBytes(bytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[DStoreyAccessor] Updated Facet #{facetId1}.StyleIndex = {newStyleIndex}");

            return true;
        }

        /// <summary>
        /// Modify an existing DStorey entry in place.
        /// </summary>
        public bool UpdateDStorey(int storeyIndex, ushort style, ushort paintIndex, sbyte count, byte padding = 0)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return false;

            if (storeyIndex < 1 || storeyIndex >= offsets.NextStorey)
                return false;

            var bytes = _svc.GetBytesCopy();
            int storeyOff = offsets.StoreysData + storeyIndex * DStoreySize;

            WriteU16(bytes, storeyOff, style);
            WriteU16(bytes, storeyOff + 2, paintIndex);
            bytes[storeyOff + 4] = unchecked((byte)count);
            bytes[storeyOff + 5] = padding;

            _svc.ReplaceBytes(bytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[DStoreyAccessor] Updated DStorey #{storeyIndex}: Style={style}, PaintIndex={paintIndex}, Count={count}");

            return true;
        }

        /// <summary>
        /// Modify an existing dstyles entry in place.
        /// </summary>
        public bool UpdateDStyle(int dstyleIndex, short newValue)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return false;

            if (dstyleIndex < 0 || dstyleIndex >= offsets.NextStyle)
                return false;

            var bytes = _svc.GetBytesCopy();
            int styleOff = offsets.StylesData + dstyleIndex * 2;

            WriteS16(bytes, styleOff, newValue);

            _svc.ReplaceBytes(bytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[DStoreyAccessor] Updated dstyles[{dstyleIndex}] = {newValue}");

            return true;
        }

        /// <summary>
        /// Get all dstyles entries with cross-references (used by UI grids).
        /// </summary>
        public List<DStyleEntry> GetDStyleEntries()
        {
            if (!TryGetSectionOffsets(out var offsets))
                return new List<DStyleEntry>();

            var dstyles = ReadAllDStyles();
            var bytes = _svc.GetBytesCopy();

            var result = new List<DStyleEntry>();
            for (int i = 0; i < dstyles.Length; i++)
            {
                result.Add(new DStyleEntry
                {
                    Index = i,
                    Value = dstyles[i]
                });
            }

            // Find facets that use each dstyles entry
            for (int f = 1; f < offsets.NextFacet; f++)
            {
                int facetOff = offsets.FacetsData + (f - 1) * DFacetSize;
                if (facetOff + DFacetSize > bytes.Length) break;

                int styleIndex = ReadU16(bytes, facetOff + 12);
                if (styleIndex < result.Count)
                {
                    result[styleIndex].UsedByFacets.Add(f);
                }
            }

            return result;
        }

        /// <summary>
        /// Alias for GetBuildingSummary (backward compat with IndoorBuildingEditorDialog).
        /// </summary>
        public IndoorBuildingSummary GetIndoorBuildingSummary(int buildingId)
        {
            return GetBuildingSummary(buildingId);
        }

        /// <summary>
        /// Update a facet's StyleIndex (alias: "FadeLevel" in the old indoor editor).
        /// </summary>
        public bool UpdateFacetFadeLevel(int facetId1, ushort newStyleIndex)
        {
            return UpdateFacetStyleIndex(facetId1, newStyleIndex);
        }

        #region Helpers

        private static ushort ReadU16(ReadOnlySpan<byte> data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static short ReadS16(ReadOnlySpan<byte> data, int offset)
        {
            return (short)(data[offset] | (data[offset + 1] << 8));
        }

        private static void WriteU16(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteS16(byte[] data, int offset, short value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)(value >> 8);
        }

        #endregion
    }

    /// <summary>
    /// Offsets for all storey-related sections in the file.
    /// </summary>
    public struct StoreysSectionOffsets
    {
        public int BuildingsHeader;
        public int BuildingsData;
        public int PadOffset;
        public int FacetsData;
        public int StylesData;
        public int PaintData;
        public int StoreysData;

        public ushort NextBuilding;
        public ushort NextFacet;
        public ushort NextStyle;
        public ushort NextPaint;
        public ushort NextStorey;
        public int SaveType;
    }
}