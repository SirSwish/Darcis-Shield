// /Services/Accessors/TextureAccessor.cs
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Textures
{
    public sealed class TexturesAccessor
    {
        private readonly MapDataService _data;
        private const int HeaderBytes = 8;
        private const int BytesPerTile = 6;

        public enum TextureGroup { World = 0, Shared = 1, Prims = 2 } // (3 also maps to Prims in v1)

        public TexturesAccessor(MapDataService data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        // Same mapping as HeightsAccessor (don’t break orientation!)
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
            if (!_data.IsLoaded || _data.MapBytes is null) throw new InvalidOperationException("No map loaded.");
            // little-endian int32 at start
            return BitConverter.ToInt32(_data.MapBytes, 0);
        }

        public int ReadTextureWorld()
        {
            if (!_data.IsLoaded || _data.MapBytes is null) throw new InvalidOperationException("No map loaded.");
            var bytes = _data.MapBytes;
            int saveType = ReadSaveType();
            int offset = saveType >= 25 ? bytes.Length - 2004 : bytes.Length - 4;
            return BitConverter.ToInt32(bytes, offset);
        }

        public void WriteTextureWorld(int world)
        {
            if (!_data.IsLoaded || _data.MapBytes is null) throw new InvalidOperationException("No map loaded.");
            var bytes = _data.MapBytes;
            int saveType = ReadSaveType();
            int offset = saveType >= 25 ? bytes.Length - 2004 : bytes.Length - 4;
            var w = BitConverter.GetBytes(world);
            Buffer.BlockCopy(w, 0, bytes, offset, 4);
            _data.MarkDirty();
            TexturesChangeBus.Instance.NotifyChanged(); // repaint textures overlay
        }

        public (string relativeKey, int rotationDeg) GetTileTextureKeyAndRotation(int tx, int ty)
        {
            if (!_data.IsLoaded || _data.MapBytes is null) throw new InvalidOperationException("No map loaded.");
            int offs = TileOffset(tx, ty);

            byte textureByte = _data.MapBytes[offs + 0]; // first of the 6
            byte combinedByte = _data.MapBytes[offs + 1]; // second of the 6

            // Decode folder/type & logical texture number
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
                    unchecked { logicalNumber = (sbyte)textureByte + 64; } // match v1 behavior
                    break;

                default:
                    folderKey = "shared";
                    logicalNumber = textureByte;
                    break;
            }

            // Build relative cache key for TextureCacheService: "<folders>_<###>"
            // e.g. "world20_208", "shared_300", "shared_prims_081"
            string id3 = Math.Clamp(logicalNumber, -999, 999).ToString("000");
            string relativeKey = $"{folderKey}_{id3}";

            // Rotation
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

        public bool TryGetTileTextureSelection(
    int tx,
    int ty,
    out TextureGroup group,
    out int textureNumber,
    out int rotationIndex)
        {
            group = TextureGroup.World;
            textureNumber = 0;
            rotationIndex = 0;

            if (!TryReadTileRecord(tx, ty, out byte textureByte, out byte combinedByte, out byte b2, out int off))
                return false;

            int groupTag = combinedByte & 0x03;
            rotationIndex = (combinedByte >> 2) & 0x03;

            switch (groupTag)
            {
                case 0: // world
                    group = TextureGroup.World;
                    textureNumber = textureByte;
                    break;

                case 1: // shared
                    group = TextureGroup.Shared;
                    textureNumber = textureByte + 256;
                    break;

                case 2: // prims
                case 3:
                    group = TextureGroup.Prims;
                    textureNumber = (sbyte)textureByte + 64;
                    break;

                default:
                    group = TextureGroup.World;
                    textureNumber = textureByte;
                    break;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[TEXACCESSOR] TryGetTileTextureSelection tx={tx} ty={ty} off={off} " +
                $"RAW=({textureByte},{combinedByte},{b2}) " +
                $"groupTag={groupTag} rotIdx={rotationIndex} " +
                $"decoded=({group},{textureNumber},{rotationIndex})");

            return true;
        }

        private bool TryReadTileRecord(int tx, int ty, out byte b0, out byte b1, out byte b2, out int off)
        {
            b0 = b1 = b2 = 0;
            off = -1;

            if (!_data.IsLoaded || _data.MapBytes == null)
                return false;

            if (tx < 0 || tx >= MapConstants.TilesPerSide || ty < 0 || ty >= MapConstants.TilesPerSide)
                return false;

            off = TileOffset(tx, ty);
            var bytes = _data.MapBytes;

            if (off < 0 || off + BytesPerTile > bytes.Length)
                return false;

            b0 = bytes[off + 0];
            b1 = bytes[off + 1];
            b2 = bytes[off + 2];
            return true;
        }

        public void WriteTileTexture(int tx, int ty, TextureGroup group, int texNumber, int rotationIndex, int currentWorld)
        {
            if (!_data.IsLoaded || _data.MapBytes is null) throw new InvalidOperationException("No map loaded.");
            int offs = TileOffset(tx, ty);

            // Build combined byte: lower 2 bits = group tag, upper = rotationIndex (0..3)
            int groupTag = group switch
            {
                TextureGroup.World => 0,
                TextureGroup.Shared => 1,
                TextureGroup.Prims => 2, // v1 treats 2 or 3 as prims; we use 2
                _ => 0
            };
            byte combined = (byte)((rotationIndex & 0x03) << 2 | (groupTag & 0x03));

            // Compute first byte (texture id within group)
            byte textureByte = group switch
            {
                TextureGroup.World => (byte)Math.Clamp(texNumber, 0, 255),
                TextureGroup.Shared => (byte)Math.Clamp(texNumber - 256, 0, 255),
                TextureGroup.Prims =>
                    unchecked((byte)(sbyte)Math.Clamp(texNumber - 64, sbyte.MinValue, sbyte.MaxValue)),
                _ => 0
            };

            _data.MapBytes[offs + 0] = textureByte;   // first byte = tex id
            _data.MapBytes[offs + 1] = combined;      // second byte = group + rotation
            _data.MarkDirty();

            // Inform the textures layer to repaint
            TexturesChangeBus.Instance.NotifyChanged();
        }
    }
}
