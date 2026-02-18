using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosMissionEditor.Models;
using UrbanChaosMissionEditor.Services;

namespace UrbanChaosMissionEditor.Views.MapOverlays;

/// <summary>
/// Draws sprite graphics for each prim/object on the map.
/// Images are loaded from Map Editor's embedded resources: Assets/Images/PrimGraphics/NNN.png
/// </summary>
public sealed class PrimGraphicsLayer : FrameworkElement
{
    /// <summary>
    /// Cached sprite: image + anchor (center-of-rotation) in image pixels from top-left.
    /// </summary>
    private sealed class PrimSprite
    {
        public ImageSource Image { get; }
        public double AnchorX { get; }
        public double AnchorY { get; }
        public double Width { get; }
        public double Height { get; }

        public PrimSprite(ImageSource image, double anchorX, double anchorY, double width, double height)
        {
            Image = image;
            AnchorX = anchorX;
            AnchorY = anchorY;
            Width = width;
            Height = height;
        }
    }

    // Cache primNumber → PrimSprite (or null if missing)
    private readonly Dictionary<byte, PrimSprite?> _spriteCache = new();

    // Cached prims for rendering
    private List<PrimDisplayInfo> _prims = new();
    private bool _needsReload = true;

    // Fallback marker when no sprite available
    private static readonly Brush PrimBrush = new SolidColorBrush(Color.FromArgb(180, 255, 165, 0));
    private static readonly Pen PrimPen = new(Brushes.Black, 0.5);

    // 256 steps around a full circle
    private const double DegreesPerYaw = 360.0 / 256.0;

    static PrimGraphicsLayer()
    {
        PrimBrush.Freeze();
        PrimPen.Freeze();
    }

    public PrimGraphicsLayer()
    {
        Width = MapConstants.MapPixelSize;
        Height = MapConstants.MapPixelSize;
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;

        // Refresh prim list when map loads/clears
        ReadOnlyMapDataService.Instance.MapLoaded += (_, __) =>
        {
            _needsReload = true;
            Dispatcher.Invoke(InvalidateVisual);
        };
        ReadOnlyMapDataService.Instance.MapCleared += (_, __) =>
        {
            _prims.Clear();
            Dispatcher.Invoke(InvalidateVisual);
        };
    }

    public void Refresh()
    {
        _needsReload = true;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(MapConstants.MapPixelSize, MapConstants.MapPixelSize);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (!ReadOnlyMapDataService.Instance.IsLoaded) return;

        if (_needsReload)
        {
            LoadPrims();
            _needsReload = false;
        }

        if (_prims.Count == 0) return;

        int drawn = 0;
        const double markerRadius = 3.0;

        foreach (var p in _prims.OrderBy(p => p.Y))
        {
            var sprite = GetPrimSprite(p.PrimNumber);
            if (sprite != null)
            {
                DrawPrimSprite(dc, p.PixelX, p.PixelZ, sprite, p.Yaw);
                drawn++;
            }
            else
            {
                // Fallback to marker
                dc.DrawEllipse(PrimBrush, PrimPen, new Point(p.PixelX, p.PixelZ), markerRadius, markerRadius);
            }
        }

        Debug.WriteLine($"[PrimGraphicsLayer] Rendered {drawn} prims with sprites, {_prims.Count - drawn} as markers");
    }

    /// <summary>
    /// Draw a single prim sprite, rotated by yaw*(360/256) degrees,
    /// with the anchor sitting on (px, pz).
    /// </summary>
    private static void DrawPrimSprite(DrawingContext dc, int px, int pz, PrimSprite sprite, byte yaw)
    {
        double w = sprite.Width;
        double h = sprite.Height;

        // Yaw 0 => no rotation. Positive yaw rotates CCW on screen
        double angleDeg = -yaw * DegreesPerYaw;

        dc.PushTransform(new TranslateTransform(px, pz));
        dc.PushTransform(new RotateTransform(angleDeg));

        var rect = new Rect(-sprite.AnchorX, -sprite.AnchorY, w, h);
        dc.DrawImage(sprite.Image, rect);

        dc.Pop(); // Rotate
        dc.Pop(); // Translate
    }

