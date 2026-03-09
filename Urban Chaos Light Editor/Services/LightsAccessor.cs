// /Services/LightsAccessor.cs
// FIXED: Correct layout and night flag interpretation based on C++ source code
using System.Diagnostics;
using UrbanChaosLightEditor.Models;

namespace UrbanChaosLightEditor.Services
{
    /// <summary>
    /// Byte-accurate accessor for .lgt buffer in LightsDataService.
    /// 
    /// CORRECTED Layout (from ed.cpp / ed.h):
    ///   0x0000: Header (12 bytes)
    ///           - sizeof_ed_light (4 bytes): low 16 bits = 20, high 16 bits = version
    ///           - ed_max_lights (4 bytes): 256
    ///           - sizeof_night_colour (4 bytes): 3
    ///   0x000C: ED_Light[0..255] (256 × 20 = 5120 bytes)
    ///           - Index 0 is sentinel (unused)
    ///           - Indices 1-255 are usable lights
    ///   0x140C: Properties (36 bytes)
    ///   0x1430: NIGHT_Colour (3 bytes)
    ///   Total = 5171 bytes
    /// 
    /// IMPORTANT: There is NO reserved padding after the header!
    /// The old code incorrectly assumed 20 bytes of padding.
    /// </summary>
    public sealed class LightsAccessor
    {
        private readonly LightsDataService _svc;

        public LightsAccessor(LightsDataService svc)
        {
            _svc = svc;
            Debug.WriteLine($"[LightsAccessor] Created with service IsLoaded={svc.IsLoaded}");
        }

        // ---- Layout constants (CORRECTED from C++ source) ----
        public const int HeaderSize = 12;
        public const int EntrySize = 20;
        public const int EntryCount = 256;  // ED_MAX_LIGHTS = 256 (index 0 is sentinel)

        // FIXED: Entries start immediately after header - NO padding!
        public const int EntriesOffset = HeaderSize;                                // 0x000C = 12
        public const int PropertiesOffset = EntriesOffset + EntrySize * EntryCount; // 0x140C = 5132
        public const int PropertiesSize = 36;
        public const int NightColourOffset = PropertiesOffset + PropertiesSize;     // 0x1430 = 5168
        public const int NightColourSize = 3;
        public const int TotalSize = NightColourOffset + NightColourSize;           // 5171

        // Night flag bit definitions (from C++ night.h, verified via ed.cpp usage)
        // CRITICAL: Bit 0 is DAYTIME, not "night"! Night = when DAYTIME bit is CLEAR.
        public const uint NIGHT_FLAG_DAYTIME = 0x01;                // Bit 0: If SET = daytime, if CLEAR = night
        public const uint NIGHT_FLAG_LIGHTS_UNDER_LAMPOSTS = 0x02;  // Bit 1: Lamppost lights enabled
        public const uint NIGHT_FLAG_DARKEN_BUILDING_POINTS = 0x04; // Bit 2: Darken wall bottoms

        private byte[] GetBytesArray()
        {
            var bytes = _svc.GetBytesCopy();
            return bytes;
        }

        private byte[] GetBufferForWrite()
        {
            var buf = _svc.GetBytesCopy();
            if (buf.Length < TotalSize)
                throw new InvalidOperationException($".lgt buffer too small ({buf.Length} < {TotalSize}).");
            return buf;
        }

        // ---------- Header ----------
        public LightHeader ReadHeader()
        {
            var bytes = GetBytesArray();
            if (bytes.Length < HeaderSize)
                throw new InvalidOperationException(".lgt: missing header.");

            int sizeField = BitConverter.ToInt32(bytes, 0);

            return new LightHeader
            {
                SizeOfEdLight = sizeField,  // Lower 16 bits = size (20), upper 16 bits = version
                EdMaxLights = BitConverter.ToInt32(bytes, 4),
                SizeOfNightColour = BitConverter.ToInt32(bytes, 8)
            };
        }

