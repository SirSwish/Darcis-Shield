using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UrbanChaosMissionEditor.Models;
using UrbanChaosMissionEditor.ViewModels;
using UrbanChaosMissionEditor.Views.MapOverlays;

namespace UrbanChaosMissionEditor.Views;

/// <summary>
/// Map view control for displaying EventPoints and map layers on a 128x128 grid (8192x8192 pixels)
/// Supports drag-to-move, drag-to-rotate, keyboard fine adjustment, and position selection mode.
/// </summary>
public partial class MapViewControl : UserControl
{
    private const double MapSize = 8192.0;
    private const int GridSize = 128;
    private const double TileSize = MapSize / GridSize; // 64 pixels per tile
    private const int PixelMoveAmount = 4; // World units per arrow key press (1 pixel = 4 world units)

    private EventPointViewModel? _hoveredEventPoint;

    // Flag to suppress centering when selecting from map
    private bool _suppressCenterOnSelection;

    // Drag state
    private enum DragMode { None, MovePoint, RotatePoint }
    private DragMode _currentDragMode = DragMode.None;
    private Point _dragStartPosition;
    private int _originalX, _originalZ;
    private byte _originalDirection;

    // Position selection mode
    private bool _positionSelectionMode;
    private Action<int, int>? _positionSelectedCallback;

    // Track last known mouse world position for AddEventPoint
    private int _lastMouseWorldX = 16384; // Center of map
    private int _lastMouseWorldZ = 16384;
    private bool _hasMousePosition;

    public MapViewControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Enable keyboard focus
        Focusable = true;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    #region Preview Event Handlers (for Position Selection Mode)

