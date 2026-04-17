// Services/Buildings/BuildingMover.cs
// Captures a building snapshot and applies in-place move or copy operations.

using System.Diagnostics;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.Services.Textures;

namespace UrbanChaosMapEditor.Services.Buildings
{
    public sealed class BuildingMover
    {
        private const int DFacetSize = 26;
        private const int DWalkableSize = 22;
        private const int RoofFace4Size = 10;

        private readonly MapDataService _svc;

        public BuildingMover(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        // ════════════════════════════════════════════════════════════════
        // Capture
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Reads the current building data and returns a snapshot suitable for ghost rendering and placement.
        /// Returns null if the building cannot be found or has no facets.
        /// </summary>
        public BuildingSnapshot? Capture(int buildingId1)
        {
            if (!_svc.IsLoaded) return null;

            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
                return null;

            var bld = snap.Buildings[buildingId1 - 1];

            int facetStart0 = bld.StartFacet - 1;   // convert 1-based to 0-based
            int facetEnd0   = bld.EndFacet - 1;       // exclusive (EndFacet is one past last)

            if (facetStart0 < 0 || facetEnd0 <= facetStart0 || facetEnd0 > snap.Facets.Length)
                return null;

            // ── Facet bounding box ─────────────────────────────────────
            int minGX = 255, maxGX = 0, minGZ = 255, maxGZ = 0;

            for (int fi = facetStart0; fi < facetEnd0; fi++)
            {
                var f = snap.Facets[fi];
                if (f.X0 < minGX) minGX = f.X0;
                if (f.X0 > maxGX) maxGX = f.X0;
                if (f.X1 < minGX) minGX = f.X1;
                if (f.X1 > maxGX) maxGX = f.X1;
                if (f.Z0 < minGZ) minGZ = f.Z0;
                if (f.Z0 > maxGZ) maxGZ = f.Z0;
                if (f.Z1 < minGZ) minGZ = f.Z1;
                if (f.Z1 > maxGZ) maxGZ = f.Z1;
            }

            if (minGX > maxGX) return null;

            // ── Walkables belonging to this building ──────────────────────
            var walkableIds = new List<int>();
            if (snap.Walkables != null)
            {
                for (int wi = 1; wi < snap.Walkables.Length; wi++)
                {
                    var w = snap.Walkables[wi];
                    if (w.Building == buildingId1)
                        walkableIds.Add(wi);
                }
            }

            // ── PAP cells covered by building footprint ────────────────────────────
            // This now uses a wall-derived 2D footprint and no longer depends on walkables.
            var cells = new List<SnapshotCellData>();
            var mapBytes = _svc.MapBytes;

            foreach (var (gx, gz) in RoofEnclosureService.GetBuildingFootprintTiles(buildingId1))
            {
                int tx = 127 - gx;  // game -> UI tile
                int tz = 127 - gz;

                if ((uint)tx >= MapConstants.TilesPerSide || (uint)tz >= MapConstants.TilesPerSide)
                    continue;

                try
                {
                    int pOff = PapTileOffset(tx, tz);
                    cells.Add(new SnapshotCellData
                    {
                        TileX = tx,
                        TileZ = tz,
                        TexByte0 = mapBytes![pOff + 0],
                        TexByte1 = mapBytes![pOff + 1],
                        Flags = (ushort)(mapBytes![pOff + 2] | (mapBytes![pOff + 3] << 8)),
                        Alt = unchecked((sbyte)mapBytes![pOff + 5]),
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BuildingMover.Capture] cell ({tx},{tz}) error: {ex.Message}");
                }
            }

            return new BuildingSnapshot
            {
                BuildingId1    = buildingId1,
                Building       = bld,
                FacetStart0    = facetStart0,
                FacetEnd0      = facetEnd0,
                MinGameX       = minGX,
                MaxGameX       = maxGX,
                MinGameZ       = minGZ,
                MaxGameZ       = maxGZ,
                WalkableIds1   = walkableIds.ToArray(),
                Cells          = cells.ToArray(),
                FacetsStart    = snap.FacetsStart,
                WalkablesStart = snap.WalkablesStart,
                NextDWalkable  = snap.NextDWalkable,
            };
        }


        private static void NotifyPapAndTextureRefresh(BuildingSnapshot snap, int dGameX, int dGameZ)
        {
            if (snap.Cells == null || snap.Cells.Length == 0)
                return;

            int minTx = int.MaxValue;
            int minTz = int.MaxValue;
            int maxTx = int.MinValue;
            int maxTz = int.MinValue;

            foreach (var cell in snap.Cells)
            {
                // Old/original tile
                Include(cell.TileX, cell.TileZ);

                // New/destination tile
                int origGameX = 127 - cell.TileX;
                int origGameZ = 127 - cell.TileZ;
                int newGameX = Math.Clamp(origGameX + dGameX, 0, 127);
                int newGameZ = Math.Clamp(origGameZ + dGameZ, 0, 127);
                int newTx = 127 - newGameX;
                int newTz = 127 - newGameZ;

                Include(newTx, newTz);
            }

            if (minTx <= maxTx && minTz <= maxTz)
            {
                AltitudeChangeBus.Instance.NotifyRegion(minTx, minTz, maxTx, maxTz);
            }

            TexturesChangeBus.Instance.NotifyChanged();

            static void Clamp(ref int tx, ref int tz)
            {
                tx = Math.Clamp(tx, 0, 127);
                tz = Math.Clamp(tz, 0, 127);
            }

            void Include(int tx, int tz)
            {
                Clamp(ref tx, ref tz);

                if (tx < minTx) minTx = tx;
                if (tz < minTz) minTz = tz;
                if (tx > maxTx) maxTx = tx;
                if (tz > maxTz) maxTz = tz;
            }
        }



        // ════════════════════════════════════════════════════════════════
        // Move in-place (same building ID; clear old cells)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Applies a delta to all facet X/Z, walkable X/Z, and RF4 tile coords.
        /// Clears old PAP cells and writes old alt/flags to new tile positions.
        /// </summary>
        /// <param name="snap">Snapshot captured before the move.</param>
        /// <param name="dGameX">Delta in game X (positive = move toward higher game X = leftward in UI).</param>
        /// <param name="dGameZ">Delta in game Z.</param>
        public bool MoveInPlace(BuildingSnapshot snap, int dGameX, int dGameZ)
        {
            if (!_svc.IsLoaded) return false;
            if (snap.FacetsStart < 0 || snap.WalkablesStart < 0) return false;

            var acc = new BuildingsAccessor(_svc);
            var current = acc.ReadSnapshot();
            if (current.FacetsStart < 0) return false;

            var bytes = _svc.GetBytesCopy();

            // ── Patch facets ──────────────────────────────────────────────
            for (int fi = snap.FacetStart0; fi < snap.FacetEnd0; fi++)
            {
                if (fi < 0 || fi >= current.Facets.Length) continue;
                int off = snap.FacetsStart + fi * DFacetSize;

                byte newX0 = (byte)Math.Clamp(bytes[off + 2] + dGameX, 0, 127);
                byte newX1 = (byte)Math.Clamp(bytes[off + 3] + dGameX, 0, 127);
                byte newZ0 = (byte)Math.Clamp(bytes[off + 8] + dGameZ, 0, 127);
                byte newZ1 = (byte)Math.Clamp(bytes[off + 9] + dGameZ, 0, 127);

                bytes[off + 2] = newX0;
                bytes[off + 3] = newX1;
                bytes[off + 8] = newZ0;
                bytes[off + 9] = newZ1;
            }

            // ── Patch walkables and their RF4s ────────────────────────────
            // Walkables array starts at WalkablesStart + 4 (after the 4-byte header)
            int walkBase = snap.WalkablesStart + 4;
            int rf4Base  = walkBase + snap.NextDWalkable * DWalkableSize;

            foreach (int wi in snap.WalkableIds1)
            {
                if (wi <= 0 || wi >= snap.NextDWalkable) continue;

                int wOff = walkBase + wi * DWalkableSize;
                if (wOff + DWalkableSize > bytes.Length) continue;

                // X1[12], Z1[13], X2[14], Z2[15]
                bytes[wOff + 12] = (byte)Math.Clamp(bytes[wOff + 12] + dGameX, 0, 127);
                bytes[wOff + 13] = (byte)Math.Clamp(bytes[wOff + 13] + dGameZ, 0, 127);
                bytes[wOff + 14] = (byte)Math.Clamp(bytes[wOff + 14] + dGameX, 0, 127);
                bytes[wOff + 15] = (byte)Math.Clamp(bytes[wOff + 15] + dGameZ, 0, 127);

                // RF4 range for this walkable
                ushort startF4 = (ushort)(bytes[wOff + 8] | (bytes[wOff + 9] << 8));
                ushort endF4   = (ushort)(bytes[wOff + 10] | (bytes[wOff + 11] << 8));

                for (int ri = startF4; ri < endF4; ri++)
                {
                    int rOff = rf4Base + ri * RoofFace4Size;
                    if (rOff + RoofFace4Size > bytes.Length) break;

                    // RX at [6]: lower 7 bits = tileX, high bit = flag
                    byte rxRaw = bytes[rOff + 6];
                    int tileX  = rxRaw & 0x7F;
                    int flag   = rxRaw & 0x80;
                    int newTX  = Math.Clamp(tileX + dGameX, 0, 127);
                    bytes[rOff + 6] = (byte)(flag | (newTX & 0x7F));

                    // RZ at [7]: tileZ = RZ - 128, so new RZ = (tileZ + dGameZ) + 128
                    byte rzRaw = bytes[rOff + 7];
                    int tileZ  = rzRaw - 128;
                    int newTZ  = Math.Clamp(tileZ + dGameZ, 0, 127);
                    bytes[rOff + 7] = (byte)(newTZ + 128);
                }
            }

            _svc.ReplaceBytes(bytes);

            // ── Write new cell data first, then clear old positions ──────────────────
            // (Write new first so that if old == new positions, data is preserved.)
            var mb = _svc.MapBytes!;

            foreach (var cell in snap.Cells)
            {
                int origGameX = 127 - cell.TileX;
                int origGameZ = 127 - cell.TileZ;
                int newGameX  = Math.Clamp(origGameX + dGameX, 0, 127);
                int newGameZ  = Math.Clamp(origGameZ + dGameZ, 0, 127);
                int newTx     = 127 - newGameX;
                int newTz     = 127 - newGameZ;

                if (newTx == cell.TileX && newTz == cell.TileZ) continue; // same tile, skip

                try
                {
                    int dOff = PapTileOffset(newTx, newTz);
                    mb[dOff + 0] = cell.TexByte0;
                    mb[dOff + 1] = cell.TexByte1;
                    mb[dOff + 2] = (byte)(cell.Flags & 0xFF);
                    mb[dOff + 3] = (byte)((cell.Flags >> 8) & 0xFF);
                    // [4] terrain height — leave untouched at destination
                    mb[dOff + 5] = unchecked((byte)cell.Alt);
                }
                catch { /* out of range, skip */ }
            }

            // ── Clear old PAP cells (zero tex, flags, alt; preserve terrain height) ──
            foreach (var cell in snap.Cells)
            {
                int origGameX = 127 - cell.TileX;
                int origGameZ = 127 - cell.TileZ;
                int newGameX  = Math.Clamp(origGameX + dGameX, 0, 127);
                int newGameZ  = Math.Clamp(origGameZ + dGameZ, 0, 127);
                int newTx     = 127 - newGameX;
                int newTz     = 127 - newGameZ;

                if (newTx == cell.TileX && newTz == cell.TileZ) continue; // same tile, nothing to clear

                try
                {
                    int sOff = PapTileOffset(cell.TileX, cell.TileZ);
                    mb[sOff + 0] = 0; // tex byte 0 → tex000
                    mb[sOff + 1] = 0; // tex byte 1 → world group, no rotation
                    mb[sOff + 2] = 0; // flags low
                    mb[sOff + 3] = 0; // flags high
                    // [4] terrain height — preserve
                    mb[sOff + 5] = 0; // alt → 0
                }
                catch { /* out of range, skip */ }
            }

            _svc.MarkDirty();

            NotifyPapAndTextureRefresh(snap, dGameX, dGameZ);

            BuildingsChangeBus.Instance.NotifyChanged();
            RoofsChangeBus.Instance.NotifyChanged();

            return true;
        }

        // ════════════════════════════════════════════════════════════════
        // Copy (new building ID; leave original intact)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a new building at the offset position, inserting new facets and walkables.
        /// The original building is left untouched.
        /// Returns the new building ID on success, -1 on failure.
        /// </summary>
        public int CopyToNewPosition(BuildingSnapshot snap, int dGameX, int dGameZ)
        {
            if (!_svc.IsLoaded) return -1;

            var acc = new BuildingsAccessor(_svc);
            var current = acc.ReadSnapshot();

            if (current.FacetsStart < 0 || current.WalkablesStart < 0) return -1;

            var bytes = _svc.GetBytesCopy();
            int blockStart = current.StartOffset;

            // Header counters
            int headerOff = blockStart;
            ushort oldNextBuilding = ReadU16(bytes, headerOff + 2);
            ushort oldNextFacet    = ReadU16(bytes, headerOff + 4);

            int facetCount = snap.FacetEnd0 - snap.FacetStart0;
            if (facetCount <= 0) return -1;

            // ── Build new facet bytes ──────────────────────────────────
            var newFacets = new byte[facetCount * DFacetSize];
            for (int i = 0; i < facetCount; i++)
            {
                int srcOff = snap.FacetsStart + (snap.FacetStart0 + i) * DFacetSize;
                int dstOff = i * DFacetSize;
                Buffer.BlockCopy(bytes, srcOff, newFacets, dstOff, DFacetSize);

                // Patch X0, X1, Z0, Z1 with delta
                newFacets[dstOff + 2] = (byte)Math.Clamp(newFacets[dstOff + 2] + dGameX, 0, 127);
                newFacets[dstOff + 3] = (byte)Math.Clamp(newFacets[dstOff + 3] + dGameX, 0, 127);
                newFacets[dstOff + 8] = (byte)Math.Clamp(newFacets[dstOff + 8] + dGameZ, 0, 127);
                newFacets[dstOff + 9] = (byte)Math.Clamp(newFacets[dstOff + 9] + dGameZ, 0, 127);

                // Update building ID field to new building (will set below)
                // We'll patch this after we know the new building ID
            }

            // New building ID is oldNextBuilding (1-based)
            int newBldId1 = oldNextBuilding;
            ushort newBldU = (ushort)newBldId1;

            // Patch building field in each new facet
            for (int i = 0; i < facetCount; i++)
            {
                int dstOff = i * DFacetSize;
                // Building field is at byte [14-15]
                newFacets[dstOff + 14] = (byte)(newBldU & 0xFF);
                newFacets[dstOff + 15] = (byte)((newBldU >> 8) & 0xFF);
            }

            ushort newStartFacet = oldNextFacet;
            ushort newEndFacet   = (ushort)(oldNextFacet + facetCount);

            // ── Build new walkable + RF4 bytes ────────────────────────────
            int walkBase = snap.WalkablesStart + 4;
            int rf4Base  = walkBase + snap.NextDWalkable * DWalkableSize;

            // We'll append walkables after the existing ones, starting at index NextDWalkable
            ushort oldNextWalkable = snap.NextDWalkable;
            ushort oldNextRf4      = ReadU16(bytes, snap.WalkablesStart + 2);

            var newWalkableBytes = new List<byte>();
            var newRf4Bytes      = new List<byte>();

            ushort walkCounter = oldNextWalkable;
            ushort rf4Counter  = oldNextRf4;

            foreach (int wi in snap.WalkableIds1)
            {
                if (wi <= 0 || wi >= snap.NextDWalkable) continue;

                int wOff = walkBase + wi * DWalkableSize;
                if (wOff + DWalkableSize > bytes.Length) continue;

                var wb = new byte[DWalkableSize];
                Buffer.BlockCopy(bytes, wOff, wb, 0, DWalkableSize);

                // Patch X1[12], Z1[13], X2[14], Z2[15]
                wb[12] = (byte)Math.Clamp(wb[12] + dGameX, 0, 127);
                wb[13] = (byte)Math.Clamp(wb[13] + dGameZ, 0, 127);
                wb[14] = (byte)Math.Clamp(wb[14] + dGameX, 0, 127);
                wb[15] = (byte)Math.Clamp(wb[15] + dGameZ, 0, 127);

                // Patch Building field [20-21]
                wb[20] = (byte)(newBldU & 0xFF);
                wb[21] = (byte)((newBldU >> 8) & 0xFF);

                // Capture old RF4 range
                ushort startF4 = (ushort)(bytes[wOff + 8] | (bytes[wOff + 9] << 8));
                ushort endF4   = (ushort)(bytes[wOff + 10] | (bytes[wOff + 11] << 8));
                int rf4Cnt = Math.Max(0, endF4 - startF4);

                // Update StartFace4[8-9] and EndFace4[10-11] to new RF4 range
                ushort newStartF4 = rf4Counter;
                ushort newEndF4   = (ushort)(rf4Counter + rf4Cnt);
                wb[8]  = (byte)(newStartF4 & 0xFF);
                wb[9]  = (byte)((newStartF4 >> 8) & 0xFF);
                wb[10] = (byte)(newEndF4 & 0xFF);
                wb[11] = (byte)((newEndF4 >> 8) & 0xFF);

                newWalkableBytes.AddRange(wb);
                walkCounter++;

                // Copy RF4 entries
                for (int ri = startF4; ri < endF4; ri++)
                {
                    int rOff = rf4Base + ri * RoofFace4Size;
                    if (rOff + RoofFace4Size > bytes.Length) break;

                    var rb = new byte[RoofFace4Size];
                    Buffer.BlockCopy(bytes, rOff, rb, 0, RoofFace4Size);

                    // Patch RX[6] and RZ[7]
                    byte rxRaw = rb[6];
                    int tileX  = rxRaw & 0x7F;
                    int flag   = rxRaw & 0x80;
                    rb[6] = (byte)(flag | (Math.Clamp(tileX + dGameX, 0, 127) & 0x7F));

                    byte rzRaw = rb[7];
                    int tileZ  = rzRaw - 128;
                    rb[7] = (byte)(Math.Clamp(tileZ + dGameZ, 0, 127) + 128);

                    newRf4Bytes.AddRange(rb);
                    rf4Counter++;
                }
            }

            // ── Rebuild the file using MemoryStream ───────────────────────
            // Layout (simplified): ... | header | buildings | pad | facets | styles | paint | storeys | indoors | walkables_hdr | walkables | rf4s | tail ...
            // We append new building record, new facets, new walkables, new RF4s.
            // The safest approach: patch the existing bytes array in-place using GetBytesCopy.

            // 1) Insert new building record
            //    Building record is at: blockStart + 48 + (N-1)*24 for building N (1-based)
            //    But inserting in the middle of the file is complex — we'll do a MemoryStream rebuild.

            const int HeaderSize       = 48;
            const int DBuildingSize    = 24;
            const int AfterBuildingsPad = 14;

            int buildingsOff    = blockStart + HeaderSize;
            int oldBuildingsEnd = buildingsOff + (oldNextBuilding - 1) * DBuildingSize;

            // Build the new building record
            var newBldBytes = new byte[DBuildingSize];
            WriteU16(newBldBytes, 0, newStartFacet);
            WriteU16(newBldBytes, 2, newEndFacet);
            WriteU16(newBldBytes, 4, 0);                   // Walkable = first new walkable (set below)
            newBldBytes[11] = snap.Building.Type;          // keep same type

            // Set Walkable field if we have new walkables
            if (newWalkableBytes.Count > 0)
                WriteU16(newBldBytes, 4, oldNextWalkable);

            // Size of everything from facets onward in the OLD file
            int afterBuildingsPadEnd = oldBuildingsEnd + AfterBuildingsPad;
            int restLen = bytes.Length - afterBuildingsPadEnd;

            using var ms = new System.IO.MemoryStream();

            // Everything before the block
            ms.Write(bytes, 0, blockStart);

            // New header (bump NextDBuilding)
            var header = new byte[HeaderSize];
            Buffer.BlockCopy(bytes, blockStart, header, 0, HeaderSize);
            WriteU16(header, 2, (ushort)(oldNextBuilding + 1));
            ms.Write(header, 0, HeaderSize);

            // Existing building records
            if (oldNextBuilding > 1)
                ms.Write(bytes, buildingsOff, (oldNextBuilding - 1) * DBuildingSize);

            // New building record
            ms.Write(newBldBytes, 0, DBuildingSize);

            // AfterBuildingsPad + existing facets (bump NextDFacet in header is done below)
            // We need to insert the new facets at the END of the facets array.
            // The facets array ends at: snap.FacetsStart + totalFacets * DFacetSize

            int totalOldFacets = (current.NextDFacet - 1);
            int oldFacetsEnd   = snap.FacetsStart + totalOldFacets * DFacetSize;

            // Write from oldBuildingsEnd to oldFacetsEnd (pad + facets)
            ms.Write(bytes, afterBuildingsPadEnd - AfterBuildingsPad, AfterBuildingsPad);
            ms.Write(bytes, snap.FacetsStart, totalOldFacets * DFacetSize);

            // New facets
            ms.Write(newFacets, 0, newFacets.Length);

            // Everything from oldFacetsEnd to the walkables section header
            // (styles, paint_mem, dstoreys, indoors)
            int walkHdrOffset = snap.WalkablesStart;
            ms.Write(bytes, oldFacetsEnd, walkHdrOffset - oldFacetsEnd);

            // ── Patch walkables header (NextDWalkable + NextRoofFace4) ────────────────
            var walkHdr = new byte[4];
            WriteU16(walkHdr, 0, (ushort)(oldNextWalkable + (uint)newWalkableBytes.Count / DWalkableSize));
            WriteU16(walkHdr, 2, (ushort)(oldNextRf4      + (uint)newRf4Bytes.Count      / RoofFace4Size));
            ms.Write(walkHdr, 0, 4);

            // Existing walkables
            int existingWalkBytes = (oldNextWalkable) * DWalkableSize;
            ms.Write(bytes, walkBase, existingWalkBytes);

            // New walkables
            if (newWalkableBytes.Count > 0)
                ms.Write(newWalkableBytes.ToArray(), 0, newWalkableBytes.Count);

            // Existing RF4s
            int existingRf4Bytes = oldNextRf4 * RoofFace4Size;
            ms.Write(bytes, rf4Base, existingRf4Bytes);

            // New RF4s
            if (newRf4Bytes.Count > 0)
                ms.Write(newRf4Bytes.ToArray(), 0, newRf4Bytes.Count);

            // Everything after the old RF4s
            int oldRf4End = rf4Base + existingRf4Bytes;
            ms.Write(bytes, oldRf4End, bytes.Length - oldRf4End);

            var newBytes = ms.ToArray();

            // Patch NextDFacet in the new header
            int nfOffset = blockStart + 4;
            WriteU16(newBytes, nfOffset, newEndFacet);

            _svc.ReplaceBytes(newBytes);

            // ── Copy tex + flags + alt to new tile positions ──────────────────────────
            var mb = _svc.MapBytes!;
            foreach (var cell in snap.Cells)
            {
                int origGameX = 127 - cell.TileX;
                int origGameZ = 127 - cell.TileZ;
                int newGameX  = Math.Clamp(origGameX + dGameX, 0, 127);
                int newGameZ  = Math.Clamp(origGameZ + dGameZ, 0, 127);
                int newTx     = 127 - newGameX;
                int newTz     = 127 - newGameZ;

                try
                {
                    int dOff = PapTileOffset(newTx, newTz);
                    mb[dOff + 0] = cell.TexByte0;
                    mb[dOff + 1] = cell.TexByte1;
                    mb[dOff + 2] = (byte)(cell.Flags & 0xFF);
                    mb[dOff + 3] = (byte)((cell.Flags >> 8) & 0xFF);
                    // [4] terrain height — leave untouched at destination
                    mb[dOff + 5] = unchecked((byte)cell.Alt);
                }
                catch { /* out of range */ }
            }

            _svc.MarkDirty();

            NotifyPapAndTextureRefresh(snap, dGameX, dGameZ);

            BuildingsChangeBus.Instance.NotifyBuildingChanged(newBldId1, BuildingChangeType.Added);
            BuildingsChangeBus.Instance.NotifyChanged();
            RoofsChangeBus.Instance.NotifyChanged();

            return newBldId1;
        }

        // ════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════

        private static ushort ReadU16(byte[] b, int off)
            => (ushort)(b[off] | (b[off + 1] << 8));

        private static void WriteU16(byte[] b, int off, ushort v)
        {
            b[off]     = (byte)(v & 0xFF);
            b[off + 1] = (byte)((v >> 8) & 0xFF);
        }

        /// <summary>
        /// Returns the byte offset into MapBytes for a given UI tile coordinate (tx, tz).
        /// Formula matches AltitudeAccessor: fileIndex = (127-tx)*128 + (127-tz); offset = 8 + fileIndex*6.
        /// </summary>
        private static int PapTileOffset(int tx, int tz)
        {
            int fy = MapConstants.TilesPerSide - 1 - tx;   // = 127 - tx
            int fx = MapConstants.TilesPerSide - 1 - tz;   // = 127 - tz
            return 8 + (fy * MapConstants.TilesPerSide + fx) * 6;
        }
    }
}
