using System.Collections.Generic;
using UrbanChaosEditor.Shared.Views.Help;

namespace UrbanChaosStyleEditor.Help;

public static class StyleEditorHelpTopics
{
    private const string AssemblyName = "UrbanChaosStyleEditor";

    public static IReadOnlyList<HelpTopic> All { get; } =
    [
        HelpTopic.FromResource("OverviewGettingStarted", "Overview / Getting Started", AssemblyName),
        HelpTopic.FromResource("AddingTextures", "Adding Textures", AssemblyName),
        HelpTopic.FromResource("EditingTextures", "Editing Textures", AssemblyName),
        HelpTopic.FromResource("TextureProperties", "Texture Properties", AssemblyName),
        HelpTopic.FromResource("Styles", "Styles", AssemblyName),
        HelpTopic.FromResource("Exporting", "Exporting", AssemblyName)
    ];
}
