// /Services/BuildingDeleter.cs
// Helper class for deleting buildings with all cascading data updates
using System.Diagnostics;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;

namespace UrbanChaosMapEditor.Services.Buildings
{
    /// <summary>
    /// Handles the complex task of deleting a building and all related data:
    /// - Removes the DBuildingRec
    /// - Removes all facets owned by the building
    /// - Updates building IDs in remaining facets and walkables
    /// - Removes walkables owned by the building
    /// - Removes orphaned RoofFace4 entries
    /// - Updates facet ranges in remaining buildings
    /// - Updates header counters
    /// </summary>
    public sealed class BuildingDeleter
    {
        private const int HeaderSize = 48;
        private const int DBuildingSize = 24;
        private const int AfterBuildingsPad = 14;
        private const int DFacetSize = 26;
        private const int DWalkableSize = 22;
        private const int RoofFace4Size = 10;

        private readonly MapDataService _svc;

        public BuildingDeleter(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        /// <summary>
        /// Attempts to delete a building and all related data.
        /// Returns a result indicating success/failure and details.
        /// </summary>
        public DeleteBuildingResult TryDeleteBuilding(int buildingId1)
        {
            if (!_svc.IsLoaded)
                return DeleteBuildingResult.Fail("No map loaded.");

            // Get current snapshot to analyze
            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.Buildings == null || snap.Buildings.Length == 0)
                return DeleteBuildingResult.Fail("No buildings in map.");

            int buildingIndex = buildingId1 - 1; // 0-based array index
            if (buildingIndex < 0 || buildingIndex >= snap.Buildings.Length)
                return DeleteBuildingResult.Fail($"Building #{buildingId1} not found.");

            var building = snap.Buildings[buildingIndex];

            // Count facets to be deleted
            int facetsToDelete = 0;
            if (snap.Facets != null)
            {
                for (int i = 0; i < snap.Facets.Length; i++)
                {
                    if (snap.Facets[i].Building == buildingId1)
                        facetsToDelete++;
                }
            }

            // Count walkables to be deleted
            int walkablesToDelete = 0;
            var walkableIdsToDelete = new HashSet<int>();
            if (snap.Walkables != null)
            {
                for (int i = 1; i < snap.Walkables.Length; i++) // Skip sentinel at [0]
                {
                    if (snap.Walkables[i].Building == buildingId1)
                    {
                        walkablesToDelete++;
                        walkableIdsToDelete.Add(i); // 1-based ID
                    }
                }
            }

            // Identify RoofFace4 entries referenced by walkables being deleted
            var roofFace4IdsToDelete = new HashSet<int>();
            if (snap.Walkables != null && snap.RoofFaces4 != null)
            {
                foreach (var walkableId in walkableIdsToDelete)
                {
                    if (walkableId > 0 && walkableId < snap.Walkables.Length)
                    {
                        var w = snap.Walkables[walkableId];
                        // Add all RoofFace4 in range [StartFace4..EndFace4)
                        for (int rf = w.StartFace4; rf < w.EndFace4 && rf < snap.RoofFaces4.Length; rf++)
                        {
                            if (rf > 0) // Skip sentinel
                                roofFace4IdsToDelete.Add(rf);
                        }
                    }
                }
            }

            Debug.WriteLine($"[BuildingDeleter] Deleting building #{buildingId1}: " +
                          $"{facetsToDelete} facets, {walkablesToDelete} walkables, " +
                          $"{roofFace4IdsToDelete.Count} roofFace4 entries");

            // Clear any roof enclosure PAP_HI tiles BEFORE deleting the building
            // (we need the facets to still exist to detect the polygon)
            try
            {
                RoofEnclosureService.ClearRoofEnclosure(buildingId1);
                Debug.WriteLine($"[BuildingDeleter] Cleared roof enclosure for building #{buildingId1}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BuildingDeleter] Failed to clear roof enclosure: {ex.Message}");
                // Continue with deletion even if roof clearing fails
            }

            // Perform the actual deletion by rewriting the building block
            try
            {
                RewriteBuildingBlock(snap, buildingId1, walkableIdsToDelete, roofFace4IdsToDelete);

                // Notify change bus
                BuildingsChangeBus.Instance.NotifyBuildingChanged(buildingId1, BuildingChangeType.Removed);
                BuildingsChangeBus.Instance.NotifyChanged();

                return DeleteBuildingResult.Success(facetsToDelete, walkablesToDelete, roofFace4IdsToDelete.Count);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BuildingDeleter] Exception: {ex}");
                return DeleteBuildingResult.Fail($"Error during deletion: {ex.Message}");
            }
        }

