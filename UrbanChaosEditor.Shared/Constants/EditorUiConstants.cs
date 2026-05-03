// ============================================================
// UrbanChaosEditor.Shared/Constants/EditorUiConstants.cs
// ============================================================
// Common editor UI measurements.

namespace UrbanChaosEditor.Shared.Constants;

/// <summary>
/// Shared UI measurements used across editor views and overlays.
/// </summary>
public static class EditorUiConstants
{
    public const double DragThreshold = 4.0;
    public const double SurfaceMargin = 25.0;
    public const double WallCanvasHeight = 64.0;
    public const double TexturePanelPixels = 64.0;
    public const int PaletteTileSize = 32;
    public const int MaxTextureIndex = 128;
    public const double DefaultHandleRadius = 7.0;
    public const double DefaultLineHitDistance = 8.0;
    public const double CollapsedRailWidth = 28.0;
    public const double MinExpandedEditorWidth = 300.0;
    public const double EditorDrawerMinOpenWidth = 366.0;
    public const double EditorDrawerMaxWidth = 600.0;
    public const double MapZoomStep = 1.10;
    public const double MinMapZoom = 0.1;
    public const double MaxMapZoom = 8.0;
    public const double TextureDragThreshold = 16.0;
    public const double CameraMarkerRadius = 90.0;
    public const double CameraMarkerHalfAngleDeg = 28.0;
    public const double CameraMarkerPickRadius = 11.0;
    public const double MapLabelPadX = 4.0;
    public const double MapLabelPadY = 2.0;
    public const double TimelineChannelHeight = 40.0;
    public const double TimelineHeaderWidth = 120.0;
    public const double TimelineRulerHeight = 30.0;
    public const double TimelineMinPacketWidth = 10.0;
    public const int TimelineSnapGridSize = 5;
}

/// <summary>
/// Shared measurements for 3D scene construction.
/// </summary>
public static class Scene3DConstants
{
    public const double StoreyWorld = 64.0;
    public const double QuarterStoreyWorld = StoreyWorld * 0.25;
    public const double EngineToViewY = 0.125;
    public const double HeightScale = 8.0;
    public const double LadderBias = 3.0;
    public const double LadderRailWidth = 4.0;
    public const double LadderRungHeight = 4.0;
    public const int LadderRunsPerPanel = 4;
    public const double LadderWidthScale = 0.45;
    public const double LadderDyToView = 8.0;
    public const double CylinderRadius = 18.0;
    public const int CylinderStacks = 8;
    public const int CylinderSlices = 10;
}

/// <summary>
/// Miscellaneous editor limits and defaults.
/// </summary>
public static class EditorLimitConstants
{
    public const int UndoMaxEntries = 10;
    public const double TerrainRoughness = 0.35;
    public const int TerrainBlurPasses = 1;
    public const int StoryboardMaxMissions = 20;
    public const int StoryboardStartingMissionId = 14;
}
