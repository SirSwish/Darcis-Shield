// /Services/FacetDeleter.cs
// Helper class for deleting individual facets from buildings
using System.Diagnostics;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Buildings
{
    /// <summary>
    /// Handles deleting a single facet from a building:
    /// - Removes the DFacetRec (26 bytes)
    /// - Updates the owning building's facet range
    /// - Shifts facet ranges for buildings that come after
    /// - Updates NextDFacet in the header
    /// - Leaves orphaned painted styles in place
    /// </summary>
    public sealed class FacetDeleter
    {
        private const int HeaderSize = 48;
        private const int DBuildingSize = 24;
        private const int AfterBuildingsPad = 14;
        private const int DFacetSize = 26;

        private readonly MapDataService _svc;

        public FacetDeleter(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        /// <summary>
        /// Attempts to delete a facet.
        /// Returns a result indicating success/failure.
        /// </summary>
        public DeleteFacetResult TryDeleteFacet(int facetId1)
        {
            if (!_svc.IsLoaded)
                return DeleteFacetResult.Fail("No map loaded.");

            var acc = new BuildingsAccessor(_svc);
            var snap = acc.ReadSnapshot();

            if (snap.Facets == null || snap.Facets.Length == 0)
                return DeleteFacetResult.Fail("No facets in map.");

            int facetIndex = facetId1 - 1; // 0-based array index
            if (facetIndex < 0 || facetIndex >= snap.Facets.Length)
                return DeleteFacetResult.Fail($"Facet #{facetId1} not found.");

            var facet = snap.Facets[facetIndex];
            int owningBuildingId = facet.Building;

            Debug.WriteLine($"[FacetDeleter] Deleting facet #{facetId1} from building #{owningBuildingId}");

            try
            {
                RewriteWithoutFacet(snap, facetId1);

                // Notify change bus
                BuildingsChangeBus.Instance.NotifyFacetChanged(facetId1);
                BuildingsChangeBus.Instance.NotifyChanged();

                return DeleteFacetResult.Success(owningBuildingId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FacetDeleter] Exception: {ex}");
                return DeleteFacetResult.Fail($"Error during deletion: {ex.Message}");
            }
        }

        private void RewriteWithoutFacet(BuildingArrays snap, int deletedFacetId1)
        {
            var bytes = _svc.GetBytesCopy();
            int blockStart = snap.StartOffset;
            int saveType = BitConverter.ToInt32(bytes, 0);

            // Read current header counters
            ushort oldNextBuilding = ReadU16(bytes, blockStart + 2);
            ushort oldNextFacet = ReadU16(bytes, blockStart + 4);

            // Calculate offsets
            int buildingsOff = blockStart + HeaderSize;
            int padOff = buildingsOff + (oldNextBuilding - 1) * DBuildingSize;
            int facetsOff = padOff + AfterBuildingsPad;

            // Calculate what comes after facets
            int afterFacetsOff = facetsOff + (oldNextFacet - 1) * DFacetSize;
            int afterFacetsLen = bytes.Length - afterFacetsOff;

            // Build new file
            using var ms = new System.IO.MemoryStream();

            // 1. Copy file header + tiles (everything up to building block header)
            ms.Write(bytes, 0, blockStart);

            // 2. Write building block header with updated NextDFacet
            var header = new byte[HeaderSize];
            Buffer.BlockCopy(bytes, blockStart, header, 0, HeaderSize);
            WriteU16(header, 4, (ushort)(oldNextFacet - 1)); // Decrement NextDFacet
            ms.Write(header, 0, HeaderSize);

            // 3. Write buildings with updated facet ranges
            for (int bldIdx = 0; bldIdx < oldNextBuilding - 1; bldIdx++)
            {
                int srcOff = buildingsOff + bldIdx * DBuildingSize;
                var bldBytes = new byte[DBuildingSize];
                Buffer.BlockCopy(bytes, srcOff, bldBytes, 0, DBuildingSize);

                ushort startFacet = ReadU16(bldBytes, 0);
                ushort endFacet = ReadU16(bldBytes, 2);

                // Adjust facet range based on where the deleted facet was
                ushort newStart = startFacet;
                ushort newEnd = endFacet;

                if (deletedFacetId1 < startFacet)
                {
                    // Deleted facet is before this building's range - shift both down
                    newStart = (ushort)(startFacet - 1);
                    newEnd = (ushort)(endFacet - 1);
                }
                else if (deletedFacetId1 >= startFacet && deletedFacetId1 < endFacet)
                {
                    // Deleted facet is within this building's range - shrink end
                    newEnd = (ushort)(endFacet - 1);
                }
                // else: deleted facet is after this building's range - no change needed

                WriteU16(bldBytes, 0, newStart);
                WriteU16(bldBytes, 2, newEnd);

                ms.Write(bldBytes, 0, DBuildingSize);
            }

            // 4. Write pad
            ms.Write(bytes, padOff, AfterBuildingsPad);

            // 5. Write facets, skipping the deleted one
            for (int fId1 = 1; fId1 < oldNextFacet; fId1++)
            {
                if (fId1 == deletedFacetId1)
                    continue; // Skip deleted facet

                int srcOff = facetsOff + (fId1 - 1) * DFacetSize;
                ms.Write(bytes, srcOff, DFacetSize);
            }

            // 6. Copy everything after facets (styles, paint, storeys, indoors, walkables, objects, tail)
            ms.Write(bytes, afterFacetsOff, afterFacetsLen);

            var newBytes = ms.ToArray();

            Debug.WriteLine($"[FacetDeleter] Old file: {bytes.Length} bytes, new file: {newBytes.Length} bytes " +
                           $"(should be -{DFacetSize} bytes)");

            if (newBytes.Length != bytes.Length - DFacetSize)
            {
                Debug.WriteLine($"[FacetDeleter] WARNING: Size mismatch! Expected {bytes.Length - DFacetSize}, got {newBytes.Length}");
            }

            _svc.ReplaceBytes(newBytes);
        }

        private static ushort ReadU16(byte[] b, int off) =>
            (ushort)(b[off] | (b[off + 1] << 8));

        private static void WriteU16(byte[] b, int off, ushort val)
        {
            b[off] = (byte)(val & 0xFF);
            b[off + 1] = (byte)((val >> 8) & 0xFF);
        }
    }

    /// <summary>Result of a facet deletion attempt.</summary>
    public sealed class DeleteFacetResult
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public int OwningBuildingId { get; }

        private DeleteFacetResult(bool success, string? error, int buildingId)
        {
            IsSuccess = success;
            ErrorMessage = error;
            OwningBuildingId = buildingId;
        }

        public static DeleteFacetResult Success(int buildingId) =>
            new(true, null, buildingId);

        public static DeleteFacetResult Fail(string error) =>
            new(false, error, 0);
    }
}