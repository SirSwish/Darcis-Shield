
namespace UrbanChaosMapEditor.Models.Buildings
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