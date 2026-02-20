// ============================================================
// UrbanChaosEditor.Shared/Views/MapOverlays/SharedTexturesLayer.cs
// ============================================================
// Shared terrain texture rendering layer

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosEditor.Shared.Models;
using UrbanChaosEditor.Shared.Services.Textures;

namespace UrbanChaosEditor.Shared.Views.MapOverlays;

/// <summary>
/// Rendering mode for textures layer.
/// </summary>
public enum TextureRenderMode
{
    /// <summary>Direct DrawImage per tile - good for edit scenarios (Map Editor)</summary>
    Direct,

    /// <summary>Pre-composite to WriteableBitmap - better performance for read-only (Mission/Light Editor)</summary>
    Composite
}

/// <summary>
/// Shared terrain textures layer.
/// Supports both direct rendering (for editing) and composite rendering (for read-only display).
/// </summary>
public class SharedTexturesLayer : MapOverlayBase
{
    #region Dependencies

    private ITextureDataProvider? _textureProvider;

    /// <summary>
    /// Set the texture data provider.
    /// </summary>
    public void SetTextureProvider(ITextureDataProvider provider)
    {
        _textureProvider = provider;

        if (_textureProvider != null)
        {
            _textureProvider.SubscribeMapLoaded(OnMapLoaded);
            _textureProvider.SubscribeMapCleared(OnMapCleared);
        }

        MarkDirty();
    }

    private void OnMapLoaded()
    {
        MarkDirty();
        RefreshOnUiThread();
    }

    private void OnMapCleared()
    {
        _compositeBitmap = null;
        RefreshOnUiThread();
    }

    #endregion

    #region Configuration

    public static readonly DependencyProperty RenderModeProperty =
        DependencyProperty.Register(nameof(RenderMode), typeof(TextureRenderMode), typeof(SharedTexturesLayer),
            new FrameworkPropertyMetadata(TextureRenderMode.Direct, FrameworkPropertyMetadataOptions.AffectsRender,
                (d, e) => ((SharedTexturesLayer)d).MarkDirty()));

    /// <summary>
    /// Rendering mode - Direct for editing, Composite for read-only performance.
    /// </summary>
    public TextureRenderMode RenderMode
    {
        get => (TextureRenderMode)GetValue(RenderModeProperty);
        set => SetValue(RenderModeProperty, value);
    }

    #endregion

    #region Composite Bitmap State

    private WriteableBitmap? _compositeBitmap;
    private bool _needsRedraw = true;

    /// <summary>
    /// Mark the layer as needing a redraw (for composite mode).
    /// </summary>
    public void MarkDirty()
    {
        _needsRedraw = true;
    }

    #endregion

    #region Constructor

    public SharedTexturesLayer()
    {
        // Subscribe to texture cache completion
        TextureCacheService.Instance.Completed += OnTextureCacheCompleted;
    }

    private void OnTextureCacheCompleted(object? sender, EventArgs e)
    {
        MarkDirty();
        RefreshOnUiThread();
    }

    #endregion

    #region Rendering

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (_textureProvider == null || !_textureProvider.IsLoaded)
        {
            // Draw placeholder background
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)), null,
                new Rect(0, 0, SharedMapConstants.MapPixelSize, SharedMapConstants.MapPixelSize));
            return;
        }

        if (RenderMode == TextureRenderMode.Composite)
            RenderComposite(dc);
        else
            RenderDirect(dc);
    }

    /// <summary>
    /// Direct rendering - draws each tile individually.
    /// Good for editing scenarios where tiles change frequently.
    /// </summary>
    private void RenderDirect(DrawingContext dc)
    {
        int ts = SharedMapConstants.TileSize;
        var placeholder = CreatePlaceholderBrush();

        for (int ty = 0; ty < SharedMapConstants.TilesPerSide; ty++)
        {
            for (int tx = 0; tx < SharedMapConstants.TilesPerSide; tx++)
            {
                Rect target = new(tx * ts, ty * ts, ts, ts);

                string relKey;
                int deg;
                try
                {
                    var info = _textureProvider!.GetTileTextureKeyAndRotation(tx, ty);
                    relKey = info.relativeKey;
                    deg = info.rotationDeg;
                }
                catch
                {
                    dc.DrawRectangle(placeholder, null, target);
                    continue;
                }

                if (!TextureCacheService.Instance.TryGetRelative(relKey, out var bmp) || bmp == null)
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

    /// <summary>
    /// Composite rendering - pre-renders to a WriteableBitmap.
    /// Better performance for read-only scenarios.
    /// </summary>
    private void RenderComposite(DrawingContext dc)
    {
        if (_needsRedraw || _compositeBitmap == null)
        {
            RenderToComposite();
            _needsRedraw = false;
        }

        if (_compositeBitmap != null)
        {
            dc.DrawImage(_compositeBitmap, new Rect(0, 0, SharedMapConstants.MapPixelSize, SharedMapConstants.MapPixelSize));
        }
    }

    private void RenderToComposite()
    {
        if (_textureProvider == null || !_textureProvider.IsLoaded) return;

        try
        {
            int tileSize = SharedMapConstants.TileSize;
            int tilesPerSide = SharedMapConstants.TilesPerSide;
            int bitmapSize = SharedMapConstants.MapPixelSize;

            if (_compositeBitmap == null || _compositeBitmap.PixelWidth != bitmapSize)
            {
                _compositeBitmap = new WriteableBitmap(bitmapSize, bitmapSize, 96, 96, PixelFormats.Bgra32, null);
            }

            int stride = bitmapSize * 4;
            byte[] pixels = new byte[stride * bitmapSize];

            // Fill with dark background
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0x1A;     // B
                pixels[i + 1] = 0x1A; // G
                pixels[i + 2] = 0x1A; // R
                pixels[i + 3] = 0xFF; // A
            }

            int rendered = 0;

            for (int ty = 0; ty < tilesPerSide; ty++)
            {
                for (int tx = 0; tx < tilesPerSide; tx++)
                {
                    try
                    {
                        var (relKey, rotDeg) = _textureProvider.GetTileTextureKeyAndRotation(tx, ty);

                        if (TextureCacheService.Instance.TryGetRelative(relKey, out var bitmap) && bitmap != null)
                        {
                            DrawTileToPixels(pixels, bitmapSize, tx * tileSize, ty * tileSize, tileSize, bitmap, rotDeg);
                            rendered++;
                        }
                        else
                        {
                            DrawPlaceholderTile(pixels, bitmapSize, tx * tileSize, ty * tileSize, tileSize, relKey);
                        }
                    }
                    catch
                    {
                        // Skip bad tiles
                    }
                }
            }

            Debug.WriteLine($"[SharedTexturesLayer] Composite rendered {rendered} textures");
            _compositeBitmap.WritePixels(new Int32Rect(0, 0, bitmapSize, bitmapSize), pixels, stride, 0);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SharedTexturesLayer] Error rendering composite: {ex.Message}");
        }
    }

    #endregion

    #region Pixel Manipulation

    private static void DrawTileToPixels(byte[] pixels, int bitmapWidth, int destX, int destY, int tileSize, BitmapSource src, int rotDeg)
    {
        BitmapSource converted = src;
        if (src.Format != PixelFormats.Bgra32)
        {
            converted = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        }

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
                    default:
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

    private static SolidColorBrush CreatePlaceholderBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(255, 0, 255)); // Magenta
        brush.Freeze();
        return brush;
    }

    #endregion
}