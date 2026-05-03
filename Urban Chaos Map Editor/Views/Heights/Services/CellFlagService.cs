// /Services/Terrain/CellFlagsLayerService.cs
// Reads PAP_HI flags for all 128x128 cells and provides flag data
// for the Cell Flags visual layer on the map canvas.

using System;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosEditor.Shared.Models;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Heights
{
    public static class CellFlagsLayerService
    {
        private const int HeaderBytes = TextureFormatConstants.HeaderBytes;
        private const int BytesPerTile = TextureFormatConstants.BytesPerTile;
        private const int TilesPerSide = SharedMapConstants.TilesPerSide;
        private const int Off_Flags = MapFormatConstants.PapFlagsByteIndex;

        /// <summary>
        /// Returns a 128x128 array of flag values.
        /// Index as [gameX, gameZ].
        /// Returns null if no map is loaded.
        /// </summary>
        public static ushort[,]? ReadAllFlags()
        {
            if (!MapDataService.Instance.IsLoaded)
                return null;

            var bytes = MapDataService.Instance.GetBytesCopy();
            var flags = new ushort[TilesPerSide, TilesPerSide];

            for (int gx = 0; gx < TilesPerSide; gx++)
            {
                for (int gz = 0; gz < TilesPerSide; gz++)
                {
                    int offset = HeaderBytes + (gx * TilesPerSide + gz) * BytesPerTile + Off_Flags;
                    if (offset + 1 < bytes.Length)
                        flags[gx, gz] = (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
                }
            }

            return flags;
        }

        /// <summary>
        /// Read flags for a single cell.
        /// </summary>
        public static ushort ReadFlags(int gameX, int gameZ)
        {
            if (!MapDataService.Instance.IsLoaded) return 0;
            if (gameX < 0 || gameX >= TilesPerSide || gameZ < 0 || gameZ >= TilesPerSide) return 0;

            var bytes = MapDataService.Instance.GetBytesCopy();
            int offset = HeaderBytes + (gameX * TilesPerSide + gameZ) * BytesPerTile + Off_Flags;

            if (offset + 1 >= bytes.Length) return 0;
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }
    }
}