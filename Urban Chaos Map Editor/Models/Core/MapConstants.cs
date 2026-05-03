// /Models/MapConstants.cs
// Thin wrapper around UrbanChaosEditor.Shared.Models.SharedMapConstants.

using SharedMap = UrbanChaosEditor.Shared.Models.SharedMapConstants;

namespace UrbanChaosMapEditor.Models.Core
{
    public static class MapConstants
    {
        public const int TileSize = SharedMap.TileSize;
        public const int TilesPerSide = SharedMap.TilesPerSide;
        public const int MapPixels = SharedMap.MapPixels;

        public const int MapWhoCellTiles = SharedMap.MapWhoCellTiles;
        public const int MapWhoCellSize = SharedMap.MapWhoCellSize;
    }
}
