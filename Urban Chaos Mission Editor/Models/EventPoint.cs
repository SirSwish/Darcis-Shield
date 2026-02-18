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
    /// 

    /// <summary>
    /// Cutscene data - only used when WaypointType == WaypointType.CutScene
    /// This contains the full timeline-based cutscene sequence with channels and packets.
    /// </summary>
    public CutsceneData? CutsceneData { get; set; }

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
            WaypointType.VisualEffect => GetVisualEffectSummary(),
            WaypointType.AdjustEnemy => GetAdjustEnemySummary(),
            WaypointType.MoveThing => $"Move EP {Data[0]} here",
            WaypointType.CreateTarget => GetCreateTargetSummary(),
            WaypointType.Simple => GetSimpleWaypointSummary(),
            WaypointType.Conversation => GetConversationSummary(),
            WaypointType.Message => ExtraText ?? "(no message)",
            WaypointType.CreateMapExit => ExtraText ?? "(no destination)",
            WaypointType.Shout => ExtraText ?? "(no shout)",
            WaypointType.NavBeacon => GetNavBeaconSummary(),
            WaypointType.BonusPoints => $"{Data[1]} points - {ExtraText ?? "(no text)"}",
            WaypointType.EndGameWin => "Victory",
            WaypointType.EndGameLose => "Failure",
            WaypointType.CreateBarrel => GetBarrelSummary(),
            WaypointType.CreateBomb => GetBombSummary(),
            WaypointType.CreateTrap => GetTrapSummary(),
            WaypointType.DynamicLight => GetDynamicLightSummary(),
            WaypointType.SpotEffect => GetSpotEffectSummary(),
            WaypointType.Extend => GetExtendTimerSummary(),
            WaypointType.MakePersonPee => GetMakePersonPeeSummary(),
            WaypointType.WareFX => GetWareFxSummary(),
            WaypointType.StallCar => GetStallCarSummary(),
            WaypointType.TransferPlayer => GetTransferPlayerSummary(),
            WaypointType.SoundEffect => GetSoundEffectSummary(),
            WaypointType.LockVehicle => GetLockVehicleSummary(),
            WaypointType.Increment => GetIncrementSummary(),
            WaypointType.ResetCounter => GetResetCounterSummary(),
            WaypointType.GroupLife => GetGroupLifeSummary(),
            WaypointType.GroupDeath => GetGroupDeathSummary(),
            WaypointType.GroupReset => GetGroupResetSummary(),
            WaypointType.KillWaypoint => GetKillWaypointSummary(),
            WaypointType.NoFloor => GetNoFloorSummary(),
            WaypointType.Autosave => GetAutosaveSummary(),
            WaypointType.Interesting => GetInterestingSummary(),
            WaypointType.ShakeCamera => GetShakeCameraSummary(),
            WaypointType.Sign => GetSignSummary(),
            WaypointType.Teleport => GetTeleportSummary(),
            WaypointType.TeleportTarget => GetTeleportTargetSummary(),
            WaypointType.LinkPlatform => GetLinkPlatformSummary(),
            WaypointType.CreateTreasure => GetTreasureSummary(),
            WaypointType.BurnPrim => GetBurnPrimSummary(),
            WaypointType.EnemyFlags => GetEnemyFlagsSummary(),
            WaypointType.ActivatePrim => GetActivatePrimSummary(),
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
    private string GetTeleportSummary()
    {
        // No Data[]; grouping is via Colour/Group in common fields
        return $"Teleport ({GroupLetter}, {WaypointColors.GetColorName(Colour)})";
    }

    private string GetTeleportTargetSummary()
    {
        return $"Teleport Target ({GroupLetter}, {WaypointColors.GetColorName(Colour)})";
    }
    private string GetSignSummary()
    {
        int t = Data[0];
        string name =
            (t >= 0 && t < EditorStrings.SignTypeNames.Length)
                ? EditorStrings.SignTypeNames[t]
                : $"Type {t}";

        List<string> flips = new();
        if ((Data[1] & 1) != 0) flips.Add("LR");
        if ((Data[1] & 2) != 0) flips.Add("TB");

        string flipSuffix = flips.Count > 0 ? $" ({string.Join(",", flips)})" : "";
        return $"Sign: {name}{flipSuffix}";
    }

    private string GetVisualEffectSummary()
    {
        int vfxTypes = Data[0];
        int scale = Data[1];

        var effects = new List<string>();
        if ((vfxTypes & (1 << 0)) != 0) effects.Add("Flare");
        if ((vfxTypes & (1 << 1)) != 0) effects.Add("Fire Dome");
        if ((vfxTypes & (1 << 2)) != 0) effects.Add("Shockwave");
        if ((vfxTypes & (1 << 3)) != 0) effects.Add("Smoke Trails");
        if ((vfxTypes & (1 << 4)) != 0) effects.Add("Bonfire");

        if (effects.Count == 0)
            return "No effects";

        string effectStr = string.Join(", ", effects);
        return scale > 0 ? $"{effectStr} (scale: {scale})" : effectStr;
    }

    private string GetAdjustEnemySummary()
    {
        int targetEP = Data[6];
        int enemyType = Data[0] & 0xFFFF;

        string typeName = (enemyType >= 1 && enemyType <= EditorStrings.EnemyTypeNames.Length)
            ? EditorStrings.EnemyTypeNames[enemyType - 1]
            : $"Type {enemyType}";

        return targetEP > 0 ? $"EP {targetEP} → {typeName}" : typeName;
    }

    private string GetCreateTargetSummary()
    {
        int moveType = Data[0];
        int targetType = Data[1];

        string moveName = (moveType >= 1 && moveType <= EditorStrings.CameraMoveNames.Length)
            ? EditorStrings.CameraMoveNames[moveType - 1]
            : $"Move {moveType}";

        string targName = (targetType >= 1 && targetType <= EditorStrings.TargetTypeNames.Length)
            ? EditorStrings.TargetTypeNames[targetType - 1]
            : $"Target {targetType}";

        return $"{moveName} {targName}";
    }

    private string GetSimpleWaypointSummary()
    {
        int delay = Data[0];
        if (delay == 0)
            return "Waypoint";

        double seconds = delay / 10.0;
        return $"Waypoint ({seconds:F1}s delay)";
    }

    private string GetConversationSummary()
    {
        int person1 = Data[1];
        int person2 = Data[2];
        bool hasText = !string.IsNullOrEmpty(ExtraText);

        if (person1 > 0 && person2 > 0)
            return hasText ? $"EP {person1} ↔ EP {person2}" : $"EP {person1} ↔ EP {person2} (no script)";

        if (person1 > 0)
            return hasText ? $"EP {person1} speaks" : $"EP {person1} (no script)";

        return hasText ? "(has script)" : "(no participants)";
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

    private string GetBurnPrimSummary()
    {
        int burnType = Data[0];

        if (burnType == 0)
            return "No effects";

        var effects = new List<string>();
        if ((burnType & (1 << 0)) != 0) effects.Add("Flickering");
        if ((burnType & (1 << 1)) != 0) effects.Add("Bonfires");
        if ((burnType & (1 << 2)) != 0) effects.Add("Thick Flames");
        if ((burnType & (1 << 3)) != 0) effects.Add("Smoke");
        if ((burnType & (1 << 4)) != 0) effects.Add("Static");

        return string.Join(", ", effects);
    }
    private string GetEnemyFlagsSummary()
    {
        int targetEP = Data[0];
        int flags = Data[1];

        if (targetEP == 0)
            return "(no target)";

        // Count active flags
        int flagCount = 0;
        for (int i = 0; i < 16; i++)
        {
            if ((flags & (1 << i)) != 0) flagCount++;
        }

        if (flagCount == 0)
            return $"EP {targetEP} → clear flags";

        // Show a few key flags
        var keyFlags = new List<string>();
        if ((flags & (1 << 12)) != 0) keyFlags.Add("Invuln");
        if ((flags & (1 << 3)) != 0) keyFlags.Add("FightBack");
        if ((flags & (1 << 4)) != 0) keyFlags.Add("KillPlayer");
        if ((flags & (1 << 13)) != 0) keyFlags.Add("Guilty");

        if (keyFlags.Count > 0)
            return $"EP {targetEP} → {string.Join(", ", keyFlags)}";

        return $"EP {targetEP} → {flagCount} flags";
    }
    private string GetActivatePrimSummary()
    {
        int primType = Data[0];
        int primAnim = Data[1];

        if (primType >= 0 && primType < EditorStrings.ActivatePrimNames.Length)
        {
            string typeName = EditorStrings.ActivatePrimNames[primType];

            // For Anim Prim, also show the animation number
            if (primType == 3 && primAnim > 0)
                return $"{typeName} #{primAnim}";

            return typeName;
        }

        return $"Unknown ({primType})";
    }
    private string GetTreasureSummary()
    {
        int value = Data[0];

        if (value == 0)
            return "0 points";

        return $"{value:N0} points";
    }
    private string GetDynamicLightSummary()
    {
        int liteType = Data[0];
        int speed = Data[1];
        int steps = Data[2];

        string typeName = (liteType >= 0 && liteType < EditorStrings.DynamicLightNames.Length)
            ? EditorStrings.DynamicLightNames[liteType]
            : $"Type {liteType}";

        if (steps <= 0) steps = 1;
        if (speed <= 0) speed = 1;

        return $"{typeName} (spd {speed}, {steps} steps)";
    }
    private string GetSpotEffectSummary()
    {
        int type = Data[0];
        int scale = Data[1];

        string typeName = (type >= 0 && type < EditorStrings.SpotFXNames.Length)
            ? EditorStrings.SpotFXNames[type]
            : $"Type {type}";

        return scale > 0 ? $"{typeName} (scale: {scale})" : typeName;
    }
    private string GetExtendTimerSummary()
    {
        int targetEp = Data[0];
        int amount = Data[1];

        string amtText = amount >= 0 ? $"+{amount}" : amount.ToString();

        if (targetEp > 0)
            return $"Extend EP {targetEp} ({amtText})";

        return $"Extend Timer ({amtText})";
    }
    private string GetMakePersonPeeSummary()
    {
        int wp = Data[0];
        if (wp <= 0) return "Make Person Pee";
        return $"Make Person Pee (WP {wp})";
    }
    private string GetWareFxSummary()
    {
        int type = Data[0];

        if (type >= 0 && type < EditorStrings.WareFXNames.Length)
            return EditorStrings.WareFXNames[type];

        return $"Ware FX (Type {type})";
    }
    private string GetStallCarSummary()
    {
        int v = Data[0];
        return v > 0 ? $"Stall Car (Vehicle {v})" : "Stall Car";
    }
    private string GetTransferPlayerSummary()
    {
        int to = Data[0];
        return to > 0 ? $"Transfer Player (to {to})" : "Transfer Player";
    }
    private string GetNavBeaconSummary()
    {
        int person = Data[1];
        string label = string.IsNullOrWhiteSpace(ExtraText) ? "Nav Beacon" : ExtraText!;

        return person > 0 ? $"{label} (EP {person})" : label;
    }
    private string GetSoundEffectSummary()
    {
        int type = Data[0];
        int id = Data[1];

        string typeName = type switch
        {
            0 => "Sound FX",
            1 => "Music",
            _ => $"Type {type}"
        };

        return $"{typeName} (ID {id})";
    }
    private string GetLockVehicleSummary()
    {
        int vehicle = Data[0];
        bool locked = Data[1] != 0;

        string action = locked ? "Lock Vehicle" : "Unlock Vehicle";
        return vehicle > 0 ? $"{action} (Vehicle {vehicle})" : action;
    }
    private string GetIncrementSummary()
    {
        int amount = Data[0];
        int counter = Data[1];

        if (counter <= 0) return "Increment";
        return $"counter {counter} by {amount}";
    }
    private string GetResetCounterSummary()
    {
        int counter = Math.Clamp(Data[0], 0, 9) + 1;
        return $"Reset counter {counter}";
    }
    private string GetGroupLifeSummary()
    {
        return $"Group Life ({GroupLetter}, {WaypointColors.GetColorName(Colour)})";
    }

    private string GetGroupDeathSummary()
    {
        return $"Group Death ({GroupLetter}, {WaypointColors.GetColorName(Colour)})";
    }

    private string GetGroupResetSummary()
    {
        return $"Group Reset ({GroupLetter}, {WaypointColors.GetColorName(Colour)})";
    }
    private string GetKillWaypointSummary()
    {
        int target = Data[0];
        return target > 0 ? $"Kill EP {target}" : "Kill Waypoint (no target)";
    }
    private string GetNoFloorSummary() => "No Floor";
    private string GetAutosaveSummary() => "Autosave";
    private string GetInterestingSummary() => "Interesting";
    private string GetShakeCameraSummary() => "Shake Camera";
    private string GetLinkPlatformSummary()
    {
        int speed = Data[0];
        int flags = Data[1];

        List<string> f = new();
        if ((flags & 1) != 0) f.Add("Axis");
        if ((flags & 2) != 0) f.Add("Rot");
        if ((flags & 4) != 0) f.Add("Rocket");

        string fs = f.Count > 0 ? $" [{string.Join(",", f)}]" : "";
        return $"Link Platform (Speed {speed}){fs}";
    }
}