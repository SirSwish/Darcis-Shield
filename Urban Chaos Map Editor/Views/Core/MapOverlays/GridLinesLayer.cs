// ============================================================
// MapEditor/Views/MapOverlays/GridLinesLayer.cs
// ============================================================
using UrbanChaosEditor.Shared.Views.MapOverlays;

namespace UrbanChaosMapEditor.Views.Core.MapOverlays
{
    /// <summary>
    /// Grid lines layer for Map Editor - Simple black grid (source of truth).
    /// </summary>
    public sealed class GridLinesLayer : SharedGridLinesLayer
    {
        public GridLinesLayer()
        {
            // Map Editor uses simple black grid (this is the source of truth)
            GridStyle = GridLineStyle.Simple;
        }
    }
}