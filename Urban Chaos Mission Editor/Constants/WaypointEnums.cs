using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanChaosMissionEditor.Constants
{
    public enum WaypointType : byte
    {
        None = 0,
        Simple = 1,
        CreatePlayer = 2,
        CreateEnemies = 3,
        CreateVehicle = 4,
        CreateItem = 5,
        CreateCreature = 6,
        CreateCamera = 7,
        CreateTarget = 8,
        CreateMapExit = 9,
        CameraWaypoint = 10,
        TargetWaypoint = 11,
        Message = 12,
        SoundEffect = 13,
        VisualEffect = 14,
        CutScene = 15,
        Teleport = 16,
        TeleportTarget = 17,
        EndGameLose = 18,
        Shout = 19,
        ActivatePrim = 20,
        CreateTrap = 21,
        AdjustEnemy = 22,
        LinkPlatform = 23,
        CreateBomb = 24,
        BurnPrim = 25,
        EndGameWin = 26,
        NavBeacon = 27,
        SpotEffect = 28,
        CreateBarrel = 29,
        KillWaypoint = 30,
        CreateTreasure = 31,
        BonusPoints = 32,
        GroupLife = 33,
        GroupDeath = 34,
        Conversation = 35,
        Interesting = 36,
        Increment = 37,
        DynamicLight = 38,
        GothereDothis = 39,
        TransferPlayer = 40,
        Autosave = 41,
        MakeSearchable = 42,
        LockVehicle = 43,
        GroupReset = 44,
        CountUpTimer = 45,
        ResetCounter = 46,
        CreateMist = 47,
        EnemyFlags = 48,
        StallCar = 49,
        Extend = 50,
        MoveThing = 51,
        MakePersonPee = 52,
        ConePenalties = 53,
        Sign = 54,
        WareFX = 55,
        NoFloor = 56,
        ShakeCamera = 57
    }

    /// <summary>
    /// Trigger types (TT_* constants)
    /// </summary>
    public enum TriggerType : byte
    {
        None = 0,
        Dependency = 1,
        Radius = 2,
        Door = 3,
        Tripwire = 4,
        PressurePad = 5,
        ElectricFence = 6,
        WaterLevel = 7,
        SecurityCamera = 8,
        Switch = 9,
        AnimPrim = 10,
        Timer = 11,
        ShoutAll = 12,
        BooleanAnd = 13,
        BooleanOr = 14,
        ItemHeld = 15,
        ItemSeen = 16,
        Killed = 17,
        ShoutAny = 18,
        Countdown = 19,
        EnemyRadius = 20,
        VisibleCountdown = 21,
        Cuboid = 22,
        HalfDead = 23,
        GroupDead = 24,
        PersonSeen = 25,
        PersonUsed = 26,
        PlayerUsesRadius = 27,
        PrimDamaged = 28,
        PersonArrested = 29,
        ConversationOver = 30,
        Counter = 31,
        KilledNotArrested = 32,
        CrimeRateAbove = 33,
        CrimeRateBelow = 34,
        PersonIsMurderer = 35,
        PersonInVehicle = 36,
        ThingRadiusDir = 37,
        PlayerCarryPerson = 38,
        SpecificItemHeld = 39,
        Random = 40,
        PlayerFiresGun = 41,
        DarciGrabbed = 42,
        PunchedAndKicked = 43,
        MoveRadiusDir = 44
    }

    /// <summary>
    /// On-trigger behavior (OT_* constants)
    /// </summary>
    public enum OnTriggerBehavior : byte
    {
        None = 0,
        Active = 1,
        ActiveWhile = 2,
        ActiveTime = 3,
        ActiveDie = 4
    }

    /// <summary>
    /// Waypoint flags (WPT_FLAGS_* constants)
    /// </summary>
    [Flags]
    public enum WaypointFlags : byte
    {
        None = 0,
        Sucks = 1,      // Invalid/broken waypoint
        Inverse = 2,    // Invert trigger condition
        Inside = 4,     // Indoor waypoint
        Ware = 8,       // Warehouse-related
        Referenced = 16, // Referenced by another EP
        Optional = 32   // Can be removed on PSX
    }

    /// <summary>
    /// Mission zone flags (ZF_* constants)
    /// </summary>
    [Flags]
    public enum ZoneFlags : byte
    {
        None = 0,
        Inside = 1,
        Reverb = 2,
        NoWander = 4,
        Zone1 = 8,
        Zone2 = 16,
        Zone3 = 32,
        Zone4 = 64,
        NoGo = 128
    }

    /// <summary>
    /// Mission flags
    /// </summary>
    [Flags]
    public enum MissionFlags : uint
    {
        None = 0,
        Used = 1,
        ShowCrimeRate = 2,
        CarsWithRoadPrims = 4
    }

    /// <summary>
    /// Player types
    /// </summary>
    public enum PlayerType
    {
        Darci = 1,
        Roper = 2,
        Cop = 3,
        Gang = 4
    }

    /// <summary>
    /// Vehicle types
    /// </summary>
    public enum VehicleType
    {
        None = 0,
        Car = 1,
        Van = 2,
        Taxi = 3,
        Helicopter = 4,
        Motorbike = 5,
        Ambulance = 6,
        FireEngine = 7,
        PoliceCar = 8,
        Truck = 9,
        Boat = 10,
        Hovercraft = 11
    }

    /// <summary>
    /// Item types
    /// </summary>
    public enum ItemType
    {
        None = 0,
        Key = 1,
        Pistol = 2,
        Health = 3,
        Shotgun = 4,
        Knife = 5,
        AK47 = 6,
        Grenade = 7,
        BaseballBat = 8,
        Crowbar = 9,
        MediPack = 10,
        Ammo = 11,
        LongShotgun = 12,
        NightStick = 13,
        Colt45 = 14,
        PlankOfWood = 15,
        Flamethrower = 16,
        Grease = 17,
        Wire = 18,
        DemoBall = 19,
        MissileAirStrike = 20,
        Balloon = 21,
        TimeBomb = 22,
        Dart = 23,
        MilkFloat = 24,
        FireExtinguisher = 25
    }

    /// <summary>
    /// Creature types
    /// </summary>
    public enum CreatureType
    {
        None = 0,
        Bat = 1,
        Gargoyle = 2,
        Balrog = 3,
        Bane = 4
    }

    /// <summary>
    /// Waypoint category for tree view organization
    /// </summary>
    public enum WaypointCategory
    {
        Player = 0,
        Enemies = 1,
        Items = 2,
        Traps = 3,
        Cameras = 4,
        Misc = 5,
        MapExits = 6,
        TextMessages = 7
    }
}
