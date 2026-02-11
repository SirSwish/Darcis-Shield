namespace UrbanChaosMissionEditor.Models;

/// <summary>
/// Constants for map data
/// </summary>
public static class MapConstants
{
    public const int TilesPerSide = 128;
    public const int TotalTiles = TilesPerSide * TilesPerSide;
    public const int PixelsPerTile = 64;
    public const int MapPixelSize = TilesPerSide * PixelsPerTile; // 8192

    public const int CellsPerSide = 32;
    public const int PixelsPerCell = 256;

    // World coordinate range
    public const int WorldSize = 32768;
    public const int WorldToPixelDivisor = 4; // 32768 / 8192
}