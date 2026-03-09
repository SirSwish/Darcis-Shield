// ============================================================
// UrbanChaosEditor.Shared/Views/MapOverlays/SharedPrimGraphicsLayer.cs
// ============================================================
// Shared prim/object graphics rendering layer

using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UrbanChaosEditor.Shared.Views.MapOverlays;

/// <summary>
/// Shared prim graphics layer that renders sprite images for each prim/object.
/// Can be used in read-only mode or extended for editing (Map Editor).
/// </summary>
public class SharedPrimGraphicsLayer : MapOverlayBase
{
    #region Sprite Cache

    /// <summary>
    /// Cached sprite: image + anchor (center-of-rotation) in image pixels from top-left.
    /// </summary>
    protected sealed class PrimSprite
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

    #endregion

    #region Constants

    // 256 steps around a full circle
    protected const double DegreesPerYaw = 360.0 / 256.0;

    #endregion

    #region Cached Data

    protected List<PrimDisplayInfo> Prims = new();

    #endregion

    #region Data Provider

    private IPrimDataProvider? _dataProvider;

    /// <summary>
    /// Set the data provider for this layer.
    /// </summary>
    public void SetDataProvider(IPrimDataProvider provider)
    {
        _dataProvider = provider;

        if (_dataProvider != null)
        {
            _dataProvider.SubscribeMapLoaded(OnMapLoaded);
            _dataProvider.SubscribeMapCleared(OnMapCleared);

            if (_dataProvider.IsLoaded)
            {
                RefreshPrims();
                RefreshOnUiThread();
            }
        }
    }

    private void OnMapLoaded()
    {
        RefreshPrims();
        RefreshOnUiThread();
    }

    private void OnMapCleared()
    {
        Prims.Clear();
        RefreshOnUiThread();
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Assembly name for loading embedded resources.
    /// Default is the current assembly. Override for project-specific resources.
    /// </summary>
    public string ResourceAssemblyName { get; set; } = "UrbanChaosEditor.Shared";

    /// <summary>
    /// Path within the assembly for prim graphics.
    /// Default: "Assets/Images/PrimGraphics"
    /// </summary>
    public string PrimGraphicsPath { get; set; } = "Assets/Images/PrimGraphics";

    #endregion

    public SharedPrimGraphicsLayer()
    {
        SnapsToDevicePixels = true;
    }

    #region Prim Data

    /// <summary>
    /// Refresh the prim list from the data provider.
    /// </summary>
    protected virtual void RefreshPrims()
    {
        Debug.WriteLine("[SharedPrimGraphicsLayer] Refreshing prims list...");

        if (_dataProvider == null || !_dataProvider.IsLoaded)
        {
            Prims.Clear();
            return;
        }

        Prims = _dataProvider.ReadAllPrims();
        Debug.WriteLine($"[SharedPrimGraphicsLayer] Loaded {Prims.Count} prims");
    }

    /// <summary>
    /// Manually set the prims to render (for editors that manage their own prim list).
    /// </summary>
    public void SetPrims(List<PrimDisplayInfo> prims)
    {
        Prims = prims ?? new List<PrimDisplayInfo>();
        InvalidateVisual();
    }

    #endregion

    #region Rendering

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (Prims.Count == 0) return;

        int drawn = 0;
        var missingPrimNumbers = new HashSet<byte>();

        // Sort by Y (height) for proper layering - lower Y drawn first
        foreach (var p in Prims.OrderBy(p => p.Y))
        {
            var sprite = GetPrimSprite(p.PrimNumber);
            if (sprite != null)
            {
                DrawPrimSprite(dc, p, sprite);
                drawn++;
            }
            else
            {
                missingPrimNumbers.Add(p.PrimNumber);
            }
        }

        Debug.WriteLine($"[SharedPrimGraphicsLayer] Rendered {drawn} prims");
        if (missingPrimNumbers.Count > 0)
        {
            Debug.WriteLine($"[SharedPrimGraphicsLayer] Missing sprites: {string.Join(", ", missingPrimNumbers.OrderBy(x => x))}");
        }
    }

