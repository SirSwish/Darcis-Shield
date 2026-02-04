using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanChaosMissionEditor.Constants
{
    public static class EditorStrings
    {
        /// <summary>
        /// Waypoint type display names (58 types)
        /// </summary>
        public static readonly string[] WaypointTypeNames =
        {
        "None",                 // 0
        "Simple Waypoint",      // 1
        "Create Player",        // 2
        "Create Enemies",       // 3
        "Create Vehicle",       // 4
        "Create Item",          // 5
        "Create Creature",      // 6
        "Create Camera",        // 7
        "Create Target",        // 8
        "Create Map Exit",      // 9
        "Camera Waypoint",      // 10
        "Target Waypoint",      // 11
        "Message",              // 12
        "Sound Effect",         // 13
        "Visual Effect",        // 14
        "Cut Scene",            // 15
        "Teleport",             // 16
        "Teleport Target",      // 17
        "End Game Win",         // 18
        "Shout",                // 19
        "Activate Prim",        // 20
        "Create Trap",          // 21
        "Adjust Enemy",         // 22
        "Link Platform",        // 23
        "Create Bomb",          // 24
        "Burn Prim",            // 25
        "Group Trigger",        // 26
        "Nav Beacon",           // 27
        "Spot Effect",          // 28
        "Create Barrel",        // 29
        "Kill Waypoint",        // 30
        "Create Treasure",      // 31
        "Bonus Points",         // 32
        "Group Life",           // 33
        "Group Death",          // 34
        "Conversation",         // 35
        "Interesting",          // 36
        "Increment",            // 37
        "Dynamic Light",        // 38
        "Go There Do This",     // 39
        "Transfer Player",      // 40
        "Autosave",             // 41
        "Make Searchable",      // 42
        "Lock Vehicle",         // 43
        "Group Reset",          // 44
        "Count Up Timer",       // 45
        "Reset Counter",        // 46
        "Create Mist",          // 47
        "Enemy Flags",          // 48
        "Stall Car",            // 49
        "Extend Timer",         // 50
        "Move Thing",           // 51
        "Make Person Pee",      // 52
        "Cone Penalties",       // 53
        "Sign",                 // 54
        "Warehouse FX",         // 55
        "No Floor",             // 56
        "Shake Camera"          // 57
    };

        /// <summary>
        /// Trigger type display names (45 types)
        /// </summary>
        public static readonly string[] TriggerTypeNames =
        {
        "None",                 // 0
        "Dependency",           // 1
        "Radius",               // 2
        "Door",                 // 3
        "Tripwire",             // 4
        "Pressure Pad",         // 5
        "Electric Fence",       // 6
        "Water Level",          // 7
        "Security Camera",      // 8
        "Switch",               // 9
        "Anim Prim",            // 10
        "Timer",                // 11
        "Shout All",            // 12
        "Boolean AND",          // 13
        "Boolean OR",           // 14
        "Item Held",            // 15
        "Item Seen",            // 16
        "Killed",               // 17
        "Shout Any",            // 18
        "Countdown",            // 19
        "Enemy Radius",         // 20
        "Visible Countdown",    // 21
        "Cuboid",               // 22
        "Half Dead",            // 23
        "Group Dead",           // 24
        "Person Seen",          // 25
        "Person Used",          // 26
        "Player Uses Radius",   // 27
        "Prim Damaged",         // 28
        "Person Arrested",      // 29
        "Conversation Over",    // 30
        "Counter",              // 31
        "Killed Not Arrested",  // 32
        "Crime Rate Above",     // 33
        "Crime Rate Below",     // 34
        "Person Is Murderer",   // 35
        "Person In Vehicle",    // 36
        "Thing Radius Dir",     // 37
        "Player Carry Person",  // 38
        "Specific Item Held",   // 39
        "Random",               // 40
        "Player Fires Gun",     // 41
        "Darci Grabbed",        // 42
        "Punched And Kicked",   // 43
        "Move Radius Dir"       // 44
    };

        /// <summary>
        /// On-trigger behavior names
        /// </summary>
        public static readonly string[] OnTriggerNames =
        {
        "None",
        "Active",
        "Active While",
        "Active Time",
        "Active Die"
    };

        /// <summary>
        /// Player type names
        /// </summary>
        public static readonly string[] PlayerTypeNames =
        {
        "Darci",
        "Roper",
        "Cop",
        "Gang Member"
    };

        /// <summary>
        /// Enemy type names (23 types)
        /// </summary>
        public static readonly string[] EnemyTypeNames =
        {
        "Thug",
        "Thug (Knife)",
        "Thug (Baseball Bat)",
        "Thug (Pistol)",
        "Gang Member",
        "Gang Member (Knife)",
        "Gang Member (Uzi)",
        "Gang Member (Shotgun)",
        "Cop",
        "Cop (Armed)",
        "SWAT",
        "Army",
        "MIB",
        "Roper",
        "Midas Boss",
        "Psycho",
        "Dog",
        "Bouncer",
        "Mechanic",
        "Sirene",
        "Balrog Boss",
        "Bum",
        "Civilian"
    };

        /// <summary>
        /// Enemy AI type names (22 types)
        /// </summary>
        public static readonly string[] EnemyAINames =
        {
        "None",
        "Idle",
        "Walk",
        "Run",
        "Jog",
        "Sprint",
        "Attack",
        "Defend",
        "Flee",
        "Follow",
        "Guard",
        "Patrol",
        "Search",
        "Wander",
        "Cover",
        "Sniper",
        "Ambush",
        "Flank",
        "Charge",
        "Retreat",
        "Support",
        "Aggressive"
    };

        /// <summary>
        /// Enemy movement type names
        /// </summary>
        public static readonly string[] EnemyMoveNames =
        {
        "Stand",
        "Patrol Ordered",
        "Patrol Random",
        "Wander",
        "Follow",
        "Follow On See",
        "Follow On See 2",
        "Assassin",
        "Bodyguard",
        "Genocide"
    };

        /// <summary>
        /// Vehicle type names (11 types)
        /// </summary>
        public static readonly string[] VehicleTypeNames =
        {
        "Car",
        "Van",
        "Taxi",
        "Helicopter",
        "Motorbike",
        "Ambulance",
        "Fire Engine",
        "Police Car",
        "Truck",
        "Boat",
        "Hovercraft"
    };

        /// <summary>
        /// Vehicle behavior names
        /// </summary>
        public static readonly string[] VehicleBehaviorNames =
        {
        "Player Drives",
        "Patrol",
        "Guard",
        "Track"
    };

        /// <summary>
        /// Vehicle key type names
        /// </summary>
        public static readonly string[] VehicleKeyNames =
        {
        "Unlocked",
        "Red Key",
        "Blue Key",
        "Green Key",
        "Black Key",
        "White Key",
        "Locked"
    };

        /// <summary>
        /// Item type names (25 types)
        /// </summary>
        public static readonly string[] ItemTypeNames =
        {
        "Key",
        "Pistol",
        "Health",
        "Shotgun",
        "Knife",
        "AK47",
        "Grenade",
        "Baseball Bat",
        "Crowbar",
        "Medi Pack",
        "Ammo",
        "Long Shotgun",
        "Night Stick",
        "Colt 45",
        "Plank of Wood",
        "Flamethrower",
        "Grease",
        "Wire",
        "Demo Ball",
        "Missile Air Strike",
        "Balloon",
        "Time Bomb",
        "Dart",
        "Milk Float",
        "Fire Extinguisher"
    };

        /// <summary>
        /// Creature type names
        /// </summary>
        public static readonly string[] CreatureTypeNames =
        {
        "Bat",
        "Gargoyle",
        "Balrog",
        "Bane"
    };

        /// <summary>
        /// Barrel type names
        /// </summary>
        public static readonly string[] BarrelTypeNames =
        {
        "Barrel",
        "Cone",
        "Bin",
        "Burning Barrel",
        "Burning Bin",
        "Drums",
        "Burning Drums"
    };

        /// <summary>
        /// Bomb type names
        /// </summary>
        public static readonly string[] BombTypeNames =
        {
        "Dynamite Stick",
        "Egg Timer",
        "Hi-Tech LED"
    };

        /// <summary>
        /// Trap type names
        /// </summary>
        public static readonly string[] TrapTypeNames =
        {
        "Steam Jet"
    };

        /// <summary>
        /// Trap axis names
        /// </summary>
        public static readonly string[] TrapAxisNames =
        {
        "Forward",
        "Up",
        "Down"
    };

        /// <summary>
        /// Spot FX type names
        /// </summary>
        public static readonly string[] SpotFXNames =
        {
        "Water Fountain",
        "Water Drip",
        "Smoke",
        "Smoke Heavy"
    };

        /// <summary>
        /// Warehouse FX type names
        /// </summary>
        public static readonly string[] WareFXNames =
        {
        "Silence",
        "Police HQ",
        "Restaurant",
        "Hi-Tech",
        "Club Music"
    };

        /// <summary>
        /// Sign type names
        /// </summary>
        public static readonly string[] SignTypeNames =
        {
        "U-Turn",
        "Right Turn",
        "Ahead",
        "Stop"
    };

        /// <summary>
        /// Activate prim type names
        /// </summary>
        public static readonly string[] ActivatePrimNames =
        {
        "Door",
        "Electric Fence",
        "Security Camera",
        "Anim Prim"
    };

        /// <summary>
        /// Camera move type names
        /// </summary>
        public static readonly string[] CameraMoveNames =
        {
        "Normal",
        "Smooth",
        "Wobbly"
    };

        /// <summary>
        /// Camera type names
        /// </summary>
        public static readonly string[] CameraTypeNames =
        {
        "Normal",
        "Security Cam",
        "Camcorder",
        "News",
        "Targetting"
    };

        /// <summary>
        /// Target type names
        /// </summary>
        public static readonly string[] TargetTypeNames =
        {
        "Normal",
        "Attached",
        "Nearest Living"
    };

        /// <summary>
        /// Dynamic light type names
        /// </summary>
        public static readonly string[] DynamicLightNames =
        {
        "Flashing On/Off",
        "Two Tone",
        "Disco",
        "Flame"
    };

        /// <summary>
        /// Message speaker type names
        /// </summary>
        public static readonly string[] MessageSpeakerNames =
        {
        "Radio",
        "Street Name",
        "Tutorial"
    };

        /// <summary>
        /// Bonus point type names
        /// </summary>
        public static readonly string[] BonusPointTypeNames =
        {
        "Primary",
        "Secondary",
        "Bonus"
    };

        /// <summary>
        /// Waypoint color names
        /// </summary>
        public static readonly string[] ColorNames =
        {
        "Black",
        "White",
        "Red",
        "Yellow",
        "Green",
        "Light Blue",
        "Blue",
        "Purple",
        "Mark's Pink",
        "Burnt Umber",
        "Deep Purple",
        "Soylent Green",
        "Terracotta",
        "Mint",
        "Rat's Piss"
    };

        /// <summary>
        /// Group letter names (A-Z)
        /// </summary>
        public static string GetGroupName(byte group)
        {
            if (group > 25) return "?";
            return ((char)('A' + group)).ToString();
        }

        /// <summary>
        /// Get waypoint type name safely
        /// </summary>
        public static string GetWaypointTypeName(WaypointType type)
        {
            int index = (int)type;
            if (index >= 0 && index < WaypointTypeNames.Length)
                return WaypointTypeNames[index];
            return $"Unknown ({index})";
        }

        /// <summary>
        /// Get trigger type name safely
        /// </summary>
        public static string GetTriggerTypeName(TriggerType type)
        {
            int index = (int)type;
            if (index >= 0 && index < TriggerTypeNames.Length)
                return TriggerTypeNames[index];
            return $"Unknown ({index})";
        }
    }
}