        // ---------- Entries ----------
        /// <summary>
        /// Read a light entry by index (0-255).
        /// Note: Index 0 is the sentinel and should not be used for real lights.
        /// Real lights use indices 1-255.
        /// </summary>
        public LightEntry ReadEntry(int index)
        {
            if ((uint)index >= EntryCount)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be 0-{EntryCount - 1}");

            var bytes = GetBytesArray();
            int off = EntriesOffset + index * EntrySize;

            if (off + EntrySize > bytes.Length)
                throw new InvalidOperationException($"Entry {index} at offset {off} exceeds buffer length {bytes.Length}");

            return new LightEntry
            {
                Range = bytes[off + 0],
                Red = unchecked((sbyte)bytes[off + 1]),
                Green = unchecked((sbyte)bytes[off + 2]),
                Blue = unchecked((sbyte)bytes[off + 3]),
                Next = bytes[off + 4],
                Used = bytes[off + 5],
                Flags = bytes[off + 6],
                Padding = bytes[off + 7],
                X = BitConverter.ToInt32(bytes, off + 8),
                Y = BitConverter.ToInt32(bytes, off + 12),
                Z = BitConverter.ToInt32(bytes, off + 16),
            };
        }

        /// <summary>
        /// Read all 256 entries (including sentinel at index 0).
        /// </summary>
        public List<LightEntry> ReadAllEntries()
        {
            var bytes = GetBytesArray();
            var list = new List<LightEntry>(EntryCount);
            int usedCount = 0;

            for (int i = 0; i < EntryCount; i++)
            {
                int off = EntriesOffset + i * EntrySize;

                if (off + EntrySize > bytes.Length)
                {
                    Debug.WriteLine($"[LightsAccessor] WARNING: Entry {i} offset {off} exceeds buffer length {bytes.Length}");
                    break;
                }

                var e = new LightEntry
                {
                    Range = bytes[off + 0],
                    Red = unchecked((sbyte)bytes[off + 1]),
                    Green = unchecked((sbyte)bytes[off + 2]),
                    Blue = unchecked((sbyte)bytes[off + 3]),
                    Next = bytes[off + 4],
                    Used = bytes[off + 5],
                    Flags = bytes[off + 6],
                    Padding = bytes[off + 7],
                    X = BitConverter.ToInt32(bytes, off + 8),
                    Y = BitConverter.ToInt32(bytes, off + 12),
                    Z = BitConverter.ToInt32(bytes, off + 16),
                };
                list.Add(e);

                if (e.Used == 1) usedCount++;
            }

            Debug.WriteLine($"[LightsAccessor] Read {list.Count} entries, {usedCount} used (excluding sentinel)");
            return list;
        }

