using System.Collections.Generic;
using UrbanChaosEditor.Shared.Views.Help;

namespace UrbanChaosStoryboardEditor.Help;

public static class StoryboardEditorHelpTopics
{
    private const string AssemblyName = "Urban Chaos Storyboard Editor";

    public static IReadOnlyList<HelpTopic> All { get; } =
    [
        HelpTopic.FromResource("GettingStartedOverview", "Getting Started / Overview", AssemblyName),
        HelpTopic.FromResource("ManagingDistricts", "Managing Districts", AssemblyName),
        HelpTopic.FromResource("ManagingMissions", "Managing Missions", AssemblyName),
        HelpTopic.FromResource("Export", "Export", AssemblyName)
    ];
}
