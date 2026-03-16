// /Services/DoorGateAdder.cs
// Specialized service for adding doors and gates to buildings/fences
using System.Diagnostics;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Services.Buildings
{

    public sealed class DoorGateAdder
    {
        private readonly BuildingAdder _buildingAdder;
        private readonly MapDataService _svc;

        // Standard heights (in Height units where real_height = Height * 64)
        public const byte STANDARD_DOOR_HEIGHT = 4;   // 256 world units (1 storey)
        public const byte STANDARD_GATE_HEIGHT = 2;   // 128 world units (half storey)
        public const byte TALL_GATE_HEIGHT = 3;       // 192 world units
        public const byte LOW_FENCE_HEIGHT = 1;       // 64 world units

        public DoorGateAdder(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _buildingAdder = new BuildingAdder(svc);
        }

        /// <summary>
        /// Adds a door to a building.
        /// </summary>
        /// <param name="buildingId1">1-based building ID</param>
        /// <param name="x0">Start X coordinate (0-127)</param>
        /// <param name="z0">Start Z coordinate (0-127)</param>
        /// <param name="x1">End X coordinate (0-127)</param>
        /// <param name="z1">End Z coordinate (0-127)</param>
        /// <param name="styleId">Texture style ID for the door</param>
        /// <param name="doorType">Type of door (default: standard Door)</param>
        /// <param name="height">Door height in bands (default: 4 = standard storey)</param>
        /// <param name="isOpen">Whether door starts in open state</param>
        /// <param name="isTwoSided">Whether door is visible from both sides (recommended)</param>
        public DoorResult TryAddDoor(
            int buildingId1,
            byte x0, byte z0,
            byte x1, byte z1,
            ushort styleId,
            DoorType doorType = DoorType.Door,
            byte height = STANDARD_DOOR_HEIGHT,
            bool isOpen = false,
            bool isTwoSided = true)
        {
            // Validate coordinates form a line, not a point or area
            bool isHorizontal = (z0 == z1 && x0 != x1);
            bool isVertical = (x0 == x1 && z0 != z1);

            if (!isHorizontal && !isVertical)
            {
                if (x0 == x1 && z0 == z1)
                    return DoorResult.Fail("Door coordinates are a single point, not a line.");
                else
                    return DoorResult.Fail("Door must be horizontal or vertical, not diagonal.");
            }

            // Build flags
            FacetFlags flags = FacetFlags.OnBuilding;
            if (isTwoSided)
                flags |= FacetFlags.TwoSided;
            if (isOpen)
                flags |= FacetFlags.Open;

            var facetType = doorType switch
            {
                DoorType.Door => FacetType.Door,
                DoorType.InsideDoor => FacetType.InsideDoor,
                DoorType.OutsideDoor => FacetType.OutsideDoor,
                DoorType.OInside => FacetType.OInside,
                _ => FacetType.Door
            };

            var template = new FacetTemplate
            {
                Type = facetType,
                Height = height,
                FHeight = 0,
                BlockHeight = 16, // Standard block
                Flags = flags,
                RawStyleId = styleId
            };

            var coords = new List<(byte, byte, byte, byte)> { (x0, z0, x1, z1) };

            Debug.WriteLine($"[DoorGateAdder] Adding {doorType} to building #{buildingId1}");
            Debug.WriteLine($"[DoorGateAdder]   Coords: ({x0},{z0}) ? ({x1},{z1})");
            Debug.WriteLine($"[DoorGateAdder]   Height: {height}, Style: {styleId}");
            Debug.WriteLine($"[DoorGateAdder]   Flags: {flags}");

            var result = _buildingAdder.TryAddFacets(buildingId1, coords, template);

            if (result.IsSuccess)
            {
                Debug.WriteLine($"[DoorGateAdder] Successfully added door");
                return DoorResult.Success();
            }
            else
            {
                Debug.WriteLine($"[DoorGateAdder] Failed: {result.ErrorMessage}");
                return DoorResult.Fail(result.ErrorMessage ?? "Unknown error");
            }
        }

        /// <summary>
        /// Adds a gate to a fence building.
        /// Gates are shorter than doors and typically part of a fence perimeter.
        /// </summary>
        public DoorResult TryAddGate(
            int fenceBuildingId1,
            byte x0, byte z0,
            byte x1, byte z1,
            ushort styleId,
            GateType gateType = GateType.Standard,
            bool isElectrified = false,
            bool hasBarbedTop = false)
        {
            byte height = gateType switch
            {
                GateType.Low => LOW_FENCE_HEIGHT,
                GateType.Standard => STANDARD_GATE_HEIGHT,
                GateType.Tall => TALL_GATE_HEIGHT,
                _ => STANDARD_GATE_HEIGHT
            };

            FacetFlags flags = FacetFlags.TwoSided | FacetFlags.OnBuilding;

            if (isElectrified)
                flags |= FacetFlags.Electrified;
            if (hasBarbedTop)
                flags |= FacetFlags.BarbTop;

            var template = new FacetTemplate
            {
                Type = FacetType.OutsideDoor,
                Height = height,
                FHeight = 0,
                BlockHeight = 16,
                Flags = flags,
                RawStyleId = styleId
            };

            var coords = new List<(byte, byte, byte, byte)> { (x0, z0, x1, z1) };

            Debug.WriteLine($"[DoorGateAdder] Adding {gateType} gate to fence building #{fenceBuildingId1}");
            Debug.WriteLine($"[DoorGateAdder]   Coords: ({x0},{z0}) ? ({x1},{z1})");
            Debug.WriteLine($"[DoorGateAdder]   Height: {height}, Electrified: {isElectrified}, Barbed: {hasBarbedTop}");

            var result = _buildingAdder.TryAddFacets(fenceBuildingId1, coords, template);

            if (result.IsSuccess)
                return DoorResult.Success();
            else
                return DoorResult.Fail(result.ErrorMessage ?? "Unknown error");
        }

        /// <summary>
        /// Creates a doorway by adding two walls with a gap between them.
        /// This is useful when you want a permanently open doorway in a wall.
        /// </summary>
        /// <param name="buildingId1">1-based building ID</param>
        /// <param name="wallX0">Wall start X</param>
        /// <param name="wallZ0">Wall start Z</param>
        /// <param name="wallX1">Wall end X</param>
        /// <param name="wallZ1">Wall end Z</param>
        /// <param name="doorStart">Position along wall where door starts (0.0-1.0)</param>
        /// <param name="doorEnd">Position along wall where door ends (0.0-1.0)</param>
        /// <param name="wallStyleId">Texture for wall segments</param>
        /// <param name="wallHeight">Height of walls</param>
        public DoorResult TryAddDoorway(
            int buildingId1,
            byte wallX0, byte wallZ0,
            byte wallX1, byte wallZ1,
            float doorStart, float doorEnd,
            ushort wallStyleId,
            byte wallHeight = STANDARD_DOOR_HEIGHT)
        {
            if (doorStart < 0 || doorStart > 1 || doorEnd < 0 || doorEnd > 1 || doorStart >= doorEnd)
                return DoorResult.Fail("Invalid door position (must be 0.0-1.0, start < end)");

            // Calculate door position along the wall
            int dx = wallX1 - wallX0;
            int dz = wallZ1 - wallZ0;

            // Door start point
            int doorX0 = wallX0 + (int)(dx * doorStart);
            int doorZ0 = wallZ0 + (int)(dz * doorStart);

            // Door end point
            int doorX1 = wallX0 + (int)(dx * doorEnd);
            int doorZ1 = wallZ0 + (int)(dz * doorEnd);

            var template = new FacetTemplate
            {
                Type = FacetType.Wall,
                Height = wallHeight,
                FHeight = 0,
                BlockHeight = 16,
                Flags = FacetFlags.OnBuilding,
                RawStyleId = wallStyleId
            };

            var coords = new List<(byte, byte, byte, byte)>();

            // Wall segment before door (if any)
            if (doorX0 != wallX0 || doorZ0 != wallZ0)
            {
                coords.Add(((byte)wallX0, (byte)wallZ0, (byte)doorX0, (byte)doorZ0));
            }

            // Wall segment after door (if any)
            if (doorX1 != wallX1 || doorZ1 != wallZ1)
            {
                coords.Add(((byte)doorX1, (byte)doorZ1, (byte)wallX1, (byte)wallZ1));
            }

            if (coords.Count == 0)
            {
                // Entire wall is door - nothing to add
                return DoorResult.Success();
            }

            Debug.WriteLine($"[DoorGateAdder] Creating doorway in wall from ({wallX0},{wallZ0}) to ({wallX1},{wallZ1})");
            Debug.WriteLine($"[DoorGateAdder]   Door gap: ({doorX0},{doorZ0}) to ({doorX1},{doorZ1})");
            Debug.WriteLine($"[DoorGateAdder]   Adding {coords.Count} wall segments");

            var result = _buildingAdder.TryAddFacets(buildingId1, coords, template);

            if (result.IsSuccess)
                return DoorResult.Success();
            else
                return DoorResult.Fail(result.ErrorMessage ?? "Unknown error");
        }

        /// <summary>
        /// Gets recommended style IDs for common door/gate types.
        /// These are typical values - actual styles depend on the loaded TMA files.
        /// </summary>
        public static class CommonStyles
        {
            // These are example values - verify against actual TMA data
            public const ushort WoodenDoor = 15;
            public const ushort MetalDoor = 16;
            public const ushort GlassDoor = 17;
            public const ushort ChainLinkGate = 22;
            public const ushort MetalGate = 23;
            public const ushort WoodenGate = 24;
            public const ushort WarehouseDoor = 25;
        }
    }

    public enum DoorType
    {
        /// <summary>Standard building door (solid, can be opened)</summary>
        Door,
        /// <summary>Interior door (usually passable)</summary>
        InsideDoor,
        /// <summary>Exterior gate (part of fence)</summary>
        OutsideDoor,
        /// <summary>Open interior space (no collision)</summary>
        OInside
    }

    public enum GateType
    {
        /// <summary>Low fence gate (~64 units)</summary>
        Low,
        /// <summary>Standard fence gate (~128 units)</summary>
        Standard,
        /// <summary>Tall security gate (~192 units)</summary>
        Tall
    }

    public sealed class DoorResult
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }

        private DoorResult(bool success, string? error)
        {
            IsSuccess = success;
            ErrorMessage = error;
        }

        public static DoorResult Success() => new(true, null);
        public static DoorResult Fail(string error) => new(false, error);
    }
}