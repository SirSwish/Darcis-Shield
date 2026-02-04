// /Models/MapConstants.cs

namespace UrbanChaosMapEditor.Models
{
    public static class MapConstants
    {
        public const int TileSize = 64;           // each texture “tile” is 64×64
        public const int TilesPerSide = 128;      // 128×128 tiles
        public const int MapPixels = TileSize * TilesPerSide; // 8192

        public const int MapWhoCellTiles = 4;                    // 4x4 tiles
        public const int MapWhoCellSize = MapWhoCellTiles * TileSize; // 256
    }
}
