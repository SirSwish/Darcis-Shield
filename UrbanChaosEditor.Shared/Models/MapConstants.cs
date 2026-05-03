// /Models/MapConstants.cs
// Thin wrapper around SharedMapConstants for backward compatibility.

namespace UrbanChaosEditor.Shared.Models
{
    public static class MapConstants
    {
        public const int TileSize = SharedMapConstants.TileSize;
        public const int TilesPerSide = SharedMapConstants.TilesPerSide;
        public const int MapPixels = SharedMapConstants.MapPixels;

        public const int MapWhoCellTiles = SharedMapConstants.MapWhoCellTiles;
        public const int MapWhoCellSize = SharedMapConstants.MapWhoCellSize;
    }
}
