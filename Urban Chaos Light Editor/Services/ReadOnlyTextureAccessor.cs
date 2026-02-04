// /Services/ReadOnlyTexturesAccessor.cs
using System;
using UrbanChaosLightEditor.Models;

namespace UrbanChaosLightEditor.Services
{
    /// <summary>
    /// Read-only accessor for texture data in .iam files.
    /// </summary>
    public sealed class ReadOnlyTexturesAccessor
    {
        private readonly ReadOnlyMapDataService _data;
        private const int HeaderBytes = 8;
        private const int BytesPerTile = 6;

        public ReadOnlyTexturesAccessor(ReadOnlyMapDataService data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        private static int FileIndexForTile(int tx, int ty)
        {
            int fx = MapConstants.TilesPerSide - 1 - ty;
            int fy = MapConstants.TilesPerSide - 1 - tx;
            return fy * MapConstants.TilesPerSide + fx;
        }

        private static int TileOffset(int tx, int ty) =>
            HeaderBytes + FileIndexForTile(tx, ty) * BytesPerTile;

        public int ReadSaveType()
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                throw new InvalidOperationException("No map loaded.");
            return BitConverter.ToInt32(_data.MapBytes, 0);
        }

        public int ReadTextureWorld()
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                throw new InvalidOperationException("No map loaded.");

            var bytes = _data.MapBytes;
            int saveType = ReadSaveType();
            int offset = saveType >= 25 ? bytes.Length - 2004 : bytes.Length - 4;
            return BitConverter.ToInt32(bytes, offset);
        }

        public (string relativeKey, int rotationDeg) GetTileTextureKeyAndRotation(int tx, int ty)
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                throw new InvalidOperationException("No map loaded.");

            int offs = TileOffset(tx, ty);

            byte textureByte = _data.MapBytes[offs + 0];
            byte combinedByte = _data.MapBytes[offs + 1];

            string folderKey;
            int logicalNumber;

            switch (combinedByte & 0x03)
            {
                case 0: // worldN
                    {
                        int world = ReadTextureWorld();
                        folderKey = $"world{world}";
                        logicalNumber = textureByte;
                        break;
                    }
                case 1: // shared (offset by +256)
                    folderKey = "shared";
                    logicalNumber = textureByte + 256;
                    break;

                case 2: // shared/prims (offset +64)
                case 3:
                    folderKey = "shared_prims";
                    unchecked { logicalNumber = (sbyte)textureByte + 64; }
                    break;

                default:
                    folderKey = "shared";
                    logicalNumber = textureByte;
                    break;
            }

            string id3 = Math.Clamp(logicalNumber, -999, 999).ToString("000");
            string relativeKey = $"{folderKey}_{id3}";

            int rotationIndex = (combinedByte >> 2) & 0x03;
            int rotationDeg = rotationIndex switch
            {
                0 => 180,
                1 => 90,
                2 => 0,
                3 => 270,
                _ => 0
            };

            return (relativeKey, rotationDeg);
        }
    }
}