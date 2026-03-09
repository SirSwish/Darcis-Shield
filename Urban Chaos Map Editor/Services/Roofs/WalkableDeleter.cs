// /Services/Buildings/WalkableDeleter.cs
using System.Diagnostics;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;

namespace UrbanChaosMapEditor.Services.Roofs
{
    /// <summary>
    /// Result of deleting a walkable.
    /// </summary>
    public sealed class DeleteWalkableResult
    {
        public bool Success { get; }
        public string? Error { get; }
        public int RoofFacesDeleted { get; }

        private DeleteWalkableResult(bool success, string? error, int roofFacesDeleted)
        {
            Success = success;
            Error = error;
            RoofFacesDeleted = roofFacesDeleted;
        }

        public static DeleteWalkableResult Succeeded(int roofFacesDeleted = 0)
            => new(true, null, roofFacesDeleted);
        public static DeleteWalkableResult Failed(string error)
            => new(false, error, 0);
    }

    /// <summary>
    /// Deletes individual walkable regions from buildings.
    /// </summary>
    public sealed class WalkableDeleter
    {
        private readonly MapDataService _svc;

        private const int HeaderSize = 48;
        private const int DWalkableSize = 22;
        private const int RoofFace4Size = 10;
        private const int DBuildingSize = 24;