        public void WriteEntry(int index, LightEntry e)
        {
            if ((uint)index >= EntryCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            var buf = GetBufferForWrite();
            int off = EntriesOffset + index * EntrySize;

            buf[off + 0] = e.Range;
            buf[off + 1] = unchecked((byte)e.Red);
            buf[off + 2] = unchecked((byte)e.Green);
            buf[off + 3] = unchecked((byte)e.Blue);
            buf[off + 4] = e.Next;
            buf[off + 5] = e.Used;
            buf[off + 6] = e.Flags;
            buf[off + 7] = e.Padding;

            WriteInt32LE(buf, off + 8, e.X);
            WriteInt32LE(buf, off + 12, e.Y);
            WriteInt32LE(buf, off + 16, e.Z);

            _svc.ReplaceAllBytes(buf);
        }

        // ---------- Properties ----------
        public LightProperties ReadProperties()
        {
            var bytes = GetBytesArray();
            if (bytes.Length < PropertiesOffset + PropertiesSize)
                throw new InvalidOperationException(".lgt: missing properties block.");

            int off = PropertiesOffset;

            var props = new LightProperties
            {
                EdLightFree = BitConverter.ToInt32(bytes, off + 0),
                NightFlag = BitConverter.ToUInt32(bytes, off + 4),
                NightAmbD3DColour = BitConverter.ToUInt32(bytes, off + 8),
                NightAmbD3DSpecular = BitConverter.ToUInt32(bytes, off + 12),
                NightAmbRed = BitConverter.ToInt32(bytes, off + 16),
                NightAmbGreen = BitConverter.ToInt32(bytes, off + 20),
                NightAmbBlue = BitConverter.ToInt32(bytes, off + 24),
                NightLampostRed = unchecked((sbyte)bytes[off + 28]),
                NightLampostGreen = unchecked((sbyte)bytes[off + 29]),
                NightLampostBlue = unchecked((sbyte)bytes[off + 30]),
                Padding = bytes[off + 31],
                NightLampostRadius = BitConverter.ToInt32(bytes, off + 32),
            };

            // Debug: Show correct interpretation
            bool isNight = (props.NightFlag & NIGHT_FLAG_DAYTIME) == 0;
            bool lampsOn = (props.NightFlag & NIGHT_FLAG_LIGHTS_UNDER_LAMPOSTS) != 0;
            bool darkenWalls = (props.NightFlag & NIGHT_FLAG_DARKEN_BUILDING_POINTS) != 0;

            Debug.WriteLine($"[LightsAccessor.ReadProperties] NightFlag=0x{props.NightFlag:X8}, " +
                $"IsNight={isNight}, LampsOn={lampsOn}, DarkenWalls={darkenWalls}");

            return props;
        }

        public void WriteProperties(LightProperties p)
        {
            var buf = GetBufferForWrite();
            int off = PropertiesOffset;

            Debug.WriteLine($"[LightsAccessor.WriteProperties] Writing NightFlag=0x{p.NightFlag:X8} at offset {off}");

            WriteInt32LE(buf, off + 0, p.EdLightFree);
            WriteUInt32LE(buf, off + 4, p.NightFlag);
            WriteUInt32LE(buf, off + 8, p.NightAmbD3DColour);
            WriteUInt32LE(buf, off + 12, p.NightAmbD3DSpecular);
            WriteInt32LE(buf, off + 16, p.NightAmbRed);
            WriteInt32LE(buf, off + 20, p.NightAmbGreen);
            WriteInt32LE(buf, off + 24, p.NightAmbBlue);
            buf[off + 28] = unchecked((byte)p.NightLampostRed);
            buf[off + 29] = unchecked((byte)p.NightLampostGreen);
            buf[off + 30] = unchecked((byte)p.NightLampostBlue);
            buf[off + 31] = p.Padding;
            WriteInt32LE(buf, off + 32, p.NightLampostRadius);

            _svc.ReplaceAllBytes(buf);
        }

        // ---------- Night Colour ----------
        public LightNightColour ReadNightColour()
        {
            var bytes = GetBytesArray();
            if (bytes.Length < NightColourOffset + NightColourSize)
                throw new InvalidOperationException(".lgt: missing night colour block.");

            int off = NightColourOffset;
            return new LightNightColour
            {
                Red = bytes[off + 0],
                Green = bytes[off + 1],
                Blue = bytes[off + 2]
            };
        }

        public void WriteNightColour(LightNightColour c)
        {
            var buf = GetBufferForWrite();
            int off = NightColourOffset;
            buf[off + 0] = c.Red;
            buf[off + 1] = c.Green;
            buf[off + 2] = c.Blue;
            _svc.ReplaceAllBytes(buf);
        }

        // ---------- Night Flag Helpers ----------

        /// <summary>
        /// Check if it's night time.
        /// Night = when DAYTIME flag is NOT set (bit 0 clear).
        /// </summary>
        public bool IsNightTime()
        {
            var props = ReadProperties();
            return (props.NightFlag & NIGHT_FLAG_DAYTIME) == 0;
        }

        /// <summary>
        /// Set night/day mode.
        /// Night = clear DAYTIME bit, Day = set DAYTIME bit.
        /// </summary>
        public void SetNightTime(bool isNight)
        {
            var props = ReadProperties();
            if (isNight)
                props.NightFlag &= ~NIGHT_FLAG_DAYTIME;  // Clear daytime bit = night
            else
                props.NightFlag |= NIGHT_FLAG_DAYTIME;   // Set daytime bit = day
            WriteProperties(props);
        }

        /// <summary>
        /// Check if lamppost lights are enabled.
        /// </summary>
        public bool AreLampsOn()
        {
            var props = ReadProperties();
            return (props.NightFlag & NIGHT_FLAG_LIGHTS_UNDER_LAMPOSTS) != 0;
        }

        /// <summary>
        /// Enable/disable lamppost lights.
        /// </summary>
        public void SetLampsOn(bool on)
        {
            var props = ReadProperties();
            if (on)
                props.NightFlag |= NIGHT_FLAG_LIGHTS_UNDER_LAMPOSTS;
            else
                props.NightFlag &= ~NIGHT_FLAG_LIGHTS_UNDER_LAMPOSTS;
            WriteProperties(props);
        }

        /// <summary>
        /// Check if wall darkening is enabled.
        /// </summary>
        public bool IsDarkenWallsOn()
        {
            var props = ReadProperties();
            return (props.NightFlag & NIGHT_FLAG_DARKEN_BUILDING_POINTS) != 0;
        }

        /// <summary>
        /// Enable/disable wall darkening.
        /// </summary>
        public void SetDarkenWallsOn(bool on)
        {
            var props = ReadProperties();
            if (on)
                props.NightFlag |= NIGHT_FLAG_DARKEN_BUILDING_POINTS;
            else
                props.NightFlag &= ~NIGHT_FLAG_DARKEN_BUILDING_POINTS;
            WriteProperties(props);
        }

        // ---------- Light Management ----------

        /// <summary>
        /// Find first free light index.
        /// Note: Real lights use indices 1-255. Index 0 is sentinel.
        /// </summary>
        public int FindFirstFreeIndex()
        {
            var props = ReadProperties();

            // EdLightFree is the index into the array (1-255 valid for real lights)
            if (props.EdLightFree >= 1 && props.EdLightFree < EntryCount)
            {
                int i = props.EdLightFree;
                if (ReadEntry(i).Used == 0)
                    return i;
            }

            // Search for any free slot (skip index 0 - it's sentinel)
            for (int i = 1; i < EntryCount; i++)
            {
                if (ReadEntry(i).Used == 0)
                    return i;
            }

            return -1;  // No free slots
        }

        /// <summary>
        /// Add a new light. Returns the index (1-255) or -1 if full.
        /// </summary>
        public int AddLight(LightEntry e)
        {
            int idx = FindFirstFreeIndex();
            if (idx < 0)
                return -1;

            e.Used = 1;
            e.Next = 0;
            WriteEntry(idx, e);

            // Update free list to point to next available slot
            var props = ReadProperties();
            int nextFree = 0;

            // Search for next free slot after this one
            for (int i = idx + 1; i < EntryCount; i++)
            {
                if (ReadEntry(i).Used == 0)
                {
                    nextFree = i;
                    break;
                }
            }

            // If not found, search from beginning (but not index 0)
            if (nextFree == 0)
            {
                for (int i = 1; i < idx; i++)
                {
                    if (ReadEntry(i).Used == 0)
                    {
                        nextFree = i;
                        break;
                    }
                }
            }

            props.EdLightFree = nextFree;
            WriteProperties(props);

            Debug.WriteLine($"[LightsAccessor.AddLight] Added light at index {idx}, next free = {props.EdLightFree}");
            return idx;
        }

        /// <summary>
        /// Delete a light by index (marks as unused and updates free list).
        /// </summary>
        public void DeleteLight(int index)
        {
            if (index < 1 || index >= EntryCount)
                throw new ArgumentOutOfRangeException(nameof(index), "Light index must be 1-255");

            var e = ReadEntry(index);
            e.Used = 0;
            WriteEntry(index, e);

            // Update free list if this slot is earlier than current free
            var props = ReadProperties();
            if (props.EdLightFree == 0 || props.EdLightFree > index)
            {
                props.EdLightFree = index;
                WriteProperties(props);
            }

            Debug.WriteLine($"[LightsAccessor.DeleteLight] Deleted light at index {index}");
        }

        // ---------- Helpers ----------
        private static void WriteInt32LE(byte[] b, int off, int v)
        {
            b[off + 0] = (byte)(v);
            b[off + 1] = (byte)(v >> 8);
            b[off + 2] = (byte)(v >> 16);
            b[off + 3] = (byte)(v >> 24);
        }

        private static void WriteUInt32LE(byte[] b, int off, uint v)
        {
            b[off + 0] = (byte)(v);
            b[off + 1] = (byte)(v >> 8);
            b[off + 2] = (byte)(v >> 16);
            b[off + 3] = (byte)(v >> 24);
        }

        // Mapping helpers (8192 UI px ↔ 32768 world units)
        public static int UiXToWorldX(int uiX) => 32768 - uiX * 4;
        public static int UiZToWorldZ(int uiZ) => 32768 - uiZ * 4;
        public static int WorldXToUiX(int worldX) => (32768 - worldX) / 4;
        public static int WorldZToUiZ(int worldZ) => (32768 - worldZ) / 4;
    }
}