    private PrimSprite? GetPrimSprite(byte primNumber)
    {
        if (_spriteCache.TryGetValue(primNumber, out var cached))
            return cached;

        var sprite = LoadPrimGraphic(primNumber);
        _spriteCache[primNumber] = sprite;

        return sprite;
    }

    /// <summary>
    /// Loads NNN.png from Map Editor's embedded resources:
    /// pack://application:,,,/Urban Chaos Map Editor;component/Assets/Images/PrimGraphics/NNN.png
    /// </summary>
    private static PrimSprite? LoadPrimGraphic(byte primNumber)
    {
        // Load from Map Editor's embedded resources
        string relativePath = $"Assets/Images/PrimGraphics/{primNumber:D3}.png";
        var uri = new Uri($"pack://application:,,,/UrbanChaosEditor.Shared;component/{relativePath}", UriKind.Absolute);

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            double w = bmp.PixelWidth;
            double h = bmp.PixelHeight;

            var anchor = GetAnchorForPrim(primNumber, w, h);

            return new PrimSprite(bmp, anchor.X, anchor.Y, w, h);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PrimGraphicsLayer] Failed to load prim {primNumber}: {ex.Message}");
            return null;
        }
    }

    private void LoadPrims()
    {
        _prims.Clear();

        var mapSvc = ReadOnlyMapDataService.Instance;
        if (!mapSvc.IsLoaded) return;

        try
        {
            var accessor = new ReadOnlyObjectsAccessor(mapSvc);
            _prims = accessor.ReadAllPrims();
            Debug.WriteLine($"[PrimGraphicsLayer] Loaded {_prims.Count} prims");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PrimGraphicsLayer] Error loading prims: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the pivot/center-of-rotation for a given prim in image pixel coords.
    /// Default: center of the image.
    /// </summary>
    private static Point GetAnchorForPrim(byte primNumber, double imgWidth, double imgHeight)
    {
        double cx = imgWidth / 2.0;
        double cy = imgHeight / 2.0;

        // Override specific prims with known anchors
        switch (primNumber)
        {
            case 1: cx = 11.0; cy = 11.0; break;
            case 2: cx = 81.0; cy = 23.0; break;
            case 3: cx = 7.0; cy = 27.0; break;
            case 4: cx = 19.0; cy = 8.0; break;
            case 5: cx = 17.5; cy = 7.0; break;
            case 6: cx = 64.0; cy = 9.0; break;
            case 7: cx = 64.0; cy = 9.0; break;
            case 8: cx = 32.0; cy = 12.0; break;
            case 9: cx = 83.0; cy = 3.0; break;
            case 10: cx = 22.0; cy = 6.0; break;
            case 11: cx = 16.0; cy = 13.0; break;
            case 12: cx = 32.0; cy = 2.0; break;
            case 13: cx = 64.0; cy = 0.0; break;
            case 14: cx = 64.0; cy = 0.0; break;
            case 15: cx = 63.0; cy = 63.0; break;
            case 16: cx = 64.0; cy = 0.0; break;
            case 17: cx = 0.0; cy = 0.0; break;
            case 18: cx = 6.0; cy = 6.0; break;
            case 19: cx = 32.0; cy = 0.0; break;
            case 20: cx = 32.0; cy = 0.0; break;
            case 21: cx = 64.0; cy = 0.0; break;
            case 22: cx = 64.0; cy = 0.0; break;
            case 23: cx = 48.0; cy = 0.0; break;
            case 24: cx = 64.0; cy = 0.0; break;
            case 25: cx = 32.0; cy = 2.0; break;
            case 26: cx = 11.0; cy = 2.0; break;
            case 27: cx = 40.0; cy = 68.0; break;
            case 28: cx = 64.0; cy = -32.0; break;
            case 29: cx = 64.0; cy = 0.0; break;
            case 30: cx = 26.0; cy = 12.0; break;
            case 31: cx = 18.0; cy = 2.0; break;
            case 32: cx = 77.0; cy = 34.0; break;
            case 33: cx = 11.0; cy = 11.0; break;
            case 34: cx = 46.0; cy = 46.0; break;
            case 35: cx = 32.0; cy = 32.0; break;
            case 36: cx = 0.0; cy = 6.0; break;
            case 37: cx = 4.0; cy = 4.0; break;
            case 38: cx = 16.0; cy = 18.0; break;
            case 39: cx = 6.0; cy = 0.0; break;
            case 40: cx = 44.0; cy = 44.0; break;
            case 41: cx = 64.0; cy = 64.0; break;
            case 42: cx = 28.0; cy = 28.0; break;
            case 43: cx = 64.0; cy = 6.0; break;
            case 44: cx = 64.0; cy = 64.0; break;
            case 45: cx = 166.0; cy = 6.0; break;
            case 46: cx = 22.0; cy = 37.0; break;
            case 47: cx = 17.0; cy = 22.0; break;
            case 48: cx = 4.0; cy = 0.0; break;
            case 49: cx = 0.0; cy = 0.0; break;
            case 50: cx = 9.0; cy = 63.0; break;
            case 51: cx = 4.0; cy = 62.0; break;
            case 52: cx = 9.0; cy = 16.0; break;
            case 53: cx = 9.0; cy = 12.0; break;
            case 54: cx = 9.0; cy = 16.0; break;
            case 55: cx = 9.0; cy = 12.0; break;
            case 56: cx = 124.0; cy = 32.0; break;
            case 57: cx = 32.0; cy = 6.0; break;
            case 58: cx = 46.0; cy = 47.0; break;
            case 59: cx = 32.0; cy = 32.0; break;
            case 60: cx = 3.0; cy = 3.0; break;
            case 61: cx = 8.5; cy = 4.5; break;
            case 62: cx = 20.0; cy = 32.0; break;
            case 63: cx = 0.0; cy = 2.0; break;
            case 64: cx = 32.0; cy = 3.0; break;
            case 65: cx = 32.0; cy = 3.0; break;
            case 66: cx = 32.0; cy = 3.0; break;
            case 67: cx = 8.0; cy = 15.0; break;
            case 68: cx = 32.0; cy = 3.0; break;
            case 69: cx = 45.0; cy = 16.0; break;
            case 70: cx = 9.0; cy = 9.0; break;
            case 71: cx = 7.0; cy = 1.0; break;
            case 72: cx = 19.0; cy = 19.0; break;
            case 73: cx = 64.0; cy = 32.0; break;
            case 74: cx = 30.0; cy = 106.0; break;
            case 75: cx = 85.0; cy = 85.0; break;
            case 76: cx = 3.0; cy = 2.0; break;
            case 77: cx = 96.0; cy = 64.0; break;
            case 78: cx = 116.0; cy = 64.0; break;
            case 79: cx = 48.0; cy = 48.0; break;
            case 80: cx = 16.0; cy = 0.0; break;
            case 81: cx = 15.0; cy = 0.0; break;
            case 82: cx = 6.0; cy = 42.0; break;
            case 83: cx = 9.5; cy = 9.5; break;
            case 84: cx = 45.0; cy = 3.0; break;
            case 85: cx = 30.0; cy = 3.0; break;
            case 86: cx = 32.0; cy = 3.0; break;
            case 87: cx = 6.0; cy = 12.0; break;
            case 88: cx = 40.0; cy = 68.0; break;
            case 89: cx = 29.0; cy = 11.0; break;
            case 90: cx = 34.0; cy = 72.0; break;
            case 91: cx = 34.0; cy = 72.0; break;
            case 92: cx = 42.0; cy = 42.0; break;
            case 93: cx = 0.0; cy = 51.0; break;
            case 94: cx = 13.0; cy = 4.0; break;
            case 95: cx = 7.5; cy = 9.5; break;
            case 96: cx = 19.5; cy = 19.0; break;
            case 97: cx = 9.0; cy = 5.0; break;
            case 98: cx = 11.0; cy = 4.0; break;
            case 99: cx = 32.0; cy = 15.0; break;
            case 100: cx = 32.0; cy = 30.0; break;
            case 101: cx = 16.0; cy = 4.0; break;
            case 102: cx = 0.0; cy = 0.0; break;
            case 103: cx = 9.0; cy = 8.0; break;
            case 104: cx = 16.0; cy = 12.0; break;
            case 105: cx = 32.0; cy = 0.0; break;
            case 106: cx = 64.0; cy = 32.0; break;
            case 107: cx = 11.0; cy = 9.0; break;
            case 108: cx = 32.0; cy = 60.0; break;
            case 109: cx = 32.0; cy = 64.0; break;
            case 110: cx = 16.0; cy = 32.0; break;
            case 111: cx = 29.0; cy = 0.0; break;
            case 112: cx = 12.5; cy = 10.5; break;
            case 113: cx = 9.0; cy = 9.0; break;
            case 114: cx = 64.0; cy = 159.0; break;
            case 115: cx = 0.0; cy = 11.0; break;
            case 116: cx = 13.5; cy = 10.0; break;
            case 117: cx = 43.0; cy = 6.0; break;
            case 118: cx = 32.0; cy = 8.0; break;
            case 119: cx = 25.0; cy = 3.0; break;
            case 120: cx = 2.0; cy = 13.0; break;
            case 121: cx = 2.0; cy = 7.0; break;
            case 122: cx = 26.0; cy = 62.0; break;
            case 123: cx = 30.0; cy = 0.0; break;
            case 124: cx = 5.0; cy = 0.0; break;
            case 125: cx = 128.0; cy = 36.0; break;
            case 126: cx = 9.5; cy = 8.5; break;
            case 127: cx = 2.0; cy = 13.5; break;
            case 128: cx = 19.0; cy = 28.5; break;
            case 129: cx = 32.0; cy = 32.0; break;
            case 130: cx = 16.0; cy = 16.0; break;
            case 131: cx = 32.0; cy = 64.0; break;
            case 132: cx = 97.0; cy = 96.0; break;
            case 133: cx = 32.0; cy = 32.0; break;
            case 134: cx = 9.5; cy = 9.5; break;
            case 135: cx = 32.0; cy = 53.0; break;
            case 136: cx = 15.0; cy = 0.0; break;
            case 137: cx = 20.0; cy = 3.0; break;
            case 138: cx = 2.0; cy = 8.0; break;
            case 139: cx = 16.0; cy = 8.0; break;
            case 140: cx = 8.0; cy = 28.0; break;
            case 141: cx = 9.0; cy = 12.0; break;
            case 142: cx = 2.0; cy = 6.0; break;
            case 143: cx = 2.0; cy = 9.0; break;
            case 144: cx = 8.0; cy = 4.0; break;
            case 145: cx = 9.0; cy = 12.0; break;
            case 146: cx = 32.0; cy = 0.0; break;
            case 147: cx = 4.0; cy = 7.0; break;
            case 148: cx = 32.0; cy = 72.0; break;
            case 149: cx = 80.0; cy = 40.0; break;
            case 150: cx = 32.0; cy = 64.0; break;
            case 151: cx = 14.0; cy = 11.0; break;
            case 152: cx = 32.0; cy = 0.0; break;
            case 153: cx = 16.0; cy = 9.0; break;
            case 154: cx = 43.0; cy = 6.0; break;
            case 155: cx = 32.0; cy = 64.0; break;
            case 156: cx = 32.0; cy = 32.0; break;
            case 157: cx = 6.0; cy = 2.0; break;
            case 158: cx = 8.0; cy = 8.5; break;
            case 159: cx = 40.0; cy = 72.0; break;
            case 161: cx = 9.0; cy = 16.0; break;
            case 162: cx = 9.0; cy = 11.0; break;
            case 163: cx = 9.0; cy = 16.0; break;
            case 164: cx = 9.0; cy = 16.0; break;
            case 166: cx = 2.0; cy = 2.0; break;
            case 167: cx = 5.0; cy = 10.0; break;
            case 168: cx = 3.0; cy = 3.0; break;
            case 169: cx = 8.5; cy = 8.5; break;
            case 170: cx = 16.0; cy = 31.0; break;
            case 171: cx = 64.0; cy = 128.0; break;
            case 172: cx = 2.0; cy = 18.0; break;
            case 173: cx = 128.0; cy = 128.0; break;
            case 174: cx = 64.0; cy = 64.0; break;
            case 175: cx = 96.0; cy = 128.0; break;
            case 176: cx = 6.0; cy = 32.0; break;
            case 177: cx = 18.0; cy = 2.0; break;
            case 178: cx = 5.0; cy = 2.0; break;
            case 179: cx = 30.0; cy = 21.0; break;
            case 180: cx = 11.0; cy = 27.0; break;
            case 181: cx = 128.0; cy = 0.0; break;
            case 182: cx = 0.0; cy = 0.0; break;
            case 183: cx = 128.0; cy = 128.0; break;
            case 184: cx = 14.0; cy = 27.0; break;
            case 185: cx = 32.0; cy = 32.0; break;
            case 186: cx = 32.0; cy = 32.0; break;
            case 187: cx = 0.0; cy = 0.0; break;
            case 188: cx = 6.0; cy = 5.0; break;
            case 190: cx = 4.0; cy = 4.0; break;
            case 191: cx = 19.0; cy = 12.0; break;
            case 192: cx = 19.0; cy = 9.0; break;
            case 193: cx = 9.0; cy = 51.0; break;
            case 194: cx = 8.0; cy = 51.0; break;
            case 195: cx = 8.0; cy = 42.0; break;
            case 196: cx = 8.0; cy = 42.0; break;
            case 197: cx = 9.0; cy = 71.0; break;
            case 198: cx = 64.0; cy = 9.0; break;
            case 199: cx = 64.0; cy = 9.0; break;
            case 200: cx = 50.0; cy = 45.0; break;
            case 201: cx = 12.0; cy = 11.0; break;
            case 202: cx = 22.0; cy = 19.0; break;
            case 203: cx = 15.0; cy = 15.0; break;
            case 204: cx = 4.0; cy = 44.0; break;
            case 205: cx = 3.0; cy = 16.0; break;
            case 206: cx = 63.0; cy = 33.0; break;
            case 207: cx = 44.5; cy = 66.5; break;
            case 208: cx = 16.0; cy = 16.0; break;
            case 209: cx = 32.0; cy = 0.0; break;
            case 210: cx = 64.0; cy = 0.0; break;
            case 211: cx = 0.0; cy = 0.0; break;
            case 212: cx = 20.0; cy = 30.0; break;
            case 213: cx = 9.0; cy = 9.0; break;
            case 214: cx = 32.0; cy = 32.0; break;
            case 215: cx = 11.0; cy = 35.0; break;
            case 216: cx = 6.0; cy = 2.0; break;
            case 217: cx = 41.5; cy = 41.5; break;
            case 218: cx = 5.0; cy = 23.5; break;
            case 219: cx = 17.0; cy = 11.0; break;
            case 220: cx = 4.5; cy = 9.0; break;
            case 222: cx = 58.0; cy = 51.0; break;
            case 223: cx = 41.0; cy = 6.0; break;
            case 224: cx = 12.0; cy = 17.0; break;
            case 225: cx = 11.0; cy = 32.0; break;
            case 226: cx = 32.0; cy = 64.0; break;
            case 227: cx = 64.0; cy = 96.0; break;
            case 228: cx = 6.0; cy = 5.0; break;
            case 229: cx = 5.0; cy = 14.0; break;
            case 230: cx = 5.0; cy = 4.0; break;
            case 231: cx = 37.0; cy = 0.0; break;
            case 232: cx = 4.0; cy = 5.0; break;
            case 233: cx = 1.0; cy = 4.0; break;
            case 234: cx = 10.0; cy = 2.0; break;
            case 235: cx = 85.0; cy = 80.0; break;
            case 236: cx = 29.0; cy = 7.0; break;
            case 237: cx = 0.0; cy = 6.0; break;
            case 239: cx = 2.0; cy = 4.0; break;
            case 240: cx = 21.0; cy = 24.0; break;
            case 241: cx = 32.0; cy = 0.0; break;
            case 242: cx = 19.0; cy = 8.0; break;
            case 243: cx = 5.0; cy = 10.0; break;
            case 244: cx = 32.0; cy = 3.0; break;
            case 245: cx = 25.0; cy = 19.0; break;
            case 246: cx = 32.0; cy = 64.0; break;
            case 247: cx = 32.0; cy = 0.0; break;
            case 248: cx = 57.0; cy = 0.0; break;
            case 249: cx = 0.0; cy = 0.0; break;
            case 250: cx = 11.0; cy = 12.0; break;
            case 251: cx = 8.0; cy = 9.0; break;
            case 252: cx = 8.0; cy = 8.0; break;
            case 253: cx = 2.0; cy = 4.0; break;
            case 254: cx = 2.0; cy = 5.0; break;
            case 255: cx = 2.0; cy = 3.0; break;
            default: break;
        }

        return new Point(cx, cy);
    }
}