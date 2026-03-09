// /Services/Accessors/ObjectAccessor.cs
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Prims
{
    /// <summary>
    /// Read/write accessor for the IAM "Objects/Prims" section:
    /// [NumObjects:int32][Prims...(NumObjects * 8 bytes)][MapWho: 1024 * 2 bytes]
    /// NumObjects includes a sentinel record at slot 0; real data starts at index 1.
    /// </summary>
    public sealed class PrimsAccessor
    {
        private readonly MapDataService _data;
        public PrimsAccessor(MapDataService data) => _data = data;

        // ---------- Disk models ----------
        /// <summary>Packed 16-bit MapWho entry: [ Num:5 | Index(1-based):11 ]</summary>
        public readonly struct MapWhoEntry
        {
            public readonly ushort Packed;
            public MapWhoEntry(ushort packed) { Packed = packed; }

            /// <summary>1-based index of first prim in this cell (0 means none).</summary>
            public int Index1 => Packed & 0x07FF;
            /// <summary>Number of prims (0..31) in this cell.</summary>
            public int Num => (Packed >> 11) & 0x1F;

            public static MapWhoEntry From(int index1, int num)
            {
                index1 = Math.Clamp(index1, 0, 0x7FF);
                num = Math.Clamp(num, 0, 0x1F);
                return new MapWhoEntry((ushort)((num << 11) | index1));
            }
        }

        /// <summary>Single 8-byte prim record (plus resolved MapWhoIndex).</summary>
        public struct PrimEntry
        {
            public short Y;          // 2 bytes
            public byte X;          // 1 byte (0..255, within 4x4 MapWho cell)
            public byte Z;          // 1 byte
            public byte PrimNumber; // 1 byte
            public byte Yaw;        // 1 byte
            public byte Flags;      // 1 byte
            public byte InsideIndex;// 1 byte

            // Not stored in the record; derived from MapWho when reading; must be supplied when writing.
            public int MapWhoIndex; // 0..1023 (row*32 + col)
        }

        /// <summary>Snapshot of the objects section + offsets for rewriting.</summary>
        public sealed class Snapshot
        {
            public int SaveType { get; init; }
            public int ObjectSectionSize { get; init; } // from header (bytes)
            public int ObjectOffset { get; init; }      // file offset of NumObjects (int32)
            public int NumObjects { get; init; }        // includes sentinel
            public PrimEntry[] Prims { get; init; } = Array.Empty<PrimEntry>();     // excludes sentinel
            public MapWhoEntry[] MapWho { get; init; } = Array.Empty<MapWhoEntry>(); // 1024 entries
            public int MapWhoOffset { get; init; }      // start of 2048 bytes
            public int TailOffsetAfterMapWho { get; init; } // start of file tail after MapWho
        }

        // ---------- Read ----------
        public Snapshot ReadSnapshot()
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                return new Snapshot();

            var bytes = _data.MapBytes;
            int saveType = BitConverter.ToInt32(bytes, 0);
            int objectBytes = BitConverter.ToInt32(bytes, 4);

            // V1-compatible placement: points at NumObjects (int32)
            int sizeAdjustment = saveType >= 25 ? 2000 : 0;
            int objectOffset = bytes.Length - 12 - sizeAdjustment - objectBytes + 8;
            if (objectOffset < 0 || objectOffset + 4 > bytes.Length)
                return new Snapshot();

            int numObjects = BitConverter.ToInt32(bytes, objectOffset);
            if (numObjects < 1) numObjects = 1; // ensure sentinel at least

            int primsOffset = objectOffset + 4;
            int primsBytes = numObjects * 8;
            int mapWhoOffset = primsOffset + primsBytes;
            int afterMapWho = mapWhoOffset + 2048;
            if (afterMapWho > bytes.Length)
                return new Snapshot();

            // Read prims (skip sentinel at index 0)
            var prims = new PrimEntry[Math.Max(0, numObjects - 1)];
            for (int i = 0; i < prims.Length; i++)
            {
                int off = primsOffset + ((i + 1) * 8);
                prims[i] = new PrimEntry
                {
                    Y = BitConverter.ToInt16(bytes, off + 0),
                    X = bytes[off + 2],
                    Z = bytes[off + 3],
                    PrimNumber = bytes[off + 4],
                    Yaw = bytes[off + 5],
                    Flags = bytes[off + 6],
                    InsideIndex = bytes[off + 7],
                    MapWhoIndex = -1 // fill from MapWho
                };
            }

            // Read MapWho safely (no BlockCopy into struct array)
            var mapWho = new MapWhoEntry[1024];
            for (int i = 0; i < 1024; i++)
            {
                ushort packed = BitConverter.ToUInt16(bytes, mapWhoOffset + i * 2);
                mapWho[i] = new MapWhoEntry(packed);
            }

            // Back-reference: assign MapWhoIndex for each prim
            for (int cell = 0; cell < 1024; cell++)
            {
                int idx1 = mapWho[cell].Index1; // 1-based into prims (excluding sentinel)
                int num = mapWho[cell].Num;
                if (idx1 == 0 || num == 0) continue;

                int startZero = idx1 - 1; // -> 0-based into 'prims'
                for (int k = 0; k < num; k++)
                {
                    int pIndex = startZero + k;
                    if ((uint)pIndex < (uint)prims.Length)
                    {
                        var p = prims[pIndex];
                        p.MapWhoIndex = cell;
                        prims[pIndex] = p;
                    }
                }
            }

            return new Snapshot
            {
                SaveType = saveType,
                ObjectSectionSize = objectBytes,
                ObjectOffset = objectOffset,
                NumObjects = numObjects,
                Prims = prims,
                MapWho = mapWho,
                MapWhoOffset = mapWhoOffset,
                TailOffsetAfterMapWho = afterMapWho
            };
        }

        // ---------- Rewrite all (after add/move/delete) ----------
        public void ReplaceAllPrims(IEnumerable<PrimEntry> newPrimsInput)
        {
            if (!_data.IsLoaded || _data.MapBytes is null)
                throw new InvalidOperationException("No map loaded.");

            var snap = ReadSnapshot();
            if (snap.ObjectOffset <= 0)
                throw new InvalidOperationException("Could not resolve object section.");

            // Normalize & sort by MapWho cell (MapWho requires monotonic blocks)
            var list = newPrimsInput
                .Select(p =>
                {
                    var q = p;
                    q.MapWhoIndex = Math.Clamp(q.MapWhoIndex, 0, 1023);
                    q.X = (byte)q.X;
                    q.Z = (byte)q.Z;
                    return q;
                })
                .OrderBy(p => p.MapWhoIndex)
                .ToList();

            // Build MapWho from sorted list (1-based indices; cap to 31 per cell)
            var mapWho = new MapWhoEntry[1024];
            int runningIndex1 = 1; // after sentinel
            for (int i = 0; i < list.Count;)
            {
                int cell = list[i].MapWhoIndex;
                int start = i;
                while (i < list.Count && list[i].MapWhoIndex == cell) i++;
                int count = i - start;

                int numForMapWho = Math.Min(31, count); // 5-bit field
                mapWho[cell] = MapWhoEntry.From(runningIndex1, numForMapWho);

                runningIndex1 += count;
            }

            // Build prim bytes (with sentinel at slot 0)
            int newNumObjects = list.Count + 1;
            int newPrimsBytes = newNumObjects * 8;

            byte[] primsBlock = new byte[newPrimsBytes];
            // sentinel 8 bytes left zeroed

            for (int n = 0; n < list.Count; n++)
            {
                int off = (n + 1) * 8;
                var p = list[n];

                unchecked
                {
                    primsBlock[off + 0] = (byte)(p.Y & 0xFF);
                    primsBlock[off + 1] = (byte)((p.Y >> 8) & 0xFF);
                }
                primsBlock[off + 2] = p.X;
                primsBlock[off + 3] = p.Z;
                primsBlock[off + 4] = p.PrimNumber;
                primsBlock[off + 5] = p.Yaw;
                primsBlock[off + 6] = p.Flags;
                primsBlock[off + 7] = p.InsideIndex;
            }

            // MapWho bytes (pack to ushort)
            byte[] mapWhoBytes = new byte[2048];
            for (int i = 0; i < 1024; i++)
            {
                ushort packed = mapWho[i].Packed;
                mapWhoBytes[i * 2 + 0] = (byte)(packed & 0xFF);
                mapWhoBytes[i * 2 + 1] = (byte)((packed >> 8) & 0xFF);
            }

            // New object section size to write back to header (bytes[4..7])
            int newObjectSectionSize = 4 + newPrimsBytes + 2048;

            // Assemble new file bytes
            var old = _data.MapBytes!;
            int objectOff = snap.ObjectOffset;
            int tailStart = snap.TailOffsetAfterMapWho;
            int tailLen = old.Length - tailStart;

            int newLen = objectOff + 4 + newPrimsBytes + 2048 + tailLen;
            var neo = new byte[newLen];

            // 1) Prefix up to objectOffset
            Buffer.BlockCopy(old, 0, neo, 0, objectOff);

            // 2) NumObjects
            Buffer.BlockCopy(BitConverter.GetBytes(newNumObjects), 0, neo, objectOff, 4);

            // 3) Prims
            Buffer.BlockCopy(primsBlock, 0, neo, objectOff + 4, newPrimsBytes);

            // 4) MapWho
            Buffer.BlockCopy(mapWhoBytes, 0, neo, objectOff + 4 + newPrimsBytes, 2048);

            // 5) Tail (after previous MapWho)
            if (tailLen > 0)
                Buffer.BlockCopy(old, tailStart, neo, objectOff + 4 + newPrimsBytes + 2048, tailLen);

            // 6) Update header field [4..7] with new object section size
            Buffer.BlockCopy(BitConverter.GetBytes(newObjectSectionSize), 0, neo, 4, 4);

            // Swap in memory & notify
            _data.ReplaceBytes(neo);                // marks dirty/loaded
            ObjectsChangeBus.Instance.NotifyChanged();
        }

        // ---------- Convenience mutators ----------
        public void AddPrim(PrimEntry prim)
        {
            var snap = ReadSnapshot();
            var list = snap.Prims.ToList();
            list.Add(prim);
            ReplaceAllPrims(list);
        }

        public void EditPrim(int primIndexZeroBased, Func<PrimEntry, PrimEntry> mutate)
        {
            var snap = ReadSnapshot();
            if ((uint)primIndexZeroBased >= (uint)snap.Prims.Length) return;

            var list = snap.Prims.ToArray();
            list[primIndexZeroBased] = mutate(list[primIndexZeroBased]);
            ReplaceAllPrims(list);
        }

        public void MovePrim(int primIndexZeroBased, int newMapWhoIndex, byte newX, byte newZ)
        {
            var snap = ReadSnapshot();
            if ((uint)primIndexZeroBased >= (uint)snap.Prims.Length) return;

            var list = snap.Prims.ToArray();
            list[primIndexZeroBased].MapWhoIndex = Math.Clamp(newMapWhoIndex, 0, 1023);
            list[primIndexZeroBased].X = newX;
            list[primIndexZeroBased].Z = newZ;

            ReplaceAllPrims(list);
        }

        public void DeletePrim(int primIndexZeroBased)
        {
            var snap = ReadSnapshot();
            if ((uint)primIndexZeroBased >= (uint)snap.Prims.Length) return;

            var list = snap.Prims.Where((_, i) => i != primIndexZeroBased).ToList();
            ReplaceAllPrims(list);
        }
    }

    /// <summary>Lightweight change bus so overlays / tables can refresh.</summary>
    public sealed class ObjectsChangeBus
    {
        private static readonly Lazy<ObjectsChangeBus> _lazy = new(() => new ObjectsChangeBus());
        public static ObjectsChangeBus Instance => _lazy.Value;

        private ObjectsChangeBus() { }

        public event EventHandler? Changed;
        public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
