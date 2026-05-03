// /Services/Accessors/HeightsAccessor.cs
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Heights
{
    /// <summary>
    /// Reads/writes per-tile height directly over MapDataService.MapBytes.
    /// Layout: 8-byte header, then 98304 bytes of (6 bytes per tile).
    /// For each 6-byte slice, byte[4] (5th byte) is the signed height (-127..127).
    /// Tiles are in column-major order: index = tx*128 + ty.
    /// </summary>
    public sealed class HeightsAccessor
    {
        private readonly MapDataService _data;
        private const int HeaderBytes = TextureFormatConstants.HeaderBytes;
        private const int BytesPerTile = TextureFormatConstants.BytesPerTile;
        private const int HeightByteIndex = MapFormatConstants.HeightByteIndex; // 0-based within the 6-byte slice

        public HeightsAccessor(MapDataService data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        private static int TileIndex(int tx, int ty)
        {
            if ((uint)tx >= MapConstants.TilesPerSide || (uint)ty >= MapConstants.TilesPerSide)
                throw new ArgumentOutOfRangeException();

            // row-major: X advances first, then Y
            return ty * MapConstants.TilesPerSide + tx;
        }

        private static int FileIndexForTile(int tx, int ty)
        {
            // WPF (tx,ty): tx = columns left?right, ty = rows top?bottom
            // File: axes are transposed AND origin is bottom-right (x?left, y?up).
            // Swap first, then flip, then row-major.
            int fx = MapConstants.TilesPerSide - 1 - ty; // swapped+flipped x
            int fy = MapConstants.TilesPerSide - 1 - tx; // swapped+flipped y
            return fy * MapConstants.TilesPerSide + fx;  // row-major
        }

        private int ByteOffsetForTile(int tx, int ty)
        {
            int tileIndex = FileIndexForTile(tx, ty);
            return HeaderBytes + tileIndex * BytesPerTile + HeightByteIndex;
        }

        public sbyte ReadHeight(int tx, int ty)
        {
            if (!_data.IsLoaded || _data.MapBytes is null) throw new InvalidOperationException("No map loaded.");
            int offs = ByteOffsetForTile(tx, ty);
            return unchecked((sbyte)_data.MapBytes[offs]);
        }

        public void WriteHeight(int tx, int ty, sbyte value)
        {
            if (!_data.IsLoaded || _data.MapBytes is null) throw new InvalidOperationException("No map loaded.");
            int offs = ByteOffsetForTile(tx, ty);
            _data.MapBytes[offs] = unchecked((byte)value);
            _data.MarkDirty();
            // Later: raise a per-tile changed event so the heights layer can invalidate a small rect.

            // NEW: notify listeners that this tile changed
            HeightsChangeBus.Instance.NotifyTile(tx, ty);
        }
        public int WriteHeightRegion(int tx0, int ty0, int tx1, int ty1, sbyte value)
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                throw new InvalidOperationException("No map loaded.");

            // Clamp and normalize bounds
            int x0 = Math.Max(0, Math.Min(tx0, tx1));
            int x1 = Math.Min(MapConstants.TilesPerSide - 1, Math.Max(tx0, tx1));
            int y0 = Math.Max(0, Math.Min(ty0, ty1));
            int y1 = Math.Min(MapConstants.TilesPerSide - 1, Math.Max(ty0, ty1));

            // Clamp value to file-safe range
            if (value < -127) value = -127;
            if (value > 127) value = 127;

            int changed = 0;

            // Bulk write (no per-tile NotifyTile spam)
            for (int ty = y0; ty <= y1; ty++)
            {
                for (int tx = x0; tx <= x1; tx++)
                {
                    int offs = ByteOffsetForTile(tx, ty);
                    byte b = unchecked((byte)value);

                    if (_data.MapBytes[offs] != b)
                    {
                        _data.MapBytes[offs] = b;
                        changed++;
                    }
                }
            }

            if (changed > 0)
            {
                _data.MarkDirty();
                HeightsChangeBus.Instance.NotifyRegion(x0, y0, x1, y1);
            }

            return changed;
        }
    }
}
