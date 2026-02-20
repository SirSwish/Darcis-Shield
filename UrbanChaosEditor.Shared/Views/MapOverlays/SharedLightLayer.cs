// ============================================================
// UrbanChaosEditor.Shared/Views/MapOverlays/SharedLightLayer.cs
// ============================================================
// Shared light display layer (read-only)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using UrbanChaosEditor.Shared.Models;

namespace UrbanChaosEditor.Shared.Views.MapOverlays;

/// <summary>
/// Shared light display layer.
/// Renders lights as colored circles, optionally with range indicators.
/// For read-only display in Mission Editor and base for editing in Light Editor.
/// </summary>
public class SharedLightLayer : MapOverlayBase
{
    #region Static Pens

    protected static readonly Pen LightOutlinePen;

    static SharedLightLayer()
    {
        LightOutlinePen = new Pen(Brushes.Black, 1.0);
        LightOutlinePen.Freeze();
    }

    #endregion

    #region Cached Data

    protected List<LightDisplayInfo> Lights = new();
    protected bool NeedsReload = true;

    #endregion

    #region Data Provider

    private ILightDataProvider? _dataProvider;

    /// <summary>
    /// Set the data provider for this layer.
    /// </summary>
    public void SetDataProvider(ILightDataProvider provider)
    {
        _dataProvider = provider;

        if (_dataProvider != null)
        {
            _dataProvider.SubscribeLightsLoaded(OnLightsLoaded);
            _dataProvider.SubscribeLightsCleared(OnLightsCleared);

            if (_dataProvider.IsLoaded)
            {
                NeedsReload = true;
                RefreshOnUiThread();
            }
        }
    }

    private void OnLightsLoaded()
    {
        NeedsReload = true;
        RefreshOnUiThread();
    }

    private void OnLightsCleared()
    {
        Lights.Clear();
        RefreshOnUiThread();
    }

    #endregion

    #region Configuration

    public static readonly DependencyProperty ShowRangesProperty =
        DependencyProperty.Register(nameof(ShowRanges), typeof(bool), typeof(SharedLightLayer),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedLightIndexProperty =
        DependencyProperty.Register(nameof(SelectedLightIndex), typeof(int), typeof(SharedLightLayer),
            new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Whether to show range circles around lights.
    /// </summary>
    public bool ShowRanges
    {
        get => (bool)GetValue(ShowRangesProperty);
        set => SetValue(ShowRangesProperty, value);
    }

    /// <summary>
    /// Index of the currently selected light (-1 for none).
    /// </summary>
    public int SelectedLightIndex
    {
        get => (int)GetValue(SelectedLightIndexProperty);
        set => SetValue(SelectedLightIndexProperty, value);
    }

    /// <summary>
    /// Light circle radius.
    /// </summary>
    public double LightRadius { get; set; } = 6.0;

    /// <summary>
    /// Range scale factor (range * scale = display radius).
    /// </summary>
    public double RangeScale { get; set; } = 2.0;

    #endregion

    #region Rendering

    public override void Refresh()
    {
        NeedsReload = true;
        base.Refresh();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (_dataProvider == null || !_dataProvider.IsLoaded) return;

        if (NeedsReload)
        {
            LoadLights();
            NeedsReload = false;
        }

        foreach (var light in Lights)
        {
            DrawLight(dc, light, light.Index == SelectedLightIndex);
        }
    }

    /// <summary>
    /// Draw a single light. Override in derived classes for custom rendering.
    /// </summary>
    protected virtual void DrawLight(DrawingContext dc, LightDisplayInfo light, bool isSelected)
    {
        // Create brush from light color
        var color = Color.FromRgb(
            (byte)Math.Clamp(light.Red + 128, 0, 255),
            (byte)Math.Clamp(light.Green + 128, 0, 255),
            (byte)Math.Clamp(light.Blue + 128, 0, 255));

        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var center = new Point(light.PixelX, light.PixelZ);

        // Draw range circle if enabled
        if (ShowRanges && light.Range > 0)
        {
            double rangeRadius = light.Range * RangeScale;
            var rangeBrush = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B));
            rangeBrush.Freeze();
            var rangePen = new Pen(new SolidColorBrush(Color.FromArgb(80, color.R, color.G, color.B)), 1.0);
            rangePen.Freeze();

            dc.DrawEllipse(rangeBrush, rangePen, center, rangeRadius, rangeRadius);
        }

        // Draw selection ring if selected
        if (isSelected)
        {
            var selectionPen = new Pen(Brushes.Yellow, 2.0);
            selectionPen.Freeze();
            dc.DrawEllipse(null, selectionPen, center, LightRadius + 4, LightRadius + 4);
        }

        // Draw light center
        dc.DrawEllipse(brush, LightOutlinePen, center, LightRadius, LightRadius);
    }

    #endregion

    #region Light Loading

    protected virtual void LoadLights()
    {
        Lights.Clear();

        if (_dataProvider == null || !_dataProvider.IsLoaded) return;

        try
        {
            Lights = _dataProvider.ReadAllLights();

            // Filter to only used lights and validate coordinates
            Lights = Lights.FindAll(l =>
                l.Used &&
                l.PixelX >= 0 && l.PixelX < SharedMapConstants.MapPixelSize &&
                l.PixelZ >= 0 && l.PixelZ < SharedMapConstants.MapPixelSize);

            Debug.WriteLine($"[SharedLightLayer] Loaded {Lights.Count} lights");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SharedLightLayer] Error loading lights: {ex.Message}");
        }
    }

    /// <summary>
    /// Manually set the lights to render.
    /// </summary>
    public void SetLights(List<LightDisplayInfo> lights)
    {
        Lights = lights ?? new List<LightDisplayInfo>();
        NeedsReload = false;
        InvalidateVisual();
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Convert world X coordinate to UI X coordinate.
    /// </summary>
    public static int WorldXToUiX(int worldX)
    {
        return SharedMapConstants.MapPixelSize - (worldX >> 8);
    }

    /// <summary>
    /// Convert world Z coordinate to UI Z coordinate.
    /// </summary>
    public static int WorldZToUiZ(int worldZ)
    {
        return SharedMapConstants.MapPixelSize - (worldZ >> 8);
    }

    #endregion
}