        public WalkableDeleter(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        /// <summary>
        /// Delete a walkable by its 1-based ID.
        /// By default uses soft delete (zeros the entry, preserves IDs).
        /// Set softDelete=false to physically remove and shift IDs (not recommended).
        /// </summary>
        public DeleteWalkableResult DeleteWalkable(int walkableId1, bool softDelete = true)
        {
            if (!_svc.IsLoaded)
                return DeleteWalkableResult.Failed("No map loaded.");

            if (walkableId1 < 1)
                return DeleteWalkableResult.Failed("Invalid walkable ID.");

            var acc = new BuildingsAccessor(_svc);

            if (!acc.TryGetWalkables(out var walkables, out var roofFaces))
                return DeleteWalkableResult.Failed("Failed to read walkables.");

            if (walkableId1 >= walkables.Length)
                return DeleteWalkableResult.Failed($"Walkable {walkableId1} does not exist.");

            var snap = acc.ReadSnapshot();
            if (snap.WalkablesStart < 0)
                return DeleteWalkableResult.Failed("Walkables section not found.");

            var walkable = walkables[walkableId1];
            int roofFacesToDelete = Math.Max(0, walkable.EndFace4 - walkable.StartFace4);

            Debug.WriteLine($"[WalkableDeleter] Deleting walkable {walkableId1} (softDelete={softDelete}): Building={walkable.Building}, " +
                          $"Rect=({walkable.X1},{walkable.Z1})->({walkable.X2},{walkable.Z2}), " +
                          $"RoofFaces={walkable.StartFace4}-{walkable.EndFace4}");

            try
            {
                if (softDelete)
                {
                    SoftDeleteWalkable(snap, walkableId1, walkables);
                }
                else
                {
                    RewriteWithoutWalkable(snap, walkableId1, walkables, roofFaces);
                }
            }
            catch (Exception ex)
            {
                return DeleteWalkableResult.Failed($"Delete failed: {ex.Message}");
            }

            _svc.MarkDirty();
            Debug.WriteLine($"[WalkableDeleter] Successfully deleted walkable {walkableId1}");

            return DeleteWalkableResult.Succeeded(softDelete ? 0 : roofFacesToDelete);
        }

        /// <summary>
        /// Soft delete: Zero out the walkable entry AND its RF4 entries but keep slots in place.
        /// This preserves walkable AND RF4 IDs - no shifting occurs.
        /// IMPORTANT: We do NOT modify Next chain pointers in OTHER walkables, because
        /// when the slot is reused, the chain should remain intact.
        /// </summary>
        private void SoftDeleteWalkable(BuildingArrays snap, int walkableId1, DWalkableRec[] walkables)
        {
            var bytes = _svc.GetBytesCopy();
            var walkableToDelete = walkables[walkableId1];

            int walkablesOff = snap.WalkablesStart;
            ushort nextWalkable = ReadU16(bytes, walkablesOff);
            ushort nextRoofFace4 = ReadU16(bytes, walkablesOff + 2);

            int walkablesDataOff = walkablesOff + 4;
            int walkableOffset = walkablesDataOff + walkableId1 * DWalkableSize;

            // Calculate RF4 data offset
            int roofFacesDataOff = walkablesDataOff + nextWalkable * DWalkableSize;

            Debug.WriteLine($"[WalkableDeleter] Soft deleting walkable {walkableId1} at offset 0x{walkableOffset:X}");

            // Soft-delete associated RF4 entries (zero them out but keep slots)
            int rf4Start = walkableToDelete.StartFace4;
            int rf4End = walkableToDelete.EndFace4;
            int rf4Count = rf4End - rf4Start;

            if (rf4Count > 0)
            {
                Debug.WriteLine($"[WalkableDeleter] Soft deleting {rf4Count} RF4 entries [{rf4Start}..{rf4End})");
                for (int i = rf4Start; i < rf4End; i++)
                {
                    int rf4Offset = roofFacesDataOff + i * RoofFace4Size;
                    // Zero out the 10-byte RF4 entry
                    for (int j = 0; j < RoofFace4Size; j++)
                    {
                        bytes[rf4Offset + j] = 0;
                    }
                }
            }

            // Zero out most of the 22-byte walkable entry, BUT PRESERVE:
            // - StartFace4/EndFace4 (so RF4 slots can be reused)
            // - Next pointer (so chain remains intact for slot reuse)
            // - Building ID (so we know which building this slot belonged to)
            ushort preservedStartFace4 = ReadU16(bytes, walkableOffset + 8);
            ushort preservedEndFace4 = ReadU16(bytes, walkableOffset + 10);
            ushort preservedNext = ReadU16(bytes, walkableOffset + 18);
            ushort preservedBuilding = ReadU16(bytes, walkableOffset + 20);

            // Zero the entry
            for (int i = 0; i < DWalkableSize; i++)
            {
                bytes[walkableOffset + i] = 0;
            }

            // Restore preserved fields
            WriteU16(bytes, walkableOffset + 8, preservedStartFace4);
            WriteU16(bytes, walkableOffset + 10, preservedEndFace4);
            WriteU16(bytes, walkableOffset + 18, preservedNext);
            WriteU16(bytes, walkableOffset + 20, preservedBuilding);

            Debug.WriteLine($"[WalkableDeleter] Preserved: RF4 range [{preservedStartFace4}..{preservedEndFace4}), Next={preservedNext}, Building={preservedBuilding}");

            // DO NOT update Next pointers in other walkables - keep chain intact!
            // DO NOT update Building.Walkable pointer - the slot will be reused with same ID

            _svc.ReplaceBytes(bytes);
        }

        /// <summary>
        /// Find a walkable at the given tile coordinates.
        /// Returns 1-based ID or 0 if none found.
        /// </summary>
        public int FindWalkableAtTile(int tileX, int tileZ)
        {
            var acc = new BuildingsAccessor(_svc);
            if (!acc.TryGetWalkables(out var walkables, out _))
                return 0;

            for (int i = 1; i < walkables.Length; i++)
            {
                var w = walkables[i];
                // Skip zeroed-out (soft-deleted) walkables
                if (w.X1 == 0 && w.Z1 == 0 && w.X2 == 0 && w.Z2 == 0 && w.Building == 0)
                    continue;

                int minX = Math.Min(w.X1, w.X2);
                int maxX = Math.Max(w.X1, w.X2);
                int minZ = Math.Min(w.Z1, w.Z2);
                int maxZ = Math.Max(w.Z1, w.Z2);

                if (tileX >= minX && tileX <= maxX && tileZ >= minZ && tileZ <= maxZ)
                    return i;
            }
            return 0;
        }

        private void RewriteWithoutWalkable(BuildingArrays snap, int walkableIdToDelete,
            DWalkableRec[] walkables, RoofFace4Rec[] roofFaces)
        {
            var bytes = _svc.GetBytesCopy();
            var walkableToDelete = walkables[walkableIdToDelete];

            int walkablesOff = snap.WalkablesStart;
            ushort oldNextWalkable = ReadU16(bytes, walkablesOff);
            ushort oldNextRoofFace4 = ReadU16(bytes, walkablesOff + 2);

            int oldWalkablesDataOff = walkablesOff + 4;
            int oldRoofFacesDataOff = oldWalkablesDataOff + oldNextWalkable * DWalkableSize;
            int oldChunkEnd = oldRoofFacesDataOff + oldNextRoofFace4 * RoofFace4Size;

            // Build ID mapping for walkables
            var walkableIdMap = new Dictionary<int, int> { [0] = 0 };
            int newWalkableId = 1;
            for (int oldId = 1; oldId < oldNextWalkable; oldId++)
            {
                if (oldId == walkableIdToDelete)
                {
                    walkableIdMap[oldId] = 0;
                    continue;
                }
                walkableIdMap[oldId] = newWalkableId++;
            }

            // Build ID mapping for roof faces
            var roofFaceIdMap = new Dictionary<int, int> { [0] = 0 };
            int deletedRoofStart = walkableToDelete.StartFace4;
            int deletedRoofEnd = walkableToDelete.EndFace4;
            int roofFacesDeleted = Math.Max(0, deletedRoofEnd - deletedRoofStart);

            int newRoofFaceId = 1;
            for (int oldId = 1; oldId < oldNextRoofFace4; oldId++)
            {
                if (oldId >= deletedRoofStart && oldId < deletedRoofEnd)
                {
                    roofFaceIdMap[oldId] = 0;
                    continue;
                }
                roofFaceIdMap[oldId] = newRoofFaceId++;
            }

            // Calculate new sizes
            ushort newNextWalkable = (ushort)(oldNextWalkable - 1);
            ushort newNextRoofFace4 = (ushort)(oldNextRoofFace4 - roofFacesDeleted);

            int newWalkablesDataSize = newNextWalkable * DWalkableSize;
            int newRoofFacesDataSize = newNextRoofFace4 * RoofFace4Size;
            int newChunkSize = 4 + newWalkablesDataSize + newRoofFacesDataSize;
            int oldChunkSize = oldChunkEnd - walkablesOff;
            int sizeDelta = newChunkSize - oldChunkSize;

            // Create new buffer
            var newBytes = new byte[bytes.Length + sizeDelta];

            // Copy everything before walkables chunk
            Buffer.BlockCopy(bytes, 0, newBytes, 0, walkablesOff);

            // Write new header
            WriteU16(newBytes, walkablesOff, newNextWalkable);
            WriteU16(newBytes, walkablesOff + 2, newNextRoofFace4);

            // Write walkables
            int destOff = walkablesOff + 4;

            // Sentinel at [0]
            Buffer.BlockCopy(bytes, oldWalkablesDataOff, newBytes, destOff, DWalkableSize);
            destOff += DWalkableSize;

            for (int oldId = 1; oldId < oldNextWalkable; oldId++)
            {
                if (oldId == walkableIdToDelete) continue;

                int srcOff = oldWalkablesDataOff + oldId * DWalkableSize;
                Buffer.BlockCopy(bytes, srcOff, newBytes, destOff, DWalkableSize);

                // Remap StartFace4 and EndFace4
                ushort oldStart = ReadU16(newBytes, destOff + 8);
                ushort oldEnd = ReadU16(newBytes, destOff + 10);

                int newStart = oldStart > deletedRoofStart ? oldStart - roofFacesDeleted : oldStart;
                int newEnd = oldEnd > deletedRoofStart ? oldEnd - roofFacesDeleted : oldEnd;

                WriteU16(newBytes, destOff + 8, (ushort)newStart);
                WriteU16(newBytes, destOff + 10, (ushort)newEnd);

                // Remap Next pointer
                ushort oldNext = ReadU16(newBytes, destOff + 18);
                ushort newNext = walkableIdMap.TryGetValue(oldNext, out var mapped) ? (ushort)mapped : (ushort)0;
                WriteU16(newBytes, destOff + 18, newNext);

                destOff += DWalkableSize;
            }

            // Write roof faces
            // Sentinel at [0]
            Buffer.BlockCopy(bytes, oldRoofFacesDataOff, newBytes, destOff, RoofFace4Size);
            destOff += RoofFace4Size;

            for (int oldId = 1; oldId < oldNextRoofFace4; oldId++)
            {
                if (oldId >= deletedRoofStart && oldId < deletedRoofEnd) continue;

                int srcOff = oldRoofFacesDataOff + oldId * RoofFace4Size;
                Buffer.BlockCopy(bytes, srcOff, newBytes, destOff, RoofFace4Size);
                destOff += RoofFace4Size;
            }

            // Copy everything after old walkables chunk
            int newChunkEnd = walkablesOff + newChunkSize;
            int remainingBytes = bytes.Length - oldChunkEnd;
            if (remainingBytes > 0)
            {
                Buffer.BlockCopy(bytes, oldChunkEnd, newBytes, newChunkEnd, remainingBytes);
            }

            // Update building Walkable pointers
            UpdateBuildingWalkablePointers(newBytes, snap, walkableIdMap);

            // Replace bytes
            _svc.ReplaceBytes(newBytes);
        }

        private void UpdateBuildingWalkablePointers(byte[] bytes, BuildingArrays snap, Dictionary<int, int> walkableIdMap)
        {
            int buildingsOff = snap.StartOffset + HeaderSize;
            ushort buildingCount = snap.NextDBuilding;

            for (int i = 1; i < buildingCount; i++)
            {
                int off = buildingsOff + (i - 1) * DBuildingSize + 16; // Walkable field at offset 16
                ushort oldWalkable = ReadU16(bytes, off);

                if (walkableIdMap.TryGetValue(oldWalkable, out var newWalkable))
                {
                    if (newWalkable != oldWalkable)
                    {
                        Debug.WriteLine($"[WalkableDeleter] Building {i}: Walkable {oldWalkable} -> {newWalkable}");
                        WriteU16(bytes, off, (ushort)newWalkable);
                    }
                }
            }
        }

        private static ushort ReadU16(byte[] b, int off)
            => (ushort)(b[off] | (b[off + 1] << 8));

        private static void WriteU16(byte[] b, int off, ushort v)
        {
            b[off] = (byte)(v & 0xFF);
            b[off + 1] = (byte)((v >> 8) & 0xFF);
        }
    }
}