using UrbanChaosMissionEditor.Constants;

namespace UrbanChaosMissionEditor.Models;

/// <summary>
/// Represents a single EventPoint (waypoint) in a UCM mission file.
/// Based on the binary structure defined in Mission.h
/// </summary>
public class EventPoint
{
    /// <summary>
    /// The index of this EventPoint in the EventPoints array
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Color index (0-14) for visual identification
    /// </summary>
    public byte Colour { get; set; }

    /// <summary>
    /// Group letter (0=A, 1=B, etc.) for logical grouping
    /// </summary>
    public byte Group { get; set; }

    /// <summary>
    /// The type of waypoint (WPT_* constant)
    /// </summary>
    public WaypointType WaypointType { get; set; }

    /// <summary>
    /// Whether this EventPoint is in use
    /// </summary>
    public bool Used { get; set; }

    /// <summary>
    /// Trigger condition type (TT_* constant)
    /// </summary>
    public TriggerType TriggeredBy { get; set; }

    /// <summary>
    /// Behavior when triggered (OT_* constant)
    /// </summary>
    public OnTriggerBehavior OnTrigger { get; set; }

    /// <summary>
    /// Direction angle (0-255 maps to 0-360 degrees)
    /// </summary>
    public byte Direction { get; set; }

    /// <summary>
    /// Waypoint flags (WPT_FLAGS_*)
    /// </summary>
    public WaypointFlags Flags { get; set; }

    /// <summary>
    /// EventPoint reference for dependency triggers
    /// </summary>
    public ushort EPRef { get; set; }

    /// <summary>
    /// Second EventPoint reference for boolean triggers
    /// </summary>
    public ushort EPRefBool { get; set; }

    /// <summary>
    /// Reset delay for OT_ACTIVE_TIME behavior
    /// </summary>
    public ushort AfterTimer { get; set; }

    /// <summary>
    /// Type-specific data array (10 integers)
    /// </summary>
    public int[] Data { get; set; } = new int[10];

    /// <summary>
    /// Trigger radius OR text pointer (context-dependent)
    /// </summary>
    public int Radius { get; set; }

    /// <summary>
    /// World X coordinate (256 = 1 tile)
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// World Y coordinate (height)
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// World Z coordinate (256 = 1 tile)
    /// </summary>
    public int Z { get; set; }

    /// <summary>
    /// Next EventPoint index in linked list
    /// </summary>
    public ushort Next { get; set; }

    /// <summary>
    /// Previous EventPoint index in linked list
    /// </summary>
    public ushort Prev { get; set; }

    // Extra data loaded from the Extra section

    /// <summary>
    /// Text content for text-based waypoints (Message, MapExit, Shout, etc.)
    /// </summary>
    public string? ExtraText { get; set; }

    /// <summary>
    /// Trigger text for shout triggers (TT_SHOUT_ALL, TT_SHOUT_ANY)
    /// </summary>
    public string? TriggerText { get; set; }

    // Computed properties

    /// <summary>
    /// X coordinate in map grid units (0-127) - inverted for display
    /// Game grid (0,0) is at bottom-right, display grid (0,0) is at top-left
    /// </summary>
    public int MapX => 127 - (X >> 8);

    /// <summary>
    /// Z coordinate in map grid units (0-127) - inverted for display
    /// </summary>
    public int MapZ => 127 - (Z >> 8);

    /// <summary>
    /// X coordinate in pixels on an 8192x8192 map view
    /// Inverted to match engine rendering (game origin is bottom-right)
    /// </summary>
    public double PixelX => 8192.0 - (X / 4.0);

    /// <summary>
    /// Z coordinate in pixels on an 8192x8192 map view
    /// Inverted to match engine rendering (game origin is bottom-right)
    /// </summary>
    public double PixelZ => 8192.0 - (Z / 4.0);

    /// <summary>
    /// Direction in degrees (0-360)
    /// </summary>
    public double DirectionDegrees => (Direction / 255.0) * 360.0;

    /// <summary>
    /// Group letter (A-Z)
    /// </summary>
    public char GroupLetter => (char)('A' + Math.Min(Group, (byte)25));

    /// <summary>
    /// Display name for this EventPoint
    /// </summary>
    public string DisplayName => $"{Index}{GroupLetter}: {EditorStrings.GetWaypointTypeName(WaypointType)}";

    /// <summary>
    /// Get the category for this waypoint type
    /// </summary>
    public WaypointCategory Category => GetCategory(WaypointType);

    /// <summary>
    /// Check if this waypoint has valid data (is not broken)
    /// </summary>
    public bool IsValid => !Flags.HasFlag(WaypointFlags.Sucks);

