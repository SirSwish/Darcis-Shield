using UrbanChaosEditor.Shared.Views.MapOverlays;

namespace UrbanChaosMissionEditor.Views.MapOverlays
{
    /// <summary>
    /// Read-only RF4 roof tile overlay for the Mission Editor.
    /// </summary>
    public sealed class RoofTilesLayer : SharedRoofTilesLayer
    {
        public RoofTilesLayer()
        {
            SetDataProvider(new MissionEditorBuildingProvider());
        }
    }
}
