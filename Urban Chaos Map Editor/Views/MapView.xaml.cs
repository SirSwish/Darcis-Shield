using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;
using UrbanChaosMapEditor.ViewModels;

namespace UrbanChaosMapEditor.Views
{
    public partial class MapView : UserControl
    {
        // Tweak to taste
        private const double ZoomStep = 1.10;      // 10% per wheel notch
        private const double MinZoom = 0.10;
        private const double MaxZoom = 8.00;
        private bool _isSettingAltitude;      // For click-and-drag altitude painting
        private HashSet<(int, int)>? _altitudePaintedTiles; // Track painted tiles to avoid redundant writes

        // Walkable drawing state
        private bool _isDrawingWalkableRect;

        // Texture painting state - HYBRID MODE
        private bool _isTextureDragging;           // True when actively dragging for rectangle
        private bool _isTextureStrokePainting;     // True when Shift+drag painting
        private Point _textureMouseDownPos;        // Initial click position
        private HashSet<(int, int)>? _texturePaintedTiles;  // Track painted tiles to avoid redundant writes
        private const double TextureDragThreshold = 16.0;    // Pixels before rectangle mode activates

        private static bool IsCtrlDown()
            => Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

        private static bool IsShiftDown()
            => Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        private readonly HeightsAccessor _heights = new HeightsAccessor(MapDataService.Instance);
        private AltitudeAccessor? _altitude = new AltitudeAccessor(MapDataService.Instance);

        // Level tool state
        private bool _isLeveling = false;
        private sbyte _levelSource;
        private (int tx, int ty)? _lastLeveledTile;

        /// <summary>
        /// Event raised when walkable rectangle drawing is completed.
        /// Subscribe to this in BuildingsTab to show the Add Walkable dialog.
        /// </summary>
        public event EventHandler? WalkableDrawingCompleted;

        public MapView()
        {
            InitializeComponent();
            PreviewMouseMove += OnPreviewMouseMove;
            MouseLeave += OnMouseLeave;
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            LostMouseCapture += OnLostMouseCapture;
            PreviewMouseWheel += OnPreviewMouseWheel;
            PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;

            // Ensure we can receive keyboard input
            MouseEnter += (_, __) => Focus();
            PreviewMouseLeftButtonDown += (_, __) => Focus();
        }

