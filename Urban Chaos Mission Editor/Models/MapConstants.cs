// Thin wrapper around UrbanChaosEditor.Shared.Models.SharedMapConstants.

using SharedMap = UrbanChaosEditor.Shared.Models.SharedMapConstants;

namespace UrbanChaosMissionEditor.Models;

/// <summary>
/// Constants for map data. Re-exports values from <see cref="SharedMap"/>.
/// </summary>
public static class MapConstants
{
    public const int TilesPerSide = SharedMap.TilesPerSide;
    public const int TotalTiles = SharedMap.TotalTiles;
    public const int PixelsPerTile = SharedMap.PixelsPerTile;
    public const int MapPixelSize = SharedMap.MapPixelSize;

    public const int CellsPerSide = SharedMap.CellsPerSide;
    public const int PixelsPerCell = SharedMap.PixelsPerCell;

    // World coordinate range
    public const int WorldSize = SharedMap.WorldSize;
    public const int WorldToPixelDivisor = SharedMap.WorldToPixelDivisor;
}
