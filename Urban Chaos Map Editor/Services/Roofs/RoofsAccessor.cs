// Services/Roofs/RoofsAccessor.cs
// Unified read accessor for walkable entries, RoofFace4 entries, and cell altitudes.
// Matches the pattern used by BuildingsAccessor, HeightsAccessor, TextureAccessor, etc.

using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Roofs
{
    /// <summary>
    /// Reads walkable entries, RoofFace4 entries, and related data from the .iam file.
    /// Write operations are handled by WalkableAdder, WalkableDeleter, WalkableEditor,
    /// RoofFace4Adder, and AltitudeAccessor respectively.
    /// </summary>
    public sealed class RoofsAccessor
    {
        private readonly MapDataService _svc;

        public RoofsAccessor(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        /// <summary>
        /// Reads all walkable entries from the file.
        /// Returns array including sentinel at index 0; valid entries start at 1.
        /// </summary>
        public DWalkableRec[] ReadWalkables()
        {
            if (!_svc.IsLoaded) return Array.Empty<DWalkableRec>();

            if (_svc.TryGetWalkables(out var walkables, out _))
                return walkables;

            return Array.Empty<DWalkableRec>();
        }

        /// <summary>
        /// Reads all RoofFace4 entries from the file.
        /// Returns array including sentinel at index 0.
        /// </summary>
        public RoofFace4Rec[] ReadRoofFace4s()
        {
            if (!_svc.IsLoaded) return Array.Empty<RoofFace4Rec>();

            if (_svc.TryGetWalkables(out _, out var rf4s))
                return rf4s;

            return Array.Empty<RoofFace4Rec>();
        }

        /// <summary>
        /// Reads both walkables and RF4 arrays in one call (avoids double parsing).
        /// </summary>
        public (DWalkableRec[] Walkables, RoofFace4Rec[] RoofFaces4) ReadSnapshot()
        {
            if (!_svc.IsLoaded)
                return (Array.Empty<DWalkableRec>(), Array.Empty<RoofFace4Rec>());

            if (_svc.TryGetWalkables(out var walkables, out var rf4s))
                return (walkables, rf4s);

            return (Array.Empty<DWalkableRec>(), Array.Empty<RoofFace4Rec>());
        }

        /// <summary>
        /// Returns walkable entries filtered by building id.
        /// </summary>
        public DWalkableRec[] ReadWalkablesForBuilding(int buildingId)
        {
            var all = ReadWalkables();
            if (all.Length <= 1) return Array.Empty<DWalkableRec>();

            var result = new System.Collections.Generic.List<DWalkableRec>();
            for (int i = 1; i < all.Length; i++)
            {
                if (all[i].Building == buildingId)
                    result.Add(all[i]);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Returns RoofFace4 entries for a given walkable's face range.
        /// </summary>
        public RoofFace4Rec[] ReadRoofFace4sForWalkable(DWalkableRec walkable)
        {
            var all = ReadRoofFace4s();
            if (all.Length == 0) return Array.Empty<RoofFace4Rec>();

            int start = walkable.StartFace4;
            int end = walkable.EndFace4;
            if (end <= start) return Array.Empty<RoofFace4Rec>();

            start = Math.Max(0, start);
            end = Math.Min(end, all.Length);

            var result = new RoofFace4Rec[end - start];
            Array.Copy(all, start, result, 0, result.Length);
            return result;
        }

        /// <summary>
        /// Returns the 1-based index of a walkable entry, or -1 if not found.
        /// Useful for mapping back from a DWalkableRec to its array position.
        /// </summary>
        public int FindWalkableIndex(DWalkableRec target)
        {
            var all = ReadWalkables();
            for (int i = 1; i < all.Length; i++)
            {
                if (all[i].Equals(target))
                    return i;
            }
            return -1;
        }
    }
}