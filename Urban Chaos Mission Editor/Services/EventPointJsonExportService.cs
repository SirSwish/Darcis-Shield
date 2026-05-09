using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.Models;

namespace UrbanChaosMissionEditor.Services;

public sealed class EventPointJsonExportService
{
    private readonly UcmFileService _ucmFileService = new();

    public EventPointExportSummary ExportDirectory(string directoryPath, string outputPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"UCM workspace directory not found: {directoryPath}");

        var files = Directory.EnumerateFiles(directoryPath, "*.ucm", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missions = new ConcurrentBag<MissionExport>();
        var failures = new ConcurrentBag<FileExportFailure>();

        Parallel.ForEach(files, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
        }, filePath =>
        {
            try
            {
                var mission = _ucmFileService.ReadMission(filePath);
                var eventPoints = mission.UsedEventPoints
                    .OrderBy(ep => ep.Index)
                    .Select(BuildEventPointExport)
                    .ToList();

                missions.Add(new MissionExport
                {
                    FileName = Path.GetFileName(filePath),
                    FullPath = filePath,
                    RelativePath = Path.GetRelativePath(directoryPath, filePath),
                    MissionName = mission.MissionName,
                    MapName = mission.MapName,
                    LightMapName = mission.LightMapName,
                    Version = mission.Version,
                    EventPointCount = eventPoints.Count,
                    EventPoints = eventPoints
                });
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                failures.Add(new FileExportFailure
                {
                    FileName = Path.GetFileName(filePath),
                    FullPath = filePath,
                    RelativePath = Path.GetRelativePath(directoryPath, filePath),
                    Error = ex.Message
                });
            }
        });

