// ============================================================
// UrbanChaosEditor.Shared/Constants/MapFormatConstants.cs
// ============================================================
// Binary layout constants for map tile/PAP cell data.

namespace UrbanChaosEditor.Shared.Constants;

using UrbanChaosEditor.Shared.Models;

/// <summary>
/// Binary layout constants for map tile and PAP cell data.
/// </summary>
public static class MapFormatConstants
{
    /// <summary>Size of the PAP/map header in bytes.</summary>
    public const int PapHeaderBytes = 8;

    /// <summary>Size of one PAP tile/cell record in bytes.</summary>
    public const int PapTileBytes = 6;

    /// <summary>Size of one map cell record in bytes.</summary>
    public const int MapCellDataBytes = 12;

    /// <summary>Byte offset of the PAP flags byte within a cell.</summary>
    public const int PapFlagsByteIndex = 2;

    /// <summary>Byte offset of the terrain height byte within a tile/cell.</summary>
    public const int HeightByteIndex = 4;

    /// <summary>Byte offset of the roof altitude byte within a PAP cell.</summary>
    public const int PapAltitudeByteIndex = 5;

    /// <summary>Bit shift for packed PAP altitude values.</summary>
    public const int PapAltitudeShift = 3;

    /// <summary>Number of bytes in one full tile-data block.</summary>
    public const int TileDataBytes = SharedMapConstants.TilesPerSide * SharedMapConstants.TilesPerSide * PapTileBytes;
}
