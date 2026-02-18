namespace UrbanChaosMissionEditor.Constants;

/// <summary>
/// String arrays for display names (translated from EdStrings.cpp)
/// </summary>
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
        "End Game Lose",         // 18
        "Shout",                // 19
        "Activate Prim",        // 20
        "Create Trap",          // 21
        "Adjust Enemy",         // 22
        "Link Platform",        // 23
        "Create Bomb",          // 24
        "Burn Prim",            // 25
        "End Game Win",        // 26
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
    /// Enemy type names (23 types) - from wenemy_strings in EdStrings.cpp
    /// </summary>
    public static readonly string[] EnemyTypeNames =
    {
        "Old Civilian",                    // ET_CIV = 1
        "Young Civilian",       // ET_CIV_BALLOON = 2
        "Prostitute",                  // ET_SLAG = 3
        "Fat Prostitute",         // ET_UGLY_FAT_SLAG = 4
        "Workman",                     // ET_WORKMAN = 5
        "Mako",                  // ET_GANG_RASTA = 6
        "Bish",                    // ET_GANG_RED = 7
        "McClean",                   // ET_GANG_GREY = 8
        "Mako (Pistol)",      // ET_GANG_RASTA_PISTOL = 9
        "Bish (Shotgun)",       // ET_GANG_RED_SHOTGUN = 10
        "McClean (M16)",         // ET_GANG_GREY_AK47 = 11
        "Cop",                         // ET_COP = 12
        "Cop (Pistol)",             // ET_COP_PISTOL = 13
        "Cop (Shotgun)",            // ET_COP_SHOTGUN = 14
        "Cop (M16)",               // ET_COP_AK47 = 15
        "Gordansky",                     // ET_HOSTAGE = 16
        "Workman (Grenade)",        // ET_WORKMAN_GRENADE = 17
        "Wild Bill",                       // ET_TRAMP = 18
        "M.I.B 1",                     // ET_MIB1 = 19
        "M.I.B 2",                     // ET_MIB2 = 20
        "M.I.B 3",                     // ET_MIB3 = 21
        "Darci",                       // ET_DARCI = 22
        "Roper"                        // ET_ROPER = 23
    };

    /// <summary>
    /// Enemy AI type names - from wenemy_ai_strings in EdStrings.cpp
    /// </summary>
    public static readonly string[] EnemyAINames =
    {
        "Nothing",
        "Civilian",
        "Guard an area",
        "Assassin",
        "Boss/Captain",
        "Cop",
        "Violent youth",
        "Guard a door",
        "Bodyguard",
        "Driver",
        "Bomb-disposer",
        "Biker",
        "Fight-test dummy",
        "Bully",
        "Cop driver",
        "Suicide",
        "Flee player",
        "Group Genocide",
        "M.I.B.",
        "Summoner",
        "Hypochondriac",
        "Shoot dead assassin"
    };

    /// <summary>
    /// Enemy movement type names - from wenemy_move_strings in EdStrings.cpp
    /// </summary>
    public static readonly string[] EnemyMoveNames =
    {
        "Stand Still",
        "Patrol Waypts (in order)",
        "Patrol Waypts (randomly)",
        "Wander",
        "Follow",
        "Warm hands",
        "Follow on see",
        "Dance",
        "Hands up",
        "Tied up"
    };

    /// <summary>
    /// Enemy ability level names - from wenemy_ability_strings in EdStrings.cpp
    /// </summary>
    public static readonly string[] EnemyAbilityNames =
    {
        "Default",
        "1 (weak)",
        "2", "3", "4", "5", "6", "7",
        "8 (average)",
        "9", "10", "11", "12", "13", "14",
        "15 (badass)"
    };

    /// <summary>
    /// Enemy flag names - from wenemy_flag_strings in EdStrings.cpp
    /// Bit flags in Data[4]
    /// </summary>
    public static readonly string[] EnemyFlagNames =
    {
        "Lazy",                // bit 0
        "Diligent",            // bit 1
        "Gang",                // bit 2
        "Fight Back",          // bit 3
        "Just kill player",    // bit 4
        "Robotic",             // bit 5
        "Restricted Movement", // bit 6
        "Only player kills",   // bit 7
        "Blue Zone",           // bit 8
        "Cyan Zone",           // bit 9
        "Yellow Zone",         // bit 10
        "Magenta Zone",        // bit 11
        "Invulnerable",        // bit 12
        "Guilty",              // bit 13
        "Fake wandering",      // bit 14
        "Can be carried"       // bit 15
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
        "Police Sedan",
        "Ambulance",
        "Police SUV",
        "Civ SUV",
        "Soft-Top Car",
        "Wildcat Van"
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
    /// Visual effect type names - from wvfx_strings in EdStrings.cpp
    /// These are used as a bitmask (multiple can be selected)
    /// </summary>
    public static readonly string[] VisualEffectNames =
    {
        "Flare",
        "Fire Dome",
        "Shockwave",
        "Smoke Trails",
        "Bonfire"
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
        "M16",
        "Land Mine",
        "Baseball Bat",
        "Oil Barrel",
        "Pistol Ammo",
        "Shotgun Ammo",
        "M16 Ammo",
        "Napkin",
        "Red Book",
        "Floppy Disk",
        "M16 (Non Functional)",
        "Key",
        "Key",
        "VHS Tape",
        "Nothing",
        "Weed Away",
        "Grenade",
        "Explosives",
        "Key",
        "Inferno"
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
        "Barrel",           // BT_BARREL = 0
        "Traffic cone",     // BT_TRAFFIC_CONE = 1
        "Bin",              // BT_BIN = 2
        "Burning barrel",   // BT_BURNING_BARREL = 3
        "Burning bin",      // BT_BURNING_BIN = 4
        "Oil drum",         // BT_OIL_DRUM = 5
        "LOX drum"          // BT_LOX_DRUM = 6
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
        "Dark Smokestack Smoke",
        "Light Chimney Smoke"
    };

    /// <summary>
    /// Warehouse FX type names
    /// </summary>
    public static readonly string[] WareFXNames =
    {
        "Silence",
        "Police HQ",
        "Restaurant",
        "Hi-Tech/Office",
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

    public static readonly string[] PlatformFlagNames =
    {
        "Lock to axis",
        "Lock rotation",
        "Is a rocket"
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

    // TEMP TEST LIST (first 20 SFX + the music01 entries that match "music" filter)
    public static readonly string[] SoundList =
    {
        "NULL.wav",
    "f_WIND1.wav",
    "f_WIND3.wav",
    "f_WIND4.wav",
    "f_WIND5.wav",
    "f_WIND2.wav",
    "f_WIND6.wav",
    "f_WIND7.wav",
    "f_THNDR1.wav",
    "f_THNDR2.wav",
    "f_LGTNG1.wav",
    "f_LGTNG2.wav",
    "f_RAIN.wav",
    "f_WETAMB.wav",
    "f_CTYAMB.wav",
    "f_siren1.wav",
    "f_siren2.wav",
    "f_siren3.wav",
    "amb_car1.wav",
    "f_pDUD.wav",
    "f_pSHOT1.wav",
    "f_pSHOT2.wav",
    /*"silencer_m&p9mmpistolsuppressed.wav",
    "silencer_m&p9mmpistolsuppressed.wav",*/
    "f_RICO1.wav",
    "f_RICO2.wav",
    "f_RICO3.wav",
    "f_RICO4.wav",
    "f_RICO5.wav",
    "f_RICO6.wav",
    "f_fDIE1.wav",
    "f_fDIE2.wav",
    "f_mDIE1.wav",
    "f_mDIE2.wav",
    "f_PUNCH1.wav",
    "f_PUNCH2.wav",
    "f_PUNCH3.wav",
    "f_PUNCH4.wav",
    "f_PUNCH5.wav",
    "f_NCKBRK.wav",
    "f_ftBOX1.wav",
    "f_ftBOX2.wav",
    "f_ftBOX3.wav",
    "f_ftBOX4.wav",
    "f_ftCAR1.wav",
    "f_ftCAR2.wav",
    "f_ftCAR3.wav",
    "f_ftCAR4.wav",
    "f_ftGRS1.wav",
    "f_ftGRS2.wav",
    "f_ftGRS3.wav",
    "f_ftGRS4.wav",
    "f_ftGVL1.wav",
    "f_ftGVL2.wav",
    "f_ftGVL3.wav",
    "f_ftGVL4.wav",
    "f_ftHRD1.wav",
    "f_ftHRD2.wav",
    "f_ftHRD3.wav",
    "f_ftHRD4.wav",
    "f_ftSTL1.wav",
    "f_ftSTL2.wav",
    "f_ftSTL3.wav",
    "f_ftSTL4.wav",
    "f_ftwet1.wav",
    "f_ftwet2.wav",
    "f_ftwet3.wav",
    "f_ftwet4.wav",
    "f_ftSWR1.wav",
    "f_ftSWR2.wav",
    "f_ftSWR3.wav",
    "f_ftSWR4.wav",
    "clmSWR1.wav",
    "f_ELEC1.wav",
    "f_ELEC2.wav",
    "f_ELEC3.wav",
    "f_ELEC4.wav",
    "f_ELEC5.wav",
    "f_CanKIK.wav",
    "car_strt.wav",
    "car_idle.wav",
    "f_cACC1.wav",
    "f_cACC2.wav",
    "f_cACC3.wav",
    "car_stop.wav",
    "van_strt.wav",
    "van_idle.wav",
    "van_stop.wav",
    "f_RAT1.wav",
    "f_RAT2.wav",
    "f_RAT3.wav",
    "f_RAT4.wav",
    "f_RAT5.wav",
    "f_DOG1.wav",
    "f_DOG2.wav",
    "f_DOG3.wav",
    "f_CAT1.wav",
    "f_CAT2.wav",
    "f_CAT3.wav",
    "f_CAT4.wav",
    "f_MANHOL.wav",
    "f_SWITCH.wav",
    "f_FIRE.wav",
    "f_EXPLD1.wav",
    "f_EXPLD2.wav",
    "f_EXPLD3.wav",
    "amb_expl.wav",
    "ExploFireBall1.wav",
    "ExploMed1.wav",
    "ExploLrg1.wav",
    "ExploMetal1.wav",
    "heli.wav",
    "whoryou.wav",
    "thumpsquish.wav",
    "hevywatr.wav",
    "beep.wav",
    "getouttheway.wav",
    "f_knSWP1.wav",
    "f_knSWP2.wav",
    "f_knSWP3.wav",
    "f_knSWP4.wav",
    "f_sLOAD.wav",
    "f_sPUMP.wav",
    "f_sSHOT1.wav",
    "f_MENU09.wav",
    "f_MENU10.wav",
    "f_MENU11.wav",
    "f_MENU04.wav",
    "f_MENU13.wav",
    "f_MENU14.wav",
    "Skid1.wav",
    "Skid2.wav",
    "Skid3.wav",
    "SlowSkidx1.wav",
    "SlowSkidx2.wav",
    "Impact1.wav",
    "Impact2.wav",
    "Impact3.wav",
    "MedCrash2.wav",
    "ImpactSml.wav",
    "CarXPullAway.wav",
    "CarXLoop.wav",
    "CarXDecel.wav",
    "CarXDecel2.wav",
    "CarXIdle.wav",
    "CarXEnd.wav",
    "Suspension1.wav",
    "Suspension2.wav",
    "Suspension3.wav",
    "Suspension4.wav",
    "Horn1.wav",
    "Horn2.wav",
    "Horn3.wav",
    "Scrape3.wav",
    "Scrape5.wav",
    "Scrape6.wav",
    "Scrape4.wav",
    "Cone1.wav",
    "crow3.wav",
    "ZipWire.wav",
    "darci\\under arrest.WAV",
    "darci\\die 1.WAV",
    "darci\\die 2.WAV",
    "darci\\die 3.WAV",
    "darci\\death breath.WAV",
    "darci\\effort 1.WAV",
    "darci\\effort 2.WAV",
    "darci\\effort 3.WAV",
    "darci\\short effort.wav",
    "darci\\hit 1.WAV",
    "darci\\hit 2.WAV",
    "darci\\hit 3.WAV",
    "darci\\hit 4.WAV",
    "darci\\hit 5.WAV",
    "darci\\scream fall 2.WAV",
    "darci\\scream fall.WAV",
    "darci\\no scream 2.WAV",
    "darci\\no scream.WAV",
    "darci\\running breath.WAV",
    "darci\\lookout warning.WAV",
    "darci\\lookout aggr.WAV",
    "darci\\hey.WAV",
    "darci\\hey 2.WAV",
    "darci\\hi.WAV",
    "darci\\hi 2.WAV",
    "darci\\see ya 2.WAV",
    "darci\\freeze.wav",
    "darci\\freeze tk2.wav",
    "darci\\stop police tk2.wav",
    "roper\\hit1.WAV",
    "roper\\hit2.WAV",
    "roper\\hit3.WAV",
    "roper\\hit4.WAV",
    "roper\\hit5.WAV",
    "roper\\hit6.WAV",
    "roper\\hit7.WAV",
    "roper\\effort1.WAV",
    "roper\\effort2.WAV",
    "roper\\effort3.WAV",
    "roper\\effort4.WAV",
    "roper\\short effort.wav",
    "roper\\roper fall1.WAV",
    "roper\\roper fall2.WAV",
    "roper\\roper fall3.WAV",
    "roper\\you're busted tk2.WAV",
    "roper\\run breath.WAV",
    "roper\\heyhello.WAV",
    "roper\\darci ack.WAV",
    "roper\\darci question.WAV",
    "roper\\darci overhere.WAV",
    "roper\\have nice day2.WAV",
    "borrowed\\heartfail.wav",
    "bthug1\\huh 1.wav",
    "bthug1\\huh 2.wav",
    "bthug1\\whats that.wav",
    "bthug1\\whats that 2.wav",
    "bthug1\\did you hear.wav",
    "bthug1\\whos there.wav",
    "bthug1\\whos there 2.wav",
    "bthug1\\hey you.wav",
    "bthug1\\hey you 2.wav",
    "bthug1\\there they are.wav",
    "bthug1\\there they are 2.wav",
    "bthug1\\over there.wav",
    "bthug1\\over there 2.wav",
    "wthug1\\huh.wav",
    "wthug1\\huh 2.wav",
    "wthug1\\huh 3.wav",
    "wthug1\\what was that.wav",
    "wthug1\\what was that 2.wav",
    "wthug1\\did you hear that.wav",
    "wthug1\\did you hear that 2.wav",
    "wthug1\\whos there.wav",
    "wthug1\\whos there 2.wav",
    "wthug1\\hey you.wav",
    "wthug1\\hey you 2.wav",
    "wthug1\\over there.wav",
    "wthug1\\over there 2.wav",
    "wthug1\\there they are.wav",
    "wthug1\\there they are 2.wav",
    "wthug2\\huh 1.wav",
    "wthug2\\huh 2.wav",
    "wthug2\\what was that.wav",
    "wthug2\\what was that2.wav",
    "wthug2\\you hear that.wav",
    "wthug2\\you hear that 2.wav",
    "wthug2\\whos there.wav",
    "wthug2\\whos there 2.wav",
    "wthug2\\hey you.wav",
    "wthug2\\hey you 2.wav",
    "wthug2\\over there.wav",
    "wthug2\\over there 2.wav",
    "wthug2\\there they are.wav",
    "wthug2\\there they are 2.wav",
    "music01\\Sneak.wav",
    "music01\\Sewer.wav",
    "GeneralMusic\\ChaosTheEnd.wav",
    "GeneralMusic\\Club1.wav",
    "Sfx\\DangerGreen.wav",
    "Sfx\\DangerYellow.wav",
    "Sfx\\DangerRed.wav",
    "music01\\darci\\Fight1.wav",
    "music01\\darci\\Fight2.wav",
    "music01\\darci\\Sprint1.wav",
    "music01\\darci\\Sprint2.wav",
    "music01\\darci\\Crawl.wav",
    "music01\\darci\\DrivingStart.wav",
    "music01\\darci\\Driving1.wav",
    "music01\\darci\\Driving2.wav",
    "GeneralMusic\\Timer122.wav",
    "music01\\Panic.wav",
    "music01\\Music1.wav",
    "music01\\Music2.wav",
    "music01\\Music3.wav",
    "General\\CopSiren1.wav",
    "General\\AttackDog.wav",
    "General\\Angdog.wav",
    "General\\Dog.wav",
    "General\\Dog2.wav",
    "General\\FireCrackle1.wav",
    "General\\FireHydrant.wav",
    "Footsteps\\GlassStep1.wav",
    "Footsteps\\GlassStep2.wav",
    "Footsteps\\GlassStep3.wav",
    "Footsteps\\GlassStep4.wav",
    "Footsteps\\GlassLand1.wav",
    "Footsteps\\GrassyStep1.wav",
    "Footsteps\\GrassyStep2.wav",
    "Footsteps\\GrassyStep3.wav",
    "Footsteps\\GrassyStep4.wav",
    "Footsteps\\GravelLand1.wav",
    "Footsteps\\MetalLandHeavy1.wav",
    "Footsteps\\MetalLandLite1.wav",
    "Footsteps\\StreetLand1.wav",
    "Footsteps\\WaterLand1.wav",
    "Footsteps\\WaterOut1.wav",
    "Footsteps\\WetLand1.wav",
    "Footsteps\\WoodLand1.wav",
    "Footsteps\\SnowStep1.wav",
    "Footsteps\\SnowStep2.wav",
    "Footsteps\\SnowStep3.wav",
    "Footsteps\\SnowStep4.wav",
    "Footsteps\\SnowLand1.wav",
    "Ambience\\BirdSong1.wav",
    "Ambience\\BirdSong2.wav",
    "Ambience\\BirdSong3.wav",
    "Ambience\\BirdSong4.wav",
    "Ambience\\BirdSong5.wav",
    "Ambience\\BirdSong6.wav",
    "Ambience\\Peacock1.wav",
    "Ambience\\SnowWind1.wav",
    "Ambience\\SnowWind5.wav",
    "Ambience\\SnowWind2.wav",
    "Ambience\\SnowWind3.wav",
    "Ambience\\SnowWind4.wav",
    "Ambience\\Wolfhowl.wav",
    "Ambience\\Aircraft1.wav",
    "sfx080799+\\Barrel1.wav",
    "sfx080799+\\Barrel2.wav",
    "sfx080799+\\Barrel3.wav",
    "sfx080799+\\Barrel4.wav",
    "sfx080799+\\Cone1.wav",
    "sfx080799+\\Glass1.wav",
    "sfx080799+\\Glass2.wav",
    "sfx080799+\\Glass3.wav",
    "sfx080799+\\Glass4.wav",
    "sfx080799+\\Glass5.wav",
    "sfx080799+\\HiddenItemRev2.wav",
    "sfx080799+\\Search2.wav",
    "sfx080799+\\Slide1.wav",
    "sfx080799+\\Slide2.wav",
    "sfx080799+\\TrashCan2.wav",
    "Ambience\\BirdCall1.wav",
    "Ambience\\Cockatoo1.wav",
    "Ambience\\Cockatoo2.wav",
    "Ambience\\Cricket.wav",
    "Ambience\\FogHorn.wav",
    "Ambience\\TropicalLoop.wav",
    "GeneralMusic\\Acid.wav",
    "GeneralMusic\\Dead2.wav",
    "GeneralMusic\\RoperIn.wav",
    "6Sound.wav",
    "Ambience\\sirenAmb1.wav",
    "Ambience\\sirenAmb2.wav",
    "Ambience\\trainpass.wav",
    "RadioMessage.wav",
    "KnifeSwish.wav",
    "Stab1.wav",
    "Stab2.wav",
    "Stab3.wav",
    "BatHit1.wav",
    "BatBody1.wav",
    "Batbody2.wav",
    "DarciBatHit1.wav",
    "DarciBatHit2.wav",
    "GeneralMusic\\BriefingLoop.wav",
    "GeneralMusic\\Complete.wav",
    "GeneralMusic\\FrontLoop.wav",
    "DoorOpenCls1.wav",
    "Item1.wav",
    "Bat1.wav",
    "Bat2.wav",
    "Bat3.wav",
    "Bat4.wav",
    "Bat5.wav",
    "Bat6.wav",
    "Balrog\\Growl1.wav",
    "Balrog\\Growl2.wav",
    "Balrog\\Growl3.wav",
    "Balrog\\BaalStep1.wav",
    "Balrog\\BaalStep2.wav",
    "Balrog\\BalrogDeath.wav",
    "Balrog\\roar31.wav",
    "Balrog\\FlameThrown1.wav",
    "General\\Re1.wav",
    "General\\Load1.wav",
    "General\\Click2.wav",
    "General\\Clicks5.wav",
    "General\\Autorifle1.wav",
    "General\\AK1.wav",
    "General\\PickupSwish2.wav",
    "RevCarStartx.wav",
    "RevCarLoopx.wav",
    "RevCarStopx.wav",
    "General\\Bollox1.wav",
    "General\\Bollox3.wav",
    "Ambience\\Howl4.wav",
    "Ambience\\Wolves.wav",
    "Ambience\\Police1.wav",
    "Ambience\\PosheataAmb1.wav",
    "Ambience\\Office1.wav",
    "GeneralMusic\\AlbumTracks\\BleepTune.wav",
    "GeneralMusic\\AlbumTracks\\OomaGabba.wav",
    "GeneralMusic\\AlbumTracks\\FeelinIt.wav",
    "GeneralMusic\\AlbumTracks\\BinaryFinary.wav",
    "GeneralMusic\\AlbumTracks\\TourDeForce.wav",
    "JudoChop1.wav",
    "MIBexplo1.wav",
    "MIBlevitate1.wav",
    "Footsteps\\HardStep1.wav",
    "Footsteps\\HardStep2.wav",
    "Footsteps\\HardStep3.wav",
    "Footsteps\\HardStep4.wav",
    "roper\\ROPER-TAUNT03.wav",
    "roper\\ROPER-TAUNT06.wav",
    "roper\\ROPER-TAUNT04B.wav",
    "roper\\ROPER-TAUNT15.wav",
    "roper\\ROPER-TAUNT17.wav",
    "roper\\ROPER-TAUNT04.wav",
    "roper\\ROPER-TAUNT02.wav",
    "roper\\ROPER-TAUNT05.wav",
    "roper\\ROPER-TAUNT08.wav",
    "roper\\ROPER-TAUNT01.wav",
    "roper\\ROPER-TAUNT10.wav",
    "roper\\ROPER-TAUNT12.wav",
    "roper\\ROPER-TAUNT13.wav",
    "roper\\ROPER-TAUNT16.wav",
    "roper\\ROPER-TAUNT16B.wav",
    "roper\\ROPER-TAUNT19.wav",
    "roper\\ROPER-TAUNT18.wav",
    "roper\\ROPER-TAUNT20.wav",
    "roper\\ROPER-TAUNT09.wav",
    "roper\\ROPER-TAUNT18B.wav",
    "darci\\DARCI-TAUNT09.wav",
    "darci\\DARCI-TAUNT19C.wav",
    "darci\\DARCI-TAUNT19D.wav",
    "darci\\DARCI-TAUNT02B.wav",
    "darci\\DARCI-TAUNT13.wav",
    "darci\\DARCI-TAUNT13B.wav",
    "darci\\DARCI-TAUNT09B.wav",
    "darci\\DARCI-TAUNT01B.wav",
    "darci\\DARCI-TAUNT03.wav",
    "darci\\DARCI-TAUNT06GB.wav",
    "darci\\DARCI-TAUNT07.wav",
    "darci\\DARCI-TAUNT08.wav",
    "darci\\DARCI-TAUNT08B.wav",
    "darci\\DARCI-TAUNT10.wav",
    "darci\\DARCI-TAUNT10B.wav",
    "darci\\DARCI-TAUNT01.wav",
    "darci\\DARCI-TAUNT18.wav",
    "darci\\DARCI-TAUNT18B.wav",
    "darci\\DARCI-TAUNT19.wav",
    "darci\\DARCI-TAUNT19B.wav",
    "darci\\DARCI-TAUNT16.wav",
    "darci\\DARCI-TAUNT04.wav",
    "darci\\DARCI-TAUNT04B.wav",
    "darci\\DARCI-TAUNT16B.wav",
    "darci\\DARCI-TAUNT12.wav",
    "darci\\DARCI-TAUNT17.wav",
    "darci\\DARCI-TAUNT14.wav",
    "darci\\DARCI-TAUNT15B.wav",
    "darci\\DARCI-TAUNT15.wav",
    "darci\\DARCI-TAUNT12B.wav",
    "darci\\DARCI-ARREST02B.wav",
    "darci\\DARCI-ARREST01B.wav",
    "darci\\DARCI-ARREST02.wav",
    "darci\\DARCI-ARREST01.wav",
    "bthug1\\GRASTA-TAUNT07.wav",
    "bthug1\\GRASTA-TAUNT02.wav",
    "bthug1\\GRASTA-TAUNT02B.wav",
    "bthug1\\GRASTA-TAUNT03.wav",
    "bthug1\\GRASTA-TAUNT04.wav",
    "bthug1\\GRASTA-TAUNT04B.wav",
    "bthug1\\GRASTA-TAUNT05.wav",
    "bthug1\\GRASTA-TAUNT06.wav",
    "bthug1\\GRASTA-TAUNT01.wav",
    "wthug1\\GGREY-TAUNT05B.wav",
    "wthug1\\GGREY-TAUNT01B.wav",
    "wthug1\\GGREY-TAUNT02.wav",
    "wthug1\\GGREY-TAUNT02B.wav",
    "wthug1\\GGREY-TAUNT03.wav",
    "wthug1\\GGREY-TAUNT03B.wav",
    "wthug1\\GGREY-TAUNT04.wav",
    "wthug1\\GGREY-TAUNT04B.wav",
    "wthug1\\GGREY-TAUNT05.wav",
    "wthug1\\GGREY-TAUNT01.wav",
    "wthug2\\GRED-TAUNT05B.wav",
    "wthug2\\GRED-TAUNT01B.wav",
    "wthug2\\GRED-TAUNT02B.wav",
    "wthug2\\GRED-TAUNT03B.wav",
    "wthug2\\GRED-TAUNT04B.wav",
    "wthug2\\GRED-TAUNT01.wav",
    "wthug2\\GRED-TAUNT02.wav",
    "wthug2\\GRED-TAUNT03.wav",
    "wthug2\\GRED-TAUNT04.wav",
    "wthug2\\GRED-TAUNT05.wav",
    "cop\\COP-ARREST01.wav",
    "cop\\COP-ARREST02.wav",
    "cop\\COP-ARREST03.wav",
    "cop\\COP-ARREST04.wav",
    "cop\\COP-DIE01.wav",
    "cop\\COP-DIE02B.wav",
    "cop\\COP-DIE03.wav",
    "cop\\COP-DIE04.wav",
    "cop\\COP-DIE04B.wav",
    "cop\\COP-PAIN01.wav",
    "cop\\COP-PAIN02.wav",
    "cop\\COP-PAIN03.wav",
    "cop\\COP-PAIN03B.wav",
    "cop\\COP-PAIN04.wav",
    "cop\\COP-TAUNT08.wav",
    "cop\\COP-TAUNT02.wav",
    "cop\\COP-TAUNT03.wav",
    "cop\\COP-TAUNT04.wav",
    "cop\\COP-TAUNT05.wav",
    "cop\\COP-TAUNT06.wav",
    "cop\\COP-TAUNT07.wav",
    "cop\\COP-TAUNT01.wav",
    "Reckoning1.wav",
    "GeneralMusic\\Bonus.wav",
    "MIBgun1.wav",
    "MIBgun1wd.wav",
    "Footsteps\\SoftStep1.wav",
    "Footsteps\\SoftStep2.wav",
    "Footsteps\\SoftStep3.wav",
    "Footsteps\\SoftStep4.wav",
    "MIBwarning1.wav",
    "Paintgun1.wav",
    "ScreamF1.wav",
    "ScreamF2.wav",
    "ScreamM1.wav",
    "GeneralMusic\\CreditDue.wav",
    "ExploFireball2.wav",
    "ExploFireball3.wav",
    "misc\\GRASTA-PAIN02.wav",
    "misc\\GRASTA-PAIN02B.wav",
    "misc\\GRASTA-PAIN01.wav",
    "misc\\GRASTA-PAIN05.wav",
    "misc\\GRED-PAIN01.wav",
    "misc\\GRED-PAIN01B.wav",
    "misc\\GRED-PAIN02.wav",
    "misc\\GRED-PAIN02B.wav",
    "misc\\HOSTAGE-PAIN01.wav",
    "misc\\HOSTAGE-PAIN01B.wav",
    "misc\\HOSTAGE-PAIN02.wav",
    "misc\\HOSTAGE-PAIN02B.wav",
    "misc\\GWORK-PAIN01.wav",
    "misc\\GWORK-PAIN03.wav",
    "misc\\GWORK-PAIN04.wav",
    "misc\\GWORK-PAIN05.wav",
    "misc\\GGREY-PAIN02.wav",
    "misc\\GGREY-PAIN02B.wav",
    "misc\\GGREY-PAIN05.wav",
    "misc\\GTRAMP-PAIN01.wav",
    "misc\\GTRAMP-PAIN02.wav",
    "misc\\GTRAMP-PAIN03.wav",
    "misc\\GTRAMP-PAIN04.wav",
    "misc\\GTRAMP-PAIN05.wav",
    "GeneralMusic\\AlbumTracks\\ADFoundation.wav",
    "GeneralMusic\\FrontLoop.wav",
    "!",
};

    /// <summary>
    /// Burn/Fire type names (bitmask options) - from wfire_strings in EdStrings.cpp
    /// </summary>
    public static readonly string[] BurnTypeNames =
    {
    "Flickering Flames",
    "Bonfires All Over",
    "Thick Flames",
    "Thick Smoke",
    "Static"
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