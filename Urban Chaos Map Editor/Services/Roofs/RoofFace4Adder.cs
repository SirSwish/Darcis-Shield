// /Services/RoofFace4Adder.cs
// Service for adding and deleting RoofFace4 entries
using System.Diagnostics;
using System.IO;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;

namespace UrbanChaosMapEditor.Services.Roofs
{

    public sealed class RoofFace4Adder
    {
        private const int HeaderSize = BuildingFormatConstants.HeaderSize;
        private const int DBuildingSize = BuildingFormatConstants.DBuildingSize;
        private const int AfterBuildingsPad = BuildingFormatConstants.AfterBuildingsPad;
        private const int DFacetSize = BuildingFormatConstants.DFacetSize;
        private const int DWalkableSize = BuildingFormatConstants.DWalkableSize;
        private const int RoofFace4Size = BuildingFormatConstants.RoofFace4Size;

        private readonly MapDataService _svc;

        public RoofFace4Adder(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        /// <summary>
        /// Adds a single RoofFace4 entry to a walkable region.
        /// If empty (zeroed) RF4 slots exist within the walkable's range, reuses them.
        /// Otherwise appends to the end.
        /// </summary>
        /// <param name="walkableId1">1-based walkable ID (index into array, 0 is sentinel)</param>
        /// <param name="rx">X offset within walkable (0 to width-1), converted to absolute internally</param>
        /// <param name="rz">Z offset within walkable (0 to depth-1), converted to absolute internally</param>
        /// <param name="y">Base altitude of this roof tile</param>
        /// <param name="dy0">Corner 0 (NW) height delta</param>
        /// <param name="dy1">Corner 1 (NE) height delta</param>
        /// <param name="dy2">Corner 2 (SE) height delta</param>
        /// <param name="drawFlags">Render flags (default 0x00 for working roofs)</param>
        public RoofFace4Result TryAddRoofFace4(
            int walkableId1,
            byte rx, byte rz,
            short y,
            sbyte dy0 = 0, sbyte dy1 = 0, sbyte dy2 = 0,
            byte drawFlags = 0x00)
        {
            if (!_svc.IsLoaded)
                return RoofFace4Result.Fail("No map loaded.");

            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.Walkables == null || snap.Walkables.Length <= 1)
                return RoofFace4Result.Fail("No walkables in map (only sentinel exists).");

            if (walkableId1 < 1 || walkableId1 >= snap.Walkables.Length)
                return RoofFace4Result.Fail($"Walkable #{walkableId1} not found (valid: 1-{snap.Walkables.Length - 1}).");

            var walkable = snap.Walkables[walkableId1];

            // Validate RX/RZ within walkable bounds
            int width = walkable.X2 - walkable.X1;
            int depth = walkable.Z2 - walkable.Z1;
            if (width <= 0 || depth <= 0)
                return RoofFace4Result.Fail($"Walkable #{walkableId1} has invalid dimensions ({width}x{depth}).");
            if (rx >= width)
                return RoofFace4Result.Fail($"RX={rx} outside walkable width (0-{width - 1}).");
            if (rz >= depth)
                return RoofFace4Result.Fail($"RZ={rz} outside walkable depth (0-{depth - 1}).");

            var bytes = _svc.GetBytesCopy();
            int blockStart = snap.StartOffset;
            int saveType = BitConverter.ToInt32(bytes, 0);

            var offsets = CalculateOffsets(bytes, blockStart, saveType);

            // Check if there are empty RF4 slots within the walkable's range we can reuse
            int emptySlot = FindEmptyRF4SlotInRange(bytes, offsets, walkable.StartFace4, walkable.EndFace4);

            // Create new RoofFace4 record
            // Convert relative offsets to ABSOLUTE coordinates as the engine expects:
            byte absRx = (byte)(walkable.X1 + rx);
            byte absRz = (byte)(walkable.Z1 + rz + 128);

            var newRf4 = new byte[RoofFace4Size];
            WriteS16(newRf4, 0, y);
            newRf4[2] = (byte)dy0;
            newRf4[3] = (byte)dy1;
            newRf4[4] = (byte)dy2;
            newRf4[5] = drawFlags;
            newRf4[6] = absRx;
            newRf4[7] = absRz;
            WriteS16(newRf4, 8, 0); // Next = 0

            if (emptySlot >= 0)
            {
                // REUSE empty slot - no file restructuring needed!
                Debug.WriteLine($"[RoofFace4Adder] Reusing empty RF4 slot {emptySlot} for walkable #{walkableId1}");
                Debug.WriteLine($"[RoofFace4Adder]   Absolute: RX={absRx}, RZ={absRz}, Y={y}");

                int rf4Offset = offsets.RoofFacesDataOff + emptySlot * RoofFace4Size;
                Buffer.BlockCopy(newRf4, 0, bytes, rf4Offset, RoofFace4Size);

                // Update walkable's EndFace4 if needed (expand range to include this slot + 1)
                int walkableOffset = offsets.WalkablesDataOff + walkableId1 * DWalkableSize;
                ushort currentEndFace4 = ReadU16(bytes, walkableOffset + 10);
                if (emptySlot >= currentEndFace4)
                {
                    WriteU16(bytes, walkableOffset + 10, (ushort)(emptySlot + 1));
                    Debug.WriteLine($"[RoofFace4Adder] Expanded walkable EndFace4 to {emptySlot + 1}");
                }

                _svc.ReplaceBytes(bytes);
                BuildingsChangeBus.Instance.NotifyChanged();

                return RoofFace4Result.Success(emptySlot);
            }

            // No empty slot found - append to end (original logic)
            int insertPosition = walkable.EndFace4;

            Debug.WriteLine($"[RoofFace4Adder] Appending RF4 to walkable #{walkableId1}");
            Debug.WriteLine($"[RoofFace4Adder]   Range: [{walkable.StartFace4}..{walkable.EndFace4}), insert at {insertPosition}");
            Debug.WriteLine($"[RoofFace4Adder]   Relative: rx={rx}, rz={rz} -> Absolute: RX={absRx}, RZ={absRz}");
            Debug.WriteLine($"[RoofFace4Adder]   Y={y}, DY=({dy0},{dy1},{dy2})");

            using var ms = new MemoryStream();

            // 1. Copy everything before walkables header
            ms.Write(bytes, 0, offsets.WalkablesHeaderOff);

            // 2. Write updated header
            WriteU16ToStream(ms, offsets.NextWalkable);
            WriteU16ToStream(ms, (ushort)(offsets.NextRoofFace4 + 1));

            // 3. Write walkables with updated ranges
            for (int wIdx = 0; wIdx < offsets.NextWalkable; wIdx++)
            {
                int srcOff = offsets.WalkablesDataOff + wIdx * DWalkableSize;
                var wBytes = new byte[DWalkableSize];
                Buffer.BlockCopy(bytes, srcOff, wBytes, 0, DWalkableSize);

                ushort startF4 = ReadU16(wBytes, 8);
                ushort endF4 = ReadU16(wBytes, 10);

                if (wIdx == walkableId1)
                {
                    // Target walkable: expand EndFace4
                    WriteU16(wBytes, 10, (ushort)(endF4 + 1));
                }
                else if (startF4 >= insertPosition)
                {
                    // Walkable after insertion: shift both indices
                    WriteU16(wBytes, 8, (ushort)(startF4 + 1));
                    WriteU16(wBytes, 10, (ushort)(endF4 + 1));
                }

                ms.Write(wBytes, 0, DWalkableSize);
            }

            // 4. Write RoofFace4 with insertion
            // Entries 0..insertPosition-1
            if (insertPosition > 0)
                ms.Write(bytes, offsets.RoofFacesDataOff, insertPosition * RoofFace4Size);

            // New entry
            ms.Write(newRf4, 0, RoofFace4Size);

            // Entries insertPosition..end
            int afterCount = offsets.NextRoofFace4 - insertPosition;
            if (afterCount > 0)
            {
                int afterOff = offsets.RoofFacesDataOff + insertPosition * RoofFace4Size;
                ms.Write(bytes, afterOff, afterCount * RoofFace4Size);
            }

            // 5. Copy tail (objects + end data)
            int tailOff = offsets.RoofFacesDataOff + offsets.NextRoofFace4 * RoofFace4Size;
            int tailLen = bytes.Length - tailOff;
            if (tailLen > 0)
                ms.Write(bytes, tailOff, tailLen);

            var newBytes = ms.ToArray();

            if (newBytes.Length != bytes.Length + RoofFace4Size)
            {
                return RoofFace4Result.Fail($"Size mismatch: expected +{RoofFace4Size}, got {newBytes.Length - bytes.Length}");
            }

            _svc.ReplaceBytes(newBytes);
            BuildingsChangeBus.Instance.NotifyChanged();

            Debug.WriteLine($"[RoofFace4Adder] Success: new RF4 at index {insertPosition}");
            return RoofFace4Result.Success(insertPosition);
        }

