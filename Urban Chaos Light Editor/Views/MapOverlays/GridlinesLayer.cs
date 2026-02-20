// ============================================================
// LightEditor/Views/MapOverlays/GridLinesLayer.cs
// ============================================================
using UrbanChaosEditor.Shared.Views.MapOverlays;

namespace UrbanChaosLightEditor.Views.MapOverlays
{
    /// <summary>
    /// Grid lines layer for Light Editor - Simple black grid (same as Map Editor).
    /// </summary>
    public sealed class GridLinesLayer : SharedGridLinesLayer
    {
        public GridLinesLayer()
        {
            // Light Editor uses simple black grid (same as Map Editor)
            GridStyle = GridLineStyle.Simple;
        }
    }
}