    /// <summary>
    /// Determine the category for a waypoint type
    /// </summary>
    public static WaypointCategory GetCategory(WaypointType type)
    {
        return type switch
        {
            WaypointType.CreatePlayer or WaypointType.Autosave => WaypointCategory.Player,

            WaypointType.CreateEnemies or WaypointType.AdjustEnemy or
            WaypointType.CreateCreature or WaypointType.EnemyFlags => WaypointCategory.Enemies,

            WaypointType.CreateItem or WaypointType.CreateBarrel or
            WaypointType.CreateTreasure or WaypointType.BonusPoints => WaypointCategory.Items,

            WaypointType.CreateTrap or WaypointType.CreateBomb => WaypointCategory.Traps,

            WaypointType.CreateCamera or WaypointType.CreateTarget or
            WaypointType.CameraWaypoint or WaypointType.TargetWaypoint => WaypointCategory.Cameras,

            WaypointType.CreateMapExit => WaypointCategory.MapExits,

            WaypointType.Message or WaypointType.Conversation => WaypointCategory.TextMessages,

            _ => WaypointCategory.Misc
        };
    }

    /// <summary>
    /// Get a summary description of this EventPoint
    /// </summary>
    public string GetSummary()
    {
        return WaypointType switch
        {
            WaypointType.CreatePlayer => GetPlayerSummary(),
            WaypointType.CreateEnemies => GetEnemySummary(),
            WaypointType.CreateVehicle => GetVehicleSummary(),
            WaypointType.CreateItem => GetItemSummary(),
            WaypointType.CreateCreature => GetCreatureSummary(),
            WaypointType.Message => ExtraText ?? "(no message)",
            WaypointType.CreateMapExit => ExtraText ?? "(no destination)",
            WaypointType.Shout => ExtraText ?? "(no shout)",
            WaypointType.NavBeacon => ExtraText ?? "(no label)",
            WaypointType.BonusPoints => $"{Data[1]} points - {ExtraText ?? "(no text)"}",
            WaypointType.CreateBarrel => GetBarrelSummary(),
            WaypointType.CreateBomb => GetBombSummary(),
            WaypointType.CreateTrap => GetTrapSummary(),
            _ => EditorStrings.GetWaypointTypeName(WaypointType)
        };
    }

    private string GetPlayerSummary()
    {
        int playerType = Data[0];
        if (playerType >= 1 && playerType <= EditorStrings.PlayerTypeNames.Length)
            return EditorStrings.PlayerTypeNames[playerType - 1];
        return "Unknown Player";
    }

    private string GetEnemySummary()
    {
        // Data[0] = MAKELONG(EnemyType, Count)
        int enemyType = Data[0] & 0xFFFF;
        int count = (Data[0] >> 16) & 0xFFFF;

        string typeName = (enemyType >= 1 && enemyType <= EditorStrings.EnemyTypeNames.Length)
            ? EditorStrings.EnemyTypeNames[enemyType - 1]
            : $"Type {enemyType}";

        return count > 1 ? $"{count}x {typeName}" : typeName;
    }

    private string GetVehicleSummary()
    {
        int vehType = Data[0];
        if (vehType >= 1 && vehType <= EditorStrings.VehicleTypeNames.Length)
            return EditorStrings.VehicleTypeNames[vehType - 1];
        return "Unknown Vehicle";
    }

    private string GetItemSummary()
    {
        int itemType = Data[0];
        int count = Data[1];

        string typeName = (itemType >= 1 && itemType <= EditorStrings.ItemTypeNames.Length)
            ? EditorStrings.ItemTypeNames[itemType - 1]
            : $"Item {itemType}";

        return count > 1 ? $"{count}x {typeName}" : typeName;
    }

    private string GetCreatureSummary()
    {
        int creatureType = Data[0];
        int count = Data[1];

        string typeName = (creatureType >= 1 && creatureType <= EditorStrings.CreatureTypeNames.Length)
            ? EditorStrings.CreatureTypeNames[creatureType - 1]
            : $"Creature {creatureType}";

        return count > 1 ? $"{count}x {typeName}" : typeName;
    }

    private string GetBarrelSummary()
    {
        int barrelType = Data[0];
        if (barrelType >= 0 && barrelType < EditorStrings.BarrelTypeNames.Length)
            return EditorStrings.BarrelTypeNames[barrelType];
        return "Unknown Barrel";
    }

    private string GetBombSummary()
    {
        int bombType = Data[0];
        if (bombType >= 0 && bombType < EditorStrings.BombTypeNames.Length)
            return EditorStrings.BombTypeNames[bombType];
        return "Unknown Bomb";
    }

    private string GetTrapSummary()
    {
        int trapType = Data[0];
        if (trapType >= 0 && trapType < EditorStrings.TrapTypeNames.Length)
            return EditorStrings.TrapTypeNames[trapType];
        return "Unknown Trap";
    }
}