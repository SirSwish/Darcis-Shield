// /Views/MapOverlays/TexturesLayer.cs
using System.Windows;
using System.Windows.Media;
using UrbanChaosLightEditor.Models;
using UrbanChaosLightEditor.Services;

namespace UrbanChaosLightEditor.Views.MapOverlays
{
    /// <summary>
    /// Renders ground textures for the map (read-only display).
    /// </summary>
    public sealed class TexturesLayer : FrameworkElement
    {
        private readonly ReadOnlyTexturesAccessor _tex;

        public TexturesLayer()
        {
            _tex = new ReadOnlyTexturesAccessor(ReadOnlyMapDataService.Instance);

            Width = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = false;

            // Repaint when map changes
            ReadOnlyMapDataService.Instance.MapLoaded += (_, __) => Dispatcher.Invoke(InvalidateVisual);
            ReadOnlyMapDataService.Instance.MapCleared += (_, __) => Dispatcher.Invoke(InvalidateVisual);

            // Repaint when textures finish loading
            TextureCacheService.Instance.Completed += (_, __) => Dispatcher.Invoke(InvalidateVisual);
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            if (!ReadOnlyMapDataService.Instance.IsLoaded) return;

            int ts = MapConstants.TileSize;

            // Placeholder brush if missing texture
            var placeholder = new SolidColorBrush(Color.FromRgb(255, 0, 255)); // magenta
            placeholder.Freeze();

            for (int ty = 0; ty < MapConstants.TilesPerSide; ty++)
            {
                for (int tx = 0; tx < MapConstants.TilesPerSide; tx++)
                {
                    Rect target = new(tx * ts, ty * ts, ts, ts);

                    string relKey;
                    int deg;
                    try
                    {
                        var info = _tex.GetTileTextureKeyAndRotation(tx, ty);
                        relKey = info.relativeKey;
                        deg = info.rotationDeg;
                    }
                    catch
                    {
                        dc.DrawRectangle(placeholder, null, target);
                        continue;
                    }

                    if (!TextureCacheService.Instance.TryGetRelative(relKey, out var bmp) || bmp is null)
                    {
                        dc.DrawRectangle(placeholder, null, target);
                        continue;
                    }

                    Point center = new(target.X + ts / 2.0, target.Y + ts / 2.0);
                    if (deg != 0)
                        dc.PushTransform(new RotateTransform(deg, center.X, center.Y));

                    dc.DrawImage(bmp, target);

                    if (deg != 0)
                        dc.Pop();
                }
            }
        }
    }
}