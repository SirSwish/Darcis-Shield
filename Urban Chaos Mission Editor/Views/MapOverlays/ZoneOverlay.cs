using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.ViewModels;

namespace UrbanChaosMissionEditor.Views.MapOverlays;

/// <summary>
/// Overlay control that renders mission zones as a semi-transparent layer
/// </summary>
public class ZoneOverlay : FrameworkElement
{
    private const int GridSize = 128;
    private const double TileSize = 64.0; // 8192 / 128 = 64 pixels per tile

    private MainViewModel? _viewModel;
    private WriteableBitmap? _zoneBitmap;

    public ZoneOverlay()
    {
        // Set size to match map
        Width = 8192;
        Height = 8192;

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is MainViewModel newVm)
        {
            _viewModel = newVm;
            newVm.PropertyChanged += ViewModel_PropertyChanged;
            Refresh();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ShowZones) ||
            e.PropertyName == nameof(MainViewModel.CurrentMission) ||
            e.PropertyName == nameof(MainViewModel.SelectedZoneType))
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_viewModel == null || !_viewModel.ShowZones) return;

        var zones = _viewModel.GetZoneArray();
        if (zones == null) return;

        // Create or update bitmap
        if (_zoneBitmap == null || _zoneBitmap.PixelWidth != GridSize)
        {
            _zoneBitmap = new WriteableBitmap(GridSize, GridSize, 96, 96, PixelFormats.Bgra32, null);
        }

        // Update bitmap pixels
        int stride = GridSize * 4;
        byte[] pixels = new byte[GridSize * GridSize * 4];

        for (int z = 0; z < GridSize; z++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                // Invert coordinates for display (game origin is bottom-right)
                int displayX = GridSize - 1 - x;
                int displayZ = GridSize - 1 - z;

                var selected = _viewModel.SelectedZoneType;
                if (selected == ZoneType.None) return;

                var flags = (ZoneType)zones[x, z];
                var color = (flags & selected) != 0
                    ? ZoneColors.GetColor(selected)
                    : Colors.Transparent;

                int pixelIndex = (displayZ * GridSize + displayX) * 4;
                pixels[pixelIndex + 0] = color.B;     // Blue
                pixels[pixelIndex + 1] = color.G;     // Green
                pixels[pixelIndex + 2] = color.R;     // Red
                pixels[pixelIndex + 3] = color.A;     // Alpha
            }
        }

        _zoneBitmap.WritePixels(new Int32Rect(0, 0, GridSize, GridSize), pixels, stride, 0);

        // Draw scaled to full map size
        dc.DrawImage(_zoneBitmap, new Rect(0, 0, 8192, 8192));
    }
}