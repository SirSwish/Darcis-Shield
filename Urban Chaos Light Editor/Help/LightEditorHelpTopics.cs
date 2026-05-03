using System.Collections.Generic;
using UrbanChaosEditor.Shared.Views.Help;

namespace UrbanChaosLightEditor.Help;

public static class LightEditorHelpTopics
{
    private const string AssemblyName = "Urban Chaos Light Editor";

    public static IReadOnlyList<HelpTopic> All { get; } =
    [
        HelpTopic.FromResource("GettingStarted", "Getting Started", AssemblyName),
        HelpTopic.FromResource("AddingEditingLights", "Adding & Editing Lights", AssemblyName),
        HelpTopic.FromResource("MapProperties", "Map Properties", AssemblyName),
        HelpTopic.FromResource("Using3DPreview", "Using 3D Preview", AssemblyName)
    ];
}
