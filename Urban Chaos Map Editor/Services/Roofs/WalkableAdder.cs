// /Services/Buildings/WalkableAdder.cs
using System.Diagnostics;
using System.IO;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;

namespace UrbanChaosMapEditor.Services.Roofs
{
    /// <summary>
    /// Result of adding a walkable.
    /// </summary>
    public sealed class AddWalkableResult
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
        public int WalkableId1 { get; init; }

        public static AddWalkableResult Ok(int walkableId1) => new() { Success = true, WalkableId1 = walkableId1 };
        public static AddWalkableResult Fail(string error) => new() { Success = false, Error = error };
    }

    /// <summary>
    /// Parameters for creating a walkable.
    /// </summary>
    public sealed class WalkableTemplate
    {
        public int BuildingId1 { get; init; }
        public byte X1 { get; init; }
        public byte Z1 { get; init; }
        public byte X2 { get; init; }
        public byte Z2 { get; init; }
        public int WorldY { get; init; }
        public byte StoreyY { get; init; }
    }

    /// <summary>
    /// Adds walkable regions (DWalkable entries) to buildings.
    /// Walkables define roof surfaces that can be walked on and grabbed.
    /// </summary>
    public sealed class WalkableAdder
    {
        private const int HeaderSize = BuildingFormatConstants.HeaderSize;     // Buildings region header
        private const int DWalkableSize = BuildingFormatConstants.DWalkableSize;
        private const int RoofFace4Size = BuildingFormatConstants.RoofFace4Size;
        private const int DBuildingSize = BuildingFormatConstants.DBuildingSize;

        private readonly MapDataService _svc;

        public WalkableAdder(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        /// <summary>
        /// Add a new walkable to a building.
        /// Automatically reuses empty (soft-deleted) slots if available,
        /// otherwise appends to end of walkable list.
        /// </summary>
        public AddWalkableResult TryAddWalkable(WalkableTemplate template)
        {
            if (!_svc.IsLoaded)
                return AddWalkableResult.Fail("No map loaded.");

            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null)
                return AddWalkableResult.Fail("Failed to read buildings.");

            if (snap.WalkablesStart < 0)
                return AddWalkableResult.Fail("Walkables section not found.");

            if (template.BuildingId1 < 1 || template.BuildingId1 >= snap.NextDBuilding)
                return AddWalkableResult.Fail($"Invalid building ID: {template.BuildingId1}");

            // First, try to find an empty slot to reuse
            int emptySlot = FindEmptyWalkableSlot();
            if (emptySlot > 0)
            {
                Debug.WriteLine($"[WalkableAdder] Found empty slot {emptySlot}, reusing it instead of appending");
                return TryReuseWalkableSlot(emptySlot, template);
            }

            // No empty slot found, append to end
            var bytes = _svc.GetBytesCopy();

            // Read walkables header
            int walkablesHeaderOff = snap.WalkablesStart;
            ushort oldNextWalkable = ReadU16(bytes, walkablesHeaderOff);
            ushort oldNextRoofFace4 = ReadU16(bytes, walkablesHeaderOff + 2);

            // Calculate offsets
            int walkablesDataOff = walkablesHeaderOff + 4;
            int roofFacesDataOff = walkablesDataOff + oldNextWalkable * DWalkableSize;
            int walkablesSectionEnd = roofFacesDataOff + oldNextRoofFace4 * RoofFace4Size;

            // New walkable ID
            int newWalkableId1 = oldNextWalkable;

            // Get the building's current walkable head (for chaining)
            int buildingOff = snap.StartOffset + HeaderSize + (template.BuildingId1 - 1) * DBuildingSize;
            ushort oldWalkableHead = ReadU16(bytes, buildingOff + 4);

            // Calculate Y value: worldY >> 5
            byte walkableY = (byte)Math.Clamp(template.WorldY >> 5, 0, 255);

            Debug.WriteLine($"[WalkableAdder] Appending new walkable: ID={newWalkableId1}, Building={template.BuildingId1}, " +
                           $"Rect=({template.X1},{template.Z1})->({template.X2},{template.Z2}), " +
                           $"WorldY={template.WorldY}, Y={walkableY}, Next={oldWalkableHead}");

            // Create new DWalkable (22 bytes)
            var newWalkable = new byte[DWalkableSize];

            WriteU16(newWalkable, 0, 0);  // StartPoint
            WriteU16(newWalkable, 2, 0);  // EndPoint
            WriteU16(newWalkable, 4, 0);  // StartFace3
            WriteU16(newWalkable, 6, 0);  // EndFace3
            WriteU16(newWalkable, 8, oldNextRoofFace4);   // StartFace4 (empty range)
            WriteU16(newWalkable, 10, oldNextRoofFace4); // EndFace4 (same = empty)

            newWalkable[12] = template.X1;
            newWalkable[13] = template.Z1;
            newWalkable[14] = template.X2;
            newWalkable[15] = template.Z2;
            newWalkable[16] = walkableY;
            newWalkable[17] = template.StoreyY;

            WriteU16(newWalkable, 18, oldWalkableHead); // Next = old head
            WriteU16(newWalkable, 20, (ushort)template.BuildingId1);

            // Build new file
            using var ms = new MemoryStream();

            // 1. Copy everything up to walkables data
            ms.Write(bytes, 0, walkablesDataOff);

            // 2. Write existing walkables
            if (oldNextWalkable > 0)
                ms.Write(bytes, walkablesDataOff, oldNextWalkable * DWalkableSize);

            // 3. Write new walkable
            ms.Write(newWalkable, 0, DWalkableSize);

            // 4. Write existing RoofFace4 entries and everything after
            int afterWalkablesLen = bytes.Length - roofFacesDataOff;
            ms.Write(bytes, roofFacesDataOff, afterWalkablesLen);

            var newBytes = ms.ToArray();

            // Update walkables header
            WriteU16(newBytes, walkablesHeaderOff, (ushort)(oldNextWalkable + 1));

            // Update building's Walkable pointer to new walkable
            WriteU16(newBytes, buildingOff + 4, (ushort)newWalkableId1);

            // Apply changes
            _svc.ReplaceBytes(newBytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[WalkableAdder] Successfully added walkable {newWalkableId1}");

            return AddWalkableResult.Ok(newWalkableId1);
        }

        /// <summary>
        /// Reuse an existing (soft-deleted) walkable slot instead of appending.
        /// This writes data into an existing slot, preserving the walkable ID.
        /// If the slot has preserved RF4 range and Next pointer from soft-deletion, they are reused.
        /// </summary>
        public AddWalkableResult TryReuseWalkableSlot(int existingSlotId1, WalkableTemplate template)
        {
            if (!_svc.IsLoaded)
                return AddWalkableResult.Fail("No map loaded.");

            if (existingSlotId1 < 1)
                return AddWalkableResult.Fail("Invalid slot ID.");

            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null)
                return AddWalkableResult.Fail("Failed to read buildings.");

            if (snap.WalkablesStart < 0)
                return AddWalkableResult.Fail("Walkables section not found.");

            if (template.BuildingId1 < 1 || template.BuildingId1 >= snap.NextDBuilding)
                return AddWalkableResult.Fail($"Invalid building ID: {template.BuildingId1}");

            if (snap.Walkables == null || existingSlotId1 >= snap.Walkables.Length)
                return AddWalkableResult.Fail($"Slot {existingSlotId1} does not exist.");

            var bytes = _svc.GetBytesCopy();

            // Read walkables header
            int walkablesHeaderOff = snap.WalkablesStart;
            ushort nextWalkable = ReadU16(bytes, walkablesHeaderOff);
            ushort nextRoofFace4 = ReadU16(bytes, walkablesHeaderOff + 2);

            // Calculate slot offset
            int walkablesDataOff = walkablesHeaderOff + 4;
            int slotOffset = walkablesDataOff + existingSlotId1 * DWalkableSize;

            // Read preserved values from soft-deleted slot
            ushort existingStartFace4 = ReadU16(bytes, slotOffset + 8);
            ushort existingEndFace4 = ReadU16(bytes, slotOffset + 10);
            ushort existingNext = ReadU16(bytes, slotOffset + 18);
            ushort existingBuilding = ReadU16(bytes, slotOffset + 20);

            bool hasPreservedRF4Range = (existingEndFace4 > existingStartFace4);
            bool hasPreservedChain = (existingNext > 0 || existingBuilding > 0);

            // Calculate Y value: worldY >> 5
            byte walkableY = (byte)Math.Clamp(template.WorldY >> 5, 0, 255);

            Debug.WriteLine($"[WalkableAdder] Reusing walkable slot {existingSlotId1}");
            Debug.WriteLine($"[WalkableAdder]   Preserved: RF4[{existingStartFace4}..{existingEndFace4}), Next={existingNext}, Building={existingBuilding}");
            Debug.WriteLine($"[WalkableAdder]   New: Building={template.BuildingId1}, " +
                           $"Rect=({template.X1},{template.Z1})->({template.X2},{template.Z2}), " +
                           $"WorldY={template.WorldY}, Y={walkableY}");

            // Write walkable data directly into the existing slot
            WriteU16(bytes, slotOffset + 0, 0);  // StartPoint
            WriteU16(bytes, slotOffset + 2, 0);  // EndPoint
            WriteU16(bytes, slotOffset + 4, 0);  // StartFace3
            WriteU16(bytes, slotOffset + 6, 0);  // EndFace3

            // Use preserved RF4 range if available
            if (hasPreservedRF4Range)
            {
                WriteU16(bytes, slotOffset + 8, existingStartFace4);
                WriteU16(bytes, slotOffset + 10, existingEndFace4);
            }
            else
            {
                WriteU16(bytes, slotOffset + 8, nextRoofFace4);
                WriteU16(bytes, slotOffset + 10, nextRoofFace4);
            }

            bytes[slotOffset + 12] = template.X1;
            bytes[slotOffset + 13] = template.Z1;
            bytes[slotOffset + 14] = template.X2;
            bytes[slotOffset + 15] = template.Z2;
            bytes[slotOffset + 16] = walkableY;
            bytes[slotOffset + 17] = template.StoreyY;

            // CRITICAL: Preserve the Next pointer to maintain chain integrity!
            // The chain was set up when the original walkable was created.
            // Soft-delete preserved it, so we keep it.
            WriteU16(bytes, slotOffset + 18, existingNext);

            // Use the template's building ID (should match preserved, but allow override)
            WriteU16(bytes, slotOffset + 20, (ushort)template.BuildingId1);

            // DO NOT update Building.Walkable - the chain is already intact!
            // The building already points to the head of the chain, which eventually
            // leads to this slot via Next pointers.

            // Apply changes (no file restructuring needed!)
            _svc.ReplaceBytes(bytes);
            _svc.MarkDirty();

            Debug.WriteLine($"[WalkableAdder] Successfully reused walkable slot {existingSlotId1} (chain preserved)");

            return AddWalkableResult.Ok(existingSlotId1);
        }

        /// <summary>
        /// Find a soft-deleted walkable slot that can be reused.
        /// Returns 0 if no empty slots found.
        /// A soft-deleted slot has bounds zeroed (X1=Z1=X2=Z2=Y=0) but may have:
        /// - Preserved RF4 range (StartFace4/EndFace4)
        /// - Preserved chain info (Next pointer, Building ID)
        /// </summary>
        public int FindEmptyWalkableSlot()
        {
            if (!_svc.IsLoaded) return 0;

            var acc = new BuildingsAccessor(_svc);
            if (!acc.TryGetWalkables(out var walkables, out _))
                return 0;

            for (int i = 1; i < walkables.Length; i++)
            {
                var w = walkables[i];
                // A soft-deleted walkable has bounds and Y zeroed
                // But may still have RF4 range, Next, and Building preserved
                if (w.X1 == 0 && w.Z1 == 0 && w.X2 == 0 && w.Z2 == 0 && w.Y == 0)
                {
                    Debug.WriteLine($"[WalkableAdder] Found empty slot {i}: RF4[{w.StartFace4}..{w.EndFace4}), Next={w.Next}, Building={w.Building}");
                    return i;
                }
            }
            return 0;
        }

        /// <summary>
        /// Finds an existing active walkable for the given building with matching bounds
        /// and updates its Y and StoreyY fields in-place.
        /// Returns true if a match was found and updated.
        /// </summary>
        public bool TryUpdateExistingWalkableY(int buildingId1, byte x1, byte z1, byte x2, byte z2, int worldY, byte storeyY)
        {
            if (!_svc.IsLoaded) return false;

            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.Walkables == null || snap.WalkablesStart < 0) return false;

            for (int i = 1; i < snap.Walkables.Length; i++)
            {
                var w = snap.Walkables[i];
                if (w.Building == buildingId1 && w.X1 == x1 && w.Z1 == z1 && w.X2 == x2 && w.Z2 == z2)
                {
                    byte walkableY = (byte)Math.Clamp(worldY >> 5, 0, 255);

                    var bytes = _svc.GetBytesCopy();
                    int walkablesDataOff = snap.WalkablesStart + 4;
                    int slotOffset = walkablesDataOff + i * DWalkableSize;
                    bytes[slotOffset + 16] = walkableY;
                    bytes[slotOffset + 17] = storeyY;

                    _svc.ReplaceBytes(bytes);
                    _svc.MarkDirty();

                    Debug.WriteLine($"[WalkableAdder] Updated walkable #{i} in-place: WorldY={worldY} -> Y={walkableY}, StoreyY={storeyY}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Convert world Y to walkable Y field (worldY >> 5).
        /// </summary>
        public static byte WorldYToWalkableY(int worldY)
            => (byte)Math.Clamp(worldY >> 5, 0, 255);

        /// <summary>
        /// Convert walkable Y to world Y (Y << 5).
        /// </summary>
        public static int WalkableYToWorldY(byte y)
            => y << 5;

        private static ushort ReadU16(byte[] b, int off)
            => (ushort)(b[off] | (b[off + 1] << 8));

        private static void WriteU16(byte[] b, int off, ushort v)
        {
            b[off] = (byte)(v & 0xFF);
            b[off + 1] = (byte)((v >> 8) & 0xFF);
        }
    }
}