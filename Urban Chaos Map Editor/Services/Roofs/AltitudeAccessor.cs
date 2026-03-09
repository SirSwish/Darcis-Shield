// /Services/Accessors/AltitudeAccessor.cs
using System.Diagnostics;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Roofs
{
    /// <summary>
    /// PAP_HI flags from pap.h
    /// </summary>
    [Flags]
    public enum PapFlags : ushort
    {
        None = 0,
        Shadow1 = 1 << 0,
        Shadow2 = 1 << 1,
        Shadow3 = 1 << 2,
        Reflective = 1 << 3,
        Hidden = 1 << 4,
        SinkSquare = 1 << 5,    // Lowers the floorsquare to create a curb
        SinkPoint = 1 << 6,     // Transform the point on the lower level
        NoUpper = 1 << 7,       // Don't transform the point on the upper level
        NoGo = 1 << 8,          // A square nobody is allowed onto
        AnimTmap = 1 << 9,      // Animated texture map
        RoofExists = 1 << 9,    // Same bit as AnimTmap - roof exists on this tile
        Zone1 = 1 << 10,
        Zone2 = 1 << 11,
        Zone3 = 1 << 12,
        Zone4 = 1 << 13,
        Wander = 1 << 14,
        FlatRoof = 1 << 14,     // Same bit as Wander - flat roof flag
        Water = 1 << 15
    }

    /// <summary>
    /// Reads/writes the floor altitude (PAP_HI.Alt) and flags for each tile.
    /// This is the altitude at which the floor cell begins, used for roofs
    /// and elevated floor surfaces.
    /// 
    /// Layout: 8-byte header, then 98304 bytes of (6 bytes per tile).
    /// Byte layout per tile (per PAP_Hi struct):
    ///   [0-1] Texture (UWORD)
    ///   [2-3] Flags (UWORD)
    ///   [4]   Height (SBYTE) - terrain vertex height offset (used by HeightsAccessor)
    ///   [5]   Alt (SBYTE) - floor altitude for roofs/elevated areas
    /// 
    /// NOTE: The struct in pap.h shows Alt before Height, but HeightsAccessor
    /// uses byte 4 for terrain heights, so we use byte 5 for floor altitude.
    /// 
    /// The Alt value is stored as SBYTE (-127..127) and represents altitude
    /// divided by 8 (PAP_ALT_SHIFT = 3). To get world altitude: Alt << 3.
    /// To set from world altitude: Alt = worldY >> 3.
    /// </summary>
    public sealed class AltitudeAccessor
    {
        private readonly MapDataService _data;
        private const int HeaderBytes = 8;
        private const int BytesPerTile = 6;
        private const int FlagsByteIndex = 2;  // bytes 2-3 are Flags (UWORD)
        private const int AltByteIndex = 5;    // byte 5 is Alt (floor altitude)

        /// <summary>
        /// The shift value used to convert between stored Alt and world altitude.
        /// World altitude = Alt << PAP_ALT_SHIFT
        /// Alt = World altitude >> PAP_ALT_SHIFT
        /// </summary>
        public const int PAP_ALT_SHIFT = 3;

        public AltitudeAccessor(MapDataService data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Convert WPF tile coordinates to file index.
        /// Same transformation as HeightsAccessor and TexturesAccessor.
        /// </summary>
        private static int FileIndexForTile(int tx, int ty)
        {
            // WPF (tx,ty): tx = columns left?right, ty = rows top?bottom
            // File: axes are transposed AND origin is bottom-right (x?left, y?up).
            // Swap first, then flip, then row-major.
            int fx = MapConstants.TilesPerSide - 1 - ty; // swapped+flipped x
            int fy = MapConstants.TilesPerSide - 1 - tx; // swapped+flipped y
            return fy * MapConstants.TilesPerSide + fx;  // row-major
        }

        private int TileBaseOffset(int tx, int ty)
        {
            int tileIndex = FileIndexForTile(tx, ty);
            return HeaderBytes + tileIndex * BytesPerTile;
        }

        #region Altitude (Alt field - byte 5)

        /// <summary>
        /// Read the raw Alt value for a tile (as stored, -127..127).
        /// </summary>
        public sbyte ReadAltRaw(int tx, int ty)
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                throw new InvalidOperationException("No map loaded.");

            if ((uint)tx >= MapConstants.TilesPerSide || (uint)ty >= MapConstants.TilesPerSide)
                throw new ArgumentOutOfRangeException($"Tile coordinates ({tx},{ty}) out of range.");

            int offs = TileBaseOffset(tx, ty) + AltByteIndex;
            return unchecked((sbyte)_data.MapBytes[offs]);
        }

        /// <summary>
        /// Write the raw Alt value for a tile (as stored, -127..127).
        /// </summary>
        public void WriteAltRaw(int tx, int ty, sbyte value)
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                throw new InvalidOperationException("No map loaded.");

            if ((uint)tx >= MapConstants.TilesPerSide || (uint)ty >= MapConstants.TilesPerSide)
                throw new ArgumentOutOfRangeException($"Tile coordinates ({tx},{ty}) out of range.");

            int offs = TileBaseOffset(tx, ty) + AltByteIndex;
            _data.MapBytes[offs] = unchecked((byte)value);
            _data.MarkDirty();

            AltitudeChangeBus.Instance.NotifyTile(tx, ty);
        }

        /// <summary>
        /// Read the world-space altitude for a tile (Alt shifted by PAP_ALT_SHIFT).
        /// </summary>
        public int ReadWorldAltitude(int tx, int ty)
        {
            sbyte rawAlt = ReadAltRaw(tx, ty);
            return rawAlt << PAP_ALT_SHIFT;
        }

        /// <summary>
        /// Write the world-space altitude for a tile (will be shifted down by PAP_ALT_SHIFT).
        /// Values are clamped to fit in SBYTE range after shifting.
        /// </summary>
        public void WriteWorldAltitude(int tx, int ty, int worldAltitude)
        {
            int rawAlt = worldAltitude >> PAP_ALT_SHIFT;
            rawAlt = Math.Clamp(rawAlt, sbyte.MinValue, sbyte.MaxValue);
            WriteAltRaw(tx, ty, (sbyte)rawAlt);
        }

        #endregion

        #region Flags (bytes 2-3)

        /// <summary>
        /// Read the flags for a tile.
        /// </summary>
        public PapFlags ReadFlags(int tx, int ty)
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                throw new InvalidOperationException("No map loaded.");

            if ((uint)tx >= MapConstants.TilesPerSide || (uint)ty >= MapConstants.TilesPerSide)
                throw new ArgumentOutOfRangeException($"Tile coordinates ({tx},{ty}) out of range.");

            int offs = TileBaseOffset(tx, ty) + FlagsByteIndex;
            ushort flags = (ushort)(_data.MapBytes[offs] | (_data.MapBytes[offs + 1] << 8));
            return (PapFlags)flags;
        }

        /// <summary>
        /// Write the flags for a tile (replaces all flags).
        /// </summary>
        public void WriteFlags(int tx, int ty, PapFlags flags)
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                throw new InvalidOperationException("No map loaded.");

            if ((uint)tx >= MapConstants.TilesPerSide || (uint)ty >= MapConstants.TilesPerSide)
                throw new ArgumentOutOfRangeException($"Tile coordinates ({tx},{ty}) out of range.");

            int offs = TileBaseOffset(tx, ty) + FlagsByteIndex;
            ushort value = (ushort)flags;
            _data.MapBytes[offs] = (byte)(value & 0xFF);
            _data.MapBytes[offs + 1] = (byte)((value >> 8) & 0xFF);
            _data.MarkDirty();
        }

        /// <summary>
        /// Set specific flags on a tile (OR operation).
        /// </summary>
        public void SetFlags(int tx, int ty, PapFlags flagsToSet)
        {
            PapFlags current = ReadFlags(tx, ty);
            WriteFlags(tx, ty, current | flagsToSet);
        }

        /// <summary>
        /// Clear specific flags on a tile (AND NOT operation).
        /// </summary>
        public void ClearFlags(int tx, int ty, PapFlags flagsToClear)
        {
            PapFlags current = ReadFlags(tx, ty);
            WriteFlags(tx, ty, current & ~flagsToClear);
        }

        /// <summary>
        /// Check if specific flags are set on a tile.
        /// </summary>
        public bool HasFlags(int tx, int ty, PapFlags flagsToCheck)
        {
            PapFlags current = ReadFlags(tx, ty);
            return (current & flagsToCheck) == flagsToCheck;
        }

        #endregion

        #region Roof Operations (combined altitude + flags)

        /// <summary>
        /// Set a tile as a roof tile with the specified altitude.
        /// Sets the Alt value AND the RoofExists and FlatRoof flags.
        /// </summary>
        public void SetRoofTile(int tx, int ty, int worldAltitude)
        {
            Debug.WriteLine($"[AltitudeAccessor] SetRoofTile({tx}, {ty}, alt={worldAltitude})");

            // Set the altitude
            WriteWorldAltitude(tx, ty, worldAltitude);

            // Set roof flags
            SetFlags(tx, ty, PapFlags.RoofExists | PapFlags.Hidden);

            PapFlags newFlags = ReadFlags(tx, ty);
            Debug.WriteLine($"[AltitudeAccessor] Tile ({tx}, {ty}) flags after: 0x{(ushort)newFlags:X4} ({newFlags})");
        }

        /// <summary>
        /// Clear roof status from a tile (reset altitude to 0 and clear roof flags).
        /// </summary>
        public void ClearRoofTile(int tx, int ty)
        {
            Debug.WriteLine($"[AltitudeAccessor] ClearRoofTile({tx}, {ty})");

            // Reset altitude
            WriteAltRaw(tx, ty, 0);

            // Clear roof flags including Hidden (which enables ledge grabbing)
            ClearFlags(tx, ty, PapFlags.RoofExists | PapFlags.FlatRoof | PapFlags.Hidden);
        }

        /// <summary>
        /// Check if a tile is marked as a roof tile.
        /// </summary>
        public bool IsRoofTile(int tx, int ty)
        {
            return HasFlags(tx, ty, PapFlags.RoofExists);
        }

        #endregion

        #region Region Operations

        /// <summary>
        /// Set roof tiles for a rectangular region.
        /// </summary>
        public void SetRegionAsRoof(int minTx, int minTy, int maxTx, int maxTy, int worldAltitude)
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                throw new InvalidOperationException("No map loaded.");

            // Clamp to valid range
            minTx = Math.Clamp(minTx, 0, MapConstants.TilesPerSide - 1);
            minTy = Math.Clamp(minTy, 0, MapConstants.TilesPerSide - 1);
            maxTx = Math.Clamp(maxTx, 0, MapConstants.TilesPerSide - 1);
            maxTy = Math.Clamp(maxTy, 0, MapConstants.TilesPerSide - 1);

            for (int ty = minTy; ty <= maxTy; ty++)
            {
                for (int tx = minTx; tx <= maxTx; tx++)
                {
                    SetRoofTile(tx, ty, worldAltitude);
                }
            }

            AltitudeChangeBus.Instance.NotifyRegion(minTx, minTy, maxTx, maxTy);
        }

        /// <summary>
        /// Set the altitude for a rectangular region of tiles (without setting roof flags).
        /// </summary>
        public void SetRegionAltitude(int minTx, int minTy, int maxTx, int maxTy, sbyte rawAlt)
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                throw new InvalidOperationException("No map loaded.");

            minTx = Math.Clamp(minTx, 0, MapConstants.TilesPerSide - 1);
            minTy = Math.Clamp(minTy, 0, MapConstants.TilesPerSide - 1);
            maxTx = Math.Clamp(maxTx, 0, MapConstants.TilesPerSide - 1);
            maxTy = Math.Clamp(maxTy, 0, MapConstants.TilesPerSide - 1);

            for (int ty = minTy; ty <= maxTy; ty++)
            {
                for (int tx = minTx; tx <= maxTx; tx++)
                {
                    int offs = TileBaseOffset(tx, ty) + AltByteIndex;
                    _data.MapBytes[offs] = unchecked((byte)rawAlt);
                }
            }

            _data.MarkDirty();
            AltitudeChangeBus.Instance.NotifyRegion(minTx, minTy, maxTx, maxTy);
        }

        /// <summary>
        /// Set the world-space altitude for a rectangular region of tiles.
        /// </summary>
        public void SetRegionWorldAltitude(int minTx, int minTy, int maxTx, int maxTy, int worldAltitude)
        {
            int rawAlt = worldAltitude >> PAP_ALT_SHIFT;
            rawAlt = Math.Clamp(rawAlt, sbyte.MinValue, sbyte.MaxValue);
            SetRegionAltitude(minTx, minTy, maxTx, maxTy, (sbyte)rawAlt);
        }

        #endregion
    }

    /// <summary>
    /// Change notification bus for altitude changes.
    /// </summary>
    public sealed class AltitudeChangeBus
    {
        public static AltitudeChangeBus Instance { get; } = new();
        private AltitudeChangeBus() { }

        public event Action<int, int>? TileChanged;
        public event Action<int, int, int, int>? RegionChanged;
        public event Action? AllChanged;

        public void NotifyTile(int tx, int ty) => TileChanged?.Invoke(tx, ty);
        public void NotifyRegion(int minTx, int minTy, int maxTx, int maxTy) => RegionChanged?.Invoke(minTx, minTy, maxTx, maxTy);
        public void NotifyAll() => AllChanged?.Invoke();
    }
}