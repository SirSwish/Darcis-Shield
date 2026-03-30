// UrbanChaosMapEditor/Services/BuildingsAccessor.cs
using System.Diagnostics;
using System.IO;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Buildings
{
    /// <summary>
    /// Primary accessor for the building (�super map�) block.
    /// Produces a BuildingArrays snapshot (DBuildings, DFacets, dstyles, paint_mem, dstoreys, cables).
    /// </summary>
    public sealed class BuildingsAccessor
    {
        private readonly MapDataService _svc;

        // Fixed V1 layout we�re targeting (same as renderer):
        private const int HeaderSize = 48;
        private const int DBuildingSize = 24;
        private const int AfterBuildingsPad = 14;
        private const int InsideStoreySize = 22;
        private const int StaircaseSize = 10;
        private const int DWalkableSize = 22;
        private const int RoofFace4Size = 10;

        public const int DFacetSize = 26; // expose for callers
        private const int DStoreyRecSize = 6;  // U16 Style; U16 PaintIndex; SBYTE Count; UBYTE pad

        public BuildingsAccessor(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));

            // Forward map buffer lifecycle events so overlays/VMs can react using this accessor.
            _svc.MapLoaded += (_, __) => BuildingsBytesReset?.Invoke(this, EventArgs.Empty);
            _svc.MapCleared += (_, __) => BuildingsBytesReset?.Invoke(this, EventArgs.Empty);
            _svc.MapBytesReset += (_, __) => BuildingsBytesReset?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Raised whenever the underlying building bytes may have changed.</summary>
        public event EventHandler? BuildingsBytesReset;

        public static int ReadS32(byte[] b, int off)
=> b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24);

        /// <summary>
        /// Returns a full, immutable snapshot of the building block:
        /// DBuildings, DFacets, dstyles[], paint_mem[], dstoreys[] and header counters.
        /// Also extracts all Cable facets into BuildingArrays.Cables.
        /// If no map is loaded or bounds are invalid, returns an empty snapshot.
        /// </summary>
        public BuildingArrays ReadSnapshot()
        {
            var empty = new BuildingArrays
            {
                StartOffset = -1,
                Length = 0,
                FacetsStart = -1,
                Buildings = Array.Empty<DBuildingRec>(),
                Facets = Array.Empty<DFacetRec>(),
                Styles = Array.Empty<short>(),
                PaintMem = Array.Empty<byte>(),
                Storeys = Array.Empty<BuildingArrays.DStoreyRec>(),
                Cables = Array.Empty<CableFacet>(),
                NextDBuilding = 0,
                NextDFacet = 0,
                NextDStyle = 0,
                NextPaintMem = 0,
                NextDStorey = 0,
                SaveType = 0,

                TailOffset = -1,
                TailBytes = Array.Empty<byte>(),

                InsideStoreys = Array.Empty<BuildingArrays.InsideStoreyRec>(),
                InsideStairs = Array.Empty<BuildingArrays.StaircaseRec>(),
                WalkablesOffset = -1,
            };

            if (!_svc.IsLoaded) return empty;

            _svc.ComputeAndCacheBuildingRegion();
            if (!_svc.TryGetBuildingRegion(out int start, out int len)) return empty;

            var bytes = _svc.GetBytesCopy();
            if (start < 0 || len <= HeaderSize || start + len > bytes.Length) return empty;

            int saveType = BitConverter.ToInt32(bytes, 0);

            // Header counters
            ushort nextDBuilding = ReadU16(bytes, start + 2);
            ushort nextDFacet = ReadU16(bytes, start + 4);
            ushort nextDStyle = ReadU16(bytes, start + 6);
            ushort nextPaintMem = (saveType >= 17) ? ReadU16(bytes, start + 8) : (ushort)0;
            ushort nextDStorey = (saveType >= 17) ? ReadU16(bytes, start + 10) : (ushort)0;

            Debug.WriteLine($"[BuildingsAccessor] saveType={saveType}  nextDBuilding={nextDBuilding} nextDFacet={nextDFacet} nextDStyle={nextDStyle} nextPaintMem={nextPaintMem} nextDStorey={nextDStorey}");
            Debug.WriteLine($"[BuildingsAccessor] region start=0x{start:X} len=0x{len:X}");

            int totalBuildings = Math.Max(0, nextDBuilding - 1);
            int totalFacets = Math.Max(0, nextDFacet - 1);

            int buildingsOff = start + HeaderSize;
            int facetsOff = buildingsOff + totalBuildings * DBuildingSize + AfterBuildingsPad;

            long cursor = facetsOff;
            long blockEnd = (long)start + len;

            long facetsEnd = cursor + (long)totalFacets * DFacetSize;
            if (facetsOff < start || facetsEnd > blockEnd)
            {
                Debug.WriteLine($"[BuildingsAccessor] Bad facet bounds: start=0x{start:X} len={len} facetsOff=0x{facetsOff:X} facetsEnd=0x{facetsEnd:X} blockEnd=0x{blockEnd:X}");
                return empty;
            }

            // ---- DBuildings ----
            var buildings = new DBuildingRec[totalBuildings];
            for (int i = 0; i < totalBuildings; i++)
            {
                int off = buildingsOff + i * DBuildingSize;
                Debug.WriteLine($"[BuildingsAccessor] DBuilding#{i + 1} raw @ 0x{off:X}: {DumpHex(bytes, off, DBuildingSize)}");
                ushort startFacet = ReadU16(bytes, off + 0);
                ushort endFacet = ReadU16(bytes, off + 2);
                ushort walkable = ReadU16(bytes, off + 4);
                byte counter0 = bytes[off + 6];
                byte counter1 = bytes[off + 7];
                int worldX = BitConverter.ToInt32(bytes, off + 8);
                byte type = bytes[off + 11];
                int worldY = 0;  // not yet identified in on-disk layout
                int worldZ = BitConverter.ToInt32(bytes, off + 12);
                byte ware = bytes[off + 22];
                buildings[i] = new DBuildingRec(
                    worldX, worldY, worldZ,
                    startFacet, endFacet,
                    walkable,
                    counter0, counter1,
                    ware, type);
            }

            // ---- DFacets ----
            var facets = new DFacetRec[totalFacets];
            for (int i = 0; i < totalFacets; i++)
            {
                int off = facetsOff + i * DFacetSize;

                var type = (FacetType)bytes[off + 0];
                byte h = bytes[off + 1];

                byte x0 = bytes[off + 2];
                byte x1 = bytes[off + 3];
                short y0 = BitConverter.ToInt16(bytes, off + 4);
                short y1 = BitConverter.ToInt16(bytes, off + 6);
                byte z0 = bytes[off + 8];
                byte z1 = bytes[off + 9];

                var flags = (FacetFlags)ReadU16(bytes, off + 10);

                ushort sty = ReadU16(bytes, off + 12);
                ushort bld = ReadU16(bytes, off + 14);

                ushort st = ReadU16(bytes, off + 16);

                byte fh = bytes[off + 18];
                byte blockH = bytes[off + 19];
                byte open = bytes[off + 20];
                byte dfcache = bytes[off + 21];
                byte shake = bytes[off + 22];
                byte cutHole = bytes[off + 23];
                byte counter0 = bytes[off + 24];
                byte counter1 = bytes[off + 25];

                facets[i] = new DFacetRec(
                    type, x0, z0, x1, z1,
                    h, fh, sty, bld, st, flags,
                    y0, y1, blockH, open, dfcache, shake, cutHole, counter0, counter1
                );
            }
            cursor = facetsEnd;

            if (totalFacets > 0)
            {
                var f0 = facets[0];
                Debug.WriteLine($"[BuildingsAccessor] first facet: id=1 type={f0.Type} bld={f0.Building} st={f0.Storey} styIdx={f0.StyleIndex} xy=({f0.X0},{f0.Z0})->({f0.X1},{f0.Z1})");
            }
            else
            {
                Debug.WriteLine("[BuildingsAccessor] no facets parsed.");
            }

            // ---- Cables ----
            var cablesList = new List<CableFacet>();
            for (int i = 0; i < facets.Length; i++)
            {
                var f = facets[i];
                if (!f.IsCable) continue;

                int facetId1 = i + 1;

                int wx1 = f.X0 * 256;
                int wy1 = f.Y0;
                int wz1 = f.Z0 * 256;

                int wx2 = f.X1 * 256;
                int wy2 = f.Y1;
                int wz2 = f.Z1 * 256;

                int buildingIndex = FindBuildingIndexForFacet(buildings, facetId1);

                cablesList.Add(new CableFacet
                {
                    FacetIndex = facetId1,
                    WorldX1 = wx1,
                    WorldY1 = wy1,
                    WorldZ1 = wz1,
                    WorldX2 = wx2,
                    WorldY2 = wy2,
                    WorldZ2 = wz2,
                    SegmentCount = f.CableSegments,
                    SagBase = (short)f.FHeight,
                    SagAngleDelta1 = f.CableStep1Signed,
                    SagAngleDelta2 = f.CableStep2Signed,
                    BuildingIndex = buildingIndex,
                    RawFacet = f
                });
            }

            var cables = cablesList.ToArray();
            Debug.WriteLine($"[BuildingsAccessor] Extracted cables={cables.Length}");

            // ---- dstyles ----
            // NOTE: Unlike paint_mem and dstoreys, dstyles DOES use index 0
            // The file contains next_dstyle entries (indices 0 to next_dstyle-1)
            short[] styles = Array.Empty<short>();
            if (nextDStyle > 0)
            {
                long stylesBytes = (long)nextDStyle * 2;
                long stylesEnd = cursor + stylesBytes;
                if (stylesEnd <= blockEnd)
                {
                    styles = new short[nextDStyle];
                    for (int i = 0; i < nextDStyle; i++)
                        styles[i] = BitConverter.ToInt16(bytes, (int)(cursor + i * 2));
                    cursor = stylesEnd;
                }
                else
                {
                    Debug.WriteLine($"[BuildingsAccessor] styles OOB: cursor=0x{cursor:X} count={nextDStyle} blockEnd=0x{blockEnd:X}");
                }
            }

            // ---- paint_mem ----
            // NOTE: The original format writes next_paint_mem bytes including unused slot 0
            // We read all bytes so that paint_mem[Index] works with 1-based Index values
            byte[] paintMem = Array.Empty<byte>();
            if (saveType >= 17 && nextPaintMem > 0)
            {
                long pmEnd = cursor + nextPaintMem;
                if (pmEnd <= blockEnd)
                {
                    paintMem = new byte[nextPaintMem];
                    Buffer.BlockCopy(bytes, (int)cursor, paintMem, 0, nextPaintMem);
                    cursor = pmEnd;
                }
                else
                {
                    Debug.WriteLine($"[BuildingsAccessor] paint_mem OOB: cursor=0x{cursor:X} size={nextPaintMem} blockEnd=0x{blockEnd:X}");
                }
            }

            // ---- dstoreys ----
            // NOTE: The original format writes next_dstorey entries including unused slot 0
            // We read all entries so that dstoreys[Id] works with 1-based Id values
            var storeys = Array.Empty<BuildingArrays.DStoreyRec>();
            if (saveType >= 17 && nextDStorey > 0)
            {
                long storeyBytes = (long)nextDStorey * DStoreyRecSize;
                long storeyEnd = cursor + storeyBytes;
                if (storeyEnd <= blockEnd)
                {
                    storeys = new BuildingArrays.DStoreyRec[nextDStorey];
                    for (int i = 0; i < nextDStorey; i++)
                    {
                        int off = (int)(cursor + i * DStoreyRecSize);

                        ushort style = ReadU16(bytes, off + 0);
                        ushort index = ReadU16(bytes, off + 2);
                        sbyte count = unchecked((sbyte)bytes[off + 4]);
                        byte padding = bytes[off + 5];

                        storeys[i] = new BuildingArrays.DStoreyRec(style, index, count, padding);
                    }
                    cursor = storeyEnd;
                }
                else
                {
                    Debug.WriteLine($"[BuildingsAccessor] dstoreys OOB: cursor=0x{cursor:X} count={nextDStorey} blockEnd=0x{blockEnd:X}");
                }
            }

            int indoorsStart = -1;
            int walkablesStart = -1;

            // Indoors outputs
            int nextInsideStorey = 0, nextInsideStair = 0, nextInsideBlock = 0;
            byte[] insideStoreysRaw = Array.Empty<byte>();
            byte[] insideStairsRaw = Array.Empty<byte>();
            byte[] insideBlock = Array.Empty<byte>();
            BuildingArrays.InsideStoreyRec[] insideStoreys = Array.Empty<BuildingArrays.InsideStoreyRec>();
            BuildingArrays.StaircaseRec[] insideStairs = Array.Empty<BuildingArrays.StaircaseRec>();

            // Walkables outputs
            ushort nextDWalkable = 0, nextRoofFace4 = 0;
            byte[] dwalkablesRaw = Array.Empty<byte>();
            byte[] roofFacesRaw = Array.Empty<byte>();
            DWalkableRec[] walkables = Array.Empty<DWalkableRec>();
            RoofFace4Rec[] roofFaces4 = Array.Empty<RoofFace4Rec>();

            static short ReadS16(byte[] b, int off) => BitConverter.ToInt16(b, off);
            static sbyte ReadS8(byte[] b, int off) => unchecked((sbyte)b[off]);

            // ---- Indoors chunk (optional) ----
            if (saveType >= 21)
            {
                indoorsStart = (int)cursor;

                // IMPORTANT: your evidence shows these are U16, not S32.
                // Layout appears: U16 storey, U16 stair, U16 blockBytes, U16 pad(0)
                if (cursor + 8 <= blockEnd)
                {
                    nextInsideStorey = ReadU16(bytes, (int)cursor + 0);
                    nextInsideStair = ReadU16(bytes, (int)cursor + 2);
                    nextInsideBlock = ReadU16(bytes, (int)cursor + 4);
                    // pad at +6 (often 0)
                    cursor += 8;

                    long storeysBytes = (long)nextInsideStorey * InsideStoreySize;
                    long stairsBytes = (long)nextInsideStair * StaircaseSize;
                    long blockBytes = (long)nextInsideBlock;

                    long indoorsEnd = cursor + storeysBytes + stairsBytes + blockBytes;

                    if (indoorsEnd <= blockEnd)
                    {
                        // Raw capture
                        if (storeysBytes > 0)
                        {
                            insideStoreysRaw = new byte[storeysBytes];
                            Buffer.BlockCopy(bytes, (int)cursor, insideStoreysRaw, 0, (int)storeysBytes);
                        }
                        cursor += storeysBytes;

                        if (stairsBytes > 0)
                        {
                            insideStairsRaw = new byte[stairsBytes];
                            Buffer.BlockCopy(bytes, (int)cursor, insideStairsRaw, 0, (int)stairsBytes);
                        }
                        cursor += stairsBytes;

                        if (blockBytes > 0)
                        {
                            insideBlock = new byte[blockBytes];
                            Buffer.BlockCopy(bytes, (int)cursor, insideBlock, 0, (int)blockBytes);
                        }
                        cursor += blockBytes;

                        // Typed parse (includes dummy [0] entry; engine is 1-based)
                        if (nextInsideStorey > 0)
                        {
                            insideStoreys = new BuildingArrays.InsideStoreyRec[nextInsideStorey];
                            int baseOff = (int)(cursor - (storeysBytes + stairsBytes + blockBytes) - storeysBytes - stairsBytes - blockBytes); // not used
                                                                                                                                               // easier: parse from insideStoreysRaw
                            for (int i = 0; i < nextInsideStorey; i++)
                            {
                                int off = i * InsideStoreySize;
                                insideStoreys[i] = new BuildingArrays.InsideStoreyRec(
                                    insideStoreysRaw[off + 0],
                                    insideStoreysRaw[off + 1],
                                    insideStoreysRaw[off + 2],
                                    insideStoreysRaw[off + 3],
                                    (ushort)(insideStoreysRaw[off + 4] | (insideStoreysRaw[off + 5] << 8)),
                                    (ushort)(insideStoreysRaw[off + 6] | (insideStoreysRaw[off + 7] << 8)),
                                    (ushort)(insideStoreysRaw[off + 8] | (insideStoreysRaw[off + 9] << 8)),
                                    (ushort)(insideStoreysRaw[off + 10] | (insideStoreysRaw[off + 11] << 8)),
                                    (ushort)(insideStoreysRaw[off + 12] | (insideStoreysRaw[off + 13] << 8)),
                                    (short)(insideStoreysRaw[off + 14] | (insideStoreysRaw[off + 15] << 8)),
                                    (ushort)(insideStoreysRaw[off + 16] | (insideStoreysRaw[off + 17] << 8)),
                                    (ushort)(insideStoreysRaw[off + 18] | (insideStoreysRaw[off + 19] << 8)),
                                    (ushort)(insideStoreysRaw[off + 20] | (insideStoreysRaw[off + 21] << 8))
                                );
                            }
                        }

                        if (nextInsideStair > 0)
                        {
                            insideStairs = new BuildingArrays.StaircaseRec[nextInsideStair];
                            for (int i = 0; i < nextInsideStair; i++)
                            {
                                int off = i * StaircaseSize;
                                insideStairs[i] = new BuildingArrays.StaircaseRec(
                                    insideStairsRaw[off + 0],
                                    insideStairsRaw[off + 1],
                                    insideStairsRaw[off + 2],
                                    insideStairsRaw[off + 3],
                                    (short)(insideStairsRaw[off + 4] | (insideStairsRaw[off + 5] << 8)),
                                    (short)(insideStairsRaw[off + 6] | (insideStairsRaw[off + 7] << 8)),
                                    (short)(insideStairsRaw[off + 8] | (insideStairsRaw[off + 9] << 8))
                                );
                            }
                        }

                        Debug.WriteLine($"[BuildingsAccessor] Indoors parsed: next_storey={nextInsideStorey} next_stair={nextInsideStair} next_block={nextInsideBlock}");
                    }
                    else
                    {
                        Debug.WriteLine($"[BuildingsAccessor] Indoors OOB: storey={nextInsideStorey} stair={nextInsideStair} block={nextInsideBlock} cursor=0x{cursor:X} blockEnd=0x{blockEnd:X}");
                    }
                }
            }

            // ---- Walkables chunk ----
            walkablesStart = (int)cursor;

            if (cursor + 4 <= blockEnd)
            {
                nextDWalkable = ReadU16(bytes, (int)cursor + 0);
                nextRoofFace4 = ReadU16(bytes, (int)cursor + 2);
                cursor += 4;

                long dwBytes = (long)nextDWalkable * DWalkableSize;
                long rfBytes = (long)nextRoofFace4 * RoofFace4Size;

                long walkEnd = cursor + dwBytes + rfBytes;
                if (walkEnd <= blockEnd)
                {
                    // Raw
                    if (dwBytes > 0)
                    {
                        dwalkablesRaw = new byte[dwBytes];
                        Buffer.BlockCopy(bytes, (int)cursor, dwalkablesRaw, 0, (int)dwBytes);
                    }
                    cursor += dwBytes;

                    if (rfBytes > 0)
                    {
                        roofFacesRaw = new byte[rfBytes];
                        Buffer.BlockCopy(bytes, (int)cursor, roofFacesRaw, 0, (int)rfBytes);
                    }
                    cursor += rfBytes;

                    // Typed (includes dummy [0]; engine is 1-based)
                    if (nextDWalkable > 0)
                    {
                        walkables = new DWalkableRec[nextDWalkable];
                        for (int i = 0; i < nextDWalkable; i++)
                        {
                            int off = i * DWalkableSize;
                            walkables[i] = new DWalkableRec(
                                (ushort)(dwalkablesRaw[off + 0] | (dwalkablesRaw[off + 1] << 8)),
                                (ushort)(dwalkablesRaw[off + 2] | (dwalkablesRaw[off + 3] << 8)),
                                (ushort)(dwalkablesRaw[off + 4] | (dwalkablesRaw[off + 5] << 8)),
                                (ushort)(dwalkablesRaw[off + 6] | (dwalkablesRaw[off + 7] << 8)),
                                (ushort)(dwalkablesRaw[off + 8] | (dwalkablesRaw[off + 9] << 8)),
                                (ushort)(dwalkablesRaw[off + 10] | (dwalkablesRaw[off + 11] << 8)),
                                dwalkablesRaw[off + 12],
                                dwalkablesRaw[off + 13],
                                dwalkablesRaw[off + 14],
                                dwalkablesRaw[off + 15],
                                dwalkablesRaw[off + 16],
                                dwalkablesRaw[off + 17],
                                (ushort)(dwalkablesRaw[off + 18] | (dwalkablesRaw[off + 19] << 8)),
                                (ushort)(dwalkablesRaw[off + 20] | (dwalkablesRaw[off + 21] << 8))
                            );
                        }
                    }

                    if (nextRoofFace4 > 0)
                    {
                        roofFaces4 = new RoofFace4Rec[nextRoofFace4];
                        for (int i = 0; i < nextRoofFace4; i++)
                        {
                            int off = i * RoofFace4Size;
                            roofFaces4[i] = new RoofFace4Rec(
                                (short)(roofFacesRaw[off + 0] | (roofFacesRaw[off + 1] << 8)),
                                unchecked((sbyte)roofFacesRaw[off + 2]),
                                unchecked((sbyte)roofFacesRaw[off + 3]),
                                unchecked((sbyte)roofFacesRaw[off + 4]),
                                roofFacesRaw[off + 5],
                                roofFacesRaw[off + 6],
                                roofFacesRaw[off + 7],
                                (short)(roofFacesRaw[off + 8] | (roofFacesRaw[off + 9] << 8))
                            );
                        }
                    }

                    Debug.WriteLine($"[BuildingsAccessor] Walkables header: next_dwalkable={nextDWalkable} next_roof_face4={nextRoofFace4} at 0x{walkablesStart:X}");

                    for (int i = 1; i < nextDWalkable; i++)
                    {
                        var w = walkables[i];
                        Debug.WriteLine($"[Walkable {i}] face4=[{w.StartFace4}..{w.EndFace4}) " +
                                        $"rect=({w.X1},{w.Z1})-({w.X2},{w.Z2}) y={w.Y} storeyY={w.StoreyY} " +
                                        $"next={w.Next} bld={w.Building}");
                    }

                    for (int i = 1; i < nextRoofFace4; i++)
                    {
                        var r = roofFaces4[i];
                        Debug.WriteLine($"[RoofFace4 {i}] Y={r.Y} DY=({r.DY0},{r.DY1},{r.DY2}) RX={r.RX} RZ={r.RZ} next={r.Next}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[BuildingsAccessor] Walkables OOB: dw={nextDWalkable} rf={nextRoofFace4} cursor=0x{cursor:X} blockEnd=0x{blockEnd:X}");
                }
            }

            return new BuildingArrays
            {
                StartOffset = start,
                Length = len,
                FacetsStart = facetsOff,

                Buildings = buildings,
                Facets = facets,
                Styles = styles,
                PaintMem = paintMem,
                Storeys = storeys,
                Cables = cables,

                NextDBuilding = nextDBuilding,
                NextDFacet = nextDFacet,
                NextDStyle = nextDStyle,
                NextPaintMem = nextPaintMem,
                NextDStorey = nextDStorey,
                SaveType = saveType,

                IndoorsStart = indoorsStart,
                WalkablesStart = walkablesStart,
                NextInsideStorey = nextInsideStorey,
                NextInsideStair = nextInsideStair,
                InsideStoreysRaw = insideStoreysRaw,
                InsideStairsRaw = insideStairsRaw,
                NextInsideBlock = nextInsideBlock,
                InsideStoreys = insideStoreys,
                InsideStairs = insideStairs,
                InsideBlock = insideBlock,
                NextDWalkable = nextDWalkable,
                NextRoofFace4 = nextRoofFace4,
                DWalkablesRaw = dwalkablesRaw,
                RoofFaces4Raw = roofFacesRaw,
                Walkables = walkables,
                RoofFaces4 = roofFaces4,
            };
        }

        // -------------------- ADD THESE HELPERS INSIDE BuildingsAccessor --------------------
        public bool TryUpdateDStyleValue(int dstyleIndex, short newRawStyleId)
        {
            if (!_svc.IsLoaded) return false;

            _svc.ComputeAndCacheBuildingRegion();
            if (!_svc.TryGetBuildingRegion(out int start, out int len)) return false;

            // SaveType lives at file start in your code
            var bytesCopy = _svc.GetBytesCopy();
            int saveType = BitConverter.ToInt32(bytesCopy, 0);

            ushort nextDBuilding = ReadU16(bytesCopy, start + 2);
            ushort nextDFacet = ReadU16(bytesCopy, start + 4);
            ushort nextDStyle = ReadU16(bytesCopy, start + 6);

            int totalBuildings = Math.Max(0, nextDBuilding - 1);
            int totalFacets = Math.Max(0, nextDFacet - 1);

            if (dstyleIndex < 0 || dstyleIndex >= nextDStyle)
                return false;

            int buildingsOff = start + HeaderSize;
            int facetsOff = buildingsOff + totalBuildings * DBuildingSize + AfterBuildingsPad;
            int stylesOff = facetsOff + totalFacets * DFacetSize;

            int off = stylesOff + dstyleIndex * 2;

            _svc.Edit(bytes =>
            {
                bytes[off + 0] = (byte)(newRawStyleId & 0xFF);
                bytes[off + 1] = (byte)((newRawStyleId >> 8) & 0xFF);
            });

            // If you have notifications, do them here:
            // BuildingsChangeBus.Instance.NotifyChanged();

            return true;
        }

        public bool TryUpdateDStoreyBaseStyle(int storeyId, ushort newBaseStyleId)
        {
            if (!_svc.IsLoaded) return false;

            _svc.ComputeAndCacheBuildingRegion();
            if (!_svc.TryGetBuildingRegion(out int start, out int len)) return false;

            var bytesCopy = _svc.GetBytesCopy();
            int saveType = BitConverter.ToInt32(bytesCopy, 0);

            if (saveType < 17) return false;

            ushort nextDBuilding = ReadU16(bytesCopy, start + 2);
            ushort nextDFacet = ReadU16(bytesCopy, start + 4);
            ushort nextDStyle = ReadU16(bytesCopy, start + 6);
            ushort nextPaintMem = ReadU16(bytesCopy, start + 8);
            ushort nextDStorey = ReadU16(bytesCopy, start + 10);

            int totalBuildings = Math.Max(0, nextDBuilding - 1);
            int totalFacets = Math.Max(0, nextDFacet - 1);

            // NOTE: storeyId here is the one stored as -dstyles[i]
            // Your format commonly keeps slot 0 unused, so storeyId must be within [1 .. nextDStorey-1]
            if (storeyId <= 0 || storeyId >= nextDStorey)
                return false;

            int buildingsOff = start + HeaderSize;
            int facetsOff = buildingsOff + totalBuildings * DBuildingSize + AfterBuildingsPad;
            int stylesOff = facetsOff + totalFacets * DFacetSize;

            int paintMemOff = stylesOff + nextDStyle * 2;
            int storeysOff = paintMemOff + nextPaintMem;

            int off = storeysOff + storeyId * DStoreyRecSize;   // storeyId is used as index (slot 0 unused)
            _svc.Edit(bytes =>
            {
                bytes[off + 0] = (byte)(newBaseStyleId & 0xFF);
                bytes[off + 1] = (byte)((newBaseStyleId >> 8) & 0xFF);
            });

            // BuildingsChangeBus.Instance.NotifyChanged();

            return true;
        }
        private static int ReadS24(byte[] b, int off)
        {
            int v = b[off + 0] | (b[off + 1] << 8) | (b[off + 2] << 16);
            // sign-extend 24-bit
            if ((v & 0x0080_0000) != 0)
                v |= unchecked((int)0xFF00_0000);
            return v;
        }

        private static string DumpHex(byte[] b, int off, int len)
        {
            if (off < 0 || len <= 0 || off + len > b.Length) return "<oob>";
            var sb = new System.Text.StringBuilder(len * 3);
            for (int i = 0; i < len; i++)
            {
                if (i != 0) sb.Append(' ');
                sb.Append(b[off + i].ToString("X2"));
            }
            return sb.ToString();
        }

        // ---------- Optional: strict scan helper ----------
        // ---------- Region finder: PC .IAM only (no heuristic scan) ----------
        public static bool TryFindRegion(byte[] bytes, int objectOffset, out int headerOffset, out int regionLength)
        {
            headerOffset = -1;
            regionLength = 0;

            if (bytes == null)
                return false;

            // PC .IAM: building block header is always at 0x18008
            const int PcBuildingsHeaderOffset = 0x18008;

            // Basic sanity checks
            if (bytes.Length <= PcBuildingsHeaderOffset)
                return false;

            if (objectOffset <= PcBuildingsHeaderOffset || objectOffset > bytes.Length)
                return false;

            headerOffset = PcBuildingsHeaderOffset;
            regionLength = objectOffset - PcBuildingsHeaderOffset;
            return true;
        }

        // ---------- Walkables getters ----------

        /// <summary>Absolute offset of the Walkables header (U16 nextDWalkable, U16 nextRoofFace4).</summary>
        public bool TryGetWalkablesHeaderOffset(out int headerOffset)
        {
            headerOffset = -1;
            if (!_svc.IsLoaded) return false;

            _svc.ComputeAndCacheBuildingRegion();
            if (!_svc.TryGetBuildingRegion(out int start, out _)) return false;

            // Cheapest: reuse ReadSnapshot, because it already walks cursor correctly (saveType aware).
            var snap = ReadSnapshot();
            if (snap.WalkablesStart < 0) return false;

            headerOffset = snap.WalkablesStart;
            return true;
        }

        /// <summary>Compute absolute offset of a 1-based DWalkable entry.</summary>
        public bool TryGetDWalkableOffset(int walkableId1, out int walkableOffset)
        {
            walkableOffset = -1;
            var snap = ReadSnapshot();

            // snap.NextDWalkable is the "next" index (includes dummy [0])
            if (snap.WalkablesStart < 0) return false;
            if (walkableId1 < 1 || walkableId1 >= snap.NextDWalkable) return false;

            // Layout: [U16 nextDWalkable][U16 nextRoofFace4] then DWalkable array
            walkableOffset = snap.WalkablesStart + 4 + (walkableId1 * DWalkableSize);
            return true;
        }

        /// <summary>Compute absolute offset of a 1-based RoofFace4 entry.</summary>
        public bool TryGetRoofFace4Offset(int roofFaceId1, out int roofFaceOffset)
        {
            roofFaceOffset = -1;
            var snap = ReadSnapshot();

            if (snap.WalkablesStart < 0) return false;
            if (roofFaceId1 < 1 || roofFaceId1 >= snap.NextRoofFace4) return false;

            int roofBase = snap.WalkablesStart + 4 + (snap.NextDWalkable * DWalkableSize);
            roofFaceOffset = roofBase + (roofFaceId1 * RoofFace4Size);
            return true;
        }

        /// <summary>Convenience: returns typed arrays for rendering (includes dummy [0]).</summary>
        public bool TryGetWalkables(out DWalkableRec[] walkables, out RoofFace4Rec[] roofFaces4)
        {
            var snap = ReadSnapshot();
            walkables = snap.Walkables ?? Array.Empty<DWalkableRec>();
            roofFaces4 = snap.RoofFaces4 ?? Array.Empty<RoofFace4Rec>();
            return walkables.Length > 1 || roofFaces4.Length > 1;
        }

        // ---------- Editing helpers ----------
        /// <summary>Compute the absolute byte offset of a 1-based facet id.</summary>
        public bool TryGetFacetOffset(int facetId1, out int facetOffset)
        {
            facetOffset = -1;
            if (!_svc.IsLoaded) return false;

            _svc.ComputeAndCacheBuildingRegion();
            if (!_svc.TryGetBuildingRegion(out int start, out int _)) return false;

            var bytes = _svc.GetBytesCopy();
            if (start < 0 || start + HeaderSize > bytes.Length) return false;

            ushort nextDBuilding = ReadU16(bytes, start + 2);
            ushort nextDFacet = ReadU16(bytes, start + 4);

            int totalFacets = Math.Max(0, nextDFacet - 1);
            if (facetId1 < 1 || facetId1 > totalFacets) return false;

            int buildingsOff = start + HeaderSize;
            int facetsOff = buildingsOff + (nextDBuilding - 1) * DBuildingSize + AfterBuildingsPad;

            facetOffset = facetsOff + (facetId1 - 1) * DFacetSize;
            return facetOffset + DFacetSize <= bytes.Length;
        }

        public bool TryGetBuildingBytes(int buildingId1, out byte[] raw24, out int buildingOffset)
        {
            raw24 = Array.Empty<byte>();
            buildingOffset = -1;

            if (!_svc.IsLoaded) return false;

            _svc.ComputeAndCacheBuildingRegion();
            if (!_svc.TryGetBuildingRegion(out int start, out int _)) return false;

            var bytes = _svc.GetBytesCopy();
            if (start < 0 || start + HeaderSize > bytes.Length) return false;

            ushort nextDBuilding = ReadU16(bytes, start + 2);
            int totalBuildings = Math.Max(0, nextDBuilding - 1);

            if (buildingId1 < 1 || buildingId1 > totalBuildings)
                return false;

            int buildingsOff = start + HeaderSize;
            buildingOffset = buildingsOff + (buildingId1 - 1) * DBuildingSize;

            if (buildingOffset < 0 || buildingOffset + DBuildingSize > bytes.Length)
                return false;

            raw24 = new byte[DBuildingSize];
            Buffer.BlockCopy(bytes, buildingOffset, raw24, 0, DBuildingSize);
            return true;
        }

        public bool TryUpdateFacetFlags(int facetId1, FacetFlags flags)
        {
            if (!TryGetFacetOffset(facetId1, out int facet0)) return false;
            int flagsOff = facet0 + 10; // U16 at +10
            bool success = _svc.TryWriteU16_LE(flagsOff, (ushort)flags);

            if (success)
            {
                BuildingsChangeBus.Instance.NotifyFacetChanged(facetId1);
                BuildingsChangeBus.Instance.NotifyChanged();
            }

            return success;
        }

        public bool TryUpdateFacetType(int facetId1, FacetType newType)
        {
            if (!_svc.IsLoaded) return false;
            if (!TryGetFacetOffset(facetId1, out int facet0)) return false;

            _svc.Edit(bytes =>
            {
                if (facet0 >= 0 && facet0 < bytes.Length)
                    bytes[facet0 + 0] = (byte)newType; // first byte is type
            });

            BuildingsChangeBus.Instance.NotifyFacetChanged(facetId1);
            BuildingsChangeBus.Instance.NotifyChanged();

            return true;
        }

        /// <summary>
        /// Updates the X0, Z0, X1, Z1 coordinates of a facet.
        /// Offsets in DFacetRec: X0=+2, X1=+3, Z0=+8, Z1=+9 (all bytes)
        /// </summary>
        public bool TryUpdateFacetCoords(int facetId1, byte x0, byte z0, byte x1, byte z1)
        {
            if (!_svc.IsLoaded) return false;
            if (!TryGetFacetOffset(facetId1, out int facet0)) return false;

            // Validate range
            if (x0 > 127 || z0 > 127 || x1 > 127 || z1 > 127)
            {
                Debug.WriteLine($"[BuildingsAccessor] TryUpdateFacetCoords: coords out of range (0-127)");
                return false;
            }

            _svc.Edit(bytes =>
            {
                if (facet0 >= 0 && facet0 + DFacetSize <= bytes.Length)
                {
                    bytes[facet0 + 2] = x0;  // X0
                    bytes[facet0 + 3] = x1;  // X1
                    bytes[facet0 + 8] = z0;  // Z0
                    bytes[facet0 + 9] = z1;  // Z1
                }
            });

            Debug.WriteLine($"[BuildingsAccessor] Updated facet #{facetId1} coords: ({x0},{z0})->({x1},{z1})");

            // Notify change bus
            BuildingsChangeBus.Instance.NotifyFacetChanged(facetId1);
            BuildingsChangeBus.Instance.NotifyChanged();

            return true;
        }

        /// <summary>
        /// Updates the height-related fields of a facet.
        /// Offsets in DFacetRec:
        ///   Height (coarse) = +1 (byte)
        ///   Y0 = +4 (short, little-endian)
        ///   Y1 = +6 (short, little-endian)
        ///   FHeight (fine) = +18 (byte)
        ///   BlockHeight = +19 (byte)
        /// </summary>
        public bool TryUpdateFacetHeights(int facetId1, byte height, byte fheight, short y0, short y1, byte blockHeight)
        {
            if (!_svc.IsLoaded) return false;
            if (!TryGetFacetOffset(facetId1, out int facet0)) return false;

            _svc.ComputeAndCacheBuildingRegion();
            if (!_svc.TryGetBuildingRegion(out int blockStart, out int _)) return false;

            var bytes = _svc.GetBytesCopy();
            if (blockStart < 0 || blockStart + HeaderSize > bytes.Length) return false;

            // Read current facet fields needed to determine dstyle allocation
            byte currentHeight = bytes[facet0 + 1];
            var facetFlags = (FacetFlags)ReadU16(bytes, facet0 + 10);
            ushort styleIndex = ReadU16(bytes, facet0 + 12);
            ushort buildingId = ReadU16(bytes, facet0 + 14);

            int oldBands = currentHeight == 0 ? 1 : (currentHeight / 4) + 1;
            int newBands = height == 0 ? 1 : (height / 4) + 1;

            if (newBands > oldBands)
            {
                // More bands are being added: we must allocate new dstyle slots in the file
                // and fix up all subsequent facets' StyleIndex values.
                bool twoTextured = (facetFlags & FacetFlags.TwoTextured) != 0;
                bool twoSided    = (facetFlags & FacetFlags.TwoSided)    != 0;
                bool hugFloor    = (facetFlags & FacetFlags.HugFloor)    != 0;
                bool isInside    = (facetFlags & FacetFlags.Inside)      != 0;
                int entriesPerBand = (!hugFloor && (twoTextured || twoSided)) ? 2 : 1;

                // Warehouse outside/inside dstyles are interleaved, so step skips one extra slot
                bool isWarehouse = false;
                ushort nextBuilding = ReadU16(bytes, blockStart + 2);
                if (buildingId >= 1 && buildingId < nextBuilding)
                {
                    int bldOff = blockStart + HeaderSize + (buildingId - 1) * DBuildingSize;
                    if (bldOff + DBuildingSize <= bytes.Length)
                        isWarehouse = (BuildingType)bytes[bldOff + 11] == BuildingType.Warehouse;
                }
                int styleIndexStep = isWarehouse ? entriesPerBand + 1 : entriesPerBand;

                // facetStyleStart: first dstyle slot for this facet
                // StyleIndex always points to the first allocated dstyle slot for the facet
                int facetStyleStart = styleIndex;

                // Read header counters
                ushort nextFacet    = ReadU16(bytes, blockStart + 4);
                ushort nextStyle    = ReadU16(bytes, blockStart + 6);
                ushort nextPaintMem = ReadU16(bytes, blockStart + 8);
                ushort nextStorey   = ReadU16(bytes, blockStart + 10);

                // File section offsets
                int buildingsOff    = blockStart + HeaderSize;
                int padOff          = buildingsOff + (nextBuilding - 1) * DBuildingSize;
                int facetsOff       = padOff + AfterBuildingsPad;
                int stylesOff       = facetsOff + (nextFacet - 1) * DFacetSize;
                int paintMemOff     = stylesOff + nextStyle * 2;
                int storeysOff      = paintMemOff + nextPaintMem;
                int afterStoreysOff = storeysOff + nextStorey * DStoreyRecSize;

                // Insertion point in dstyles[]: immediately after the last old-band slot.
                // Dual-face blocks have a header slot at facetStyleStart+0 before the band slots begin,
                // so the first band starts at facetStyleStart+1. insertAt must account for this.
                bool isDualFace = (!hugFloor && (twoTextured || twoSided));
                int insertAt    = facetStyleStart + (isDualFace ? 1 : 0) + oldBands * styleIndexStep;
                int extraSlots  = (newBands - oldBands) * styleIndexStep;

                if (insertAt > nextStyle)
                {
                    Debug.WriteLine($"[BuildingsAccessor] TryUpdateFacetHeights: insertAt={insertAt} > nextStyle={nextStyle}, bad dstyle layout");
                    return false;
                }

                // Determine base style for the new slots (copy from band 0)
                short baseStyle = 1;
                if (facetStyleStart >= 0 && facetStyleStart < nextStyle)
                {
                    int b0Off = stylesOff + facetStyleStart * 2;
                    short b0Val = (short)(bytes[b0Off] | (bytes[b0Off + 1] << 8));
                    if (b0Val > 0)
                    {
                        baseStyle = b0Val;
                    }
                    else if (b0Val < 0)
                    {
                        // Band 0 is painted — look up its DStorey to find the base style
                        int storeyIdx = -b0Val;
                        if (storeyIdx > 0 && storeyIdx < nextStorey)
                        {
                            int sOff = storeysOff + storeyIdx * DStoreyRecSize;
                            if (sOff + 2 <= bytes.Length)
                                baseStyle = (short)(bytes[sOff] | (bytes[sOff + 1] << 8));
                        }
                    }
                }

                Debug.WriteLine($"[BuildingsAccessor] TryUpdateFacetHeights: bands {oldBands}->{newBands}, inserting {extraSlots} dstyle slots at index {insertAt}, baseStyle={baseStyle}");

                using var ms = new MemoryStream();

                // 1. Everything before the building block
                ms.Write(bytes, 0, blockStart);

                // 2. Header with updated nextStyle
                var header = new byte[HeaderSize];
                Buffer.BlockCopy(bytes, blockStart, header, 0, HeaderSize);
                ushort newNextStyle = (ushort)(nextStyle + extraSlots);
                header[6] = (byte)(newNextStyle & 0xFF);
                header[7] = (byte)((newNextStyle >> 8) & 0xFF);
                ms.Write(header, 0, HeaderSize);

                // 3. Buildings (unchanged)
                int buildingsSize = (nextBuilding - 1) * DBuildingSize;
                if (buildingsSize > 0)
                    ms.Write(bytes, buildingsOff, buildingsSize);

                // 4. Padding (unchanged)
                ms.Write(bytes, padOff, AfterBuildingsPad);

                // 5. Facets: update height for this facet; bump StyleIndex for facets whose
                //    dstyle block starts at or after the insertion point.
                int facetsSize = (nextFacet - 1) * DFacetSize;
                var facetsData = new byte[facetsSize];
                Buffer.BlockCopy(bytes, facetsOff, facetsData, 0, facetsSize);

                // Apply height changes to the target facet
                int facetLocal = (facetId1 - 1) * DFacetSize;
                facetsData[facetLocal + 1]  = height;
                facetsData[facetLocal + 4]  = (byte)(y0 & 0xFF);
                facetsData[facetLocal + 5]  = (byte)((y0 >> 8) & 0xFF);
                facetsData[facetLocal + 6]  = (byte)(y1 & 0xFF);
                facetsData[facetLocal + 7]  = (byte)((y1 >> 8) & 0xFF);
                facetsData[facetLocal + 18] = fheight;
                facetsData[facetLocal + 19] = blockHeight;

                // Shift StyleIndex for all facets whose dstyle block starts at/after insertAt
                for (int i = 0; i < nextFacet - 1; i++)
                {
                    int off = i * DFacetSize;
                    var fFlags = (FacetFlags)((ushort)(facetsData[off + 10] | (facetsData[off + 11] << 8)));
                    ushort fStyleIdx = (ushort)(facetsData[off + 12] | (facetsData[off + 13] << 8));
                    int fBlockStart = fStyleIdx;
                    if (fBlockStart >= insertAt)
                    {
                        ushort newSI = (ushort)(fStyleIdx + extraSlots);
                        facetsData[off + 12] = (byte)(newSI & 0xFF);
                        facetsData[off + 13] = (byte)((newSI >> 8) & 0xFF);
                    }
                }
                ms.Write(facetsData, 0, facetsSize);

                // 6. dstyles: copy before insertAt, insert new slots, copy remainder
                int insertAtByte = Math.Min(insertAt * 2, nextStyle * 2);
                if (insertAtByte > 0)
                    ms.Write(bytes, stylesOff, insertAtByte);
                for (int i = 0; i < extraSlots; i++)
                {
                    ms.WriteByte((byte)(baseStyle & 0xFF));
                    ms.WriteByte((byte)((baseStyle >> 8) & 0xFF));
                }
                int remainingStyleBytes = nextStyle * 2 - insertAtByte;
                if (remainingStyleBytes > 0)
                    ms.Write(bytes, stylesOff + insertAtByte, remainingStyleBytes);

                // 7. paint_mem (unchanged)
                if (nextPaintMem > 0)
                    ms.Write(bytes, paintMemOff, nextPaintMem);

                // 8. dstoreys (unchanged)
                int storeysSize = nextStorey * DStoreyRecSize;
                if (storeysSize > 0)
                    ms.Write(bytes, storeysOff, storeysSize);

                // 9. Tail (unchanged)
                int tailSize = bytes.Length - afterStoreysOff;
                if (tailSize > 0)
                    ms.Write(bytes, afterStoreysOff, tailSize);

                var newBytes = ms.ToArray();
                int expectedGrowth = extraSlots * 2;
                int actualGrowth = newBytes.Length - bytes.Length;

                if (actualGrowth != expectedGrowth)
                {
                    Debug.WriteLine($"[BuildingsAccessor] TryUpdateFacetHeights size mismatch: expected={expectedGrowth}, actual={actualGrowth}");
                    return false;
                }

                _svc.ReplaceBytes(newBytes);
                Debug.WriteLine($"[BuildingsAccessor] TryUpdateFacetHeights: inserted {extraSlots} dstyle slots, file grew by {actualGrowth} bytes");
            }
            else
            {
                // Band count unchanged or decreased — in-place height update only
                _svc.Edit(bytes2 =>
                {
                    if (facet0 >= 0 && facet0 + DFacetSize <= bytes2.Length)
                    {
                        bytes2[facet0 + 1]  = height;
                        bytes2[facet0 + 4]  = (byte)(y0 & 0xFF);
                        bytes2[facet0 + 5]  = (byte)((y0 >> 8) & 0xFF);
                        bytes2[facet0 + 6]  = (byte)(y1 & 0xFF);
                        bytes2[facet0 + 7]  = (byte)((y1 >> 8) & 0xFF);
                        bytes2[facet0 + 18] = fheight;
                        bytes2[facet0 + 19] = blockHeight;
                    }
                });
            }

            Debug.WriteLine($"[BuildingsAccessor] Updated facet #{facetId1} heights: H={height} FH={fheight} Y0={y0} Y1={y1} BH={blockHeight}");
            BuildingsChangeBus.Instance.NotifyFacetChanged(facetId1);
            BuildingsChangeBus.Instance.NotifyChanged();
            return true;
        }

        /// <summary>
        /// Updates the StyleIndex of a facet.
        /// Offset in DFacetRec: StyleIndex = +12 (ushort, little-endian)
        /// </summary>
        public bool TryUpdateFacetStyleIndex(int facetId1, ushort styleIndex)
        {
            if (!_svc.IsLoaded) return false;
            if (!TryGetFacetOffset(facetId1, out int facet0)) return false;

            _svc.Edit(bytes =>
            {
                if (facet0 >= 0 && facet0 + DFacetSize <= bytes.Length)
                {
                    bytes[facet0 + 12] = (byte)(styleIndex & 0xFF);
                    bytes[facet0 + 13] = (byte)((styleIndex >> 8) & 0xFF);
                }
            });

            Debug.WriteLine($"[BuildingsAccessor] Updated facet #{facetId1} StyleIndex: {styleIndex}");

            BuildingsChangeBus.Instance.NotifyFacetChanged(facetId1);
            BuildingsChangeBus.Instance.NotifyChanged();

            return true;
        }

        // ---------- Helpers ----------

        /// <summary>
        /// Updates the Building field of a facet.
        /// Offset in DFacetRec: Building = +14 (ushort, little-endian)
        /// Note: For Cable facets, this field is repurposed as step_angle2.
        /// </summary>
        public bool TryUpdateFacetBuilding(int facetId1, ushort buildingId)
        {
            if (!_svc.IsLoaded) return false;
            if (!TryGetFacetOffset(facetId1, out int facet0)) return false;

            _svc.Edit(bytes =>
            {
                if (facet0 >= 0 && facet0 + DFacetSize <= bytes.Length)
                {
                    bytes[facet0 + 14] = (byte)(buildingId & 0xFF);
                    bytes[facet0 + 15] = (byte)((buildingId >> 8) & 0xFF);
                }
            });

            Debug.WriteLine($"[BuildingsAccessor] Updated facet #{facetId1} Building: {buildingId}");
            return true;
        }

        /// <summary>
        /// Updates the Storey field of a facet.
        /// Offset in DFacetRec: Storey = +16 (ushort, little-endian)
        /// </summary>
        public bool TryUpdateFacetStorey(int facetId1, ushort storey)
        {
            if (!_svc.IsLoaded) return false;
            if (!TryGetFacetOffset(facetId1, out int facet0)) return false;

            _svc.Edit(bytes =>
            {
                if (facet0 >= 0 && facet0 + DFacetSize <= bytes.Length)
                {
                    bytes[facet0 + 16] = (byte)(storey & 0xFF);
                    bytes[facet0 + 17] = (byte)((storey >> 8) & 0xFF);
                }
            });

            Debug.WriteLine($"[BuildingsAccessor] Updated facet #{facetId1} Storey: {storey}");
            return true;
        }

        /// <summary>
        /// Updates the Open field of a facet (used for doors).
        /// Offset in DFacetRec: Open = +20 (byte)
        /// </summary>
        public bool TryUpdateFacetOpen(int facetId1, byte open)
        {
            if (!_svc.IsLoaded) return false;
            if (!TryGetFacetOffset(facetId1, out int facet0)) return false;

            _svc.Edit(bytes =>
            {
                if (facet0 >= 0 && facet0 + DFacetSize <= bytes.Length)
                {
                    bytes[facet0 + 20] = open;
                }
            });

            Debug.WriteLine($"[BuildingsAccessor] Updated facet #{facetId1} Open: {open}");
            return true;
        }

        public static int DecodePaintPage(byte b) => b & 0x7F;          // lower 7 bits
        public static bool DecodePaintFlag(byte b) => (b & 0x80) != 0;   // high bit

        private static bool PlausibleHeader(byte[] b, int off, int objOff, out long facetsOff, out long facetsEnd)
        {
            facetsOff = facetsEnd = -1;
            if (off < 0 || off + HeaderSize > b.Length) return false;

            int nextB = ReadU16(b, off + 2);
            int nextF = ReadU16(b, off + 4);
            int buildings = nextB - 1;
            int facets = nextF - 1;

            if (buildings < 1 || facets < 8) return false;
            if (buildings > 5000 || facets > 200000) return false;

            facetsOff = off + HeaderSize + (long)buildings * DBuildingSize + AfterBuildingsPad;
            facetsEnd = facetsOff + (long)facets * DFacetSize;

            if (facetsOff < off + HeaderSize) return false;
            if (facetsEnd > objOff) return false;

            int check = Math.Min(facets, 8);
            for (int i = 0; i < check; i++)
            {
                int fOff = (int)(facetsOff + i * DFacetSize);
                if (fOff + DFacetSize > b.Length) return false;

                byte type = b[fOff + 0];
                byte h = b[fOff + 1];
                byte x0 = b[fOff + 2];
                byte x1 = b[fOff + 3];
                byte z0 = b[fOff + 8];
                byte z1 = b[fOff + 9];

                if (!((type <= 25) || type == 100)) return false;
                if (h > 64) return false;
                if (x0 > 127 || x1 > 127 || z0 > 127 || z1 > 127) return false;
            }
            return true;
        }

        private static bool IsZero(byte[] b, int off, int len)
        {
            if (len <= 0) return true;
            if (off < 0 || off + len > b.Length) return false;
            for (int i = 0; i < len; i++) if (b[off + i] != 0) return false;
            return true;
        }

        private static ushort ReadU16(byte[] b, int off)
            => (ushort)(b[off + 0] | (b[off + 1] << 8));

        /// <summary>
        /// Finds the 1-based DBuilding index that owns the given 1-based facet id,
        /// based on the DBuilding.StartFacet/EndFacet ranges. Returns 0 if none.
        /// </summary>
        private static int FindBuildingIndexForFacet(DBuildingRec[] buildings, int facetId1)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                var b = buildings[i];
                if (facetId1 >= b.StartFacet && facetId1 < b.EndFacet)
                {
                    // DBuilding ids are 1-based in the engine
                    return i + 1;
                }
            }
            return 0;
        }
    }
}