// ============================================================
// UrbanChaosEditor.Shared/Constants/ApplicationConstants.cs
// ============================================================
// Cross-editor application and resource constants.

namespace UrbanChaosEditor.Shared.Constants;

/// <summary>
/// Application-level constants shared by editor projects.
/// </summary>
public static class ApplicationConstants
{
    /// <summary>Shared resource assembly name used by pack URIs.</summary>
    public const string SharedAssemblyName = "UrbanChaosEditor.Shared";

    /// <summary>Default file polling interval for external-change watchers.</summary>
    public const int ExternalFilePollIntervalMs = 1000;

    /// <summary>Display name for the map editor.</summary>
    public const string MapEditorAppName = "Urban Chaos Map Editor";
}
