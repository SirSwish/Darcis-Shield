// /Models/DStoreyRec.cs
// Data structures for DStorey (floor/level) entries in indoor buildings

using System;
using System.Collections.Generic;
using System.Linq;

namespace UrbanChaosMapEditor.Models
{
    /// <summary>
    /// DStorey entry - defines a storey/floor level for indoor buildings.
    /// Size: 6 bytes
    /// 
    /// The rendering chain works as follows:
    /// 1. Facet.FadeLevel points to a dstyles[] index
    /// 2. dstyles[index] can be:
    ///    - Positive: direct texture ID
    ///    - Negative: -StoreyIndex (reference to DStorey entry)
    /// 3. DStorey.Style provides the texture for that floor level
    /// </summary>
    public struct DStoreyRec
    {
        /// <summary>Texture style index for this storey level (0-based)</summary>
        public ushort Style;

        /// <summary>Height/Y position in coarse units</summary>
        public byte Height;

        /// <summary>
        /// Rendering flags:
        /// 0x00 = Ground floor?
        /// 0x01-0x07 = Different storey levels/types
        /// </summary>
        public byte Flags;

        /// <summary>Building ID (1-based) that owns this storey</summary>
        public ushort Building;

        public override string ToString()
        {
            return $"DStorey: Style={Style}, Height={Height}, Flags=0x{Flags:X2}, Building={Building}";
        }
    }

    /// <summary>
    /// View model for displaying DStorey entries in UI
    /// </summary>
    public class DStoreyViewModel
    {
        public int Index { get; set; }
        public ushort Style { get; set; }
        public byte Height { get; set; }
        public byte Flags { get; set; }
        public ushort Building { get; set; }

        /// <summary>File offset for this entry</summary>
        public int FileOffset { get; set; }

        /// <summary>
        /// dstyles indices that reference this storey (via negative values)
        /// </summary>
        public List<int> ReferencedByDStyles { get; set; } = new();

        /// <summary>
        /// Facets that ultimately use this storey (through the dstyles chain)
        /// </summary>
        public List<int> UsedByFacets { get; set; } = new();

        public string FlagsDisplay => $"0x{Flags:X2}";
        public string ReferencesDisplay => ReferencedByDStyles.Count > 0
            ? string.Join(", ", ReferencedByDStyles.Take(5)) + (ReferencedByDStyles.Count > 5 ? "..." : "")
            : "None";
    }

    /// <summary>
    /// Entry in the dstyles[] array
    /// </summary>
    public class DStyleEntry
    {
        public int Index { get; set; }
        public short Value { get; set; }

        /// <summary>True if this is a storey reference (negative value)</summary>
        public bool IsStoreyRef => Value < 0;

        /// <summary>If IsStoreyRef, this is the storey index being referenced</summary>
        public int StoreyIndex => IsStoreyRef ? -Value : 0;

        /// <summary>Display string for the type</summary>
        public string TypeDisplay => IsStoreyRef ? "Storey Ref" : "Texture";

        /// <summary>Facets that reference this dstyles entry via FadeLevel</summary>
        public List<int> UsedByFacets { get; set; } = new();

        public string Display => IsStoreyRef
            ? $"dstyles[{Index}] = {Value} → Storey #{StoreyIndex}"
            : $"dstyles[{Index}] = {Value} (texture)";
    }

    /// <summary>
    /// Summary of indoor building configuration
    /// </summary>
    public class IndoorBuildingSummary
    {
        public int BuildingId { get; set; }
        public byte BuildingType { get; set; }
        public int FacetStart { get; set; }
        public int FacetEnd { get; set; }
        public int FacetCount => FacetEnd - FacetStart;
        public ushort WalkablePointer { get; set; }

        /// <summary>DStorey entries owned by this building</summary>
        public List<DStoreyViewModel> Storeys { get; set; } = new();

        /// <summary>Unique dstyles indices used by this building's facets</summary>
        public List<DStyleEntry> UsedDStyles { get; set; } = new();

        /// <summary>Count of negative dstyles (storey refs) used</summary>
        public int NegativeDStylesCount => UsedDStyles?.Count(d => d.IsStoreyRef) ?? 0;

        /// <summary>Walkable entries for this building</summary>
        public List<int> WalkableIds { get; set; } = new();

        /// <summary>True if building appears to be properly configured for indoor rendering</summary>
        public bool IsProperlyConfigured =>
            BuildingType == 1 &&
            NegativeDStylesCount > 0;

        public string StatusDisplay => IsProperlyConfigured
            ? "✓ Configured"
            : "⚠ Missing storey refs";
    }
}