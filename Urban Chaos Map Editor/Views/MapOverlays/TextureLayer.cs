// /Views/MapOverlays/TextureLayer.cs
using System;
using System.Windows;
using System.Windows.Media;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;
using UrbanChaosEditor.Shared.Services.Textures;

namespace UrbanChaosMapEditor.Views.MapOverlays
{
    public sealed class TexturesLayer : FrameworkElement
    {
        private readonly TexturesAccessor _tex = new TexturesAccessor(MapDataService.Instance);

        public TexturesLayer()
        {
            Width = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = false;

            // Repaint when data changes or new buffer arrives
            MapDataService.Instance.MapLoaded += (_, __) => Dispatcher.Invoke(InvalidateVisual);
            MapDataService.Instance.MapSaved += (_, __) => Dispatcher.Invoke(InvalidateVisual);
            MapDataService.Instance.MapCleared += (_, __) => Dispatcher.Invoke(InvalidateVisual);
            MapDataService.Instance.MapBytesReset += (_, __) => Dispatcher.Invoke(InvalidateVisual);
            // Optional: repaint after textures cached
            TextureCacheService.Instance.Completed += (_, __) => Dispatcher.Invoke(InvalidateVisual);
            TexturesChangeBus.Instance.Changed += (_, __) => Dispatcher.Invoke(InvalidateVisual);
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            if (!MapDataService.Instance.IsLoaded) return;

            int ts = MapConstants.TileSize;

            // Placeholder brush if missing texture
            var placeholder = new SolidColorBrush(Color.FromRgb(255, 0, 255)); // magenta
            placeholder.Freeze();

            for (int ty = 0; ty < MapConstants.TilesPerSide; ty++)
            {
                for (int tx = 0; tx < MapConstants.TilesPerSide; tx++)
                {
                    Rect target = new(tx * ts, ty * ts, ts, ts);

                    // Key + angle
                    string relKey; int deg;
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

                    // Get bitmap
                    if (!TextureCacheService.Instance.TryGetRelative(relKey, out var bmp) || bmp is null)
                    {
                        dc.DrawRectangle(placeholder, null, target);
                        continue;
                    }

                    // Draw with rotation around tile center
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
