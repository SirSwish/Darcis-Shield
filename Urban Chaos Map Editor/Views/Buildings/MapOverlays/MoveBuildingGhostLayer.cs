// Views/Buildings/MapOverlays/MoveBuildingGhostLayer.cs
// Draws a ghost outline of the building being moved, following the cursor.

using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Buildings.MapOverlays
{
    public sealed class MoveBuildingGhostLayer : FrameworkElement
    {
        private MapViewModel? _vm;

        private const double TileSize = 64.0;

        private static readonly Pen PenGhost;
        private static readonly Pen PenAnchor;
        private static readonly Brush BrushAnchor;
        private static readonly Brush BrushFill;

        static MoveBuildingGhostLayer()
        {
            var ghostBrush = new SolidColorBrush(Color.FromArgb(220, 0, 220, 255));
            ghostBrush.Freeze();
            PenGhost = new Pen(ghostBrush, 2.5) { DashStyle = DashStyles.Dash };
            PenGhost.Freeze();

            BrushAnchor = new SolidColorBrush(Color.FromArgb(200, 255, 255, 0));
            BrushAnchor.Freeze();
            PenAnchor = new Pen(BrushAnchor, 2.0);
            PenAnchor.Freeze();

            BrushFill = new SolidColorBrush(Color.FromArgb(30, 0, 200, 255));
            BrushFill.Freeze();
        }

        public MoveBuildingGhostLayer()
        {
            IsHitTestVisible = false;
            DataContextChanged += (_, __) => HookVm();
        }

        private void HookVm()
        {
            if (_vm is not null)
                _vm.PropertyChanged -= OnVmChanged;

            _vm = DataContext as MapViewModel;

            if (_vm is not null)
                _vm.PropertyChanged += OnVmChanged;

            InvalidateVisual();
        }

        private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapViewModel.SelectedTool)
             || e.PropertyName == nameof(MapViewModel.BuildingMoveClipboard)
             || e.PropertyName == nameof(MapViewModel.MoveGhostUiX)
             || e.PropertyName == nameof(MapViewModel.MoveGhostUiZ))
            {
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_vm is null
             || _vm.SelectedTool != EditorTool.MoveBuilding
             || _vm.BuildingMoveClipboard is null)
                return;

            var snap      = _vm.BuildingMoveClipboard;
            int cursorUiX = _vm.MoveGhostUiX;
            int cursorUiZ = _vm.MoveGhostUiZ;

            // Anchor is the top-left corner (maxGameX, maxGameZ) of the original bounding box.
            // Ghost facets are drawn relative to cursor, offset so anchor follows cursor.
            // For each facet point in game coords (gx, gz):
            //   uiX = (128 - gx) * 64
            //   relX = uiX - snap.AnchorUiX  = (snap.MaxGameX - gx) * 64
            //   screenX = cursorUiX + relX
            foreach (var facet in GetFacets(snap))
            {
                double sx0 = cursorUiX + (snap.MaxGameX - facet.gx0) * TileSize;
                double sz0 = cursorUiZ + (snap.MaxGameZ - facet.gz0) * TileSize;
                double sx1 = cursorUiX + (snap.MaxGameX - facet.gx1) * TileSize;
                double sz1 = cursorUiZ + (snap.MaxGameZ - facet.gz1) * TileSize;

                dc.DrawLine(PenGhost, new Point(sx0, sz0), new Point(sx1, sz1));
            }

            // Draw bounding rectangle
            double bx = cursorUiX;
            double bz = cursorUiZ;
            double bw = (snap.MaxGameX - snap.MinGameX) * TileSize;
            double bh = (snap.MaxGameZ - snap.MinGameZ) * TileSize;
            dc.DrawRectangle(BrushFill, PenAnchor, new Rect(bx, bz, bw, bh));

            // Draw anchor crosshair at cursor
            const double ch = 8.0;
            dc.DrawLine(PenAnchor, new Point(cursorUiX - ch, cursorUiZ), new Point(cursorUiX + ch, cursorUiZ));
            dc.DrawLine(PenAnchor, new Point(cursorUiX, cursorUiZ - ch), new Point(cursorUiX, cursorUiZ + ch));
        }

        private static IEnumerable<(int gx0, int gz0, int gx1, int gz1)> GetFacets(BuildingSnapshot snap)
        {
            // We reconstruct the facet list from the snapshot using the current service.
            // This is cheap — the service does the parsing.
            var acc = new Services.Buildings.BuildingsAccessor(Services.Core.MapDataService.Instance);
            var current = acc.ReadSnapshot();
            if (current.Facets == null) yield break;

            for (int fi = snap.FacetStart0; fi < snap.FacetEnd0; fi++)
            {
                if (fi < 0 || fi >= current.Facets.Length) continue;
                var f = current.Facets[fi];
                yield return (f.X0, f.Z0, f.X1, f.Z1);
            }
        }
    }
}
