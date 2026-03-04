// /Services/DataServices/DStoreyAccessor.cs
// Service for reading and writing DStorey and dstyles data

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UrbanChaosMapEditor.Models;

namespace UrbanChaosMapEditor.Services.DataServices
{
    /// <summary>
    /// Accessor for DStorey entries and dstyles array.
    /// These are critical for indoor building rendering.
    /// </summary>
    public class DStoreyAccessor
    {
        private readonly MapDataService _svc;

        // Structure sizes
        private const int DStoreySize = 6;
        private const int DBuildingSize = 24;
        private const int DFacetSize = 26;
        private const int HeaderSize = 48;

        public DStoreyAccessor(MapDataService svc)
        {
            _svc = svc;
        }

        /// <summary>
        /// Read all section offsets needed for storey operations
        /// </summary>
        public bool TryGetSectionOffsets(out StoreysSectionOffsets offsets)
        {
            offsets = default;

            if (!_svc.IsLoaded) return false;

            var bytes = _svc.GetBytesCopy();

            // Buildings header at 0x18008
            int buildingsHeader = 0x18008;
            if (buildingsHeader + 12 > bytes.Length) return false;

            ushort nextBuilding = ReadU16(bytes, buildingsHeader + 2);
            ushort nextFacet = ReadU16(bytes, buildingsHeader + 4);
            ushort nextStyle = ReadU16(bytes, buildingsHeader + 6);
            ushort nextPaint = ReadU16(bytes, buildingsHeader + 8);
            ushort nextStorey = ReadU16(bytes, buildingsHeader + 10);

            int buildingsDataOff = buildingsHeader + HeaderSize;
            int facetsOff = buildingsDataOff + (nextBuilding - 1) * DBuildingSize + 14; // 14 bytes padding
            int stylesOff = facetsOff + (nextFacet - 1) * DFacetSize;
            int paintOff = stylesOff + nextStyle * 2;
            int storeysOff = paintOff + nextPaint;

            offsets = new StoreysSectionOffsets
            {
                BuildingsHeader = buildingsHeader,
                BuildingsData = buildingsDataOff,
                FacetsData = facetsOff,
                StylesData = stylesOff,
                PaintData = paintOff,
                StoreysData = storeysOff,
                NextBuilding = nextBuilding,
                NextFacet = nextFacet,
                NextStyle = nextStyle,
                NextPaint = nextPaint,
                NextStorey = nextStorey
            };

            return true;
        }

