using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosMissionEditor.Models;
using UrbanChaosMissionEditor.Services;
using UrbanChaosEditor.Shared.Services.Textures;

namespace UrbanChaosMissionEditor.Views.MapOverlays;

/// <summary>
/// Renders the terrain textures layer using Map Editor's embedded texture assets.
/// </summary>
public class TexturesLayer : FrameworkElement
{
    private WriteableBitmap? _compositeBitmap;
    private bool _needsRedraw = true;

    public TexturesLayer()
    {
        // Subscribe to map loaded events
        ReadOnlyMapDataService.Instance.MapLoaded += (s, e) => { _needsRedraw = true; InvalidateVisual(); };
        ReadOnlyMapDataService.Instance.MapCleared += (s, e) => { _needsRedraw = true; InvalidateVisual(); };
        TextureCacheService.Instance.Completed += (s, e) => { _needsRedraw = true; InvalidateVisual(); };
    }

    public void Refresh()
    {
        _needsRedraw = true;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (!ReadOnlyMapDataService.Instance.IsLoaded)
        {
            // Draw placeholder background
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)), null,
                new Rect(0, 0, MapConstants.MapPixelSize, MapConstants.MapPixelSize));
            return;
        }

        if (_needsRedraw || _compositeBitmap == null)
        {
            RenderToComposite();
            _needsRedraw = false;
        }

        if (_compositeBitmap != null)
        {
            dc.DrawImage(_compositeBitmap, new Rect(0, 0, MapConstants.MapPixelSize, MapConstants.MapPixelSize));
        }
    }

    private void RenderToComposite()
    {
        try
        {
            var mapSvc = ReadOnlyMapDataService.Instance;
            var texCache = TextureCacheService.Instance;

            if (!mapSvc.IsLoaded) return;

            var accessor = new ReadOnlyTexturesAccessor(mapSvc);
            int tileSize = MapConstants.PixelsPerTile;
            int tilesPerSide = MapConstants.TilesPerSide;
            int bitmapSize = MapConstants.MapPixelSize;

            // Create or reuse the composite bitmap
            if (_compositeBitmap == null || _compositeBitmap.PixelWidth != bitmapSize)
            {
                _compositeBitmap = new WriteableBitmap(bitmapSize, bitmapSize, 96, 96, PixelFormats.Bgra32, null);
            }

            // Fill with dark background first
            int stride = bitmapSize * 4;
            byte[] pixels = new byte[stride * bitmapSize];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0x1A;     // B
                pixels[i + 1] = 0x1A; // G
                pixels[i + 2] = 0x1A; // R
                pixels[i + 3] = 0xFF; // A
            }

            int rendered = 0;
            int missing = 0;

            // Render each tile
            for (int ty = 0; ty < tilesPerSide; ty++)
            {
                for (int tx = 0; tx < tilesPerSide; tx++)
                {
                    try
                    {
                        var (relKey, rotDeg) = accessor.GetTileTextureKeyAndRotation(tx, ty);

                        // Use Map Editor's texture cache
                        if (texCache.TryGetRelative(relKey, out var bitmap) && bitmap != null)
                        {
                            DrawTileToPixels(pixels, bitmapSize, tx * tileSize, ty * tileSize, tileSize, bitmap, rotDeg);
                            rendered++;
                        }
                        else
                        {
                            // Draw colored placeholder based on texture key
                            DrawPlaceholderTile(pixels, bitmapSize, tx * tileSize, ty * tileSize, tileSize, relKey);
                            missing++;
                        }
                    }
                    catch
                    {
                        // Skip bad tiles
                    }
                }
            }

            Debug.WriteLine($"[TexturesLayer] Rendered {rendered} textures, {missing} missing (cache has {texCache.Count})");
            _compositeBitmap.WritePixels(new Int32Rect(0, 0, bitmapSize, bitmapSize), pixels, stride, 0);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TexturesLayer] Error rendering: {ex.Message}");
        }
    }

    private static void DrawTileToPixels(byte[] pixels, int bitmapWidth, int destX, int destY, int tileSize, BitmapSource src, int rotDeg)
    {
        // Convert source to Bgra32 if needed
        BitmapSource converted = src;
        if (src.Format != PixelFormats.Bgra32)
        {
            converted = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        }

        // Scale source to tile size
        int srcWidth = converted.PixelWidth;
        int srcHeight = converted.PixelHeight;
        int srcStride = srcWidth * 4;
        byte[] srcPixels = new byte[srcStride * srcHeight];
        converted.CopyPixels(srcPixels, srcStride, 0);

        int destStride = bitmapWidth * 4;

        for (int y = 0; y < tileSize && y < srcHeight; y++)
        {
            for (int x = 0; x < tileSize && x < srcWidth; x++)
            {
                // Apply rotation
                int sx, sy;
                switch (rotDeg)
                {
                    case 90:
                        sx = y;
                        sy = tileSize - 1 - x;
                        break;
                    case 180:
                        sx = tileSize - 1 - x;
                        sy = tileSize - 1 - y;
                        break;
                    case 270:
                        sx = tileSize - 1 - y;
                        sy = x;
                        break;
                    default: // 0
                        sx = x;
                        sy = y;
                        break;
                }

                if (sx >= 0 && sx < srcWidth && sy >= 0 && sy < srcHeight)
                {
                    int srcIdx = sy * srcStride + sx * 4;
                    int destIdx = (destY + y) * destStride + (destX + x) * 4;

                    if (destIdx + 3 < pixels.Length && srcIdx + 3 < srcPixels.Length)
                    {
                        pixels[destIdx] = srcPixels[srcIdx];
                        pixels[destIdx + 1] = srcPixels[srcIdx + 1];
                        pixels[destIdx + 2] = srcPixels[srcIdx + 2];
                        pixels[destIdx + 3] = srcPixels[srcIdx + 3];
                    }
                }
            }
        }
    }

    private static void DrawPlaceholderTile(byte[] pixels, int bitmapWidth, int destX, int destY, int tileSize, string relKey)
    {
        // Generate a color based on the key hash
        int hash = relKey.GetHashCode();
        byte r = (byte)((hash >> 16) & 0x3F | 0x20);
        byte g = (byte)((hash >> 8) & 0x3F | 0x20);
        byte b = (byte)((hash) & 0x3F | 0x20);

        int destStride = bitmapWidth * 4;

        for (int y = 0; y < tileSize; y++)
        {
            for (int x = 0; x < tileSize; x++)
            {
                int destIdx = (destY + y) * destStride + (destX + x) * 4;
                if (destIdx + 3 < pixels.Length)
                {
                    pixels[destIdx] = b;
                    pixels[destIdx + 1] = g;
                    pixels[destIdx + 2] = r;
                    pixels[destIdx + 3] = 0xFF;
                }
            }
        }
    }
}