        /// <summary>
        /// Deletes a RoofFace4 entry and updates walkable references.
        /// </summary>
        public RoofFace4Result TryDeleteRoofFace4(int roofFace4Id)
        {
            if (!_svc.IsLoaded)
                return RoofFace4Result.Fail("No map loaded.");

            if (roofFace4Id < 1)
                return RoofFace4Result.Fail("Cannot delete sentinel at index 0.");

            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.RoofFaces4 == null || roofFace4Id >= snap.RoofFaces4.Length)
                return RoofFace4Result.Fail($"RoofFace4 #{roofFace4Id} not found.");

            var bytes = _svc.GetBytesCopy();
            int blockStart = snap.StartOffset;
            int saveType = BitConverter.ToInt32(bytes, 0);

            var offsets = CalculateOffsets(bytes, blockStart, saveType);

            Debug.WriteLine($"[RoofFace4Adder] Deleting RF4 #{roofFace4Id}");

            using var ms = new MemoryStream();

            // 1. Copy before header
            ms.Write(bytes, 0, offsets.WalkablesHeaderOff);

            // 2. Updated header
            WriteU16ToStream(ms, offsets.NextWalkable);
            WriteU16ToStream(ms, (ushort)(offsets.NextRoofFace4 - 1));

            // 3. Walkables with updated ranges
            for (int wIdx = 0; wIdx < offsets.NextWalkable; wIdx++)
            {
                int srcOff = offsets.WalkablesDataOff + wIdx * DWalkableSize;
                var wBytes = new byte[DWalkableSize];
                Buffer.BlockCopy(bytes, srcOff, wBytes, 0, DWalkableSize);

                ushort startF4 = ReadU16(wBytes, 8);
                ushort endF4 = ReadU16(wBytes, 10);

                if (roofFace4Id >= startF4 && roofFace4Id < endF4)
                {
                    // Deleted RF4 in this walkable's range: shrink end
                    WriteU16(wBytes, 10, (ushort)(endF4 - 1));
                }
                else if (startF4 > roofFace4Id)
                {
                    // After deletion: shift down
                    WriteU16(wBytes, 8, (ushort)(startF4 - 1));
                    WriteU16(wBytes, 10, (ushort)(endF4 - 1));
                }

                ms.Write(wBytes, 0, DWalkableSize);
            }

