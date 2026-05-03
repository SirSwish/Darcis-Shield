// ============================================================
// UrbanChaosEditor.Shared/Models/SharedMapConstants.cs
// ============================================================
// Single source of truth for Urban Chaos map dimensions.

namespace UrbanChaosEditor.Shared.Models;

/// <summary>
/// Shared map constants for Urban Chaos editors.
/// All editors should reference these values for consistency.
/// </summary>
public static class SharedMapConstants
{
    // ---- Tiles ----

    /// <summary>Size of each tile in pixels (64).</summary>
    public const int TileSize = 64;

    /// <summary>Alias for TileSize.</summary>
    public const int PixelsPerTile = TileSize;

    /// <summary>Number of tiles per side of the map (128).</summary>
    public const int TilesPerSide = 128;

    /// <summary>Total tile count (128 × 128 = 16384).</summary>
    public const int TotalTiles = TilesPerSide * TilesPerSide;

    /// <summary>Total map size in pixels (8192 = 128 × 64).</summary>
    public const int MapPixelSize = TilesPerSide * TileSize;

    /// <summary>Alias for MapPixelSize.</summary>
    public const int MapPixels = MapPixelSize;

    // ---- MapWho cells ----

    /// <summary>Tiles per MapWho cell side (4).</summary>
    public const int MapWhoCellTiles = 4;

    /// <summary>Size of one MapWho cell in pixels (256 = 4 × 64).</summary>
    public const int MapWhoCellSize = MapWhoCellTiles * TileSize;

    /// <summary>Number of MapWho cells per side (32 = 128 / 4).</summary>
    public const int MapWhoCellsPerSide = TilesPerSide / MapWhoCellTiles;

    // ---- Object/grid space (aliases for consistency) ----

    /// <summary>Number of object-space cells per side (32). Alias of MapWhoCellsPerSide.</summary>
    public const int CellsPerSide = MapWhoCellsPerSide;

    /// <summary>Pixels per object-space cell (256). Alias of MapWhoCellSize.</summary>
    public const int PixelsPerCell = MapWhoCellSize;

    // ---- World coordinates ----

    /// <summary>Full world coordinate range (32768).</summary>
    public const int WorldSize = 32768;

    /// <summary>Divisor converting world coordinates to UI pixels (32768 / 8192 = 4).</summary>
    public const int WorldToPixelDivisor = WorldSize / MapPixelSize;
}
