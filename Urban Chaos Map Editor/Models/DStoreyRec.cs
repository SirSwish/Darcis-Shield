// /Models/DStoreyRec.cs
// Data structures for DStorey (painted texture) entries.
//
// C struct (from supermap.h):
//   struct DStorey {
//       UWORD Style;          // offset 0-1: base texture style (fallback)
//       UWORD Index;          // offset 2-3: index into paint_mem[]
//       SBYTE Count;          // offset 4:   number of paint_mem bytes (+ve count)
//       UBYTE BloodyPadding;  // offset 5:   unused padding
//   };
//
// Chain: Facet.StyleIndex → dstyles[idx] → if negative: DStorey[-value]
//        → paint_mem[DStorey.PaintIndex .. +Count]
//
// Each paint_mem byte: lower 7 bits = texture tile id, bit 7 = flip flag.
// A zero paint byte means "use the base style at this position".

using System;
using System.Collections.Generic;
using System.Linq;

namespace UrbanChaosMapEditor.Models
{
    /// <summary>
    /// DStorey entry — defines a painted-texture slice for a wall band.
    /// Size: 6 bytes.
    ///
    /// The rendering chain:
    /// 1. Facet.StyleIndex → dstyles[] index
    /// 2. dstyles[index] can be:
    ///    - Positive: direct TMA style id (uniform texture)
    ///    - Negative: -StoreyIndex → DStorey entry (per-tile painted texture)
    /// 3. DStorey.Style   = fallback TMA style when paint byte is 0
    ///    DStorey.PaintIndex = offset into paint_mem[]
    ///    DStorey.Count      = number of paint bytes (one per column/tile)
    /// </summary>
    public struct DStoreyRec
    {
        /// <summary>Base/fallback TMA texture style id</summary>
        public ushort Style;

        /// <summary>Index into the paint_mem[] array (start of this storey's paint bytes)</summary>
        public ushort PaintIndex;

        /// <summary>
        /// Number of paint_mem bytes for this storey (positive).
        /// Stored as SBYTE in the engine; negative values have special meaning (unused here).
        /// </summary>
        public sbyte Count;

        /// <summary>Padding byte (unused, preserve original value on round-trip)</summary>
        public byte Padding;

        public override string ToString()
        {
            return $"DStorey: Style={Style}, PaintIndex={PaintIndex}, Count={Count}, Pad=0x{Padding:X2}";
        }
    }

    /// <summary>
    /// View model for displaying DStorey entries in UI.
    /// </summary>
    public class DStoreyViewModel
    {
        public int Index { get; set; }

        /// <summary>Base/fallback TMA texture style id</summary>
        public ushort Style { get; set; }

        /// <summary>Index into paint_mem[]</summary>
        public ushort PaintIndex { get; set; }

        /// <summary>Number of paint bytes</summary>
        public sbyte Count { get; set; }

        /// <summary>Padding byte</summary>
        public byte Padding { get; set; }

        /// <summary>File offset for this entry</summary>
        public int FileOffset { get; set; }

        /// <summary>
        /// dstyles indices that reference this storey (via negative values).
        /// </summary>
        public List<int> ReferencedByDStyles { get; set; } = new();

        /// <summary>
        /// Facets that ultimately use this storey (through the dstyles chain).
        /// </summary>
        public List<int> UsedByFacets { get; set; } = new();

        /// <summary>The actual paint bytes from paint_mem (loaded separately)</summary>
        public byte[] PaintBytes { get; set; } = Array.Empty<byte>();

        public string StyleDisplay => $"Style {Style}";
        public string PaintDisplay => $"PaintIndex={PaintIndex}, Count={Count}";
        public string PaintHex => PaintBytes.Length > 0
            ? string.Join(" ", PaintBytes.Select(b => b.ToString("X2")))
            : "(none)";
        public string ReferencesDisplay => ReferencedByDStyles.Count > 0
            ? string.Join(", ", ReferencedByDStyles.Take(5)) + (ReferencedByDStyles.Count > 5 ? "..." : "")
            : "None";
    }

    /// <summary>
    /// Entry in the dstyles[] array.
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
        public string TypeDisplay => IsStoreyRef ? "Painted (DStorey)" : "Raw Style";

        /// <summary>Facets that reference this dstyles entry via StyleIndex</summary>
        public List<int> UsedByFacets { get; set; } = new();

        public string Display => IsStoreyRef
            ? $"dstyles[{Index}] = {Value} → DStorey #{StoreyIndex}"
            : $"dstyles[{Index}] = {Value} (TMA style)";
    }

    /// <summary>
    /// Summary of a building's painted-texture configuration.
    /// </summary>
    public class IndoorBuildingSummary
    {
        public int BuildingId { get; set; }
        public byte BuildingType { get; set; }
        public int FacetStart { get; set; }
        public int FacetEnd { get; set; }
        public int FacetCount => FacetEnd - FacetStart;
        public ushort WalkablePointer { get; set; }

        /// <summary>DStorey entries referenced by this building's facets</summary>
        public List<DStoreyViewModel> Storeys { get; set; } = new();

        /// <summary>Unique dstyles indices used by this building's facets</summary>
        public List<DStyleEntry> UsedDStyles { get; set; } = new();

        /// <summary>Count of negative dstyles (painted storey refs) used</summary>
        public int PaintedDStylesCount => UsedDStyles?.Count(d => d.IsStoreyRef) ?? 0;

        /// <summary>Count of positive dstyles (raw flat styles) used</summary>
        public int RawDStylesCount => UsedDStyles?.Count(d => !d.IsStoreyRef) ?? 0;

        /// <summary>Walkable entries for this building</summary>
        public List<int> WalkableIds { get; set; } = new();

        /// <summary>True if building has at least some painted textures</summary>
        public bool HasPaintedTextures => PaintedDStylesCount > 0;

        /// <summary>True if building is warehouse type (needs dual dstyles per wall)</summary>
        public bool IsWarehouse => BuildingType == 1;

        public string StatusDisplay
        {
            get
            {
                if (HasPaintedTextures)
                    return $"✓ {PaintedDStylesCount} painted + {RawDStylesCount} raw";
                if (RawDStylesCount > 0)
                    return $"○ {RawDStylesCount} raw (no paint)";
                return "⚠ No styles";
            }
        }
    }
}