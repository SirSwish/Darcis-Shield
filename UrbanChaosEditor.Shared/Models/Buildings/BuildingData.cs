// Models/BuildingData.cs
using System;

namespace UrbanChaosMapEditor.Models
{
    // ----- DBuilding (what we actually use right now) -----
    // Stored 1-based facet range: [StartFacet .. EndFacet) for the building.
    public readonly struct DBuildingRec
    {
        // World position (as stored in the map)
        public int WorldX { get; }
        public int WorldY { get; }
        public int WorldZ { get; }

        // 1-based facet range [StartFacet .. EndFacet)
        public ushort StartFacet { get; }
        public ushort EndFacet { get; }

        // 1-based index into DWalkable; 0 = none
        public ushort Walkable { get; }

        public byte Counter0 { get; }
        public byte Counter1 { get; }

        // If this building is a warehouse, this is an index into WARE_ware[]
        public byte Ware { get; }

        // Raw type byte from DBuilding.Type
        public byte Type { get; }

        // Typed view of the raw byte
        public BuildingType BuildingType => (BuildingType)Type;

        public string TypeDisplay => BuildingType switch
        {
            Models.BuildingType.House => "House",
            Models.BuildingType.Warehouse => "Warehouse",
            Models.BuildingType.Office => "Office",
            Models.BuildingType.Apartment => "Apartment",
            Models.BuildingType.CrateIn => "Crate (In)",
            Models.BuildingType.CrateOut => "Crate (Out)",
            _ => $"Unknown ({Type})"
        };

        public bool HasWalkable => Walkable != 0;

        // Old ctor kept for any callers that only cared about the facet range
        public DBuildingRec(ushort startFacet, ushort endFacet)
            : this(0, 0, 0, startFacet, endFacet, 0, 0, 0, 0, 0)
        {
        }

        // Main ctor (used by BuildingsAccessor)
        public DBuildingRec(
            int worldX,
            int worldY,
            int worldZ,
            ushort startFacet,
            ushort endFacet,
            ushort walkable,
            byte counter0,
            byte counter1,
            byte ware,
            byte type)
        {
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            StartFacet = startFacet;
            EndFacet = endFacet;
            Walkable = walkable;
            Counter0 = counter0;
            Counter1 = counter1;
            Ware = ware;
            Type = type;
        }
    }

    // ----- Facet enums (unchanged) -----
    public enum FacetType : byte
    {
        Normal = 1,
        Roof = 2,
        Wall = 3,
        RoofQuad = 4,
        FloorPoints = 5,
        FireEscape = 6,
        Staircase = 7,
        Skylight = 8,
        Cable = 9,
        Fence = 10,
        FenceBrick = 11,
        Ladder = 12,
        FenceFlat = 13,
        Trench = 14,
        JustCollision = 15,
        Partition = 16,
        Inside = 17,
        Door = 18,
        InsideDoor = 19,
        OInside = 20,
        OutsideDoor = 21,
        NormalFoundation = 100
    }

    [Flags]
    public enum FacetFlags : ushort
    {
        Invisible = 1 << 0,
        Inside = 1 << 3,
        Dlit = 1 << 4,
        HugFloor = 1 << 5,
        Electrified = 1 << 6,
        TwoSided = 1 << 7,
        Unclimbable = 1 << 8,
        OnBuilding = 1 << 9,
        BarbTop = 1 << 10,
        SeeThrough = 1 << 11,
        Open = 1 << 12,
        Deg90 = 1 << 13,
        TwoTextured = 1 << 14,
        FenceCut = 1 << 15
    }

    public enum BuildingType : byte
    {
        House = 0,
        Warehouse = 1,
        Office = 2,
        Apartment = 3,
        CrateIn = 4,
        CrateOut = 5
    }

