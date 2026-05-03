using System.Collections.Generic;
using UrbanChaosEditor.Shared.Views.Help;

namespace UrbanChaosPrimEditor.Help;

public static class PrimEditorHelpTopics
{
    private const string AssemblyName = "UrbanChaosPrimEditor";

    public static IReadOnlyList<HelpTopic> All { get; } =
    [
        HelpTopic.FromResource("OverviewGettingStarted", "Overview / Getting Started", AssemblyName),
        HelpTopic.FromResource("CreatingPrmObjects", "Creating PRM Objects", AssemblyName),
        HelpTopic.FromResource("EditingPrmObjects", "Editing PRM Objects", AssemblyName),
        HelpTopic.FromResource("TexturingPrmObjects", "Texturing PRM Objects", AssemblyName),
        HelpTopic.FromResource("ExportingPrmObjects", "Exporting PRM Objects", AssemblyName)
    ];
}
