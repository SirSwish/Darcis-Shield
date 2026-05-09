using UrbanChaosEditor.Shared.Views.MapOverlays;

namespace UrbanChaosMissionEditor.Views.MapOverlays
{
    /// <summary>
    /// Read-only PAP_HI altitude overlay for the Mission Editor.
    /// </summary>
    public sealed class RoofAltitudesLayer : SharedRoofAltitudesLayer
    {
        public RoofAltitudesLayer()
        {
            SetDataProvider(new MissionEditorBuildingProvider());
        }
    }
}