            // 4. RoofFace4 entries, skipping deleted
            for (int rf4 = 0; rf4 < offsets.NextRoofFace4; rf4++)
            {
                if (rf4 == roofFace4Id) continue;
                int srcOff = offsets.RoofFacesDataOff + rf4 * RoofFace4Size;
                ms.Write(bytes, srcOff, RoofFace4Size);
            }

            // 5. Tail
            int tailOff = offsets.RoofFacesDataOff + offsets.NextRoofFace4 * RoofFace4Size;
            int tailLen = bytes.Length - tailOff;
            if (tailLen > 0)
                ms.Write(bytes, tailOff, tailLen);

            var newBytes = ms.ToArray();

            if (newBytes.Length != bytes.Length - RoofFace4Size)
            {
                return RoofFace4Result.Fail($"Size mismatch on delete.");
            }

            _svc.ReplaceBytes(newBytes);
            BuildingsChangeBus.Instance.NotifyChanged();

            return RoofFace4Result.Success(roofFace4Id);
        }

        /// <summary>
        /// Rewrites the Y field of every RoofFace4 entry in the given walkable's range.
        /// Used when a warehouse walkable's WorldY changes (e.g. because a higher enclosing
        /// polygon was formed above it, or a bulk facet height edit was applied). DY deltas
        /// and draw flags are preserved — only the base Y is updated.
        /// Returns the number of RF4 entries updated, or -1 on failure.
        /// </summary>
        public int TryUpdateWalkableRoofFace4Y(int walkableId1, short newY)
        {
            if (!_svc.IsLoaded) return -1;

            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.Walkables == null || walkableId1 < 1 || walkableId1 >= snap.Walkables.Length)
                return -1;

            var walkable = snap.Walkables[walkableId1];
            if (walkable.EndFace4 <= walkable.StartFace4)
                return 0;

