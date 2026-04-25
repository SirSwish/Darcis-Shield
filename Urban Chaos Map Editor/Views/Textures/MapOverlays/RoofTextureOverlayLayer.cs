// Views/Textures/MapOverlays/RoofTextureOverlayLayer.cs
//
// Visual overlay that substitutes the warehouse roof texture for cells covered by an RF4
// tile when "Paint Roofs" mode is active.  Rendered on top of the normal TexturesLayer.
//
// Rendering rule (per spec):
//   - If PaintRoofMode is false  → render nothing (normal TexturesLayer shows through)
//   - If PaintRoofMode is true:
//       For every cell (tx, ty) covered by a warehouse RF4:
//         read RoofTex[game_x, game_z] (where game_x = 127-tx, game_z = 127-ty)
//         if entry != 0 → draw that texture over the cell
//         if entry == 0 → draw a neutral "default" tint so the user can see which
//                         cells are in the roof texture domain

using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosEditor.Shared.Services.Textures;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.ViewModels.Core;
using static UrbanChaosMapEditor.Services.Textures.TexturesAccessor;

namespace UrbanChaosMapEditor.Views.Textures.MapOverlays
{
    public sealed class RoofTextureOverlayLayer : FrameworkElement
    {
        // Semi-transparent tint drawn over cells where RoofTex == 0 to mark them
        // as part of the roof domain (so the user knows painting is active there).
        private static readonly Brush DefaultRoofCellBrush =
            new SolidColorBrush(Color.FromArgb(60, 80, 160, 255));   // blue-ish tint

        // Hairline border for each RF4-covered cell
        private static readonly Pen RoofCellBorderPen =
            new Pen(new SolidColorBrush(Color.FromArgb(100, 100, 180, 255)), 1.0);

        static RoofTextureOverlayLayer()
        {
            DefaultRoofCellBrush.Freeze();
            ((SolidColorBrush)RoofCellBorderPen.Brush).Freeze();
            RoofCellBorderPen.Freeze();
        }

        private MapViewModel? _vm;

        public RoofTextureOverlayLayer()
        {
            Width  = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = false;

            // Refresh when roof textures change
            RoofTexturesChangeBus.Instance.Changed += (_, __) => RefreshOnUiThread();

            // Refresh when RF4 structure changes (occupancy cache was invalidated)
            RoofsChangeBus.Instance.Changed += (_, __) => RefreshOnUiThread();

            // Refresh when map is loaded / cleared
            MapDataService.Instance.MapLoaded  += (_, __) => RefreshOnUiThread();
            MapDataService.Instance.MapCleared += (_, __) => RefreshOnUiThread();

            // Refresh when texture images finish loading
            TextureCacheService.Instance.Completed += (_, __) => RefreshOnUiThread();

            DataContextChanged += (_, __) => HookVm();
        }

        private void RefreshOnUiThread()
        {
            if (Dispatcher.CheckAccess())
                InvalidateVisual();
            else
                Dispatcher.BeginInvoke(InvalidateVisual);
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
            if (e.PropertyName == nameof(MapViewModel.PaintRoofMode) ||
                e.PropertyName == nameof(MapViewModel.TextureWorld)   ||
                e.PropertyName == nameof(MapViewModel.UseBetaTextures))
            {
                Dispatcher.BeginInvoke(InvalidateVisual);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            if (_vm is null || !_vm.PaintRoofMode) return;
            if (!MapDataService.Instance.IsLoaded) return;

            var svc   = RoofTextureService.Instance;
            svc.EnsureLoadedForCurrentMap();
            var cache = TextureCacheService.Instance;
            var occupied = svc.GetRF4OccupiedCells(); // game-coordinate set

            if (occupied.Count == 0) return;

            int textureWorld = _vm.TextureWorld;

            foreach (var (gx, gz) in occupied)
            {
                // Convert game coords → UI tile coords
                int tx = MapConstants.TilesPerSide - 1 - gx;
                int ty = MapConstants.TilesPerSide - 1 - gz;

                if ((uint)tx >= MapConstants.TilesPerSide || (uint)ty >= MapConstants.TilesPerSide)
                    continue;

                double px = tx * MapConstants.TileSize;
                double py = ty * MapConstants.TileSize;
                var tileRect = new Rect(px, py, MapConstants.TileSize, MapConstants.TileSize);

                ushort entry = svc.ReadEntry(gx, gz);

                if (entry == 0)
                {
                    // Default/unset: draw a blue tint so the user sees the RF4 domain
                    dc.DrawRectangle(DefaultRoofCellBrush, RoofCellBorderPen, tileRect);
                }
                else
                {
                    // Decode UWORD and look up the texture image
                    var (group, texNumber, rotIdx) = RoofTextureService.DecodeEntry(entry);

                    string relKey = group switch
                    {
                        TextureGroup.World  => $"world{textureWorld}_{texNumber:000}",
                        TextureGroup.Shared => $"shared_{texNumber:000}",
                        TextureGroup.Prims  => $"shared_prims_{texNumber:000}",
                        _                   => ""
                    };

                    if (!string.IsNullOrEmpty(relKey) &&
                        cache.TryGetRelative(relKey, out var bmp) &&
                        bmp is not null)
                    {
                        double angle = rotIdx switch { 0 => 180, 1 => 90, 2 => 0, 3 => 270, _ => 0 };

                        if (angle == 0)
                        {
                            dc.DrawImage(bmp, tileRect);
                        }
                        else
                        {
                            dc.PushTransform(new RotateTransform(
                                angle,
                                px + MapConstants.TileSize / 2.0,
                                py + MapConstants.TileSize / 2.0));
                            dc.DrawImage(bmp, tileRect);
                            dc.Pop();
                        }

                        // Thin border so the user can see the roof domain boundary
                        dc.DrawRectangle(null, RoofCellBorderPen, tileRect);
                    }
                    else
                    {
                        // Texture not yet loaded — show the default tint as fallback
                        dc.DrawRectangle(DefaultRoofCellBrush, RoofCellBorderPen, tileRect);
                    }
                }
            }
        }
    }
}
