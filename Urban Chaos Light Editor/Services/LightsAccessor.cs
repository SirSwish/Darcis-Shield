using System;
using System.Collections.Generic;
using System.Diagnostics;
using UrbanChaosLightEditor.Models;

namespace UrbanChaosLightEditor.Services
{
    /// <summary>
    /// Byte-accurate accessor for .lgt buffer in LightsDataService.
    /// 
    /// Layout (V1):
    ///   0x0000: LightHeader                 (12 bytes)
    ///   0x000C: Reserved padding            (20 bytes)
    ///   0x0020: 255 * LightEntry (20 bytes) (5100 bytes)
    ///   0x140C: LightProperties             (36 bytes)
    ///   0x1430: LightNightColour            ( 3 bytes)
    ///   Total = 5171 bytes
    /// </summary>
    public sealed class LightsAccessor
    {
        private readonly LightsDataService _svc;
        public LightsAccessor(LightsDataService svc)
        {
            _svc = svc;
            Debug.WriteLine($"[LightsAccessor] Created with service IsLoaded={svc.IsLoaded}");
        }

        // ---- Layout constants ----
        public const int HeaderSize = 12;
        public const int ReservedPad = 20;
        public const int EntrySize = 20;
        public const int EntryCount = 255;

        public const int EntriesOffset = HeaderSize + ReservedPad;                 // 0x0020 = 32
        public const int PropertiesOffset = EntriesOffset + EntrySize * EntryCount;// 0x140C = 5132
        public const int PropertiesSize = 36;
        public const int NightColourOffset = PropertiesOffset + PropertiesSize;    // 0x1430 = 5168
        public const int NightColourSize = 3;
        public const int TotalSize = NightColourOffset + NightColourSize;          // 5171

        private byte[] GetBytesArray()
        {
            var bytes = _svc.GetBytesCopy();
            Debug.WriteLine($"[LightsAccessor.GetBytesArray] Got {bytes.Length} bytes from service");
            return bytes;
        }

        private byte[] GetBufferForWrite()
        {
            var buf = _svc.GetBytesCopy();
            Debug.WriteLine($"[LightsAccessor.GetBufferForWrite] Got {buf.Length} bytes (need {TotalSize})");
            if (buf.Length < TotalSize)
                throw new InvalidOperationException($".lgt buffer too small ({buf.Length} < {TotalSize}).");
            return buf;
        }

        // ---------- Header ----------
        public LightHeader ReadHeader()
        {
            Debug.WriteLine("[LightsAccessor.ReadHeader] Reading header...");
            var bytes = GetBytesArray();
            if (bytes.Length < HeaderSize) throw new InvalidOperationException(".lgt: missing header.");
            return new LightHeader
            {
                SizeOfEdLight = BitConverter.ToInt32(bytes, 0),
                EdMaxLights = BitConverter.ToInt32(bytes, 4),
                SizeOfNightColour = BitConverter.ToInt32(bytes, 8)
            };
        }

        // ---------- Entries ----------
        public LightEntry ReadEntry(int index)
        {
            if ((uint)index >= EntryCount) throw new ArgumentOutOfRangeException(nameof(index));
            var bytes = GetBytesArray();
            int off = EntriesOffset + index * EntrySize;

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

        public List<LightEntry> ReadAllEntries()
        {
            Debug.WriteLine("[LightsAccessor.ReadAllEntries] Starting...");

            var bytes = GetBytesArray();
            Debug.WriteLine($"[LightsAccessor.ReadAllEntries] Buffer size: {bytes.Length}");

            var list = new List<LightEntry>(EntryCount);
            int usedCount = 0;

            for (int i = 0; i < EntryCount; i++)
            {
                int off = EntriesOffset + i * EntrySize;

                if (off + EntrySize > bytes.Length)
                {
                    Debug.WriteLine($"[LightsAccessor.ReadAllEntries] WARNING: Entry {i} offset {off} exceeds buffer length {bytes.Length}");
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

            Debug.WriteLine($"[LightsAccessor.ReadAllEntries] Read {list.Count} entries, {usedCount} used");
            return list;
        }

        public void WriteEntry(int index, LightEntry e)
        {
            if ((uint)index >= EntryCount) throw new ArgumentOutOfRangeException(nameof(index));
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

            WriteIntLE(buf, off + 8, e.X);
            WriteIntLE(buf, off + 12, e.Y);
            WriteIntLE(buf, off + 16, e.Z);

            _svc.ReplaceAllBytes(buf); // fires LightsBytesReset + Dirty
        }

        // ---------- Properties ----------
        public LightProperties ReadProperties()
        {
            var bytes = GetBytesArray();
            if (bytes.Length < PropertiesOffset + PropertiesSize)
                throw new InvalidOperationException(".lgt: missing properties block.");

            int off = PropertiesOffset;

            return new LightProperties
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
        }

        public void WriteProperties(LightProperties p)
        {
            var buf = GetBufferForWrite();
            int off = PropertiesOffset;

            WriteIntLE(buf, off + 0, p.EdLightFree);
            WriteUIntLE(buf, off + 4, p.NightFlag);
            WriteUIntLE(buf, off + 8, p.NightAmbD3DColour);
            WriteUIntLE(buf, off + 12, p.NightAmbD3DSpecular);
            WriteIntLE(buf, off + 16, p.NightAmbRed);
            WriteIntLE(buf, off + 20, p.NightAmbGreen);
            WriteIntLE(buf, off + 24, p.NightAmbBlue);
            buf[off + 28] = unchecked((byte)p.NightLampostRed);
            buf[off + 29] = unchecked((byte)p.NightLampostGreen);
            buf[off + 30] = unchecked((byte)p.NightLampostBlue);
            buf[off + 31] = p.Padding;
            WriteIntLE(buf, off + 32, p.NightLampostRadius);

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

        // ---------- Convenience ----------
        public int FindFirstFreeIndex()
        {
            var props = ReadProperties();
            if (props.EdLightFree > 0 && props.EdLightFree <= EntryCount)
            {
                int i = props.EdLightFree - 1; // 1-based in file
                if (ReadEntry(i).Used == 0) return i;
            }
            for (int i = 0; i < EntryCount; i++)
                if (ReadEntry(i).Used == 0) return i;
            return -1;
        }

        public int AddLight(LightEntry e)
        {
            int idx = FindFirstFreeIndex();
            if (idx < 0) return -1;

            e.Used = 1;
            e.Next = 0;
            WriteEntry(idx, e);

            var props = ReadProperties();
            int next = -1;
            for (int i = idx + 1; i < EntryCount; i++) if (ReadEntry(i).Used == 0) { next = i; break; }
            if (next < 0) for (int i = 0; i < idx; i++) if (ReadEntry(i).Used == 0) { next = i; break; }
            props.EdLightFree = next >= 0 ? next + 1 : 0;
            WriteProperties(props);

            return idx;
        }

        public void DeleteLight(int index)
        {
            var e = ReadEntry(index);
            e.Used = 0;
            WriteEntry(index, e);

            var props = ReadProperties();
            if (props.EdLightFree == 0 || props.EdLightFree - 1 > index)
            {
                props.EdLightFree = index + 1;
                WriteProperties(props);
            }
        }

        // ---------- Helpers ----------
        private static void WriteIntLE(byte[] b, int off, int v)
        {
            b[off + 0] = (byte)(v);
            b[off + 1] = (byte)(v >> 8);
            b[off + 2] = (byte)(v >> 16);
            b[off + 3] = (byte)(v >> 24);
        }

        private static void WriteUIntLE(byte[] b, int off, uint v)
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