        var orderedMissions = missions
            .OrderBy(mission => mission.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var export = new WorkspaceEventPointExport
        {
            ExportedAtLocal = DateTimeOffset.Now,
            WorkspaceDirectory = directoryPath,
            UcmFileCount = files.Count,
            ExportedMissionCount = orderedMissions.Count,
            FailedFileCount = failures.Count,
            EventPointCount = orderedMissions.Sum(mission => mission.EventPointCount),
            Missions = orderedMissions,
            Failures = failures
                .OrderBy(failure => failure.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? directoryPath);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        return new EventPointExportSummary(files.Count, orderedMissions.Count, export.EventPointCount, failures.Count, outputPath);
    }

    private static EventPointExport BuildEventPointExport(EventPoint ep)
    {
        return new EventPointExport
        {
            Index = ep.Index,
            DisplayName = ep.DisplayName,
            Summary = SafeSummary(ep),
            Common = new EventPointCommonExport
            {
                Used = ep.Used,
                TypeId = (int)ep.WaypointType,
                Type = ep.WaypointType.ToString(),
                TypeName = EditorStrings.GetWaypointTypeName(ep.WaypointType),
                Category = ep.Category.ToString(),
                ColourId = ep.Colour,
                ColourName = NameAt(EditorStrings.ColorNames, ep.Colour),
                GroupId = ep.Group,
                Group = EditorStrings.GetGroupName(ep.Group),
                FlagsValue = (int)ep.Flags,
                Flags = ep.Flags.ToString(),
                DirectionRaw = ep.Direction,
                DirectionDegrees = ep.DirectionDegrees,
                Position = new EventPointPositionExport
                {
                    X = ep.X,
                    Y = ep.Y,
                    Z = ep.Z,
                    MapX = ep.MapX,
                    MapZ = ep.MapZ,
                    PixelX = ep.PixelX,
                    PixelZ = ep.PixelZ
                },
                Next = ep.Next,
                Previous = ep.Prev
            },
            Trigger = BuildTriggerExport(ep),
            TypeSpecific = BuildTypeSpecificExport(ep),
            Text = new EventPointTextExport
            {
                ExtraText = ep.ExtraText,
                TriggerText = ep.TriggerText
            }
        };
    }

    private static EventPointTriggerExport BuildTriggerExport(EventPoint ep)
    {
        var mode = GetTriggerParamMode(ep.TriggeredBy);
        var decoded = new Dictionary<string, object?>();

        switch (mode)
        {
            case TriggerParamMode.Depend:
                decoded["dependencyEventPoint"] = ep.EPRef;
                break;
            case TriggerParamMode.Boolean:
                decoded["eventPointA"] = ep.EPRef;
                decoded["eventPointB"] = ep.EPRefBool;
                break;
            case TriggerParamMode.EPRefOnly:
                decoded["targetEventPoint"] = ep.EPRef;
                break;
            case TriggerParamMode.PersonSeen:
                decoded["personEventPoint"] = ep.EPRef;
                decoded["observerEventPoint"] = ep.EPRefBool;
                break;
            case TriggerParamMode.PersonInVehicle:
                decoded["personEventPoint"] = ep.EPRef;
                decoded["vehicleEventPoint"] = ep.EPRefBool;
                break;
            case TriggerParamMode.Time:
                decoded["timeTicks"] = ep.Radius;
                decoded["timeSeconds"] = ep.Radius / 100.0;
                break;
            case TriggerParamMode.Countdown:
                decoded["dependencyEventPoint"] = ep.EPRef;
                decoded["countdownTicks"] = ep.Radius;
                decoded["countdownSeconds"] = ep.Radius / 100.0;
                break;
            case TriggerParamMode.Proximity:
                decoded["radius"] = ep.Radius;
                break;
            case TriggerParamMode.TargetRadius:
                decoded["targetEventPoint"] = ep.EPRef;
                decoded["distanceRaw"] = ep.Radius;
                decoded["distanceTiles"] = ep.Radius / 64.0;
                break;
            case TriggerParamMode.Counter:
                decoded["counterIndex"] = ep.EPRef;
                decoded["threshold"] = ep.Radius;
                break;
            case TriggerParamMode.CrimeRate:
                decoded["crimeRateRaw"] = ep.Radius;
                decoded["crimeRatePercent"] = ep.Radius / 100.0;
                break;
            case TriggerParamMode.Cuboid:
                decoded["halfSizeX"] = ep.Radius & 0xFFFF;
                decoded["halfSizeZ"] = (ep.Radius >> 16) & 0xFFFF;
                break;
        }

        return new EventPointTriggerExport
        {
            TriggerTypeId = (int)ep.TriggeredBy,
            TriggerType = ep.TriggeredBy.ToString(),
            TriggerName = EditorStrings.GetTriggerTypeName(ep.TriggeredBy),
            ParameterMode = mode.ToString(),
            OnTriggerId = (int)ep.OnTrigger,
            OnTrigger = ep.OnTrigger.ToString(),
            OnTriggerName = NameAt(EditorStrings.OnTriggerNames, (int)ep.OnTrigger),
            EpRef = ep.EPRef,
            EpRefBool = ep.EPRefBool,
            Radius = ep.Radius,
            AfterTimer = ep.AfterTimer,
            Decoded = decoded
        };
    }

    private static EventPointTypeSpecificExport BuildTypeSpecificExport(EventPoint ep)
    {
        var decoded = new Dictionary<string, object?>();

        switch (ep.WaypointType)
        {
            case WaypointType.Simple:
                decoded["delayTenthsOfSecond"] = ep.Data[0];
                decoded["delaySeconds"] = ep.Data[0] / 10.0;
                break;
            case WaypointType.CreatePlayer:
                AddOneBased(decoded, "playerType", ep.Data[0], EditorStrings.PlayerTypeNames);
                break;
            case WaypointType.CreateEnemies:
                AddOneBased(decoded, "enemyType", ep.Data[0] & 0xFFFF, EditorStrings.EnemyTypeNames);
                decoded["count"] = (ep.Data[0] >> 16) & 0xFFFF;
                AddOneBased(decoded, "aiType", ep.Data[1], EditorStrings.EnemyAINames);
                AddOneBased(decoded, "movementType", ep.Data[2], EditorStrings.EnemyMoveNames);
                decoded["ability"] = ep.Data[3];
                decoded["enemyFlags"] = DecodeBitmask(ep.Data[4], EditorStrings.EnemyFlagNames);
                decoded["targetEventPoint"] = ep.Data[6];
                decoded["weaponDrops"] = DecodeBitmask(ep.Data[8], EditorStrings.EnemyWeaponDropNames);
                break;
            case WaypointType.CreateVehicle:
                AddOneBased(decoded, "vehicleType", ep.Data[0], EditorStrings.VehicleTypeNames);
                AddZeroBased(decoded, "behaviour", ep.Data[1], EditorStrings.VehicleBehaviorNames);
                decoded["targetEventPoint"] = ep.Data[2];
                AddZeroBased(decoded, "keyRequired", ep.Data[3], EditorStrings.VehicleKeyNames);
                break;
            case WaypointType.CreateItem:
                AddOneBased(decoded, "itemType", ep.Data[0], EditorStrings.ItemTypeNames);
                decoded["count"] = ep.Data[1];
                decoded["flags"] = ep.Data[2];
                decoded["hiddenInPrim"] = (ep.Data[2] & (1 << 1)) != 0;
                break;
            case WaypointType.CreateCreature:
                AddOneBased(decoded, "creatureType", ep.Data[0], EditorStrings.CreatureTypeNames);
                decoded["count"] = ep.Data[1];
                break;
            case WaypointType.CreateCamera:
            case WaypointType.CameraWaypoint:
                AddZeroBased(decoded, "cameraType", ep.Data[0], EditorStrings.CameraTypeNames);
                AddZeroBased(decoded, "movementType", ep.Data[1], EditorStrings.CameraMoveNames);
                decoded["speed"] = ep.Data[2];
                decoded["delay"] = ep.Data[3];
                decoded["pausePlayer"] = ep.Data[4] != 0;
                decoded["directionLock"] = ep.Data[5] != 0;
                decoded["cannotBeSkipped"] = ep.Data[6] != 0;
                break;
            case WaypointType.CreateTarget:
            case WaypointType.TargetWaypoint:
                AddOneBased(decoded, "movementType", ep.Data[0], EditorStrings.CameraMoveNames);
                AddOneBased(decoded, "targetType", ep.Data[1], EditorStrings.TargetTypeNames);
                decoded["speed"] = ep.Data[2];
                decoded["delay"] = ep.Data[3];
                decoded["cameraZoom"] = ep.Data[4];
                decoded["cameraRotate"] = ep.Data[5] != 0;
                break;
            case WaypointType.Message:
                decoded["durationSeconds"] = ep.Data[1] > 0 ? ep.Data[1] : 4;
                decoded["speaker"] = DecodeMessageSpeaker(ep.Data[2]);
                break;
            case WaypointType.SoundEffect:
                decoded["soundKind"] = ep.Data[0] == 1 ? "Music" : "Sound FX";
                decoded["soundId"] = ep.Data[1];
                break;
            case WaypointType.VisualEffect:
                decoded["effects"] = DecodeBitmask(ep.Data[0], EditorStrings.VisualEffectNames);
                decoded["scale"] = ep.Data[1];
                break;
            case WaypointType.Conversation:
                decoded["person1EventPoint"] = ep.Data[1];
                decoded["person2EventPoint"] = ep.Data[2];
                decoded["grabCamera"] = ep.Data[3] != 0;
                break;
            case WaypointType.ActivatePrim:
                AddZeroBased(decoded, "primType", ep.Data[0], EditorStrings.ActivatePrimNames);
                decoded["animationNumber"] = ep.Data[1];
                break;
            case WaypointType.CreateTrap:
                AddZeroBased(decoded, "trapType", ep.Data[0], EditorStrings.TrapTypeNames);
                decoded["speed"] = ep.Data[1];
                decoded["steps"] = ep.Data[2];
                decoded["mask"] = ep.Data[3];
                AddZeroBased(decoded, "axis", ep.Data[4], EditorStrings.TrapAxisNames);
                decoded["range"] = ep.Data[5];
                break;
            case WaypointType.AdjustEnemy:
                AddOneBased(decoded, "enemyType", ep.Data[0] & 0xFFFF, EditorStrings.EnemyTypeNames);
                decoded["targetEventPoint"] = ep.Data[6];
                break;
            case WaypointType.LinkPlatform:
                decoded["speed"] = ep.Data[0];
                decoded["flags"] = DecodeBitmask(ep.Data[1], EditorStrings.PlatformFlagNames);
                break;
            case WaypointType.CreateBomb:
                AddZeroBased(decoded, "bombType", ep.Data[0], EditorStrings.BombTypeNames);
                decoded["size"] = ep.Data[1];
                decoded["effects"] = ep.Data[2];
                break;
            case WaypointType.BurnPrim:
                decoded["burnEffects"] = DecodeBitmask(ep.Data[0], EditorStrings.BurnTypeNames);
                break;
            case WaypointType.SpotEffect:
                AddZeroBased(decoded, "spotEffectType", ep.Data[0], EditorStrings.SpotFXNames);
                decoded["scale"] = ep.Data[1];
                break;
            case WaypointType.CreateBarrel:
                AddZeroBased(decoded, "barrelType", ep.Data[0], EditorStrings.BarrelTypeNames);
                break;
            case WaypointType.KillWaypoint:
                decoded["targetEventPoint"] = ep.Data[0];
                break;
            case WaypointType.CreateTreasure:
                decoded["value"] = ep.Data[0];
                break;
            case WaypointType.BonusPoints:
                decoded["points"] = ep.Data[1];
                AddZeroBased(decoded, "bonusType", ep.Data[2], EditorStrings.BonusPointTypeNames);
                decoded["gender"] = ep.Data[3] == 0 ? "Male" : "Female";
                break;
            case WaypointType.DynamicLight:
                AddZeroBased(decoded, "lightType", ep.Data[0], EditorStrings.DynamicLightNames);
                decoded["speed"] = ep.Data[1];
                decoded["steps"] = ep.Data[2];
                break;
            case WaypointType.TransferPlayer:
            case WaypointType.StallCar:
            case WaypointType.MakePersonPee:
            case WaypointType.MoveThing:
            case WaypointType.EnemyFlags:
                decoded["targetEventPoint"] = ep.Data[0];
                decoded["value"] = ep.Data[1];
                break;
            case WaypointType.LockVehicle:
                decoded["vehicleEventPoint"] = ep.Data[0];
                decoded["locked"] = ep.Data[1] != 0;
                break;
            case WaypointType.Increment:
                decoded["incrementNumber"] = ep.Data[0];
                decoded["counterIndex"] = ep.Data[1];
                break;
            case WaypointType.ResetCounter:
                decoded["counterIndex"] = ep.Data[0] + 1;
                decoded["storedCounterIndex"] = ep.Data[0];
                break;
            case WaypointType.Sign:
                AddZeroBased(decoded, "signType", ep.Data[0], EditorStrings.SignTypeNames);
                decoded["flipLeftRight"] = (ep.Data[1] & 1) != 0;
                decoded["flipTopBottom"] = (ep.Data[1] & 2) != 0;
                break;
            case WaypointType.WareFX:
                AddZeroBased(decoded, "warehouseFx", ep.Data[0], EditorStrings.WareFXNames);
                break;
        }

        return new EventPointTypeSpecificExport
        {
            Decoded = decoded,
            RawData = ep.Data.ToArray(),
            DataSlots = ep.Data
                .Select((value, index) => new DataSlotExport { Index = index, Name = $"Data[{index}]", Value = value })
                .ToList(),
            HasCutsceneData = ep.CutsceneData != null
        };
    }

    private static TriggerParamMode GetTriggerParamMode(TriggerType type) => type switch
    {
        TriggerType.None => TriggerParamMode.None,
        TriggerType.Dependency => TriggerParamMode.Depend,
        TriggerType.BooleanAnd or TriggerType.BooleanOr => TriggerParamMode.Boolean,
        TriggerType.PersonSeen => TriggerParamMode.PersonSeen,
        TriggerType.PersonInVehicle => TriggerParamMode.PersonInVehicle,
        TriggerType.Timer => TriggerParamMode.Time,
        TriggerType.Countdown or TriggerType.VisibleCountdown => TriggerParamMode.Countdown,
        TriggerType.Radius or TriggerType.PlayerUsesRadius => TriggerParamMode.Proximity,
        TriggerType.EnemyRadius or TriggerType.ThingRadiusDir or TriggerType.MoveRadiusDir => TriggerParamMode.TargetRadius,
        TriggerType.Cuboid => TriggerParamMode.Cuboid,
        TriggerType.Counter => TriggerParamMode.Counter,
        TriggerType.CrimeRateAbove or TriggerType.CrimeRateBelow => TriggerParamMode.CrimeRate,
        TriggerType.ItemHeld or TriggerType.SpecificItemHeld or TriggerType.Killed or TriggerType.KilledNotArrested
            or TriggerType.HalfDead or TriggerType.PersonArrested or TriggerType.ConversationOver
            or TriggerType.PersonIsMurderer or TriggerType.PunchedAndKicked => TriggerParamMode.EPRefOnly,
        _ => TriggerParamMode.None
    };

    private static string SafeSummary(EventPoint ep)
    {
        try
        {
            return ep.GetSummary();
        }
        catch
        {
            return EditorStrings.GetWaypointTypeName(ep.WaypointType);
        }
    }

    private static void AddOneBased(Dictionary<string, object?> decoded, string key, int value, string[] names)
    {
        decoded[$"{key}Id"] = value;
        decoded[$"{key}Name"] = value >= 1 && value <= names.Length ? names[value - 1] : $"Unknown ({value})";
    }

    private static void AddZeroBased(Dictionary<string, object?> decoded, string key, int value, string[] names)
    {
        decoded[$"{key}Id"] = value;
        decoded[$"{key}Name"] = NameAt(names, value);
    }

    private static string NameAt(string[] names, int index)
    {
        return index >= 0 && index < names.Length ? names[index] : $"Unknown ({index})";
    }

    private static List<string> DecodeBitmask(int mask, string[] names)
    {
        var values = new List<string>();
        for (int i = 0; i < names.Length; i++)
        {
            if ((mask & (1 << i)) != 0)
                values.Add(names[i]);
        }
        return values;
    }

    private static string DecodeMessageSpeaker(int value)
    {
        return value switch
        {
            0 => "Radio",
            0xFFFF => "Place/Street Name",
            0xFFFE => "Tutorial",
            _ => $"Event Point {value}"
        };
    }
}

public sealed record EventPointExportSummary(int UcmFileCount, int ExportedMissionCount, int EventPointCount, int FailedFileCount, string OutputPath);

public sealed class WorkspaceEventPointExport
{
    public DateTimeOffset ExportedAtLocal { get; init; }
    public string WorkspaceDirectory { get; init; } = string.Empty;
    public int UcmFileCount { get; init; }
    public int ExportedMissionCount { get; init; }
    public int FailedFileCount { get; init; }
    public int EventPointCount { get; init; }
    public List<MissionExport> Missions { get; init; } = [];
    public List<FileExportFailure> Failures { get; init; } = [];
}

public sealed class MissionExport
{
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string MissionName { get; init; } = string.Empty;
    public string MapName { get; init; } = string.Empty;
    public string LightMapName { get; init; } = string.Empty;
    public uint Version { get; init; }
    public int EventPointCount { get; init; }
    public List<EventPointExport> EventPoints { get; init; } = [];
}

public sealed class FileExportFailure
{
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
}

public sealed class EventPointExport
{
    public int Index { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public EventPointCommonExport Common { get; init; } = new();
    public EventPointTriggerExport Trigger { get; init; } = new();
    public EventPointTypeSpecificExport TypeSpecific { get; init; } = new();
    public EventPointTextExport Text { get; init; } = new();
}

public sealed class EventPointCommonExport
{
    public bool Used { get; init; }
    public int TypeId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public byte ColourId { get; init; }
    public string ColourName { get; init; } = string.Empty;
    public byte GroupId { get; init; }
    public string Group { get; init; } = string.Empty;
    public int FlagsValue { get; init; }
    public string Flags { get; init; } = string.Empty;
    public byte DirectionRaw { get; init; }
    public double DirectionDegrees { get; init; }
    public EventPointPositionExport Position { get; init; } = new();
    public ushort Next { get; init; }
    public ushort Previous { get; init; }
}

public sealed class EventPointPositionExport
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
    public int MapX { get; init; }
    public int MapZ { get; init; }
    public double PixelX { get; init; }
    public double PixelZ { get; init; }
}

public sealed class EventPointTriggerExport
{
    public int TriggerTypeId { get; init; }
    public string TriggerType { get; init; } = string.Empty;
    public string TriggerName { get; init; } = string.Empty;
    public string ParameterMode { get; init; } = string.Empty;
    public int OnTriggerId { get; init; }
    public string OnTrigger { get; init; } = string.Empty;
    public string OnTriggerName { get; init; } = string.Empty;
    public ushort EpRef { get; init; }
    public ushort EpRefBool { get; init; }
    public int Radius { get; init; }
    public ushort AfterTimer { get; init; }
    public Dictionary<string, object?> Decoded { get; init; } = [];
}

public sealed class EventPointTypeSpecificExport
{
    public Dictionary<string, object?> Decoded { get; init; } = [];
    public int[] RawData { get; init; } = [];
    public List<DataSlotExport> DataSlots { get; init; } = [];
    public bool HasCutsceneData { get; init; }
}

public sealed class DataSlotExport
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Value { get; init; }
}

public sealed class EventPointTextExport
{
    public string? ExtraText { get; init; }
    public string? TriggerText { get; init; }
}
