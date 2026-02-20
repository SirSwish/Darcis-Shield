// ============================================================
// UrbanChaosEditor.Shared/Models/SharedMapConstants.cs
// ============================================================
// Shared map constants used across all editors

namespace UrbanChaosEditor.Shared.Models;

/// <summary>
/// Shared map constants for Urban Chaos editors.
/// All editors should reference these values for consistency.
/// </summary>
public static class SharedMapConstants
{
    /// <summary>Size of each tile in pixels (64)</summary>
    public const int TileSize = 64;

    /// <summary>Alias for TileSize</summary>
    public const int PixelsPerTile = 64;

    /// <summary>Number of tiles per side of the map (128)</summary>
    public const int TilesPerSide = 128;

    /// <summary>Total map size in pixels (8192 = 128 * 64)</summary>
    public const int MapPixelSize = TilesPerSide * TileSize; // 8192

    /// <summary>Alias for MapPixelSize</summary>
    public const int MapPixels = MapPixelSize;

    /// <summary>Size of MapWho cells in pixels (256 = 4 tiles)</summary>
    public const int MapWhoCellSize = 256;

    /// <summary>Number of MapWho cells per side (32)</summary>
    public const int MapWhoCellsPerSide = 32;
}