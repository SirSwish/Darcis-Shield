using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using UrbanChaosMissionEditor.Models;
using UrbanChaosMissionEditor.Services;

namespace UrbanChaosMissionEditor.Views.MapOverlays;

/// <summary>
/// Renders lights as colored circles.
/// </summary>
public class LightLayer : FrameworkElement
{
    private List<LightDisplayInfo> _lights = new();
    private bool _needsReload = true;

    private static readonly Pen LightOutlinePen = new(Brushes.Black, 1.0);

    static LightLayer()
    {
        LightOutlinePen.Freeze();
    }

    public static readonly DependencyProperty ShowRangesProperty =
        DependencyProperty.Register(nameof(ShowRanges), typeof(bool), typeof(LightLayer),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool ShowRanges
    {
        get => (bool)GetValue(ShowRangesProperty);
        set => SetValue(ShowRangesProperty, value);
    }

    public LightLayer()
    {
        ReadOnlyLightsDataService.Instance.LightsLoaded += (s, e) => { _needsReload = true; InvalidateVisual(); };
        ReadOnlyLightsDataService.Instance.LightsCleared += (s, e) => { _lights.Clear(); InvalidateVisual(); };
    }

    public void Refresh()
    {
        _needsReload = true;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (!ReadOnlyLightsDataService.Instance.IsLoaded) return;

        if (_needsReload)
        {
            LoadLights();
            _needsReload = false;
        }

        foreach (var light in _lights)
        {
            // Create brush from light color
            var color = Color.FromRgb(
                (byte)Math.Clamp(light.Red + 128, 0, 255),
                (byte)Math.Clamp(light.Green + 128, 0, 255),
                (byte)Math.Clamp(light.Blue + 128, 0, 255));

            var brush = new SolidColorBrush(color);
            brush.Freeze();

            // Draw range circle if enabled
            if (ShowRanges && light.Range > 0)
            {
                double rangeRadius = light.Range * 2; // Scale for visibility
                var rangeBrush = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B));
                rangeBrush.Freeze();
                var rangePen = new Pen(new SolidColorBrush(Color.FromArgb(80, color.R, color.G, color.B)), 1.0);
                rangePen.Freeze();

                dc.DrawEllipse(rangeBrush, rangePen, new Point(light.PixelX, light.PixelZ), rangeRadius, rangeRadius);
            }

            // Draw light center
            dc.DrawEllipse(brush, LightOutlinePen, new Point(light.PixelX, light.PixelZ), 6, 6);
        }
    }

    private void LoadLights()
    {
        _lights.Clear();

        var lightSvc = ReadOnlyLightsDataService.Instance;
        if (!lightSvc.IsLoaded) return;

        try
        {
            var entries = lightSvc.ReadAllEntries();

            foreach (var e in entries)
            {
                if (e.Used != 1) continue;

                int pixelX = ReadOnlyLightsDataService.WorldXToUiX(e.X);
                int pixelZ = ReadOnlyLightsDataService.WorldZToUiZ(e.Z);

                // Validate coordinates
                if (pixelX >= 0 && pixelX < MapConstants.MapPixelSize &&
                    pixelZ >= 0 && pixelZ < MapConstants.MapPixelSize)
                {
                    _lights.Add(new LightDisplayInfo
                    {
                        PixelX = pixelX,
                        PixelZ = pixelZ,
                        Range = e.Range,
                        Red = e.Red,
                        Green = e.Green,
                        Blue = e.Blue
                    });
                }
            }

            Debug.WriteLine($"[LightLayer] Loaded {_lights.Count} lights");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LightLayer] Error loading lights: {ex.Message}");
        }
    }

    private struct LightDisplayInfo
    {
        public int PixelX, PixelZ;
        public byte Range;
        public sbyte Red, Green, Blue;
    }
}