            var bytes = _svc.GetBytesCopy();
            int blockStart = snap.StartOffset;
            int saveType = BitConverter.ToInt32(bytes, 0);
            var offsets = CalculateOffsets(bytes, blockStart, saveType);

            int updated = 0;
            for (int i = walkable.StartFace4; i < walkable.EndFace4; i++)
            {
                int rf4Offset = offsets.RoofFacesDataOff + i * RoofFace4Size;

                // Skip empty/zeroed slots — they aren't real RF4 entries
                bool isEmpty = true;
                for (int j = 0; j < RoofFace4Size; j++)
                {
                    if (bytes[rf4Offset + j] != 0) { isEmpty = false; break; }
                }
                if (isEmpty) continue;

                WriteS16(bytes, rf4Offset, newY);
                updated++;
            }

            if (updated > 0)
            {
                _svc.ReplaceBytes(bytes);
                BuildingsChangeBus.Instance.NotifyChanged();
                Debug.WriteLine($"[RoofFace4Adder] Bulk updated {updated} RF4 Y values to {newY} in walkable #{walkableId1}");
            }

            return updated;
        }

        /// <summary>
        /// Creates a flat roof filling all tiles in a walkable region.
        /// </summary>
        public RoofFace4Result TryCreateFlatRoof(int walkableId1, short altitude)
        {
            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (walkableId1 < 1 || walkableId1 >= snap.Walkables.Length)
                return RoofFace4Result.Fail($"Walkable #{walkableId1} not found.");

            var w = snap.Walkables[walkableId1];
            int width = w.X2 - w.X1;
            int depth = w.Z2 - w.Z1;

            Debug.WriteLine($"[RoofFace4Adder] Creating flat roof: {width}x{depth} tiles at Y={altitude}");

            int count = 0;
            for (byte rz = 0; rz < depth; rz++)
            {
                for (byte rx = 0; rx < width; rx++)
                {
                    var r = TryAddRoofFace4(walkableId1, rx, rz, altitude, 0, 0, 0, 0x08);
                    if (r.IsSuccess) count++;
                }
            }

            return RoofFace4Result.Success(count);
        }

        /// <summary>
        /// Creates a pitched roof with slope in specified direction.
        /// </summary>
        public RoofFace4Result TryCreatePitchedRoof(
            int walkableId1,
            short baseAltitude,
            sbyte pitchPerTile,
            PitchDirection direction)
        {
            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (walkableId1 < 1 || walkableId1 >= snap.Walkables.Length)
                return RoofFace4Result.Fail($"Walkable #{walkableId1} not found.");

            var w = snap.Walkables[walkableId1];
            int width = w.X2 - w.X1;
            int depth = w.Z2 - w.Z1;

            int count = 0;
            for (byte rz = 0; rz < depth; rz++)
            {
                for (byte rx = 0; rx < width; rx++)
                {
                    sbyte dy0 = 0, dy1 = 0, dy2 = 0;

                    switch (direction)
                    {
                        case PitchDirection.SlopesNorth: // High at south, low at north
                            dy0 = 0; dy1 = 0;
                            dy2 = (sbyte)(rz * pitchPerTile);
                            break;
                        case PitchDirection.SlopesSouth: // High at north, low at south
                            dy0 = (sbyte)((depth - 1 - rz) * pitchPerTile);
                            dy1 = dy0;
                            dy2 = (sbyte)((depth - 1 - rz) * pitchPerTile);
                            break;
                        case PitchDirection.SlopesEast: // High at west, low at east
                            dy0 = (sbyte)(rx * pitchPerTile);
                            dy1 = 0;
                            dy2 = 0;
                            break;
                        case PitchDirection.SlopesWest: // High at east, low at west
                            dy0 = 0;
                            dy1 = (sbyte)((width - 1 - rx) * pitchPerTile);
                            dy2 = dy1;
                            break;
                    }

                    var r = TryAddRoofFace4(walkableId1, rx, rz, baseAltitude, dy0, dy1, dy2, 0x08);
                    if (r.IsSuccess) count++;
                }
            }

            return RoofFace4Result.Success(count);
        }

        #region Offset Calculation

        private struct FileOffsets
        {
            public int WalkablesHeaderOff;
            public int WalkablesDataOff;
            public int RoofFacesDataOff;
            public ushort NextWalkable;
            public ushort NextRoofFace4;
        }

        private FileOffsets CalculateOffsets(byte[] bytes, int blockStart, int saveType)
        {
            // Mirror BuildingDeleter logic exactly
            ushort nextBuilding = ReadU16(bytes, blockStart + 2);
            ushort nextFacet = ReadU16(bytes, blockStart + 4);
            ushort nextStyle = ReadU16(bytes, blockStart + 6);
            ushort nextPaintMem = (saveType >= 17) ? ReadU16(bytes, blockStart + 8) : (ushort)0;
            ushort nextStorey = (saveType >= 17) ? ReadU16(bytes, blockStart + 10) : (ushort)0;

            int buildingsOff = blockStart + HeaderSize;
            int facetsOff = buildingsOff + (nextBuilding - 1) * DBuildingSize + AfterBuildingsPad;
            int stylesOff = facetsOff + (nextFacet - 1) * DFacetSize;
            int paintOff = stylesOff + nextStyle * 2;
            int storeysOff = paintOff + ((saveType >= 17) ? nextPaintMem : 0);
            int indoorsOff = storeysOff + ((saveType >= 17) ? nextStorey * 6 : 0);

            int indoorsLen = 0;
            if (saveType >= 21 && indoorsOff + 8 <= bytes.Length)
            {
                ushort nextIS = ReadU16(bytes, indoorsOff);
                ushort nextISt = ReadU16(bytes, indoorsOff + 2);
                ushort nextIB = ReadU16(bytes, indoorsOff + 4);
                indoorsLen = 8 + nextIS * 22 + nextISt * 10 + nextIB;
            }

            int walkablesHeaderOff = indoorsOff + indoorsLen;
            ushort nextWalkable = ReadU16(bytes, walkablesHeaderOff);
            ushort nextRoofFace4 = ReadU16(bytes, walkablesHeaderOff + 2);
            int walkablesDataOff = walkablesHeaderOff + 4;
            int roofFacesDataOff = walkablesDataOff + nextWalkable * DWalkableSize;

            return new FileOffsets
            {
                WalkablesHeaderOff = walkablesHeaderOff,
                WalkablesDataOff = walkablesDataOff,
                RoofFacesDataOff = roofFacesDataOff,
                NextWalkable = nextWalkable,
                NextRoofFace4 = nextRoofFace4
            };
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Find an empty (zeroed) RF4 slot within the given range.
        /// Returns the slot index, or -1 if none found.
        /// </summary>
        private int FindEmptyRF4SlotInRange(byte[] bytes, FileOffsets offsets, int startFace4, int endFace4)
        {
            for (int i = startFace4; i < endFace4; i++)
            {
                int rf4Offset = offsets.RoofFacesDataOff + i * RoofFace4Size;

                // Check if all 10 bytes are zero
                bool isEmpty = true;
                for (int j = 0; j < RoofFace4Size; j++)
                {
                    if (bytes[rf4Offset + j] != 0)
                    {
                        isEmpty = false;
                        break;
                    }
                }

                if (isEmpty)
                {
                    Debug.WriteLine($"[RoofFace4Adder] Found empty RF4 slot {i} in range [{startFace4}..{endFace4})");
                    return i;
                }
            }
            return -1;
        }

        private static ushort ReadU16(byte[] b, int off) => (ushort)(b[off] | (b[off + 1] << 8));

        private static void WriteU16(byte[] b, int off, ushort val)
        {
            b[off] = (byte)(val & 0xFF);
            b[off + 1] = (byte)((val >> 8) & 0xFF);
        }

        private static void WriteS16(byte[] b, int off, short val)
        {
            b[off] = (byte)(val & 0xFF);
            b[off + 1] = (byte)((val >> 8) & 0xFF);
        }

        private static void WriteU16ToStream(Stream s, ushort val)
        {
            s.WriteByte((byte)(val & 0xFF));
            s.WriteByte((byte)((val >> 8) & 0xFF));
        }

        #endregion
    }

    public enum PitchDirection
    {
        SlopesNorth,  // High at south edge, slopes down toward north (low Z)
        SlopesSouth,  // High at north edge, slopes down toward south (high Z)
        SlopesEast,   // High at west edge, slopes down toward east (high X)
        SlopesWest    // High at east edge, slopes down toward west (low X)
    }

    public sealed class RoofFace4Result
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public int ResultId { get; }

        private RoofFace4Result(bool success, string? error, int id)
        {
            IsSuccess = success;
            ErrorMessage = error;
            ResultId = id;
        }

        public static RoofFace4Result Success(int id) => new(true, null, id);
        public static RoofFace4Result Fail(string error) => new(false, error, 0);
    }
}