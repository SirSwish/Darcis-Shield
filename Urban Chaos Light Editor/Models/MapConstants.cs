// /Models/MapConstants.cs
namespace UrbanChaosLightEditor.Models
{
    /// <summary>
    /// Core constants for Urban Chaos map dimensions.
    /// </summary>
    public static class MapConstants
    {
        /// <summary>Size of each tile in UI pixels (64).</summary>
        public const int TileSize = 64;

        /// <summary>Number of tiles per side (128).</summary>
        public const int TilesPerSide = 128;

        /// <summary>Total map size in UI pixels (8192 = 128 * 64).</summary>
        public const int MapPixels = TilesPerSide * TileSize; // 8192
    }
}