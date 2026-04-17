// Models/Buildings/BuildingSnapshot.cs
// Snapshot of a building's data used for the Move Building feature.
namespace UrbanChaosMapEditor.Models.Buildings
{
    /// <summary>PAP cell data captured before a move, so old cells can be cleared and new cells written.</summary>
    public sealed class SnapshotCellData
    {
        /// <summary>UI tile coord X (0 = left side of map).</summary>
        public int TileX { get; init; }
        /// <summary>UI tile coord Z (0 = top side of map).</summary>
        public int TileZ { get; init; }
        /// <summary>Raw alt byte as stored in PAP (byte 5 per tile).</summary>
        public sbyte Alt { get; init; }
        /// <summary>Raw flags as stored in PAP (bytes 2-3 per tile).</summary>
        public ushort Flags { get; init; }
        /// <summary>Raw texture byte 0 (PAP byte 0).</summary>
        public byte TexByte0 { get; init; }
        /// <summary>Raw texture byte 1 (PAP byte 1 — group tag + rotation).</summary>
        public byte TexByte1 { get; init; }
    }

    /// <summary>
    /// An immutable snapshot of all data for a single building,
    /// captured at the moment the user triggers "Move Building".
    /// </summary>
    public sealed class BuildingSnapshot
    {
        /// <summary>1-based building ID.</summary>
        public int BuildingId1 { get; init; }

        public DBuildingRec Building { get; init; }

        // ── Bounding box in game-tile coords (same coords as facet X/Z fields) ─────
        // MinGameX < MaxGameX, MinGameZ < MaxGameZ
        public int MinGameX { get; init; }
        public int MaxGameX { get; init; }
        public int MinGameZ { get; init; }
        public int MaxGameZ { get; init; }

        /// <summary>
        /// UI-pixel X of the top-left anchor point.
        /// Top-left in UI == highest game X/Z.
        /// AnchorUiX = (128 - MaxGameX) * 64
        /// </summary>
        public int AnchorUiX => (128 - MaxGameX) * 64;

        /// <summary>
        /// UI-pixel Z of the top-left anchor point.
        /// AnchorUiZ = (128 - MaxGameZ) * 64
        /// </summary>
        public int AnchorUiZ => (128 - MaxGameZ) * 64;

        // ── Facet range (0-based inclusive, exclusive) ───────────────────────────────
        /// <summary>0-based first facet index (= Building.StartFacet - 1).</summary>
        public int FacetStart0 { get; init; }
        /// <summary>0-based exclusive end (= Building.EndFacet - 1).</summary>
        public int FacetEnd0 { get; init; }

        // ── Walkables (1-based indices into the DWalkable array) ─────────────────────
        public int[] WalkableIds1 { get; init; } = Array.Empty<int>();

        // ── PAP cell data for all tiles covered by this building's walkables ─────────
        public SnapshotCellData[] Cells { get; init; } = Array.Empty<SnapshotCellData>();

        // ── File offsets captured at snapshot time ────────────────────────────────────
        public int FacetsStart { get; init; }
        public int WalkablesStart { get; init; }
        public ushort NextDWalkable { get; init; }
    }
}
