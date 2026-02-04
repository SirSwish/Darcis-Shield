using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UrbanChaosMissionEditor.ViewModels;

namespace UrbanChaosMissionEditor.Views;

/// <summary>
/// Map view control for displaying EventPoints on a 128x128 grid (8192x8192 pixels)
/// </summary>
public partial class MapViewControl : UserControl
{
    private const double MapSize = 8192.0;
    private const int GridSize = 128;
    private const double TileSize = MapSize / GridSize; // 64 pixels per tile

    private EventPointViewModel? _hoveredEventPoint;

    public MapViewControl()
    {
        InitializeComponent();

        // Subscribe to DataContext changes to handle selection updates
        DataContextChanged += OnDataContextChanged;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is MainViewModel newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedEventPoint))
        {
            UpdateSelectionRing();
            ScrollToSelectedEventPoint();
        }
    }

    private void UpdateSelectionRing()
    {
        var selected = ViewModel?.SelectedEventPoint;
        if (selected != null && selected.IsVisible)
        {
            Canvas.SetLeft(SelectionRing, selected.PixelX - 12);
            Canvas.SetTop(SelectionRing, selected.PixelZ - 12);
            SelectionRing.Visibility = Visibility.Visible;
        }
        else
        {
            SelectionRing.Visibility = Visibility.Collapsed;
        }
    }

    private void ScrollToSelectedEventPoint()
    {
        var selected = ViewModel?.SelectedEventPoint;
        if (selected == null) return;

        // Calculate the position in scrollviewer coordinates
        double zoom = ViewModel?.MapZoom ?? 1.0;
        double targetX = selected.PixelX * zoom;
        double targetY = selected.PixelZ * zoom;

        // Get the viewport size
        double viewportWidth = ScrollContainer.ViewportWidth;
        double viewportHeight = ScrollContainer.ViewportHeight;

        // Calculate scroll offsets to center the point
        double scrollX = targetX - (viewportWidth / 2);
        double scrollY = targetY - (viewportHeight / 2);

        // Clamp to valid scroll range
        scrollX = Math.Max(0, Math.Min(scrollX, ScrollContainer.ScrollableWidth));
        scrollY = Math.Max(0, Math.Min(scrollY, ScrollContainer.ScrollableHeight));

        ScrollContainer.ScrollToHorizontalOffset(scrollX);
        ScrollContainer.ScrollToVerticalOffset(scrollY);
    }

    private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null) return;

        var position = e.GetPosition(MapCanvas);

        // Find EventPoint at this position
        var eventPoint = ViewModel.FindEventPointAtPosition(position.X, position.Y, 12);

        if (eventPoint != null)
        {
            ViewModel.SelectedEventPoint = eventPoint;
        }
        else
        {
            // Click on empty space - optionally deselect
            // ViewModel.SelectedEventPoint = null;
        }

        // Focus the canvas to receive keyboard input
        MapCanvas.Focus();
    }

    private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(MapCanvas);

        // Update coordinate display
        int gridX = (int)(position.X / TileSize);
        int gridZ = (int)(position.Y / TileSize);

        // Clamp to valid range
        gridX = Math.Clamp(gridX, 0, GridSize - 1);
        gridZ = Math.Clamp(gridZ, 0, GridSize - 1);

        // World coordinates (256 per tile)
        int worldX = (int)position.X * 4; // 8192 / 32768 = 0.25, so multiply by 4
        int worldZ = (int)position.Y * 4;

        CoordinateText.Text = $"Grid: ({gridX}, {gridZ})  World: ({worldX}, {worldZ})  Pixel: ({(int)position.X}, {(int)position.Y})";

        // Handle hover highlighting
        if (ViewModel != null)
        {
            var eventPoint = ViewModel.FindEventPointAtPosition(position.X, position.Y, 12);

            if (eventPoint != _hoveredEventPoint)
            {
                // Clear old hover
                if (_hoveredEventPoint != null)
                {
                    _hoveredEventPoint.IsHovered = false;
                }

                // Set new hover
                _hoveredEventPoint = eventPoint;
                if (_hoveredEventPoint != null)
                {
                    _hoveredEventPoint.IsHovered = true;
                }
            }
        }
    }

    private void MapCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        CoordinateText.Text = string.Empty;

        // Clear hover state
        if (_hoveredEventPoint != null)
        {
            _hoveredEventPoint.IsHovered = false;
            _hoveredEventPoint = null;
        }
    }

    private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel == null) return;

        // Get mouse position before zoom
        var mousePos = e.GetPosition(MapCanvas);

        // Calculate zoom factor
        double zoomFactor = e.Delta > 0 ? 1.1 : 1 / 1.1;
        double oldZoom = ViewModel.MapZoom;
        double newZoom = Math.Clamp(oldZoom * zoomFactor, 0.1, 10.0);

        if (Math.Abs(newZoom - oldZoom) < 0.001) return;

        // Get current scroll position
        double oldScrollX = ScrollContainer.HorizontalOffset;
        double oldScrollY = ScrollContainer.VerticalOffset;

        // Calculate the point we want to keep fixed (mouse position in content coordinates)
        double contentX = (oldScrollX + mousePos.X) / oldZoom;
        double contentY = (oldScrollY + mousePos.Y) / oldZoom;

        // Apply new zoom
        ViewModel.MapZoom = newZoom;

        // Calculate new scroll position to keep the same point under the mouse
        double newScrollX = contentX * newZoom - mousePos.X;
        double newScrollY = contentY * newZoom - mousePos.Y;

        // Update scroll position after layout updates
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ScrollContainer.ScrollToHorizontalOffset(Math.Max(0, newScrollX));
            ScrollContainer.ScrollToVerticalOffset(Math.Max(0, newScrollY));
        }), System.Windows.Threading.DispatcherPriority.Loaded);

        e.Handled = true;
    }

    /// <summary>
    /// Convert pixel coordinates to grid coordinates
    /// </summary>
    public static (int X, int Z) PixelToGrid(double pixelX, double pixelY)
    {
        return (
            (int)(pixelX / TileSize),
            (int)(pixelY / TileSize)
        );
    }

    /// <summary>
    /// Convert grid coordinates to pixel coordinates (center of tile)
    /// </summary>
    public static (double X, double Y) GridToPixel(int gridX, int gridZ)
    {
        return (
            (gridX + 0.5) * TileSize,
            (gridZ + 0.5) * TileSize
        );
    }

    /// <summary>
    /// Convert world coordinates to pixel coordinates
    /// </summary>
    public static (double X, double Y) WorldToPixel(int worldX, int worldZ)
    {
        // World range: 0-32768, Pixel range: 0-8192
        return (
            worldX / 4.0,
            worldZ / 4.0
        );
    }

    /// <summary>
    /// Convert pixel coordinates to world coordinates
    /// </summary>
    public static (int X, int Z) PixelToWorld(double pixelX, double pixelY)
    {
        return (
            (int)(pixelX * 4),
            (int)(pixelY * 4)
        );
    }
}