using System;
using System.Collections.Generic;
using System.Diagnostics;
using UrbanChaosLightEditor.Models;

namespace UrbanChaosLightEditor.Services
{
    /// <summary>
    /// Info about a prim for display purposes.
    /// </summary>
    public struct PrimDisplayInfo
    {
        public int Index;
        public int PixelX;
        public int PixelZ;
        public int Y;
        public byte PrimNumber;
        public byte Yaw;
    }

    /// <summary>
    /// Read-only accessor for prim/object data in the .iam file.
    /// Matches the Map Editor's ObjectsAccessor format.
    /// </summary>
    public sealed class ReadOnlyObjectsAccessor
    {
        private readonly ReadOnlyMapDataService _svc;

        private const int CellsPerSide = 32;
        private const int PixelsPerCell = 256;

        public ReadOnlyObjectsAccessor(ReadOnlyMapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        /// <summary>
        /// Reads all prims from the objects section of the .iam file.
        /// </summary>
        public List<PrimDisplayInfo> ReadAllPrims()
        {
            var list = new List<PrimDisplayInfo>();

            if (!_svc.IsLoaded)
            {
                Debug.WriteLine("[ReadOnlyObjectsAccessor] Map not loaded.");
                return list;
            }

            var bytes = _svc.GetBytesCopy();

            // Read header
            if (bytes.Length < 8)
            {
                Debug.WriteLine("[ReadOnlyObjectsAccessor] File too small for header.");
                return list;
            }

            int saveType = BitConverter.ToInt32(bytes, 0);
            int objectBytes = BitConverter.ToInt32(bytes, 4);

            // Calculate object section offset (same logic as Map Editor)
            int sizeAdjustment = saveType >= 25 ? 2000 : 0;
            int objectOffset = bytes.Length - 12 - sizeAdjustment - objectBytes + 8;

            if (objectOffset < 0 || objectOffset + 4 > bytes.Length)
            {
                Debug.WriteLine($"[ReadOnlyObjectsAccessor] Invalid objectOffset: {objectOffset}");
                return list;
            }

            int numObjects = BitConverter.ToInt32(bytes, objectOffset);
            Debug.WriteLine($"[ReadOnlyObjectsAccessor] NumObjects={numObjects} at offset 0x{objectOffset:X}");

            if (numObjects < 1 || numObjects > 10000)
            {
                Debug.WriteLine($"[ReadOnlyObjectsAccessor] Invalid numObjects: {numObjects}");
                return list;
            }

            int primsOffset = objectOffset + 4;
            int primsBytes = numObjects * 8;
            int mapWhoOffset = primsOffset + primsBytes;

            if (mapWhoOffset + 2048 > bytes.Length)
            {
                Debug.WriteLine("[ReadOnlyObjectsAccessor] Not enough bytes for MapWho.");
                return list;
            }

            // Read MapWho (1024 entries, 2 bytes each)
            var mapWho = new ushort[1024];
            for (int i = 0; i < 1024; i++)
            {
                mapWho[i] = BitConverter.ToUInt16(bytes, mapWhoOffset + i * 2);
            }

            // Read prims (skip sentinel at index 0)
            var prims = new PrimData[numObjects - 1];
            for (int i = 0; i < prims.Length; i++)
            {
                int off = primsOffset + ((i + 1) * 8);
                prims[i] = new PrimData
                {
                    Y = BitConverter.ToInt16(bytes, off + 0),
                    X = bytes[off + 2],
                    Z = bytes[off + 3],
                    PrimNumber = bytes[off + 4],
                    Yaw = bytes[off + 5],
                    Flags = bytes[off + 6],
                    InsideIndex = bytes[off + 7],
                    MapWhoIndex = -1
                };
            }

            // Back-reference: assign MapWhoIndex from MapWho grid
            for (int cell = 0; cell < 1024; cell++)
            {
                ushort packed = mapWho[cell];
                int index1 = packed & 0x07FF;
                int num = (packed >> 11) & 0x1F;

                if (index1 == 0 || num == 0) continue;

                int startZero = index1 - 1;
                for (int k = 0; k < num; k++)
                {
                    int pIndex = startZero + k;
                    if ((uint)pIndex < (uint)prims.Length)
                    {
                        prims[pIndex].MapWhoIndex = cell;
                    }
                }
            }

            // Convert to display info with pixel coordinates
            for (int i = 0; i < prims.Length; i++)
            {
                var p = prims[i];
                if (p.PrimNumber == 0) continue;
                if (p.MapWhoIndex < 0) continue;

                GamePrimToUiPixels(p.MapWhoIndex, p.X, p.Z, out int pixelX, out int pixelZ);

                if (list.Count < 5)
                {
                    Debug.WriteLine($"[ReadOnlyObjectsAccessor] Prim[{i}]: num={p.PrimNumber}, mapWho={p.MapWhoIndex}, game=({p.X},{p.Z}) -> pixel=({pixelX},{pixelZ})");
                }

                list.Add(new PrimDisplayInfo
                {
                    Index = i,
                    PixelX = pixelX,
                    PixelZ = pixelZ,
                    Y = p.Y,
                    PrimNumber = p.PrimNumber,
                    Yaw = p.Yaw
                });
            }

            Debug.WriteLine($"[ReadOnlyObjectsAccessor] Read {list.Count} prims with valid positions");
            return list;
        }

        /// <summary>
        /// Converts game coordinates to UI pixel coordinates.
        /// Game grid has origin at bottom-right; UI has origin at top-left.
        /// </summary>
        private static void GamePrimToUiPixels(int mapWhoIndex, byte gameX, byte gameZ, out int uiPixelX, out int uiPixelZ)
        {
            // MapWho index is column-major: col = index / 32, row = index % 32
            int gameCol = mapWhoIndex / CellsPerSide;
            int gameRow = mapWhoIndex % CellsPerSide;

            // Invert cell position (game bottom-right -> UI top-left)
            int uiCol = (CellsPerSide - 1) - gameCol;
            int uiRow = (CellsPerSide - 1) - gameRow;

            // Invert local position within cell AND add to cell offset
            uiPixelX = uiCol * PixelsPerCell + (255 - gameX);
            uiPixelZ = uiRow * PixelsPerCell + (255 - gameZ);
        }

        private struct PrimData
        {
            public short Y;
            public byte X;
            public byte Z;
            public byte PrimNumber;
            public byte Yaw;
            public byte Flags;
            public byte InsideIndex;
            public int MapWhoIndex;
        }
    }
}