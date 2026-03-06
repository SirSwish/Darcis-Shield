// Updated FacetTemplate class.
// This replaces the existing FacetTemplate definition in AddWallWindow_xaml.cs (line ~362).
//
// NEW FIELDS:
//   PaintBytes          — per-column paint bytes for the outside face (enables DStorey creation)
//   InteriorStyleId     — TMA style for warehouse interior face (0 = same as RawStyleId)
//   InteriorPaintBytes  — per-column paint bytes for warehouse interior face
//
// When PaintBytes is non-null and non-empty, BuildingAdder will:
//   1. Create a DStorey entry with RawStyleId as the base style
//   2. Write PaintBytes into paint_mem
//   3. Set the dstyle entry to -storeyIndex (negative = painted reference)
//
// When PaintBytes is null or empty, BuildingAdder writes a positive raw style id.

using UrbanChaosMapEditor.Models;

namespace UrbanChaosMapEditor.Views.Dialogs.Buildings
{
    public sealed class FacetTemplate
    {
        public FacetType Type { get; init; }
        public byte Height { get; init; }
        public byte FHeight { get; init; }
        public byte BlockHeight { get; init; }
        public short Y0 { get; init; }
        public short Y1 { get; init; }

        /// <summary>
        /// The Raw Style ID from the TMA (style.tma).
        /// This is NOT an index into dstyles[] — BuildingAdder will allocate
        /// dstyles[] entries and write this value (or a negative DStorey ref) into them.
        /// </summary>
        public ushort RawStyleId { get; init; }

        public FacetFlags Flags { get; init; }
        public int BuildingId1 { get; init; }
        public int Storey { get; init; }

        /// <summary>
        /// Optional per-column paint bytes for the outside face.
        /// Each byte: lower 7 bits = texture tile id, bit 7 = flip flag.
        /// A zero byte means "use the base style (RawStyleId) at this position".
        ///
        /// When non-null and non-empty, BuildingAdder creates:
        ///   - A DStorey entry (Style=RawStyleId, PaintIndex=offset, Count=length)
        ///   - Copies these bytes into paint_mem
        ///   - Sets dstyles[StyleIndex] = -storeyIndex
        ///
        /// When null, BuildingAdder writes dstyles[StyleIndex] = RawStyleId (positive = flat).
        /// </summary>
        public byte[]? PaintBytes { get; init; }

        /// <summary>
        /// TMA style for the warehouse interior face.
        /// Only used when the target building is a warehouse (BuildingType.Warehouse).
        /// If 0 or not set, defaults to RawStyleId.
        /// </summary>
        public ushort InteriorStyleId { get; init; }

        /// <summary>
        /// Optional per-column paint bytes for the warehouse interior face.
        /// Same format as PaintBytes. Only used for warehouse buildings.
        /// </summary>
        public byte[]? InteriorPaintBytes { get; init; }
    }
}