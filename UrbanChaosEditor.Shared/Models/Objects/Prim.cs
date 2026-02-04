// /Models/Objects/Prim.cs
namespace UrbanChaosMapEditor.Models
{
    // Layout (little-endian):
    // [0..1] short Y   (height)
    // [2]    byte  X   (0..255) pos inside 4x4 MapWho cell, X grows right
    // [3]    byte  Z   (0..255) pos inside 4x4 MapWho cell, Z grows down
    // [4]    byte  PrimNumber
    // [5]    byte  Yaw (0..255) -> rotation
    // [6]    byte  Flags
    // [7]    byte  InsideIndex
    public sealed class Prim
    {
        public short Y { get; set; }
        public byte X { get; set; }
        public byte Z { get; set; }
        public byte PrimNumber { get; set; }
        public byte Yaw { get; set; }
        public byte Flags { get; set; }
        public byte InsideIndex { get; set; }

        // Derived / runtime-only (for overlay selection etc.)
        public int MapWhoIndex { get; set; }
        public int PixelX { get; set; }
        public int PixelZ { get; set; }
    }

    // 2 bytes per cell:
    // lower 11 bits -> object start index
    // upper 5  bits -> count
    public readonly struct MapWhoEntry
    {
        public readonly int Index;   // 0..2047
        public readonly int Count;   // 0..31
        public MapWhoEntry(int idx, int cnt) { Index = idx; Count = cnt; }

        public static MapWhoEntry FromUShort(ushort v)
            => new((int)(v & 0x7FF), (int)((v >> 11) & 0x1F));
    }
}
