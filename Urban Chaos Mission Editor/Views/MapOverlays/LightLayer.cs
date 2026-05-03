using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using UrbanChaosMissionEditor.Services;

namespace UrbanChaosMissionEditor.Views.MapOverlays;

/// <summary>
/// Read-only mission light overlay that mirrors the Light Editor's visual layer.
/// </summary>
public sealed class LightLayer : Canvas
{
    public static readonly DependencyProperty ShowRangesProperty =
        DependencyProperty.Register(
            nameof(ShowRanges),
            typeof(bool),
            typeof(LightLayer),
            new FrameworkPropertyMetadata(false, OnShowRangesChanged));

    public bool ShowRanges
    {
        get => (bool)GetValue(ShowRangesProperty);
        set => SetValue(ShowRangesProperty, value);
    }

    public LightLayer()
    {
        Width = 8192;
        Height = 8192;
        Background = null;
        IsHitTestVisible = false;
        Panel.SetZIndex(this, 4);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void OnShowRangesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LightLayer layer)
            layer.Redraw();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ReadOnlyLightsDataService.Instance.LightsLoaded += OnLightsChanged;
        ReadOnlyLightsDataService.Instance.LightsCleared += OnLightsChanged;
        Redraw();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ReadOnlyLightsDataService.Instance.LightsLoaded -= OnLightsChanged;
        ReadOnlyLightsDataService.Instance.LightsCleared -= OnLightsChanged;
    }

    private void OnLightsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess())
            Redraw();
        else
            Dispatcher.Invoke(Redraw);
    }

    private void Redraw()
    {
        Children.Clear();

        var svc = ReadOnlyLightsDataService.Instance;
        if (!svc.IsLoaded)
            return;

        try
        {
            var entries = svc.ReadAllEntries();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Used != 1)
                    continue;

                int uiX = ReadOnlyLightsDataService.WorldXToUiX(entry.X);
                int uiZ = ReadOnlyLightsDataService.WorldZToUiZ(entry.Z);

                if (uiX < 0 || uiX >= Width || uiZ < 0 || uiZ >= Height)
                    continue;

                double uiRadius = Math.Max(8.0, entry.Range / 4.0);

                byte r = unchecked((byte)(entry.Red + 128));
                byte g = unchecked((byte)(entry.Green + 128));
                byte b = unchecked((byte)(entry.Blue + 128));

                var fill = new RadialGradientBrush(
                    Color.FromArgb(210, r, g, b),
                    Color.FromArgb(0, r, g, b));

                var glow = new Ellipse
                {
                    Width = uiRadius * 2,
                    Height = uiRadius * 2,
                    Fill = fill,
                    Stroke = new SolidColorBrush(Color.FromArgb(100, r, g, b)),
                    StrokeThickness = 1.0,
                    IsHitTestVisible = false
                };

                SetLeft(glow, uiX - uiRadius);
                SetTop(glow, uiZ - uiRadius);
                Children.Add(glow);

                if (ShowRanges)
                {
                    double exactRadius = entry.Range / 4.0;
                    double rangeSize = exactRadius * 2;
                    var rangeCircle = new Ellipse
                    {
                        Width = rangeSize,
                        Height = rangeSize,
                        Stroke = new SolidColorBrush(Color.FromArgb(140, r, g, b)),
                        StrokeThickness = 1.0,
                        Fill = Brushes.Transparent,
                        IsHitTestVisible = false
                    };

                    SetLeft(rangeCircle, uiX - exactRadius);
                    SetTop(rangeCircle, uiZ - exactRadius);
                    Children.Add(rangeCircle);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionLightLayer] Redraw skipped: {ex.Message}");
        }
    }
}