    private void UserControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MapViewControl] PreviewMouseLeftButtonDown fired, SelectionMode: {_positionSelectionMode}");

        if (_positionSelectionMode)
        {
            // Get position relative to the Surface
            var position = e.GetPosition(Surface);

            System.Diagnostics.Debug.WriteLine($"[MapViewControl] Preview click at Surface position: ({position.X}, {position.Y})");

            // Convert pixel to world coordinates (inverted)
            int worldX = (int)((8192.0 - position.X) * 4);
            int worldZ = (int)((8192.0 - position.Y) * 4);

            System.Diagnostics.Debug.WriteLine($"[MapViewControl] Calculated world coords: ({worldX}, {worldZ})");

            if (_positionSelectedCallback != null)
            {
                System.Diagnostics.Debug.WriteLine("[MapViewControl] Invoking callback from Preview handler");
                var callback = _positionSelectedCallback;
                ExitPositionSelectionMode();
                callback.Invoke(worldX, worldZ);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MapViewControl] WARNING: Callback is null in Preview handler!");
                ExitPositionSelectionMode();
            }

            e.Handled = true;
        }
    }

    private void UserControl_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_positionSelectionMode)
        {
            // Update coordinate display even in selection mode
            var position = e.GetPosition(Surface);
            UpdateCoordinateDisplay(position);

            // Ensure crosshair cursor
            Cursor = Cursors.Cross;
        }
    }

    #endregion

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
            // Only center if not suppressed (i.e., selection came from list, not map click)
            if (!_suppressCenterOnSelection)
            {
                ScrollToSelectedEventPoint();
            }
            _suppressCenterOnSelection = false; // Reset flag

            EventPointsLayer?.Refresh();
        }
        else if (e.PropertyName == nameof(MainViewModel.VisibleEventPoints))
        {
            EventPointsLayer?.Refresh();
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

    #region Position Selection Mode

    /// <summary>
    /// Enter position selection mode. The callback will be invoked with world coordinates when user clicks.
    /// </summary>
    public void EnterPositionSelectionMode(Action<int, int> callback)
    {
        System.Diagnostics.Debug.WriteLine("[MapViewControl] EnterPositionSelectionMode called");
        _positionSelectionMode = true;
        _positionSelectedCallback = callback;
        Cursor = Cursors.Cross;

        // Visual feedback - show selection mode message
        CoordinateText.Text = "[CLICK ON MAP TO SELECT POSITION - Press Escape to cancel]";

        // Capture mouse to receive all mouse events
        Mouse.Capture(this, CaptureMode.SubTree);

        System.Diagnostics.Debug.WriteLine($"[MapViewControl] Selection mode active: {_positionSelectionMode}, Callback set: {_positionSelectedCallback != null}, MouseCaptured: {Mouse.Captured == this}");
    }

    /// <summary>
    /// Exit position selection mode without selecting
    /// </summary>
    public void ExitPositionSelectionMode()
    {
        System.Diagnostics.Debug.WriteLine("[MapViewControl] ExitPositionSelectionMode called");
        _positionSelectionMode = false;
        _positionSelectedCallback = null;
        Cursor = Cursors.Arrow;
        CoordinateText.Text = string.Empty;

        // Release mouse capture
        if (Mouse.Captured == this)
        {
            Mouse.Capture(null);
        }
    }

    public bool IsInPositionSelectionMode => _positionSelectionMode;

    /// <summary>
    /// Get the current world position for placing new EventPoints.
    /// Returns the last known mouse position if available, otherwise calculates
    /// the center of the visible viewport, or falls back to map center.
    /// </summary>
    /// <returns>Tuple of (WorldX, WorldZ) coordinates</returns>
    public (int WorldX, int WorldZ) GetCurrentWorldPosition()
    {
        // If we have a recent mouse position, use that
        if (_hasMousePosition)
        {
            return (_lastMouseWorldX, _lastMouseWorldZ);
        }

        // Try to calculate center of visible viewport
        try
        {
            var scrollViewer = FindParent<ScrollViewer>(Surface);
            if (scrollViewer != null)
            {
                // Calculate the center of the visible area
                double centerPixelX = scrollViewer.HorizontalOffset + (scrollViewer.ViewportWidth / 2);
                double centerPixelY = scrollViewer.VerticalOffset + (scrollViewer.ViewportHeight / 2);

                // Clamp to valid map area
                centerPixelX = Math.Clamp(centerPixelX, 0, MapSize);
                centerPixelY = Math.Clamp(centerPixelY, 0, MapSize);

                // Convert to world coordinates (inverted)
                int worldX = (int)((MapSize - centerPixelX) * 4);
                int worldZ = (int)((MapSize - centerPixelY) * 4);

                return (worldX, worldZ);
            }
        }
        catch
        {
            // Fall through to default
        }

        // Default to center of map
        return (16384, 16384);
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T typedParent)
                return typedParent;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    /// <summary>
    /// Handle a position selection click forwarded from MainWindow
    /// </summary>
    public void HandlePositionSelectionClick(MouseButtonEventArgs e)
    {
        if (!_positionSelectionMode) return;

        // Get position relative to the Surface (need to account for scroll and zoom)
        var positionInMapView = e.GetPosition(this);
        var positionInSurface = e.GetPosition(Surface);

        System.Diagnostics.Debug.WriteLine($"[MapViewControl] HandlePositionSelectionClick - MapView pos: ({positionInMapView.X}, {positionInMapView.Y}), Surface pos: ({positionInSurface.X}, {positionInSurface.Y})");

        // Convert pixel to world coordinates (inverted)
        int worldX = (int)((8192.0 - positionInSurface.X) * 4);
        int worldZ = (int)((8192.0 - positionInSurface.Y) * 4);

        System.Diagnostics.Debug.WriteLine($"[MapViewControl] Calculated world coords: ({worldX}, {worldZ})");

        if (_positionSelectedCallback != null)
        {
            System.Diagnostics.Debug.WriteLine("[MapViewControl] Invoking callback");
            var callback = _positionSelectedCallback;
            ExitPositionSelectionMode();
            callback.Invoke(worldX, worldZ);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[MapViewControl] WARNING: Callback is null!");
            ExitPositionSelectionMode();
        }
    }

    /// <summary>
    /// Handle mouse move during position selection forwarded from MainWindow
    /// </summary>
    public void HandlePositionSelectionMove(MouseEventArgs e)
    {
        if (!_positionSelectionMode) return;

        var position = e.GetPosition(Surface);
        UpdateCoordinateDisplay(position);
        Cursor = Cursors.Cross;
    }

    #endregion

    #region Mouse Event Handlers

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(Surface);

        System.Diagnostics.Debug.WriteLine($"[MapViewControl] Surface_MouseLeftButtonDown at ({position.X}, {position.Y}), SelectionMode: {_positionSelectionMode}");

        // Position selection is now handled in PreviewMouseLeftButtonDown
        // Skip if we're in selection mode (should already be handled)
        if (_positionSelectionMode)
        {
            return;
        }

        if (ViewModel == null) return;

        // Check if we're clicking on the rotation handle of selected point
        if (ViewModel.SelectedEventPoint != null && EventPointsLayer != null)
        {
            if (EventPointsLayer.IsOverRotationHandle(position))
            {
                // Start rotation drag
                _currentDragMode = DragMode.RotatePoint;
                _dragStartPosition = position;
                _originalDirection = ViewModel.SelectedEventPoint.Direction;
                Surface.CaptureMouse();
                e.Handled = true;
                return;
            }
        }

        // Find EventPoint at this position
        var eventPoint = ViewModel.FindEventPointAtPosition(position.X, position.Y, 12);

        if (eventPoint != null)
        {
            // Suppress centering when selecting from map click
            _suppressCenterOnSelection = true;
            ViewModel.SelectedEventPoint = eventPoint;

            // Check for double-click (ClickCount == 2)
            if (e.ClickCount == 2)
            {
                // Open editor
                if (ViewModel.EditEventPointCommand.CanExecute(null))
                {
                    ViewModel.EditEventPointCommand.Execute(null);
                }
                e.Handled = true;
            }
            else
            {
                // Start move drag
                _currentDragMode = DragMode.MovePoint;
                _dragStartPosition = position;
                _originalX = eventPoint.Model.X;
                _originalZ = eventPoint.Model.Z;
                Surface.CaptureMouse();
            }
        }

        // Focus the surface to receive keyboard input
        Surface.Focus();
    }

    private void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_currentDragMode != DragMode.None)
        {
            _currentDragMode = DragMode.None;
            Surface.ReleaseMouseCapture();

            // Mark as dirty if we moved/rotated
            if (ViewModel != null)
            {
                ViewModel.IsDirty = true;
            }
        }
    }

    private void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(Surface);

        // Handle drag operations
        if (_currentDragMode == DragMode.MovePoint && ViewModel?.SelectedEventPoint != null)
        {
            HandleMovePointDrag(position, e);
            return;
        }
        else if (_currentDragMode == DragMode.RotatePoint && ViewModel?.SelectedEventPoint != null)
        {
            HandleRotatePointDrag(position);
            return;
        }

        // Update coordinate display
        UpdateCoordinateDisplay(position);

        // Update cursor based on what we're hovering over
        UpdateCursor(position);

        // Handle hover highlighting
        if (ViewModel != null && !_positionSelectionMode)
        {
            var eventPoint = ViewModel.FindEventPointAtPosition(position.X, position.Y, 12);

            if (eventPoint != _hoveredEventPoint)
            {
                if (_hoveredEventPoint != null)
                {
                    _hoveredEventPoint.IsHovered = false;
                }

                _hoveredEventPoint = eventPoint;
                if (_hoveredEventPoint != null)
                {
                    _hoveredEventPoint.IsHovered = true;
                }
            }
        }
    }

    private void HandleMovePointDrag(Point position, MouseEventArgs e)
    {
        var selected = ViewModel!.SelectedEventPoint!;

        // Calculate new pixel position
        double newPixelX = position.X;
        double newPixelY = position.Y;

        // Snap to grid if Ctrl is held
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Snap to nearest grid vertex (corner)
            newPixelX = Math.Round(newPixelX / TileSize) * TileSize;
            newPixelY = Math.Round(newPixelY / TileSize) * TileSize;
        }

        // Clamp to map bounds
        newPixelX = Math.Clamp(newPixelX, 0, MapSize);
        newPixelY = Math.Clamp(newPixelY, 0, MapSize);

        // Convert pixel to world coordinates (inverted: X = (8192 - Pixel) * 4)
        int newWorldX = (int)((8192.0 - newPixelX) * 4);
        int newWorldZ = (int)((8192.0 - newPixelY) * 4);

        // Update the model
        selected.Model.X = newWorldX;
        selected.Model.Z = newWorldZ;

        // Refresh display
        EventPointsLayer?.Refresh();
        UpdateCoordinateDisplay(position);
    }

    private void HandleRotatePointDrag(Point position)
    {
        var selected = ViewModel!.SelectedEventPoint!;
        var center = new Point(selected.PixelX, selected.PixelZ);

        // Calculate new direction from center to mouse position
        byte newDirection = EventPointsLayer.CalculateDirectionFromPoints(center, position);

        // Update the model
        selected.Model.Direction = newDirection;

        // Refresh display
        EventPointsLayer?.Refresh();
    }

    private void UpdateCoordinateDisplay(Point position)
    {
        // Calculate UI grid position (top-left origin)
        int uiGridX = (int)(position.X / TileSize);
        int uiGridZ = (int)(position.Y / TileSize);

        // Clamp to valid range
        uiGridX = Math.Clamp(uiGridX, 0, GridSize - 1);
        uiGridZ = Math.Clamp(uiGridZ, 0, GridSize - 1);

        // Convert to game grid coordinates (inverted from UI)
        int gameGridX = (GridSize - 1) - uiGridX;
        int gameGridZ = (GridSize - 1) - uiGridZ;

        // World coordinates - inverted to match game coordinate system
        int worldX = (int)((8192.0 - position.X) * 4);
        int worldZ = (int)((8192.0 - position.Y) * 4);

        // Track the mouse position for AddEventPoint
        _lastMouseWorldX = worldX;
        _lastMouseWorldZ = worldZ;
        _hasMousePosition = true;

        string modeText = _positionSelectionMode ? " [SELECT POSITION]" : "";
        string dragText = _currentDragMode switch
        {
            DragMode.MovePoint => " [DRAGGING]",
            DragMode.RotatePoint => " [ROTATING]",
            _ => ""
        };

        CoordinateText.Text = $"Grid: ({gameGridX}, {gameGridZ})  World: ({worldX}, {worldZ})  Pixel: ({(int)position.X}, {(int)position.Y}){modeText}{dragText}";
    }

    private void UpdateCursor(Point position)
    {
        if (_positionSelectionMode)
        {
            Cursor = Cursors.Cross;
            return;
        }

        if (ViewModel?.SelectedEventPoint != null && EventPointsLayer != null)
        {
            if (EventPointsLayer.IsOverRotationHandle(position))
            {
                Cursor = Cursors.Hand;
                return;
            }
        }

        var eventPoint = ViewModel?.FindEventPointAtPosition(position.X, position.Y, 12);
        Cursor = eventPoint != null ? Cursors.SizeAll : Cursors.Arrow;
    }

    private void Surface_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_positionSelectionMode)
        {
            CoordinateText.Text = string.Empty;
            Cursor = Cursors.Arrow;
        }

        if (_hoveredEventPoint != null)
        {
            _hoveredEventPoint.IsHovered = false;
            _hoveredEventPoint = null;
        }
    }

    #endregion

    #region Keyboard Event Handlers

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (ViewModel?.SelectedEventPoint == null) return;

        var selected = ViewModel.SelectedEventPoint;
        bool handled = false;

        // Arrow keys for fine position adjustment (1 pixel = 4 world units)
        // Since world coords are inverted, we need to invert the direction
        switch (e.Key)
        {
            case Key.Left:
                selected.Model.X += PixelMoveAmount; // Inverted: moving left in UI increases X
                handled = true;
                break;
            case Key.Right:
                selected.Model.X -= PixelMoveAmount; // Inverted: moving right in UI decreases X
                handled = true;
                break;
            case Key.Up:
                selected.Model.Z += PixelMoveAmount; // Inverted: moving up in UI increases Z
                handled = true;
                break;
            case Key.Down:
                selected.Model.Z -= PixelMoveAmount; // Inverted: moving down in UI decreases Z
                handled = true;
                break;
        }

        if (handled)
        {
            ViewModel.IsDirty = true;
            EventPointsLayer?.Refresh();
            e.Handled = true;
        }
    }

    #endregion

    #region Mouse Wheel (Zoom)

    private void Surface_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel == null) return;

        // Only zoom if Ctrl is held - otherwise let ScrollViewer handle normal scrolling
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return; // Don't handle - let ScrollViewer scroll normally
        }

        // Get mouse position relative to the ScrollViewer viewport
        var mouseInViewport = e.GetPosition(ScrollContainer);

        // Calculate zoom factor
        double zoomFactor = e.Delta > 0 ? 1.1 : 1 / 1.1;
        double oldZoom = ViewModel.MapZoom;
        double newZoom = Math.Clamp(oldZoom * zoomFactor, 0.1, 10.0);

        if (Math.Abs(newZoom - oldZoom) < 0.001) return;

        // Get current scroll position
        double oldScrollX = ScrollContainer.HorizontalOffset;
        double oldScrollY = ScrollContainer.VerticalOffset;

        // Calculate the point under the cursor in unscaled map coordinates
        double mapX = (oldScrollX + mouseInViewport.X) / oldZoom;
        double mapY = (oldScrollY + mouseInViewport.Y) / oldZoom;

        // Apply new zoom
        ViewModel.MapZoom = newZoom;

        // Calculate new scroll position to keep the same map point under the cursor
        double newScrollX = mapX * newZoom - mouseInViewport.X;
        double newScrollY = mapY * newZoom - mouseInViewport.Y;

        // Update scroll position after layout updates
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ScrollContainer.ScrollToHorizontalOffset(Math.Max(0, newScrollX));
            ScrollContainer.ScrollToVerticalOffset(Math.Max(0, newScrollY));
        }), System.Windows.Threading.DispatcherPriority.Loaded);

        e.Handled = true;
    }

    #endregion

    #region Coordinate Conversion Helpers

    /// <summary>
    /// Convert pixel coordinates to UI grid coordinates (top-left origin)
    /// </summary>
    public static (int X, int Z) PixelToUiGrid(double pixelX, double pixelY)
    {
        return (
            (int)(pixelX / TileSize),
            (int)(pixelY / TileSize)
        );
    }

    /// <summary>
    /// Convert pixel coordinates to game grid coordinates (bottom-right origin, inverted)
    /// </summary>
    public static (int X, int Z) PixelToGameGrid(double pixelX, double pixelY)
    {
        int uiX = (int)(pixelX / TileSize);
        int uiZ = (int)(pixelY / TileSize);
        return (
            (GridSize - 1) - uiX,
            (GridSize - 1) - uiZ
        );
    }

    /// <summary>
    /// Convert UI grid coordinates to pixel coordinates (center of tile)
    /// </summary>
    public static (double X, double Y) UiGridToPixel(int uiGridX, int uiGridZ)
    {
        return (
            (uiGridX + 0.5) * TileSize,
            (uiGridZ + 0.5) * TileSize
        );
    }

    /// <summary>
    /// Convert game grid coordinates (inverted) to pixel coordinates (center of tile)
    /// </summary>
    public static (double X, double Y) GameGridToPixel(int gameGridX, int gameGridZ)
    {
        int uiX = (GridSize - 1) - gameGridX;
        int uiZ = (GridSize - 1) - gameGridZ;
        return (
            (uiX + 0.5) * TileSize,
            (uiZ + 0.5) * TileSize
        );
    }

    /// <summary>
    /// Convert world coordinates to pixel coordinates
    /// </summary>
    public static (double X, double Y) WorldToPixel(int worldX, int worldZ)
    {
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

    #endregion
}