    // ----- DFacet (expanded to full C struct layout; backward compatible) -----
    //
    // struct DFacet {
    //   UBYTE FacetType; UBYTE Height; UBYTE x[2]; SWORD Y[2]; UBYTE z[2];
    //   UWORD FacetFlags; UWORD StyleIndex; UWORD Building; UWORD DStorey;
    //   UBYTE FHeight; UBYTE BlockHeight; UBYTE Open; UBYTE Dfcache;
    //   UBYTE Shake; UBYTE CutHole; UBYTE Counter[2];
    // }
    //
    // IMPORTANT (CABLE SPECIAL-CASE — from create_cable_dfacet):
    //   Height     = segment count along the cable
    //   StyleIndex = step_angle1  (SWORD)  -> treat as *signed* when Type==Cable
    //   Building   = step_angle2  (SWORD)  -> treat as *signed* when Type==Cable
    //   FHeight    = second “mode” value (TextureStyle2 for the source wall)
    // For non-cable facets, StyleIndex is the index into dstyles[], and Building is 1-based building id.
    public readonly struct DFacetRec
    {
        public readonly FacetType Type;
        public readonly byte X0, Z0, X1, Z1;

        // For most facets: coarse height/segments (often connect_count*4).
        // For cables: number of segments along the cable.
        public readonly byte Height;

        // For most facets: fine height/foundation count.
        // For cables: mode (wall_list[wall].TextureStyle2).
        public readonly byte FHeight;

        // For most facets: index into dstyles[] (signed table).
        // For cables: holds step_angle1 (written as SWORD in the game).
        public readonly ushort StyleIndex;

        // For most facets: 1-based building id.
        // For cables: holds step_angle2 (written as SWORD in the game) — NOT a building id.
        public readonly ushort Building;  // see cable helpers below

        // 1-based storey id (DStorey) when applicable; not meaningful for cables.
        public readonly ushort Storey;

        public readonly FacetFlags Flags;

        // World-space Y at the endpoints (already resolved by the engine for cables; floors/others depend on flags).
        public readonly short Y0;         // world Y at (x0,z0)
        public readonly short Y1;         // world Y at (x1,z1)

        public readonly byte BlockHeight;
        public readonly byte Open;        // door openness (for outside/inside door types)
        public readonly byte Dfcache;     // index into a night/light cache (engine-side)
        public readonly byte Shake;
        public readonly byte CutHole;
        public readonly byte Counter0;
        public readonly byte Counter1;

        // Old ctor (kept for compatibility)
        public DFacetRec(
            FacetType type, byte x0, byte z0, byte x1, byte z1,
            byte height, byte fheight, ushort style, ushort building, ushort storey, FacetFlags flags)
        {
            Type = type;
            X0 = x0; Z0 = z0; X1 = x1; Z1 = z1;
            Height = height; FHeight = fheight;
            StyleIndex = style;
            Building = building; Storey = storey;
            Flags = flags;

            // default the extended fields
            Y0 = 0; Y1 = 0;
            BlockHeight = 0;
            Open = 0;
            Dfcache = 0;
            Shake = 0;
            CutHole = 0;
            Counter0 = 0;
            Counter1 = 0;
        }

        // Full ctor (used by the enhanced parser)
        public DFacetRec(
            FacetType type, byte x0, byte z0, byte x1, byte z1,
            byte height, byte fheight, ushort style, ushort building, ushort storey, FacetFlags flags,
            short y0, short y1, byte blockHeight, byte open, byte dfcache, byte shake, byte cutHole, byte counter0, byte counter1)
        {
            Type = type;
            X0 = x0; Z0 = z0; X1 = x1; Z1 = z1;
            Height = height; FHeight = fheight;
            StyleIndex = style;
            Building = building; Storey = storey;
            Flags = flags;

            Y0 = y0; Y1 = y1;
            BlockHeight = blockHeight;
            Open = open;
            Dfcache = dfcache;
            Shake = shake;
            CutHole = cutHole;
            Counter0 = counter0;
            Counter1 = counter1;
        }

        // -------- Cable helpers (safe, read-only) --------
        public bool IsCable => Type == FacetType.Cable;

        /// <summary> Cable: number of rendered segments along the span. </summary>
        public int CableSegments => IsCable ? Height : 0;

        /// <summary> Cable: step_angle1 (signed). For non-cables, 0. </summary>
        public short CableStep1Signed => IsCable ? unchecked((short)StyleIndex) : (short)0;

        /// <summary> Cable: step_angle2 (signed). For non-cables, 0. </summary>
        public short CableStep2Signed => IsCable ? unchecked((short)Building) : (short)0;

        /// <summary> Cable: raw unsigned views of the step fields, if you prefer. </summary>
        public ushort CableStep1 => IsCable ? StyleIndex : (ushort)0;
        public ushort CableStep2 => IsCable ? Building : (ushort)0;

        /// <summary> Cable: mode value (TextureStyle2 in source), carried in FHeight. </summary>
        public int CableMode => IsCable ? FHeight : 0;
    }

    public sealed class CableFacet
    {
        public int FacetIndex { get; init; }

        // Endpoints in world coords (same units as DFacet → engine uses)
        public int WorldX1 { get; init; }
        public int WorldY1 { get; init; }
        public int WorldZ1 { get; init; }
        public int WorldX2 { get; init; }
        public int WorldY2 { get; init; }
        public int WorldZ2 { get; init; }

        public int SegmentCount { get; init; }       // from Height
        public short SagBase { get; init; }          // from FHeight
        public short SagAngleDelta1 { get; init; }   // from StyleIndex
        public short SagAngleDelta2 { get; init; }   // from Building

        // Optional: raw DFacet or building index if you want round-trip editing later
        public int BuildingIndex { get; init; }
        public DFacetRec RawFacet { get; init; }
    }

