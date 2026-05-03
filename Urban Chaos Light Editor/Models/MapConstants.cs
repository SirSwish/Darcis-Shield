// /Models/MapConstants.cs
// Thin wrapper around UrbanChaosEditor.Shared.Models.SharedMapConstants.

using SharedMap = UrbanChaosEditor.Shared.Models.SharedMapConstants;

namespace UrbanChaosLightEditor.Models
{
    /// <summary>
    /// Core constants for Urban Chaos map dimensions.
    /// Re-exports values from <see cref="SharedMap"/>.
    /// </summary>
    public static class MapConstants
    {
        /// <summary>Size of each tile in UI pixels (64).</summary>
        public const int TileSize = SharedMap.TileSize;

        /// <summary>Number of tiles per side (128).</summary>
        public const int TilesPerSide = SharedMap.TilesPerSide;

        /// <summary>Total map size in UI pixels (8192 = 128 * 64).</summary>
        public const int MapPixels = SharedMap.MapPixels;
    }
}
