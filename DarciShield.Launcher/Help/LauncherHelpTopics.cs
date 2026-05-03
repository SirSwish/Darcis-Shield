using System.Collections.Generic;
using UrbanChaosEditor.Shared.Views.Help;

namespace DarciShield.Launcher.Help;

public static class LauncherHelpTopics
{
    private const string AssemblyName = "DarciShield.Launcher";

    public static IReadOnlyList<HelpTopic> All { get; } =
    [
        HelpTopic.FromResource("Launcher_Overview", "Launcher Overview", AssemblyName),
        HelpTopic.FromResource("Editor_Roles", "Editor Roles", AssemblyName)
    ];
}
