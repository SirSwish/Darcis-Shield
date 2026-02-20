// ============================================================
// MissionEditor/Views/MapOverlays/GridLinesLayer.cs
// ============================================================
using UrbanChaosEditor.Shared.Views.MapOverlays;

namespace UrbanChaosMissionEditor.Views.MapOverlays
{
    /// <summary>
    /// Grid lines layer for Mission Editor - Complex style with minor/major/center lines.
    /// </summary>
    public sealed class GridLinesLayer : SharedGridLinesLayer
    {
        public GridLinesLayer()
        {
            // Mission Editor uses complex grid with minor/major/center lines
            GridStyle = GridLineStyle.Complex;
            MajorGridInterval = 8;
        }
    }
}