    /// <summary>
    /// Draw a single prim sprite. Override in derived classes to add selection highlighting, etc.
    /// </summary>
    protected virtual void DrawPrimSprite(DrawingContext dc, PrimDisplayInfo prim, PrimSprite sprite)
    {
        double w = sprite.Width;
        double h = sprite.Height;

        // Yaw 0 => no rotation. Positive yaw rotates CCW on screen
        double angleDeg = -prim.Yaw * DegreesPerYaw;

        dc.PushTransform(new TranslateTransform(prim.PixelX, prim.PixelZ));
        dc.PushTransform(new RotateTransform(angleDeg));

        var rect = new Rect(-sprite.AnchorX, -sprite.AnchorY, w, h);
        dc.DrawImage(sprite.Image, rect);

        dc.Pop(); // Rotate
        dc.Pop(); // Translate
    }

    #endregion

    #region Sprite Loading

    protected PrimSprite? GetPrimSprite(byte primNumber)
    {
        if (_spriteCache.TryGetValue(primNumber, out var cached))
            return cached;

        var sprite = LoadPrimGraphic(primNumber);
        _spriteCache[primNumber] = sprite;

        if (sprite == null)
            Debug.WriteLine($"[SharedPrimGraphicsLayer] MISSING sprite for prim {primNumber}");

        return sprite;
    }

    /// <summary>
    /// Loads NNN.png from embedded WPF resources.
    /// Tries multiple approaches for cross-assembly resource loading.
    /// </summary>
    protected virtual PrimSprite? LoadPrimGraphic(byte primNumber)
    {
        string fileName = $"{primNumber:D3}.png";

        // Build list of URIs to try
        var urisToTry = new List<string>();

        // 1. Shared assembly with component syntax
        urisToTry.Add($"pack://application:,,,/UrbanChaosEditor.Shared;component/{PrimGraphicsPath}/{fileName}");

        // 2. Shared assembly with "Urban Chaos Editor.Shared" (spaces)
        urisToTry.Add($"pack://application:,,,/Urban Chaos Editor.Shared;component/{PrimGraphicsPath}/{fileName}");

        // 3. Try configured assembly name if different
        if (!string.IsNullOrEmpty(ResourceAssemblyName) &&
            ResourceAssemblyName != "UrbanChaosEditor.Shared" &&
            ResourceAssemblyName != "Urban Chaos Editor.Shared")
        {
            urisToTry.Add($"pack://application:,,,/{ResourceAssemblyName};component/{PrimGraphicsPath}/{fileName}");
        }

        // 4. Local application resources (no assembly prefix)
        urisToTry.Add($"pack://application:,,,/{PrimGraphicsPath}/{fileName}");

        foreach (var uriString in urisToTry)
        {
            try
            {
                var uri = new Uri(uriString, UriKind.Absolute);

                // Try using Application.GetResourceStream first (more reliable for cross-assembly)
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo != null)
                {
                    using var stream = streamInfo.Stream;
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = stream;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();

                    double w = bmp.PixelWidth;
                    double h = bmp.PixelHeight;
                    var anchor = PrimAnchors.GetAnchorForPrim(primNumber, w, h);

                    if (_spriteCache.Count == 0)
                    {
                        Debug.WriteLine($"[SharedPrimGraphicsLayer] SUCCESS via GetResourceStream: {uriString}");
                    }

                    return new PrimSprite(bmp, anchor.X, anchor.Y, w, h);
                }
            }
            catch
            {
                // Try next
            }

            // Fallback: try direct BitmapImage loading
            try
            {
                var uri = new Uri(uriString, UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                double w = bmp.PixelWidth;
                double h = bmp.PixelHeight;
                var anchor = PrimAnchors.GetAnchorForPrim(primNumber, w, h);

                if (_spriteCache.Count == 0)
                {
                    Debug.WriteLine($"[SharedPrimGraphicsLayer] SUCCESS via BitmapImage: {uriString}");
                }

                return new PrimSprite(bmp, anchor.X, anchor.Y, w, h);
            }
            catch
            {
                // Try next
            }
        }

        // All attempts failed
        if (_spriteCache.Count < 3) // Only log first few failures
        {
            Debug.WriteLine($"[SharedPrimGraphicsLayer] FAILED to load prim {primNumber}. Tried URIs:");
            foreach (var uri in urisToTry)
            {
                Debug.WriteLine($"  - {uri}");
            }
        }

        return null;
    }

    /// <summary>
    /// Clear the sprite cache (e.g., if resources are updated).
    /// </summary>
    public void ClearSpriteCache()
    {
        _spriteCache.Clear();
    }

    #endregion
}