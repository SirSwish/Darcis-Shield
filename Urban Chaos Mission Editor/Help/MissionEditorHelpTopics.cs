using System.Collections.Generic;
using UrbanChaosEditor.Shared.Views.Help;

namespace UrbanChaosMissionEditor.Help;

public static class MissionEditorHelpTopics
{
    private const string AssemblyName = "UrbanChaosMissionEditor";

    public static IReadOnlyList<HelpTopic> All { get; } =
    [
        HelpTopic.FromResource("OverviewGettingStarted", "Overview / Getting Started", AssemblyName),
        HelpTopic.FromResource("StartingNewMission", "Starting a New Mission", AssemblyName),
        HelpTopic.FromResource("EventPoints", "Event Points", AssemblyName,
        [
            HelpTopic.FromResource("PlayerEventPoint", "Create Player Event Point", AssemblyName),
            HelpTopic.FromResource("NpcEventPoints", "NPC Event Points", AssemblyName),
            HelpTopic.FromResource("CreateCreatureEventPoint", "Create Creature Event Point", AssemblyName),
            HelpTopic.FromResource("CreateVehicleEventPoint", "Create Vehicle Event Point", AssemblyName),
            HelpTopic.FromResource("CameraEventPoints", "Camera Event Points", AssemblyName),
            HelpTopic.FromResource("SimpleWaypointEventPoint", "Simple Waypoint Event Point", AssemblyName),
            HelpTopic.FromResource("MessageEventPoint", "Message Event Point", AssemblyName),
            HelpTopic.FromResource("ConversationEventPoint", "Conversation Event Point", AssemblyName),
            HelpTopic.FromResource("NavBeaconEventPoint", "Nav Beacon Event Point", AssemblyName),
            HelpTopic.FromResource("SoundEffectEventPoint", "Sound Effect Event Point", AssemblyName),
            HelpTopic.FromResource("VisualEffectEventPoint", "Visual Effect Event Point", AssemblyName),
            HelpTopic.FromResource("SpotEffectEventPoint", "Spot Effect Event Point", AssemblyName),
            HelpTopic.FromResource("SignEventPoint", "Sign Event Point", AssemblyName),
            HelpTopic.FromResource("CreateBarrelEventPoint", "Create Barrel Event Point", AssemblyName),
            HelpTopic.FromResource("CreateItemMakeSearchableEventPoints", "Create Item / Make Searchable Event Points", AssemblyName),
            HelpTopic.FromResource("CreateTreasureEventPoint", "Create Treasure Event Point", AssemblyName),
            HelpTopic.FromResource("CreateBonusPointsEventPoint", "Create Bonus Points Event Point", AssemblyName),
            HelpTopic.FromResource("TrapsEventPoint", "Create Traps Event Point", AssemblyName),
            HelpTopic.FromResource("CreateMistEventPoint", "Create Mist Event Point", AssemblyName),
            HelpTopic.FromResource("ActivatePrimEventPoint", "Activate Prim Event Point", AssemblyName),
            HelpTopic.FromResource("AdjustNpcEventPoint", "Adjust NPC Event Point", AssemblyName),
            HelpTopic.FromResource("KillWaypointEventPoint", "Kill Waypoint Event Point", AssemblyName),
            HelpTopic.FromResource("MoveThingEventPoint", "Move Thing Event Point", AssemblyName),
            HelpTopic.FromResource("TransferPlayerEventPoint", "Transfer Player Event Point", AssemblyName),
            HelpTopic.FromResource("LockVehicleEventPoint", "Lock Vehicle Event Point", AssemblyName),
            HelpTopic.FromResource("StallCarEventPoint", "Stall Car Event Point", AssemblyName),
            HelpTopic.FromResource("LinkPlatformEventPoint", "Link Platform Event Point", AssemblyName),
            HelpTopic.FromResource("GroupLifeEventPoint", "Group Life Event Point", AssemblyName),
            HelpTopic.FromResource("GroupDeathEventPoint", "Group Death Event Point", AssemblyName),
            HelpTopic.FromResource("GroupResetEventPoint", "Group Reset Event Point", AssemblyName),
            HelpTopic.FromResource("ConePenaltiesEventPoint", "Cone Penalties Event Point", AssemblyName),
            HelpTopic.FromResource("IncrementEventPoint", "Increment Event Point", AssemblyName),
            HelpTopic.FromResource("ResetCounterEventPoint", "Reset Counter Event Point", AssemblyName),
            HelpTopic.FromResource("CountUpTimerEventPoint", "Count Up Timer Event Point", AssemblyName),
            HelpTopic.FromResource("ExtendTimerEventPoint", "Extend Timer Event Point", AssemblyName),
            HelpTopic.FromResource("MakePersonPeeEventPoint", "Make Person Pee Event Point", AssemblyName),
            HelpTopic.FromResource("WarehouseFxEventPoint", "Warehouse FX Event Point", AssemblyName),
            HelpTopic.FromResource("NoFloorEventPoint", "No Floor Event Point", AssemblyName),
            HelpTopic.FromResource("ShakeCameraEventPoint", "Shake Camera Event Point", AssemblyName),
            HelpTopic.FromResource("WinLoseEventPoints", "Win and Lose Event Points", AssemblyName),
            HelpTopic.FromResource("LegacyEventPoints", "Legacy Event Points", AssemblyName)
        ]),
        HelpTopic.Group("Triggers",
        [
            HelpTopic.FromResource("TriggerSettingsReference", "Trigger Usage Reference", AssemblyName),
            HelpTopic.FromResource("Triggers/ActiveListeningMode", "Active Listening Mode", AssemblyName),
            HelpTopic.FromResource("Triggers/ActiveWhileListeningMode", "Active While Listening Mode", AssemblyName),
            HelpTopic.FromResource("Triggers/ActiveTimeListeningMode", "Active Time Listening Mode", AssemblyName),
            HelpTopic.FromResource("Triggers/ActiveDieListeningMode", "Active Die Listening Mode", AssemblyName),
            HelpTopic.FromResource("Triggers/NoneTrigger", "None Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/DependencyTrigger", "Dependency Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/RadiusTrigger", "Radius Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/CuboidTrigger", "Cuboid Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/PersonUsedTrigger", "Person Used Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/KilledTrigger", "Killed Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/ConversationOverTrigger", "Conversation Over Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/BooleanAndTrigger", "Boolean AND Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/BooleanOrTrigger", "Boolean OR Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/CounterTrigger", "Counter Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/CountdownTrigger", "Countdown Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/VisibleCountdownTrigger", "Visible Countdown Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/EnemyRadiusTrigger", "Enemy Radius Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/PersonSeenTrigger", "Person Seen Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/PlayerUsesRadiusTrigger", "Player Uses Radius Trigger", AssemblyName),
            HelpTopic.FromResource("Triggers/GroupDeadTrigger", "Group Dead Trigger", AssemblyName)
        ]),
        HelpTopic.FromResource("Zones", "Zones", AssemblyName),
        HelpTopic.FromResource("Map_And_Lights", "Map & Lights Overlays", AssemblyName),
        HelpTopic.FromResource("Keyboard_Shortcuts", "Keyboard Shortcuts", AssemblyName)
    ];
}
