// ============================================================
// UrbanChaosEditor.Shared/Constants/RoofFormatConstants.cs
// ============================================================
// Roof and walkable editing constants.

namespace UrbanChaosEditor.Shared.Constants;

using UrbanChaosEditor.Shared.Models;

/// <summary>
/// Constants for roof/walkable editing and serialization.
/// </summary>
public static class RoofFormatConstants
{
    public const int WarehouseFloorTextureIndex = 28;
    public const int MapHeaderSize = 4;
    public const int MapCellDataSize = SharedMapConstants.TilesPerSide * SharedMapConstants.TilesPerSide * MapFormatConstants.MapCellDataBytes;
    public const int RoofTextureOffset = MapHeaderSize + MapCellDataSize;
    public const int RoofTextureCount = SharedMapConstants.TilesPerSide * SharedMapConstants.TilesPerSide;
    public const int RoofTextureByteSize = RoofTextureCount * sizeof(ushort);
    public const int TotalMapSize = RoofTextureOffset + RoofTextureByteSize;
}