        private void RewriteBuildingBlock(BuildingArrays snap, int deletedBuildingId1,
                                          HashSet<int> walkableIdsToDelete,
                                          HashSet<int> roofFace4IdsToDelete)
        {
            var bytes = _svc.GetBytesCopy();
            int blockStart = snap.StartOffset;

            // Read file header
            int saveType = BitConverter.ToInt32(bytes, 0);
            int objectBytesFromHeader = BitConverter.ToInt32(bytes, 4);

            // Read building block header counters
            ushort oldNextBuilding = ReadU16(bytes, blockStart + 2);
            ushort oldNextFacet = ReadU16(bytes, blockStart + 4);
            ushort oldNextStyle = ReadU16(bytes, blockStart + 6);
            ushort oldNextPaintMem = (saveType >= 17) ? ReadU16(bytes, blockStart + 8) : (ushort)0;
            ushort oldNextStorey = (saveType >= 17) ? ReadU16(bytes, blockStart + 10) : (ushort)0;

            // Calculate key offsets in the OLD file
            int buildingsOff = blockStart + HeaderSize;
            int facetsOff = buildingsOff + (oldNextBuilding - 1) * DBuildingSize + AfterBuildingsPad;
            int stylesOff = facetsOff + (oldNextFacet - 1) * DFacetSize;
            int paintOff = stylesOff + oldNextStyle * 2;
            int storeysOff = paintOff + ((saveType >= 17) ? oldNextPaintMem : 0);
            int indoorsOff = storeysOff + ((saveType >= 17) ? oldNextStorey * 6 : 0);

            // Calculate indoors section length
            int indoorsLen = 0;
            if (saveType >= 21 && indoorsOff + 8 <= bytes.Length)
            {
                ushort nextIS = ReadU16(bytes, indoorsOff);
                ushort nextISt = ReadU16(bytes, indoorsOff + 2);
                ushort nextIB = ReadU16(bytes, indoorsOff + 4);
                indoorsLen = 8 + nextIS * 22 + nextISt * 10 + nextIB;
            }

            int walkablesHeaderOff = indoorsOff + indoorsLen;
            ushort oldNextWalkable = ReadU16(bytes, walkablesHeaderOff);
            ushort oldNextRoofFace4 = ReadU16(bytes, walkablesHeaderOff + 2);
            int walkablesDataOff = walkablesHeaderOff + 4;
            int roofFacesDataOff = walkablesDataOff + oldNextWalkable * DWalkableSize;
            int oldBuildingBlockEnd = roofFacesDataOff + oldNextRoofFace4 * RoofFace4Size;

            // What comes AFTER the building block? Objects section + tail
            int afterBuildingBlock = bytes.Length - oldBuildingBlockEnd;

            // === Build new arrays ===
            var buildingIdMap = new Dictionary<int, int>();
            var facetIdMap = new Dictionary<int, int>();
            var newBuildings = new List<byte[]>();
            var newFacets = new List<byte[]>();

            // Rebuild Buildings
            int newBuildingId = 1;
            for (int oldId1 = 1; oldId1 < oldNextBuilding; oldId1++)
            {
                if (oldId1 == deletedBuildingId1) continue;

                int off = buildingsOff + (oldId1 - 1) * DBuildingSize;
                var bBytes = new byte[DBuildingSize];
                Buffer.BlockCopy(bytes, off, bBytes, 0, DBuildingSize);
                buildingIdMap[oldId1] = newBuildingId;
                newBuildings.Add(bBytes);
                newBuildingId++;
            }

            // Build facet ID map first
            int newFacetId = 1;
            for (int oldId1 = 1; oldId1 < oldNextFacet; oldId1++)
            {
                int off = facetsOff + (oldId1 - 1) * DFacetSize;
                ushort facetBuilding = ReadU16(bytes, off + 14);
                if (facetBuilding == deletedBuildingId1) continue;
                facetIdMap[oldId1] = newFacetId++;
            }

            // Rebuild Facets with updated building references
            for (int oldId1 = 1; oldId1 < oldNextFacet; oldId1++)
            {
                int off = facetsOff + (oldId1 - 1) * DFacetSize;
                ushort facetBuilding = ReadU16(bytes, off + 14);
                if (facetBuilding == deletedBuildingId1) continue;

                var fBytes = new byte[DFacetSize];
                Buffer.BlockCopy(bytes, off, fBytes, 0, DFacetSize);

                // Update building reference
                if (buildingIdMap.TryGetValue(facetBuilding, out int newBldId))
                {
                    fBytes[14] = (byte)(newBldId & 0xFF);
                    fBytes[15] = (byte)((newBldId >> 8) & 0xFF);
                }
                newFacets.Add(fBytes);
            }

            // Update building facet ranges
            for (int i = 0; i < newBuildings.Count; i++)
            {
                var b = newBuildings[i];
                ushort oldStart = (ushort)(b[0] | (b[1] << 8));
                ushort oldEnd = (ushort)(b[2] | (b[3] << 8));

                int newStart = 0, newEnd = 0;
                bool foundFirst = false;
                for (int oldFacetId = oldStart; oldFacetId < oldEnd; oldFacetId++)
                {
                    if (facetIdMap.TryGetValue(oldFacetId, out int nfid))
                    {
                        if (!foundFirst) { newStart = nfid; foundFirst = true; }
                        newEnd = nfid + 1;
                    }
                }
                if (!foundFirst) newStart = newEnd = newFacets.Count + 1;

                b[0] = (byte)(newStart & 0xFF);
                b[1] = (byte)((newStart >> 8) & 0xFF);
                b[2] = (byte)(newEnd & 0xFF);
                b[3] = (byte)((newEnd >> 8) & 0xFF);
            }

            // === Walkables and RoofFace4 ===
            var walkableIdMap = new Dictionary<int, int>();
            var newWalkables = new List<byte[]>();

            // Sentinel at index 0
            var sentinelW = new byte[DWalkableSize];
            if (oldNextWalkable > 0) Buffer.BlockCopy(bytes, walkablesDataOff, sentinelW, 0, DWalkableSize);
            newWalkables.Add(sentinelW);
            walkableIdMap[0] = 0;

            int newWalkableId = 1;
            for (int oldId = 1; oldId < oldNextWalkable; oldId++)
            {
                if (walkableIdsToDelete.Contains(oldId)) continue;

                int off = walkablesDataOff + oldId * DWalkableSize;
                var wBytes = new byte[DWalkableSize];
                Buffer.BlockCopy(bytes, off, wBytes, 0, DWalkableSize);

                // Update building reference at offset +20
                ushort wBuilding = (ushort)(wBytes[20] | (wBytes[21] << 8));
                if (buildingIdMap.TryGetValue(wBuilding, out int newBld))
                {
                    wBytes[20] = (byte)(newBld & 0xFF);
                    wBytes[21] = (byte)((newBld >> 8) & 0xFF);
                }

                walkableIdMap[oldId] = newWalkableId++;
                newWalkables.Add(wBytes);
            }

            var roofFaceIdMap = new Dictionary<int, int>();
            var newRoofFaces = new List<byte[]>();

            // Sentinel at index 0
            var sentinelR = new byte[RoofFace4Size];
            if (oldNextRoofFace4 > 0) Buffer.BlockCopy(bytes, roofFacesDataOff, sentinelR, 0, RoofFace4Size);
            newRoofFaces.Add(sentinelR);
            roofFaceIdMap[0] = 0;

            int newRoofFaceId = 1;
            for (int oldId = 1; oldId < oldNextRoofFace4; oldId++)
            {
                if (roofFace4IdsToDelete.Contains(oldId)) continue;

                int off = roofFacesDataOff + oldId * RoofFace4Size;
                var rfBytes = new byte[RoofFace4Size];
                Buffer.BlockCopy(bytes, off, rfBytes, 0, RoofFace4Size);
                roofFaceIdMap[oldId] = newRoofFaceId++;
                newRoofFaces.Add(rfBytes);
            }

            // Update walkable RoofFace4 ranges (StartFace4/EndFace4 at +8/+10)
            foreach (var wBytes in newWalkables)
            {
                ushort startF4 = (ushort)(wBytes[8] | (wBytes[9] << 8));
                ushort endF4 = (ushort)(wBytes[10] | (wBytes[11] << 8));
                int ns = 0, ne = 0; bool ff = false;
                for (int old = startF4; old < endF4; old++)
                {
                    if (roofFaceIdMap.TryGetValue(old, out int nrf))
                    {
                        if (!ff) { ns = nrf; ff = true; }
                        ne = nrf + 1;
                    }
                }
                wBytes[8] = (byte)(ns & 0xFF); wBytes[9] = (byte)((ns >> 8) & 0xFF);
                wBytes[10] = (byte)(ne & 0xFF); wBytes[11] = (byte)((ne >> 8) & 0xFF);
            }

            // Update building Walkable references (at +16)
            foreach (var b in newBuildings)
            {
                ushort oldW = (ushort)(b[16] | (b[17] << 8));
                if (oldW > 0 && walkableIdMap.TryGetValue(oldW, out int nw))
                {
                    b[16] = (byte)(nw & 0xFF);
                    b[17] = (byte)((nw >> 8) & 0xFF);
                }
                else if (oldW > 0)
                {
                    b[16] = 0; b[17] = 0; // Deleted walkable
                }
            }

            // === Assemble new file ===
            // Structure: [Header 8B][Tiles 98304B][Building Block][Objects+Tail]

            using var ms = new System.IO.MemoryStream();

            // 1. Copy file header (8 bytes) - we'll update objectBytesFromHeader if needed
            ms.Write(bytes, 0, 8);

            // 2. Copy tiles (unchanged)
            ms.Write(bytes, 8, blockStart - 8);

            // 3. Write building block header (48 bytes)
            var header = new byte[HeaderSize];
            Buffer.BlockCopy(bytes, blockStart, header, 0, HeaderSize);
            // Update counters
            WriteU16(header, 2, (ushort)(newBuildings.Count + 1));  // NextDBuilding
            WriteU16(header, 4, (ushort)(newFacets.Count + 1));     // NextDFacet
            // Keep styles/paint/storeys unchanged
            ms.Write(header, 0, HeaderSize);

            // 4. Write buildings
            foreach (var b in newBuildings) ms.Write(b, 0, b.Length);

            // 5. Write pad
            ms.Write(new byte[AfterBuildingsPad], 0, AfterBuildingsPad);

            // 6. Write facets
            foreach (var f in newFacets) ms.Write(f, 0, f.Length);

            // 7. Copy styles (unchanged)
            int stylesLen = oldNextStyle * 2;
            if (stylesLen > 0) ms.Write(bytes, stylesOff, stylesLen);

            // 8. Copy paint (unchanged)
            int paintLen = (saveType >= 17) ? oldNextPaintMem : 0;
            if (paintLen > 0) ms.Write(bytes, paintOff, paintLen);

            // 9. Copy storeys (unchanged)
            int storeysLen = (saveType >= 17) ? oldNextStorey * 6 : 0;
            if (storeysLen > 0) ms.Write(bytes, storeysOff, storeysLen);

            // 10. Copy indoors (unchanged)
            if (indoorsLen > 0) ms.Write(bytes, indoorsOff, indoorsLen);

            // 11. Write walkables header
            ms.WriteByte((byte)(newWalkables.Count & 0xFF));
            ms.WriteByte((byte)((newWalkables.Count >> 8) & 0xFF));
            ms.WriteByte((byte)(newRoofFaces.Count & 0xFF));
            ms.WriteByte((byte)((newRoofFaces.Count >> 8) & 0xFF));

            // 12. Write walkables
            foreach (var w in newWalkables) ms.Write(w, 0, w.Length);

            // 13. Write rooffaces
            foreach (var rf in newRoofFaces) ms.Write(rf, 0, rf.Length);

            // 14. Copy everything after the building block (objects section + tail)
            if (afterBuildingBlock > 0)
                ms.Write(bytes, oldBuildingBlockEnd, afterBuildingBlock);

            var newBytes = ms.ToArray();

            // The objectBytesFromHeader stays the same since objects section is unchanged
            // File length changed, but the formula accounts for that via bytes.Length

            Debug.WriteLine($"[BuildingDeleter] Old file: {bytes.Length} bytes, new file: {newBytes.Length} bytes");
            Debug.WriteLine($"[BuildingDeleter] Buildings: {oldNextBuilding - 1} -> {newBuildings.Count}, " +
                           $"Facets: {oldNextFacet - 1} -> {newFacets.Count}");

            // Replace the map bytes
            _svc.ReplaceBytes(newBytes);
        }

        private static void WriteU16(byte[] b, int off, ushort val)
        {
            b[off] = (byte)(val & 0xFF);
            b[off + 1] = (byte)((val >> 8) & 0xFF);
        }

        private static ushort ReadU16(byte[] b, int off) =>
            (ushort)(b[off] | (b[off + 1] << 8));
    }

    /// <summary>Result of a building deletion attempt.</summary>
    public sealed class DeleteBuildingResult
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public int FacetsDeleted { get; }
        public int WalkablesDeleted { get; }
        public int RoofFacesDeleted { get; }

        private DeleteBuildingResult(bool success, string? error, int facets, int walkables, int roofFaces)
        {
            IsSuccess = success;
            ErrorMessage = error;
            FacetsDeleted = facets;
            WalkablesDeleted = walkables;
            RoofFacesDeleted = roofFaces;
        }

        public static DeleteBuildingResult Success(int facets, int walkables, int roofFaces) =>
            new(true, null, facets, walkables, roofFaces);

        public static DeleteBuildingResult Fail(string error) =>
            new(false, error, 0, 0, 0);
    }
}