        private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = LogicalTreeHelper.GetParent(d) ?? VisualTreeHelper.GetParent(d);
            }
            return null;
        }

        private void OnPreviewMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            // Ignore scrollbar clicks
            if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                return;

            if (DataContext is not MapViewModel vm || Surface == null) return;

            Point mouseDownPos = e.GetPosition(Surface);

            // Get MainWindowViewModel once for status messages
            var mainVm = Application.Current.MainWindow?.DataContext as MainWindowViewModel;

            // === WALKABLE DRAWING MODE (check first, before other tools) ===
            if (vm.IsDrawingWalkable)
            {
                int tx = (int)Math.Floor(mouseDownPos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseDownPos.Y / MapConstants.TileSize);

                tx = Math.Clamp(tx, 0, MapConstants.TilesPerSide - 1);
                ty = Math.Clamp(ty, 0, MapConstants.TilesPerSide - 1);

                vm.WalkableSelectionStartX = tx;
                vm.WalkableSelectionStartY = ty;
                vm.WalkableSelectionEndX = tx;
                vm.WalkableSelectionEndY = ty;

                _isDrawingWalkableRect = true;
                CaptureMouse();

                if (mainVm != null)
                    mainVm.StatusMessage = "Drag to select walkable region...";

                e.Handled = true;
                return;
            }

            // === FACET REDRAW MODE ===
            if (vm.IsRedrawingFacet)
            {
                Point p = e.GetPosition(Surface);
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);

                if (vm.HandleFacetRedrawClick(uiX, uiZ))
                {
                    e.Handled = true;
                    InvalidateVisual();
                    return;
                }
            }

            // === FACET MULTI-DRAW MODE ===
            if (vm.IsMultiDrawingFacets)
            {
                Point p = e.GetPosition(Surface);
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);

                if (vm.HandleFacetMultiDrawClick(uiX, uiZ))
                {
                    e.Handled = true;
                    return;
                }
            }

            // === LADDER PLACEMENT MODE ===
            if (vm.IsPlacingLadder)
            {
                Point p = e.GetPosition(Surface);
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);

                if (vm.HandleLadderPlacementClick(uiX, uiZ))
                {
                    e.Handled = true;
                    return;
                }
            }

            // === CABLE PLACEMENT MODE ===
            if (vm.IsPlacingCable)
            {
                Point p = e.GetPosition(Surface);
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);

                if (vm.HandleCablePlacementClick(uiX, uiZ))
                {
                    e.Handled = true;
                    return;
                }
            }

            // === Prim placement commit ===
            if (vm.IsPlacingPrim)
            {
                int clampedX = Math.Clamp((int)mouseDownPos.X, 0, MapConstants.MapPixels - 1);
                int clampedZ = Math.Clamp((int)mouseDownPos.Y, 0, MapConstants.MapPixels - 1);

                SnapUiToVertexIfCtrl(ref clampedX, ref clampedZ);

                ObjectSpace.UiPixelsToGamePrim(clampedX, clampedZ, out int mapWhoIndex, out byte gameX, out byte gameZ);

                try
                {
                    var acc = new ObjectsAccessor(MapDataService.Instance);
                    var prim = new ObjectsAccessor.PrimEntry
                    {
                        PrimNumber = (byte)vm.PrimNumberToPlace,
                        MapWhoIndex = mapWhoIndex,
                        X = gameX,
                        Z = gameZ,
                        Y = (short)0,
                        Yaw = (byte)0,
                        Flags = (byte)0,
                        InsideIndex = (byte)0
                    };

                    acc.AddPrim(prim);
                    vm.RefreshPrimsList();

                    var just = vm.Prims.LastOrDefault(p =>
                        p.MapWhoIndex == mapWhoIndex &&
                        p.X == gameX && p.Z == gameZ &&
                        p.PrimNumber == prim.PrimNumber);

                    vm.SelectedPrim = just;

                    if (mainVm != null)
                        mainVm.StatusMessage = $"Added {PrimCatalog.GetName(prim.PrimNumber)} ({prim.PrimNumber:000}) at cell r{just?.MapWhoRow},c{just?.MapWhoCol} ({gameX},{gameZ}).";
                }
                catch (Exception ex)
                {
                    if (mainVm != null)
                        mainVm.StatusMessage = $"Error: failed to add prim. {ex.Message}";
                }
                finally
                {
                    vm.CancelPlacePrim();
                    e.Handled = true;
                }
                return;
            }

            // ===== TEXTURE PAINT: Hybrid mode =====
            if (vm.SelectedTool == EditorTool.PaintTexture)
            {
                Point mousePos = e.GetPosition(Surface);
                int tx = (int)Math.Floor(mousePos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mousePos.Y / MapConstants.TileSize);

                if (tx < 0 || tx >= MapConstants.TilesPerSide || ty < 0 || ty >= MapConstants.TilesPerSide)
                    return;

                _textureMouseDownPos = mousePos;
                _isTextureDragging = false;
                _isTextureStrokePainting = false;

                vm.IsPaintingTexture = true;
                vm.TextureSelectionStartX = tx;
                vm.TextureSelectionStartY = ty;
                vm.TextureSelectionEndX = tx;
                vm.TextureSelectionEndY = ty;

                if (IsShiftDown())
                {
                    _isTextureStrokePainting = true;
                    _texturePaintedTiles = new HashSet<(int, int)>();

                    ApplyTextureBrush(tx, ty, vm);

                    if (mainVm != null)
                        mainVm.StatusMessage = $"Stroke painting {vm.SelectedTextureGroup} #{vm.SelectedTextureNumber:000} (brush {vm.BrushSize}×{vm.BrushSize})";
                }
                else
                {
                    if (mainVm != null)
                        mainVm.StatusMessage = $"Click to paint (brush {vm.BrushSize}×{vm.BrushSize}), drag for rectangle, Shift+drag for stroke";
                }

                CaptureMouse();
                e.Handled = true;
                return;
            }

            // ===== ALTITUDE TOOLS =====
            if (vm.SelectedTool == EditorTool.SetAltitude ||
                vm.SelectedTool == EditorTool.SampleAltitude ||
                vm.SelectedTool == EditorTool.ResetAltitude ||
                vm.SelectedTool == EditorTool.DetectRoof)
            {
                int tx = (int)Math.Floor(mouseDownPos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseDownPos.Y / MapConstants.TileSize);

                if (tx < 0 || tx >= MapConstants.TilesPerSide || ty < 0 || ty >= MapConstants.TilesPerSide)
                    return;

                if (_altitude == null)
                    _altitude = new AltitudeAccessor(MapDataService.Instance);

                switch (vm.SelectedTool)
                {
                    case EditorTool.SetAltitude:
                        vm.IsSettingAltitude = true;
                        vm.AltitudeSelectionStartX = tx;
                        vm.AltitudeSelectionStartY = ty;
                        vm.AltitudeSelectionEndX = tx;
                        vm.AltitudeSelectionEndY = ty;
                        CaptureMouse();

                        if (mainVm != null)
                            mainVm.StatusMessage = $"Drag to select area for altitude {vm.TargetAltitude}";

                        e.Handled = true;
                        return;

                    case EditorTool.SampleAltitude:
                        int sampledAlt = _altitude.ReadWorldAltitude(tx, ty);
                        vm.TargetAltitude = sampledAlt;

                        if (mainVm != null)
                            mainVm.StatusMessage = $"Sampled altitude {sampledAlt} (raw: {sampledAlt >> 3}) from cell [{tx},{ty}]";

                        e.Handled = true;
                        return;

                    case EditorTool.ResetAltitude:
                        vm.IsSettingAltitude = true;
                        vm.AltitudeSelectionStartX = tx;
                        vm.AltitudeSelectionStartY = ty;
                        vm.AltitudeSelectionEndX = tx;
                        vm.AltitudeSelectionEndY = ty;
                        CaptureMouse();

                        if (mainVm != null)
                            mainVm.StatusMessage = $"Drag to select area to clear altitude";

                        e.Handled = true;
                        return;

                    case EditorTool.DetectRoof:
                        if (mainVm != null)
                            mainVm.StatusMessage = $"Detect roof at tile [{tx},{ty}] - implement HeightsTab.OnDetectRoofClick()";

                        e.Handled = true;
                        return;
                }
            }

            // ===== HEIGHT TOOLS: vertex-based =====
            if (!TryGetVertexIndexFromHit(mouseDownPos, out int vx, out int vy))
                return;

            int baseTx = vx - 1;
            int baseTy = vy - 1;

            switch (vm.SelectedTool)
            {
                case EditorTool.LevelHeight:
                    _levelSource = _heights.ReadHeight(baseTx, baseTy);
                    _isLeveling = true;
                    _lastLeveledTile = null;
                    CaptureMouse();

                    ForEachVertexInBrush(vx, vy, vm.BrushSize, (tx, ty) => ApplyHeightToTile(tx, ty, _levelSource));

                    if (mainVm != null)
                        mainVm.StatusMessage = $"Level: picked {_levelSource} at vertex [{vx},{vy}] (brush {vm.BrushSize}×{vm.BrushSize})";

                    e.Handled = true;
                    return;

                case EditorTool.RaiseHeight:
                case EditorTool.LowerHeight:
                    int step = Math.Max(1, vm.HeightStep);
                    bool isRaise = vm.SelectedTool == EditorTool.RaiseHeight;

                    ForEachVertexInBrush(vx, vy, vm.BrushSize, (tx, ty) =>
                    {
                        sbyte h = _heights.ReadHeight(tx, ty);
                        int temp = isRaise ? h + step : h - step;
                        temp = Math.Clamp(temp, sbyte.MinValue, sbyte.MaxValue);
                        if (temp != h) _heights.WriteHeight(tx, ty, (sbyte)temp);
                    });

                    HeightsOverlay?.InvalidateVisual();

                    if (mainVm != null)
                        mainVm.StatusMessage = $"Height {(isRaise ? "+=" : "-=")} {step} at vertex [{vx},{vy}] (brush {vm.BrushSize}×{vm.BrushSize})";

                    e.Handled = true;
                    return;

                case EditorTool.FlattenHeight:
                    ForEachVertexInBrush(vx, vy, vm.BrushSize, (tx, ty) =>
                    {
                        if (_heights.ReadHeight(tx, ty) != 0) _heights.WriteHeight(tx, ty, (sbyte)0);
                    });

                    HeightsOverlay?.InvalidateVisual();

                    if (mainVm != null)
                        mainVm.StatusMessage = $"Flattened to 0 at vertex [{vx},{vy}] (brush {vm.BrushSize}×{vm.BrushSize})";

                    e.Handled = true;
                    return;

                case EditorTool.DitchTemplate:
                    ApplyDitchTemplate(baseTx, baseTy);
                    e.Handled = true;
                    return;

                default:
                    return;
            }
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                return;

            if (DataContext is not MapViewModel vm || Surface == null || Scroller == null)
                return;

            e.Handled = true;

            var current = vm.Zoom;
            var factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
            var target = Math.Max(MinZoom, Math.Min(MaxZoom, current * factor));
            if (Math.Abs(target - current) < 0.0001) return;

            Point mouseOnContent = e.GetPosition(Surface);
            Point mouseOnViewport = e.GetPosition(Scroller);

            vm.Zoom = target;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                double newOffsetX = mouseOnContent.X * target - mouseOnViewport.X;
                double newOffsetY = mouseOnContent.Y * target - mouseOnViewport.Y;

                newOffsetX = Clamp(newOffsetX, 0, Math.Max(0, Scroller.ExtentWidth - Scroller.ViewportWidth));
                newOffsetY = Clamp(newOffsetY, 0, Math.Max(0, Scroller.ExtentHeight - Scroller.ViewportHeight));

                Scroller.ScrollToHorizontalOffset(newOffsetX);
                Scroller.ScrollToVerticalOffset(newOffsetY);
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private static void ForEachTileInBrushCentered(int centerTx, int centerTy, int brushSize, Action<int, int> action)
        {
            if (brushSize < 1) brushSize = 1;
            int half = (brushSize - 1) / 2;
            for (int dy = -half; dy <= half; dy++)
            {
                for (int dx = -half; dx <= half; dx++)
                {
                    int tx = centerTx + dx;
                    int ty = centerTy + dy;
                    if (tx >= 0 && tx < MapConstants.TilesPerSide &&
                        ty >= 0 && ty < MapConstants.TilesPerSide)
                    {
                        action(tx, ty);
                    }
                }
            }
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is not MapViewModel vm || Surface == null) return;

            Point p = e.GetPosition(Surface);
            double gx = MapConstants.MapPixels - p.X;
            double gz = MapConstants.MapPixels - p.Y;

            int gameX = (int)Math.Max(0, Math.Min(MapConstants.MapPixels, Math.Round(gx)));
            int gameZ = (int)Math.Max(0, Math.Min(MapConstants.MapPixels, Math.Round(gz)));

            vm.CursorX = gameX;
            vm.CursorZ = gameZ;

            vm.CursorTileX = System.Math.Clamp(gameX / MapConstants.TileSize, 0, MapConstants.TilesPerSide - 1);
            vm.CursorTileZ = System.Math.Clamp(gameZ / MapConstants.TileSize, 0, MapConstants.TilesPerSide - 1);

            // Update facet redraw preview line
            if (vm.IsRedrawingFacet)
            {
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);
                vm.UpdateFacetRedrawPreview(uiX, uiZ);
            }

            // Update facet multi-draw preview line
            if (vm.IsMultiDrawingFacets)
            {
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);
                vm.UpdateFacetMultiDrawPreview(uiX, uiZ);
            }

            // Update ladder placement preview
            if (vm.IsPlacingLadder)
            {
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);
                vm.UpdateLadderPlacementPreview(uiX, uiZ);
            }

            // Update door placement preview
            if (vm.IsPlacingDoor)
            {
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);
                vm.UpdateDoorPlacementPreview(uiX, uiZ);
            }

            // Update cable placement preview
            if (vm.IsPlacingCable)
            {
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);
                vm.UpdateCablePlacementPreview(uiX, uiZ);
            }

            // Height leveling drag
            if (_isLeveling && e.LeftButton == MouseButtonState.Pressed && Surface != null)
            {
                if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                    return;

                Point mouseMovePos = e.GetPosition(Surface);
                if (!TryGetVertexIndexFromHit(mouseMovePos, out int vx2, out int vy2))
                    return;

                ForEachVertexInBrush(vx2, vy2, (DataContext as MapViewModel)?.BrushSize ?? 1, (tx, ty) =>
                {
                    ApplyHeightToTile(tx, ty, _levelSource);
                });

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = $"Level: set #{_levelSource} at vertex [{vx2},{vy2}] (brush {(DataContext as MapViewModel)?.BrushSize ?? 1}×{(DataContext as MapViewModel)?.BrushSize ?? 1})";
            }

            // Walkable rectangle selection - update end corner
            if (_isDrawingWalkableRect && e.LeftButton == MouseButtonState.Pressed && Surface != null)
            {
                if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                    return;

                Point mouseMovePos = e.GetPosition(Surface);
                int tx = (int)Math.Floor(mouseMovePos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseMovePos.Y / MapConstants.TileSize);

                tx = Math.Clamp(tx, 0, MapConstants.TilesPerSide - 1);
                ty = Math.Clamp(ty, 0, MapConstants.TilesPerSide - 1);

                vm.WalkableSelectionEndX = tx;
                vm.WalkableSelectionEndY = ty;

                var rect = vm.GetWalkableSelectionRect();
                if (rect.HasValue)
                {
                    int width = rect.Value.MaxX - rect.Value.MinX + 1;
                    int height = rect.Value.MaxY - rect.Value.MinY + 1;
                    int tileCount = width * height;

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = $"Walkable region: Max X = {rect.Value.MaxX} Min X = {rect.Value.MinX} | Max Y = {rect.Value.MaxY} Min Y = {rect.Value.MinY} | {width}×{height} ({tileCount} tiles)";
                }
            }

            // Altitude tool rectangle selection - update end corner
            if (vm.IsSettingAltitude && e.LeftButton == MouseButtonState.Pressed && Surface != null)
            {
                if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                    return;

                Point mouseMovePos = e.GetPosition(Surface);
                int tx = (int)Math.Floor(mouseMovePos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseMovePos.Y / MapConstants.TileSize);

                tx = Math.Clamp(tx, 0, MapConstants.TilesPerSide - 1);
                ty = Math.Clamp(ty, 0, MapConstants.TilesPerSide - 1);

                vm.AltitudeSelectionEndX = tx;
                vm.AltitudeSelectionEndY = ty;

                var rect = vm.GetAltitudeSelectionRect();
                if (rect.HasValue)
                {
                    int width = rect.Value.MaxX - rect.Value.MinX + 1;
                    int height = rect.Value.MaxY - rect.Value.MinY + 1;
                    int tileCount = width * height;

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    {
                        bool isReset = vm.SelectedTool == EditorTool.ResetAltitude;
                        string action = isReset ? "Clear" : $"Set altitude {vm.TargetAltitude}";
                        shell.StatusMessage = $"{action}: {width}×{height} ({tileCount} tiles)";
                    }
                }
            }

            // Texture paint - hybrid mode (rectangle OR stroke painting)
            if (vm.IsPaintingTexture && e.LeftButton == MouseButtonState.Pressed && Surface != null)
            {
                if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                    return;

                Point mouseMovePos = e.GetPosition(Surface);
                int tx = (int)Math.Floor(mouseMovePos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseMovePos.Y / MapConstants.TileSize);

                tx = Math.Clamp(tx, 0, MapConstants.TilesPerSide - 1);
                ty = Math.Clamp(ty, 0, MapConstants.TilesPerSide - 1);

                // STROKE PAINTING MODE (Shift held)
                if (_isTextureStrokePainting)
                {
                    ApplyTextureBrush(tx, ty, vm);

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = $"Stroke painting at [{tx},{ty}] (brush {vm.BrushSize}×{vm.BrushSize})";
                    return;
                }

                // Check if we've moved enough to start rectangle dragging
                if (!_isTextureDragging)
                {
                    double dx = mouseMovePos.X - _textureMouseDownPos.X;
                    double dy = mouseMovePos.Y - _textureMouseDownPos.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist >= TextureDragThreshold)
                    {
                        _isTextureDragging = true;
                    }
                }

                // RECTANGLE SELECTION MODE (only if dragging detected)
                if (_isTextureDragging)
                {
                    vm.TextureSelectionEndX = tx;
                    vm.TextureSelectionEndY = ty;

                    var rect = vm.GetTextureSelectionRect();
                    if (rect.HasValue)
                    {
                        int width = rect.Value.MaxX - rect.Value.MinX + 1;
                        int height = rect.Value.MaxY - rect.Value.MinY + 1;
                        int tileCount = width * height;

                        if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        {
                            shell.StatusMessage = $"Rectangle: {width}×{height} ({tileCount} tiles) with {vm.SelectedTextureGroup} #{vm.SelectedTextureNumber:000}";
                        }
                    }
                }
            }

            UpdateGhostHover(e.GetPosition(Surface));

            // Prim placement ghost
            if (vm.IsPlacingPrim && Surface != null)
            {
                Point pos = e.GetPosition(Surface);
                int clampedX = Math.Clamp((int)pos.X, 0, MapConstants.MapPixels - 1);
                int clampedZ = Math.Clamp((int)pos.Y, 0, MapConstants.MapPixels - 1);

                SnapUiToVertexIfCtrl(ref clampedX, ref clampedZ);

                ObjectSpace.UiPixelsToGamePrim(clampedX, clampedZ, out int mapWhoIndex, out byte cellX, out byte cellZ);
                ObjectSpace.GamePrimToUiPixels(mapWhoIndex, cellX, cellZ, out int uiX, out int uiZ);
                ObjectSpace.GameIndexToUiRowCol(mapWhoIndex, out int uiRow, out int uiCol);

                vm.DragPreviewPrim = new PrimListItem
                {
                    Index = -1,
                    MapWhoIndex = mapWhoIndex,
                    MapWhoRow = uiRow,
                    MapWhoCol = uiCol,
                    PrimNumber = (byte)Math.Clamp(vm.PrimNumberToPlace, 0, 255),
                    Name = PrimCatalog.GetName(vm.PrimNumberToPlace),
                    Y = 0,
                    X = cellX,
                    Z = cellZ,
                    Yaw = 0,
                    Flags = 0,
                    InsideIndex = 0,
                    PixelX = uiX,
                    PixelZ = uiZ
                };
            }
        }

        private void OnPreviewMouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                return;

            if (_isLeveling)
            {
                _isLeveling = false;
                _lastLeveledTile = null;
                ReleaseMouseCapture();
                e.Handled = true;
            }

            // Handle walkable rectangle selection completion
            if (_isDrawingWalkableRect && DataContext is MapViewModel vmWalk)
            {
                _isDrawingWalkableRect = false;
                ReleaseMouseCapture();
                WalkableDrawingCompleted?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            // Handle altitude rectangle selection completion
            if (DataContext is MapViewModel vm && vm.IsSettingAltitude)
            {
                var rect = vm.GetAltitudeSelectionRect();
                if (rect.HasValue && _altitude != null)
                {
                    bool isReset = vm.SelectedTool == EditorTool.ResetAltitude;
                    int tileCount = 0;

                    for (int ty = rect.Value.MinY; ty <= rect.Value.MaxY; ty++)
                    {
                        for (int tx = rect.Value.MinX; tx <= rect.Value.MaxX; tx++)
                        {
                            if (isReset)
                                _altitude.ClearRoofTile(tx, ty);
                            else
                                _altitude.SetRoofTile(tx, ty, vm.TargetAltitude);
                            tileCount++;
                        }
                    }

                    HeightsOverlay?.InvalidateVisual();

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    {
                        string action = isReset ? "Cleared" : $"Set altitude {vm.TargetAltitude} on";
                        shell.StatusMessage = $"{action} {tileCount} tiles";
                    }
                }

                vm.ClearAltitudeSelection();
                ReleaseMouseCapture();
                e.Handled = true;
            }

            // Handle texture painting completion (hybrid: click=brush, drag=rectangle, shift+drag=stroke)
            if (DataContext is MapViewModel vmTex && vmTex.IsPaintingTexture)
            {
                var rect = vmTex.GetTextureSelectionRect();

                if (_isTextureStrokePainting)
                {
                    // STROKE PAINTING - already applied during drag, just clean up
                    int tilesCount = _texturePaintedTiles?.Count ?? 0;
                    _texturePaintedTiles = null;
                    _isTextureStrokePainting = false;

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = $"Stroke painted {tilesCount} tiles with {vmTex.SelectedTextureGroup} #{vmTex.SelectedTextureNumber:000}";
                }
                else if (_isTextureDragging && rect.HasValue)
                {
                    // RECTANGLE MODE - apply texture to all tiles in rectangle
                    var acc = new TexturesAccessor(MapDataService.Instance);
                    int world = vmTex.TextureWorld;
                    int tileCount = 0;

                    for (int ty = rect.Value.MinY; ty <= rect.Value.MaxY; ty++)
                    {
                        for (int tx = rect.Value.MinX; tx <= rect.Value.MaxX; tx++)
                        {
                            acc.WriteTileTexture(tx, ty, vmTex.SelectedTextureGroup, vmTex.SelectedTextureNumber, vmTex.SelectedRotationIndex, world);
                            tileCount++;
                        }
                    }

                    TexturesChangeBus.Instance.NotifyChanged();

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    {
                        int width = rect.Value.MaxX - rect.Value.MinX + 1;
                        int height = rect.Value.MaxY - rect.Value.MinY + 1;
                        shell.StatusMessage = $"Rectangle painted {width}×{height} ({tileCount} tiles) with {vmTex.SelectedTextureGroup} #{vmTex.SelectedTextureNumber:000}";
                    }
                }
                else if (rect.HasValue)
                {
                    // SINGLE CLICK - apply brush at click location
                    int centerTx = rect.Value.MinX;
                    int centerTy = rect.Value.MinY;

                    ApplyTextureBrush(centerTx, centerTy, vmTex);

                    int brushSize = vmTex.BrushSize;
                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    {
                        if (brushSize == 1)
                            shell.StatusMessage = $"Painted tile [{centerTx},{centerTy}] with {vmTex.SelectedTextureGroup} #{vmTex.SelectedTextureNumber:000}";
                        else
                            shell.StatusMessage = $"Brush painted {brushSize}×{brushSize} at [{centerTx},{centerTy}] with {vmTex.SelectedTextureGroup} #{vmTex.SelectedTextureNumber:000}";
                    }
                }

                // Clean up
                _isTextureDragging = false;
                vmTex.ClearTextureSelection();
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Apply texture using brush size centered on the given tile.
        /// Respects BrushSize from ViewModel.
        /// </summary>
        private void ApplyTextureBrush(int centerTx, int centerTy, MapViewModel vm)
        {
            var acc = new TexturesAccessor(MapDataService.Instance);
            int world = vm.TextureWorld;
            int brushSize = Math.Max(1, vm.BrushSize);
            int half = (brushSize - 1) / 2;

            for (int dy = -half; dy <= half; dy++)
            {
                for (int dx = -half; dx <= half; dx++)
                {
                    int tx = centerTx + dx;
                    int ty = centerTy + dy;

                    if (tx >= 0 && tx < MapConstants.TilesPerSide &&
                        ty >= 0 && ty < MapConstants.TilesPerSide)
                    {
                        // For stroke painting, skip already-painted tiles
                        if (_texturePaintedTiles != null)
                        {
                            if (_texturePaintedTiles.Contains((tx, ty)))
                                continue;
                            _texturePaintedTiles.Add((tx, ty));
                        }

                        acc.WriteTileTexture(tx, ty, vm.SelectedTextureGroup, vm.SelectedTextureNumber, vm.SelectedRotationIndex, world);
                    }
                }
            }

            TexturesChangeBus.Instance.NotifyChanged();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;

            if (vm.SelectedTool == EditorTool.PaintTexture && e.Key == Key.Space)
            {
                vm.SelectedRotationIndex = (vm.SelectedRotationIndex + 1) % 4;
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = $"Rotation: {vm.SelectedRotationIndex}  (0→180°, 1→90°, 2→0°, 3→270°)";
                e.Handled = true;
            }
            if (e.Key == Key.Escape && vm.IsPlacingPrim)
            {
                vm.CancelPlacePrim();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Add Prim canceled.";
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && vm.IsDrawingWalkable)
            {
                _isDrawingWalkableRect = false;
                vm.ClearWalkableSelection();
                ReleaseMouseCapture();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Walkable drawing cancelled.";
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && vm.IsRedrawingFacet)
            {
                vm.CancelFacetRedraw();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Facet redraw cancelled.";
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && vm.IsPlacingLadder)
            {
                vm.CancelLadderPlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Ladder placement cancelled.";
                e.Handled = true;
                return;
            }
            //if (e.Key == Key.Escape && vm.IsPlacingDoor)
            //{
            //    vm.CancelDoorPlacement();
            //    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
            //        shell.StatusMessage = "Door placement cancelled.";
            //    e.Handled = true;
            //    return;
            //}
            if (e.Key == Key.Escape && vm.IsPlacingCable)
            {
                vm.CancelCablePlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Cable placement cancelled.";
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && vm.IsMultiDrawingFacets)
            {
                vm.CancelFacetMultiDraw();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Facet drawing cancelled.";
                e.Handled = true;
                return;
            }
            // Escape cancels texture painting
            if (e.Key == Key.Escape && vm.IsPaintingTexture)
            {
                _isTextureDragging = false;
                _isTextureStrokePainting = false;
                _texturePaintedTiles = null;
                vm.ClearTextureSelection();
                ReleaseMouseCapture();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Texture painting cancelled.";
                e.Handled = true;
                return;
            }
        }

        private void OnLostMouseCapture(object? sender, MouseEventArgs e)
        {
            _isLeveling = false;
            _lastLeveledTile = null;

            // Clean up texture painting state
            _isTextureDragging = false;
            _isTextureStrokePainting = false;
            _texturePaintedTiles = null;

            if (_isDrawingWalkableRect)
            {
                _isDrawingWalkableRect = false;
                if (DataContext is MapViewModel vm)
                    vm.ClearWalkableSelection();
            }

            // Also clear texture selection if painting was interrupted
            if (DataContext is MapViewModel vmTex && vmTex.IsPaintingTexture)
            {
                vmTex.ClearTextureSelection();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            GhostLayer?.SetHoverTile(null, null);
        }

        private void ApplyHeightToTile(int tx, int ty, sbyte value)
        {
            sbyte h = _heights.ReadHeight(tx, ty);
            if (h != value)
            {
                _heights.WriteHeight(tx, ty, value);
                HeightsOverlay?.InvalidateVisual();
            }
            _lastLeveledTile = (tx, ty);
        }

        private void OnPreviewMouseRightButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                return;

            // Right-click cancels walkable drawing
            if (DataContext is MapViewModel vmWalk && vmWalk.IsDrawingWalkable)
            {
                _isDrawingWalkableRect = false;
                vmWalk.ClearWalkableSelection();
                ReleaseMouseCapture();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Walkable drawing cancelled.";
                e.Handled = true;
                return;
            }

            // Right-click cancels texture painting
            if (DataContext is MapViewModel vmTex && vmTex.IsPaintingTexture)
            {
                _isTextureDragging = false;
                _isTextureStrokePainting = false;
                _texturePaintedTiles = null;
                vmTex.ClearTextureSelection();
                ReleaseMouseCapture();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Texture painting cancelled.";
                e.Handled = true;
                return;
            }

            if (_isLeveling)
            {
                _isLeveling = false;
                _lastLeveledTile = null;
                if (IsMouseCaptured) ReleaseMouseCapture();
            }

            if (DataContext is MapViewModel vmFacet && vmFacet.IsRedrawingFacet)
            {
                vmFacet.CancelFacetRedraw();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shellFacet)
                    shellFacet.StatusMessage = "Facet redraw cancelled.";
                e.Handled = true;
                return;
            }

            if (DataContext is MapViewModel vmMultiDraw && vmMultiDraw.IsMultiDrawingFacets)
            {
                vmMultiDraw.FinishFacetMultiDraw();
                e.Handled = true;
                return;
            }

            if (DataContext is MapViewModel vmLadder && vmLadder.IsPlacingLadder)
            {
                vmLadder.CancelLadderPlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shellLadder)
                    shellLadder.StatusMessage = "Ladder placement cancelled.";
                e.Handled = true;
                return;
            }

            //if (DataContext is MapViewModel vmDoor && vmDoor.IsPlacingDoor)
            //{
            //    vmDoor.CancelDoorPlacement();
            //    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shellDoor)
            //        shellDoor.StatusMessage = "Door placement cancelled.";
            //    e.Handled = true;
            //    return;
            //}

            if (DataContext is MapViewModel vmCable && vmCable.IsPlacingCable)
            {
                vmCable.CancelCablePlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shellCable)
                    shellCable.StatusMessage = "Cable placement cancelled.";
                e.Handled = true;
                return;
            }

            if (DataContext is MapViewModel vm0 && vm0.IsPlacingPrim)
            {
                vm0.CancelPlacePrim();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell0)
                    shell0.StatusMessage = "Add Prim canceled.";
                e.Handled = true;
                return;
            }

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel vm)
            {
                vm.Map.SelectedTool = EditorTool.None;
                bool cleared = false;
                if (vm.Map.SelectedPrim != null) { vm.Map.SelectedPrim = null; cleared = true; }

                vm.StatusMessage = cleared ? "Selection cleared." : "Action cleared.";
            }
            e.Handled = true;

            GhostLayer?.SetHoverTile(null, null);
        }

        private void ApplyDitchTemplate(int cx, int cy)
        {
            void SetIfInBounds(int tx, int ty, sbyte val)
            {
                if (tx >= 0 && tx < MapConstants.TilesPerSide &&
                    ty >= 0 && ty < MapConstants.TilesPerSide)
                {
                    var cur = _heights.ReadHeight(tx, ty);
                    if (cur != val) _heights.WriteHeight(tx, ty, val);
                }
            }

            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 1; dx++)
                {
                    SetIfInBounds(cx + dx, cy + dy, -32);
                }
            }

            var ramp = new[]
            {
                (-2, (sbyte)-26),
                (-1, (sbyte)-20),
                ( 0, (sbyte)-13),
                ( 1, (sbyte)-7),
                ( 2, (sbyte)0)
            };

            foreach (var (dy, val) in ramp)
            {
                SetIfInBounds(cx + 2, cy + dy, val);
                SetIfInBounds(cx + 3, cy + dy, val);
            }

            HeightsOverlay?.InvalidateVisual();

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                shell.StatusMessage = $"Stamped Ditch at [{cx},{cy}]";
        }

        public void GoToTileCenter(int tx, int ty)
        {
            if (Scroller == null || Surface == null) return;
            if (DataContext is not MapViewModel vm) return;

            double cx = (tx + 0.5) * MapConstants.TileSize;
            double cy = (ty + 0.5) * MapConstants.TileSize;

            double z = vm.Zoom;
            double sx = cx * z;
            double sy = cy * z;

            double targetX = sx - Scroller.ViewportWidth / 2.0;
            double targetY = sy - Scroller.ViewportHeight / 2.0;

            targetX = Clamp(targetX, 0, System.Math.Max(0, Scroller.ExtentWidth - Scroller.ViewportWidth));
            targetY = Clamp(targetY, 0, System.Math.Max(0, Scroller.ExtentHeight - Scroller.ViewportHeight));

            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                Scroller.ScrollToHorizontalOffset(targetX);
                Scroller.ScrollToVerticalOffset(targetY);
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private const double VertexHitRadius = 16.0;

        private static bool TryGetTileFromVertexHit(Point p, out int tx, out int ty)
        {
            tx = ty = -1;

            int vx = (int)Math.Round(p.X / MapConstants.TileSize);
            int vy = (int)Math.Round(p.Y / MapConstants.TileSize);

            if (vx < 1 || vy < 1 || vx > MapConstants.TilesPerSide || vy > MapConstants.TilesPerSide)
                return false;

            double cx = vx * MapConstants.TileSize;
            double cy = vy * MapConstants.TileSize;

            double dx = p.X - cx, dy = p.Y - cy;
            if ((dx * dx + dy * dy) > (VertexHitRadius * VertexHitRadius))
                return false;

            tx = vx - 1;
            ty = vy - 1;
            return true;
        }

        private static bool TryGetVertexIndexFromHit(Point p, out int vx, out int vy)
        {
            vx = vy = -1;

            int candVx = (int)Math.Round(p.X / MapConstants.TileSize);
            int candVy = (int)Math.Round(p.Y / MapConstants.TileSize);

            if (candVx < 1 || candVy < 1 || candVx > MapConstants.TilesPerSide || candVy > MapConstants.TilesPerSide)
                return false;

            double cx = candVx * MapConstants.TileSize;
            double cy = candVy * MapConstants.TileSize;

            double dx = p.X - cx, dy = p.Y - cy;
            if ((dx * dx + dy * dy) > (VertexHitRadius * VertexHitRadius))
                return false;

            vx = candVx;
            vy = candVy;
            return true;
        }

        private static void ForEachVertexInBrush(int vx, int vy, int brushSize, Action<int, int> applyByTile)
        {
            int size = Math.Clamp(brushSize, 1, 10);
            int half = size / 2;

            int startTx = (vx - 1) - half;
            int startTy = (vy - 1) - half;

            for (int dy = 0; dy < size; dy++)
            {
                for (int dx = 0; dx < size; dx++)
                {
                    int tx = startTx + dx;
                    int ty = startTy + dy;

                    if (tx >= 0 && tx < MapConstants.TilesPerSide &&
                        ty >= 0 && ty < MapConstants.TilesPerSide)
                    {
                        applyByTile(tx, ty);
                    }
                }
            }
        }

        private void ForEachTileInBrush(int centerTx, int centerTy, int brushSize, Action<int, int> action)
        {
            int half = (brushSize - 1) / 2;
            for (int dy = -half; dy <= half; dy++)
            {
                for (int dx = -half; dx <= half; dx++)
                {
                    int tx = centerTx + dx;
                    int ty = centerTy + dy;
                    if (tx >= 0 && tx < MapConstants.TilesPerSide &&
                        ty >= 0 && ty < MapConstants.TilesPerSide)
                    {
                        action(tx, ty);
                    }
                }
            }
        }

        private void UpdateGhostHover(Point p)
        {
            if (GhostLayer == null) return;
            if (DataContext is not MapViewModel vm) return;

            if (vm.SelectedTool != EditorTool.PaintTexture)
            {
                GhostLayer.SetHoverTile(null, null);
                return;
            }

            int tx = (int)Math.Floor(p.X / MapConstants.TileSize);
            int ty = (int)Math.Floor(p.Y / MapConstants.TileSize);

            if (tx < 0 || tx >= MapConstants.TilesPerSide || ty < 0 || ty >= MapConstants.TilesPerSide)
                GhostLayer.SetHoverTile(null, null);
            else
                GhostLayer.SetHoverTile(tx, ty);
        }

        public void CenterOnPixel(int px, int pz)
        {
            if (Scroller == null || Surface == null || DataContext is not MapViewModel vm) return;

            double z = vm.Zoom;
            double targetX = px * z - Scroller.ViewportWidth / 2.0;
            double targetY = pz * z - Scroller.ViewportHeight / 2.0;

            targetX = Math.Max(0, Math.Min(targetX, Scroller.ExtentWidth - Scroller.ViewportWidth));
            targetY = Math.Max(0, Math.Min(targetY, Scroller.ExtentHeight - Scroller.ViewportHeight));

            Scroller.ScrollToHorizontalOffset(targetX);
            Scroller.ScrollToVerticalOffset(targetY);
        }

        private static void SnapUiToVertexIfCtrl(ref int uiX, ref int uiZ)
        {
            if (!IsCtrlDown())
                return;

            int size = MapConstants.TileSize;

            uiX = (int)(Math.Round(uiX / (double)size) * size);
            uiZ = (int)(Math.Round(uiZ / (double)size) * size);

            uiX = Math.Clamp(uiX, 0, MapConstants.MapPixels - 1);
            uiZ = Math.Clamp(uiZ, 0, MapConstants.MapPixels - 1);
        }
    }
}