    public readonly record struct DWalkableRec(
        ushort StartPoint,
        ushort EndPoint,
        ushort StartFace3,
        ushort EndFace3,
        ushort StartFace4,
        ushort EndFace4,
        byte X1, byte Z1, byte X2, byte Z2,
        byte Y,
        byte StoreyY,
        ushort Next,
        ushort Building
    );

    public readonly record struct RoofFace4Rec(
        short Y,
        sbyte DY0, sbyte DY1, sbyte DY2,
        byte DrawFlags,
        byte RX,
        byte RZ,
        short Next
    );

    // ----- Super-map aggregate we return from the parser -----
    public sealed class BuildingArrays
    {
        public int StartOffset { get; init; }
        public int Length { get; init; }

        // NEW: absolute start of the facets table
        public int FacetsStart { get; init; }

        // Offsets (debugging)
        public int IndoorsStart { get; init; }
        public int WalkablesStart { get; init; }

        public readonly record struct InsideStoreyRec(
    byte MinX, byte MinZ, byte MaxX, byte MaxZ,
    ushort InsideBlock, ushort StairCaseHead, ushort TexType,
    ushort FacetStart, ushort FacetEnd,
    short StoreyY,
    ushort Building,
    ushort Dummy0, ushort Dummy1);

        public readonly record struct StaircaseRec(
            byte X, byte Z, byte Flags, byte Id,
            short NextStairs, short DownInside, short UpInside);

        // Indoors (raw until we model it)
        public int NextInsideStorey { get; init; }
        public int NextInsideStair { get; init; }
        public int NextInsideBlock { get; init; }

        // Typed indoors arrays (what the accessor expects)
        public InsideStoreyRec[] InsideStoreys { get; init; } = Array.Empty<InsideStoreyRec>();
        public StaircaseRec[] InsideStairs { get; init; } = Array.Empty<StaircaseRec>();

        // Alias / canonical name for “where walkables start”
        public int WalkablesOffset { get; init; }  // keep WalkablesStart too if you lik

        public byte[] InsideStoreysRaw { get; init; } = Array.Empty<byte>();
        public byte[] InsideStairsRaw { get; init; } = Array.Empty<byte>();
        public byte[] InsideBlock { get; init; } = Array.Empty<byte>();

        public int TailOffset { get; init; }        // absolute file offset where tail starts
        public byte[] TailBytes { get; init; } = Array.Empty<byte>(); // everything after dstoreys within the region


        // Walkables (raw until we model it)
        public ushort NextDWalkable { get; init; }
        public ushort NextRoofFace4 { get; init; }

        public byte[] DWalkablesRaw { get; init; } = Array.Empty<byte>();
        public byte[] RoofFaces4Raw { get; init; } = Array.Empty<byte>();

        public DWalkableRec[] Walkables { get; init; } = Array.Empty<DWalkableRec>();
        public RoofFace4Rec[] RoofFaces4 { get; init; } = Array.Empty<RoofFace4Rec>();

        public DBuildingRec[] Buildings { get; init; } = Array.Empty<DBuildingRec>();
        public DFacetRec[] Facets { get; init; } = Array.Empty<DFacetRec>();

        // Style table and “paint_mem” blob (as stored in file).
        // NOTE: dstyles is a SIGNED table in the game; keep as short[] here.
        public short[] Styles { get; init; } = Array.Empty<short>();
        public byte[] PaintMem { get; init; } = Array.Empty<byte>();

        // NEW: dstorey table (each storey points at a slice inside PaintMem).
        // Matches the C layout implied by add_painted_textures():
        //   U16 StyleIndex; U16 PaintIndex; U16 Count
        public readonly record struct DStoreyRec(ushort StyleIndex, ushort PaintIndex, sbyte Count, byte Padding);
        public DStoreyRec[] Storeys { get; init; } = Array.Empty<DStoreyRec>();
        public CableFacet[] Cables { get; init; } = Array.Empty<CableFacet>();

        // Header counters as read from the block.
        public ushort NextDBuilding { get; init; }
        public ushort NextDFacet { get; init; }
        public ushort NextDStyle { get; init; }
        public ushort NextPaintMem { get; init; }
        public ushort NextDStorey { get; init; }
        public int SaveType { get; init; }

        /// <summary>
        /// Returns the paint_mem slice for a given storey, or empty if OOB/corrupt.
        /// Each byte: lower 7 bits = page (0..127), high bit often used as a flag.
        /// </summary>
        public ReadOnlySpan<byte> GetPaintForStorey(int storeyIndex)
        {
            if ((uint)storeyIndex >= (uint)Storeys.Length) return ReadOnlySpan<byte>.Empty;
            var s = Storeys[storeyIndex];
            int start = s.PaintIndex;
            int count = s.Count;
            if (count <= 0) return ReadOnlySpan<byte>.Empty;
            int end = start + count;
            if (start < 0 || end > PaintMem.Length) return ReadOnlySpan<byte>.Empty;
            return new ReadOnlySpan<byte>(PaintMem, start, count);
        }
    }
}
