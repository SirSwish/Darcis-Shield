// ============================================================
// UrbanChaosEditor.Shared/Constants/BuildingFormatConstants.cs
// ============================================================
// Binary layout constants for the building (-super map-) block
// of the .iam file format (V1).

namespace UrbanChaosEditor.Shared.Constants;

/// <summary>
/// Binary layout constants for the buildings block (V1).
/// Sizes match the on-disk record sizes used by the game.
/// </summary>
public static class BuildingFormatConstants
{
    /// <summary>Size of the buildings header in bytes.</summary>
    public const int HeaderSize = 48;

    /// <summary>Size of one DBuilding record in bytes.</summary>
    public const int DBuildingSize = 24;

    /// <summary>Size of one DFacet record in bytes.</summary>
    public const int DFacetSize = 26;

    /// <summary>Size of one DStorey record (U16 Style; U16 PaintIndex; SBYTE Count; UBYTE pad).</summary>
    public const int DStoreySize = 6;

    /// <summary>Size of one DStyle entry in bytes.</summary>
    public const int DStyleSize = 2;

    /// <summary>Size of one DWalkable record in bytes.</summary>
    public const int DWalkableSize = 22;

    /// <summary>Size of one RoofFace4 record in bytes.</summary>
    public const int RoofFace4Size = 10;

    /// <summary>Size of the post-buildings padding/header block.</summary>
    public const int AfterBuildingsPad = 14;

    /// <summary>Size of one InsideStorey record in bytes.</summary>
    public const int InsideStoreySize = 22;

    /// <summary>Size of one Staircase record in bytes.</summary>
    public const int StaircaseSize = 10;

    /// <summary>Offset of the facet type byte in a DFacet record.</summary>
    public const int DFacetOffsetType = 0;

    /// <summary>Offset of the facet height byte in a DFacet record.</summary>
    public const int DFacetOffsetHeight = 1;

    public const int DFacetOffsetX0 = 2;
    public const int DFacetOffsetX1 = 3;
    public const int DFacetOffsetY0 = 4;
    public const int DFacetOffsetY1 = 6;
    public const int DFacetOffsetZ0 = 8;
    public const int DFacetOffsetZ1 = 9;
    public const int DFacetOffsetFlags = 10;
    public const int DFacetOffsetStyle = 12;
    public const int DFacetOffsetBuilding = 14;
    public const int DFacetOffsetStorey = 16;
    public const int DFacetOffsetFHeight = 18;

    public const byte StandardDoorHeight = 4;
    public const byte StandardGateHeight = 2;
    public const byte TallGateHeight = 3;
    public const byte LowFenceHeight = 1;

    public const ushort WoodenDoorStyle = 15;
    public const ushort MetalDoorStyle = 16;
    public const ushort GlassDoorStyle = 17;
    public const ushort ChainLinkGateStyle = 22;
    public const ushort MetalGateStyle = 23;
    public const ushort WoodenGateStyle = 24;
    public const ushort WarehouseDoorStyle = 25;
}