        /// <summary>
        /// Read all DStorey entries
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
                    Height = bytes[off + 2],
                    Flags = bytes[off + 3],
                    Building = ReadU16(bytes, off + 4)
                };
            }

            return storeys;
        }

        /// <summary>
        /// Read all dstyles entries
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
        /// Get comprehensive storey view models with cross-references
        /// </summary>
        public List<DStoreyViewModel> GetStoreyViewModels()
        {
            if (!TryGetSectionOffsets(out var offsets))
                return new List<DStoreyViewModel>();

            var storeys = ReadAllStoreys();
            var dstyles = ReadAllDStyles();
            var bytes = _svc.GetBytesCopy();

            var result = new List<DStoreyViewModel>();

            // Build storey view models
            for (int i = 1; i < storeys.Length; i++)
            {
                var s = storeys[i];
                result.Add(new DStoreyViewModel
                {
                    Index = i,
                    Style = s.Style,
                    Height = s.Height,
                    Flags = s.Flags,
                    Building = s.Building,
                    FileOffset = offsets.StoreysData + i * DStoreySize
                });
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

                int fadeLevel = ReadU16(bytes, facetOff + 12);
                if (fadeLevel < dstyles.Length && dstyles[fadeLevel] < 0)
                {
                    int storeyIdx = -dstyles[fadeLevel];
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
        /// Get storeys for a specific building
        /// </summary>
        public List<DStoreyViewModel> GetStoreysForBuilding(int buildingId)
        {
            return GetStoreyViewModels().Where(s => s.Building == buildingId).ToList();
        }

        /// <summary>
        /// Get all dstyles entries with cross-references
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

                int fadeLevel = ReadU16(bytes, facetOff + 12);
                if (fadeLevel < result.Count)
                {
                    result[fadeLevel].UsedByFacets.Add(f);
                }
            }

            return result;
        }

        /// <summary>
        /// Get negative dstyles entries only (storey references)
        /// </summary>
        public List<DStyleEntry> GetNegativeDStyleEntries()
        {
            return GetDStyleEntries().Where(d => d.IsStoreyRef).ToList();
        }

        /// <summary>
        /// Get indoor building summary
        /// </summary>
        public IndoorBuildingSummary GetIndoorBuildingSummary(int buildingId)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return null;

            var bytes = _svc.GetBytesCopy();

            // Read building
            int bldOff = offsets.BuildingsData + (buildingId - 1) * DBuildingSize;
            if (bldOff + DBuildingSize > bytes.Length) return null;

            var summary = new IndoorBuildingSummary
            {
                BuildingId = buildingId,
                FacetStart = ReadU16(bytes, bldOff),
                FacetEnd = ReadU16(bytes, bldOff + 2),
                BuildingType = bytes[bldOff + 11],
                WalkablePointer = ReadU16(bytes, bldOff + 16)
            };

            // Get storeys owned by this building
            summary.Storeys = GetStoreysForBuilding(buildingId);

            // Get dstyles used by this building's facets
            var dstyles = ReadAllDStyles();
            var usedDStyleIndices = new HashSet<int>();

            for (int f = summary.FacetStart; f < summary.FacetEnd; f++)
            {
                int facetOff = offsets.FacetsData + (f - 1) * DFacetSize;
                if (facetOff + DFacetSize > bytes.Length) break;

                int fadeLevel = ReadU16(bytes, facetOff + 12);
                usedDStyleIndices.Add(fadeLevel);
            }

            summary.UsedDStyles = usedDStyleIndices
                .Where(i => i < dstyles.Length)
                .Select(i => new DStyleEntry { Index = i, Value = dstyles[i] })
                .ToList();

            // Find walkables for this building
            var bldAcc = new BuildingsAccessor(_svc);
            if (bldAcc.TryGetWalkables(out var walkables, out _))
            {
                for (int w = 1; w < walkables.Length; w++)
                {
                    if (walkables[w].Building == buildingId)
                    {
                        summary.WalkableIds.Add(w);
                    }
                }
            }

            return summary;
        }

        /// <summary>
        /// Add a new DStorey entry
        /// </summary>
        public (bool Success, int NewIndex, string Error) AddDStorey(ushort style, byte height, byte flags, ushort building)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return (false, 0, "Failed to read section offsets");

            var bytes = _svc.GetBytesCopy();

            int newIndex = offsets.NextStorey;
            int insertOffset = offsets.StoreysData + newIndex * DStoreySize;

            // Create new storey data
            byte[] newStorey = new byte[DStoreySize];
            WriteU16(newStorey, 0, style);
            newStorey[2] = height;
            newStorey[3] = flags;
            WriteU16(newStorey, 4, building);

            // Insert into file
            var newBytes = new byte[bytes.Length + DStoreySize];
            Array.Copy(bytes, 0, newBytes, 0, insertOffset);
            Array.Copy(newStorey, 0, newBytes, insertOffset, DStoreySize);
            Array.Copy(bytes, insertOffset, newBytes, insertOffset + DStoreySize, bytes.Length - insertOffset);

            // Update nextStorey in header
            WriteU16(newBytes, offsets.BuildingsHeader + 10, (ushort)(offsets.NextStorey + 1));

            _svc.ReplaceBytes(newBytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[DStoreyAccessor] Added DStorey #{newIndex}: Style={style}, Height={height}, Flags=0x{flags:X2}, Building={building}");

            return (true, newIndex, null);
        }

        /// <summary>
        /// Add a negative dstyles entry pointing to a storey
        /// </summary>
        public (bool Success, int NewIndex, string Error) AddNegativeDStyle(int storeyIndex)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return (false, 0, "Failed to read section offsets");

            if (storeyIndex < 1 || storeyIndex >= offsets.NextStorey)
                return (false, 0, $"Invalid storey index: {storeyIndex}");

            var bytes = _svc.GetBytesCopy();

            int newIndex = offsets.NextStyle;
            int insertOffset = offsets.StylesData + newIndex * 2;

            // Create new dstyles entry (negative value)
            short negValue = (short)(-storeyIndex);

            // Insert into file
            var newBytes = new byte[bytes.Length + 2];
            Array.Copy(bytes, 0, newBytes, 0, insertOffset);
            WriteS16(newBytes, insertOffset, negValue);
            Array.Copy(bytes, insertOffset, newBytes, insertOffset + 2, bytes.Length - insertOffset);

            // Update nextStyle in header
            WriteU16(newBytes, offsets.BuildingsHeader + 6, (ushort)(offsets.NextStyle + 1));

            _svc.ReplaceBytes(newBytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[DStoreyAccessor] Added dstyles[{newIndex}] = {negValue} (-> Storey #{storeyIndex})");

            return (true, newIndex, null);
        }

        /// <summary>
        /// Update a facet's FadeLevel to point to a new dstyles index
        /// </summary>
        public bool UpdateFacetFadeLevel(int facetId1, ushort newFadeLevel)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return false;

            if (facetId1 < 1 || facetId1 >= offsets.NextFacet)
                return false;

            var bytes = _svc.GetBytesCopy();
            int facetOff = offsets.FacetsData + (facetId1 - 1) * DFacetSize;

            // FadeLevel is at offset 12 within facet
            WriteU16(bytes, facetOff + 12, newFadeLevel);

            _svc.ReplaceBytes(bytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[DStoreyAccessor] Updated Facet #{facetId1}.FadeLevel = {newFadeLevel}");

            return true;
        }

        /// <summary>
        /// Modify an existing DStorey entry
        /// </summary>
        public bool UpdateDStorey(int storeyIndex, ushort style, byte height, byte flags, ushort building)
        {
            if (!TryGetSectionOffsets(out var offsets))
                return false;

            if (storeyIndex < 1 || storeyIndex >= offsets.NextStorey)
                return false;

            var bytes = _svc.GetBytesCopy();
            int storeyOff = offsets.StoreysData + storeyIndex * DStoreySize;

            WriteU16(bytes, storeyOff, style);
            bytes[storeyOff + 2] = height;
            bytes[storeyOff + 3] = flags;
            WriteU16(bytes, storeyOff + 4, building);

            _svc.ReplaceBytes(bytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[DStoreyAccessor] Updated DStorey #{storeyIndex}: Style={style}, Height={height}, Flags=0x{flags:X2}, Building={building}");

            return true;
        }

        /// <summary>
        /// Modify an existing dstyles entry
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
    /// Offsets for all storey-related sections
    /// </summary>
    public struct StoreysSectionOffsets
    {
        public int BuildingsHeader;
        public int BuildingsData;
        public int FacetsData;
        public int StylesData;
        public int PaintData;
        public int StoreysData;

        public ushort NextBuilding;
        public ushort NextFacet;
        public ushort NextStyle;
        public ushort NextPaint;
        public ushort NextStorey;
    }
}