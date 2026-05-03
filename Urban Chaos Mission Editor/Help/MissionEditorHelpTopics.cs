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
        HelpTopic.FromResource("EventPoints", "Event Points", AssemblyName),
        HelpTopic.FromResource("PlayerEventPoint", "Create Player Event Point", AssemblyName),
        HelpTopic.FromResource("NpcEventPoints", "NPC Event Points", AssemblyName),
        HelpTopic.FromResource("CreateCreatureEventPoint", "Create Creature Event Point", AssemblyName),
        HelpTopic.FromResource("LegacyEventPoints", "Legacy Event Points", AssemblyName),
        HelpTopic.FromResource("Zones", "Zones", AssemblyName),
        HelpTopic.FromResource("Map_And_Lights", "Map & Lights Overlays", AssemblyName),
        HelpTopic.FromResource("Keyboard_Shortcuts", "Keyboard Shortcuts", AssemblyName)
    ];
}
