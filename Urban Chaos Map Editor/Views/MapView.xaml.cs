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

        private static bool IsCtrlDown()
    => Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

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
            PreviewMouseMove += OnPreviewMouseMove;   // you already have this
            MouseLeave += OnMouseLeave;         // optional
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown; // NEW
            PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;      // NEW
            LostMouseCapture += OnLostMouseCapture;                      // NEW
            PreviewMouseWheel += OnPreviewMouseWheel;  // NEW
            PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown; // NEW
                                                                          // ensure we can receive keyboard input
            MouseEnter += (_, __) => Focus();                // give keyboard focus when hovering
            PreviewMouseLeftButtonDown += (_, __) => Focus(); // also grab focus on click


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

            // === WALKABLE DRAWING MODE (check first, before other tools) ===
            if (vm.IsDrawingWalkable)
            {
                int tx = (int)Math.Floor(mouseDownPos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseDownPos.Y / MapConstants.TileSize);

                if (tx >= 0 && tx < MapConstants.TilesPerSide &&
                    ty >= 0 && ty < MapConstants.TilesPerSide)
                {
                    vm.WalkableSelectionStartX = tx;
                    vm.WalkableSelectionStartY = ty;
                    vm.WalkableSelectionEndX = tx;
                    vm.WalkableSelectionEndY = ty;
                    _isDrawingWalkableRect = true;
                    CaptureMouse();

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = "Drag to select walkable region...";

                    e.Handled = true;
                    return;
                }
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
                    InvalidateVisual(); // Refresh to show/hide preview line
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

            // === DOOR PLACEMENT MODE ===
            if (vm.IsPlacingDoor)
            {
                Point p = e.GetPosition(Surface);
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);

                if (vm.HandleDoorPlacementClick(uiX, uiZ))
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

                // NEW: Ctrl → snap to nearest 64×64 vertex before converting
                SnapUiToVertexIfCtrl(ref clampedX, ref clampedZ);

                ObjectSpace.UiPixelsToGamePrim(clampedX, clampedZ, out int mapWhoIndex, out byte gameX, out byte gameZ);

                try
                {
                    var acc = new ObjectsAccessor(MapDataService.Instance);

                    // If PrimEntry is nested: ObjectsAccessor.PrimEntry
                    // If PrimEntry is top-level (e.g., in Models), use that type instead.
                    var prim = new ObjectsAccessor.PrimEntry
                    {
                        PrimNumber = (byte)vm.PrimNumberToPlace,
                        MapWhoIndex = mapWhoIndex,
                        X = gameX,
                        Z = gameZ,
                        Y = (short)0,   // default height
                        Yaw = (byte)0,    // default yaw
                        Flags = (byte)0,    // default flags
                        InsideIndex = (byte)0     // default inside/outside
                    };

                    // Use your existing mutator
                    acc.AddPrim(prim);

                    vm.RefreshPrimsList();

                    // Re-select the one we just added (match by cell, coords, and prim number)
                    var just = vm.Prims.LastOrDefault(p =>
                        p.MapWhoIndex == mapWhoIndex &&
                        p.X == gameX && p.Z == gameZ &&
                        p.PrimNumber == prim.PrimNumber);

                    vm.SelectedPrim = just;

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = $"Added {PrimCatalog.GetName(prim.PrimNumber)} ({prim.PrimNumber:000}) at cell r{just?.MapWhoRow},c{just?.MapWhoCol} ({gameX},{gameZ}).";
                }
                catch (Exception ex)
                {
                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = $"Error: failed to add prim. {ex.Message}";
                }
                finally
                {
                    vm.CancelPlacePrim();  // clear placement mode + ghost
                    e.Handled = true;
                }
                return;
            }

            // ===== TEXTURE PAINT: tile-based with rectangle selection =====
            if (vm.SelectedTool == EditorTool.PaintTexture)
            {
                int tx = (int)Math.Floor(mouseDownPos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseDownPos.Y / MapConstants.TileSize);
                if (tx < 0 || tx >= MapConstants.TilesPerSide || ty < 0 || ty >= MapConstants.TilesPerSide) return;

                // Start rectangle selection
                vm.IsPaintingTexture = true;
                vm.TextureSelectionStartX = tx;
                vm.TextureSelectionStartY = ty;
                vm.TextureSelectionEndX = tx;
                vm.TextureSelectionEndY = ty;
                CaptureMouse();

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = $"Drag to select area for texture {vm.SelectedTextureGroup} #{vm.SelectedTextureNumber:000}";

                e.Handled = true;
                return;
            }


            // ===== ALTITUDE TOOLS: tile-based with rectangle selection =====
            if (vm.SelectedTool == EditorTool.SetAltitude ||
                vm.SelectedTool == EditorTool.SampleAltitude ||
                vm.SelectedTool == EditorTool.ResetAltitude ||
                vm.SelectedTool == EditorTool.DetectRoof)
            {
                // Get tile coordinates from click position
                int tx = (int)Math.Floor(mouseDownPos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseDownPos.Y / MapConstants.TileSize);

                Debug.WriteLine($"[MapView] Altitude tool click at pixel ({mouseDownPos.X:F0}, {mouseDownPos.Y:F0})");
                Debug.WriteLine($"[MapView] Calculated tile: tx={tx}, ty={ty}");
                Debug.WriteLine($"[MapView] Selected tool: {vm.SelectedTool}, Target altitude: {vm.TargetAltitude}");

                if (tx < 0 || tx >= MapConstants.TilesPerSide || ty < 0 || ty >= MapConstants.TilesPerSide)
                {
                    Debug.WriteLine("[MapView] Tile out of bounds, ignoring click");
                    return;
                }

                if (_altitude == null)
                {
                    Debug.WriteLine("[MapView] Initializing _altitude accessor");
                    _altitude = new AltitudeAccessor(MapDataService.Instance);
                }

                switch (vm.SelectedTool)
                {
                    case EditorTool.SetAltitude:
                        {
                            // Start rectangle selection
                            vm.IsSettingAltitude = true;
                            vm.AltitudeSelectionStartX = tx;
                            vm.AltitudeSelectionStartY = ty;
                            vm.AltitudeSelectionEndX = tx;
                            vm.AltitudeSelectionEndY = ty;
                            CaptureMouse();

                            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                                shell.StatusMessage = $"Drag to select area for altitude {vm.TargetAltitude}";

                            e.Handled = true;
                            return;
                        }

                    case EditorTool.SampleAltitude:
                        {
                            int sampledAlt = _altitude.ReadWorldAltitude(tx, ty);
                            PapFlags flags = _altitude.ReadFlags(tx, ty);
                            Debug.WriteLine($"[MapView] Sampled: altitude={sampledAlt}, flags=0x{(ushort)flags:X4} ({flags})");
                            vm.TargetAltitude = sampledAlt;

                            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                                shell.StatusMessage = $"Sampled altitude {sampledAlt} (raw: {sampledAlt >> 3}), flags: {flags}";

                            e.Handled = true;
                            return;
                        }

                    case EditorTool.ResetAltitude:
                        {
                            // Start rectangle selection for reset
                            vm.IsSettingAltitude = true;
                            vm.AltitudeSelectionStartX = tx;
                            vm.AltitudeSelectionStartY = ty;
                            vm.AltitudeSelectionEndX = tx;
                            vm.AltitudeSelectionEndY = ty;
                            CaptureMouse();

                            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                                shell.StatusMessage = $"Drag to select area to clear altitude";

                            e.Handled = true;
                            return;
                        }

                    case EditorTool.DetectRoof:
                        {
                            Debug.WriteLine($"[MapView] DetectRoof at tile ({tx}, {ty})");

                            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                                shell.StatusMessage = $"Detect roof at tile [{tx},{ty}]";

                            e.Handled = true;
                            return;
                        }
                }
            }


            // ===== HEIGHT TOOLS: vertex-based =====
            // Require a vertex hit; ignore if not near a vertex
            if (!TryGetVertexIndexFromHit(mouseDownPos, out int vx, out int vy))
                return;

            // For templates that index by tile from the vertex's lower-left
            int baseTx = vx - 1;
            int baseTy = vy - 1;

            switch (vm.SelectedTool)
            {
                case EditorTool.LevelHeight:
                    {
                        _levelSource = _heights.ReadHeight(baseTx, baseTy);
                        _isLeveling = true;
                        _lastLeveledTile = null;
                        CaptureMouse();

                        // Apply to initial brush area
                        ForEachVertexInBrush(vx, vy, vm.BrushSize, (tx, ty) => ApplyHeightToTile(tx, ty, _levelSource));

                        if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell1)
                            shell1.StatusMessage = $"Level: picked {_levelSource} at vertex [{vx},{vy}] (brush {vm.BrushSize}×{vm.BrushSize})";

                        e.Handled = true;
                        return;
                    }

                case EditorTool.RaiseHeight:
                case EditorTool.LowerHeight:
                    {
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

                        if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell2)
                            shell2.StatusMessage = $"Height {(isRaise ? "+=" : "-=")} {step} at vertex [{vx},{vy}] (brush {vm.BrushSize}×{vm.BrushSize})";

                        e.Handled = true;
                        return;
                    }

                case EditorTool.FlattenHeight:
                    {
                        ForEachVertexInBrush(vx, vy, vm.BrushSize, (tx, ty) =>
                        {
                            if (_heights.ReadHeight(tx, ty) != 0) _heights.WriteHeight(tx, ty, (sbyte)0);
                        });

                        HeightsOverlay?.InvalidateVisual();

                        if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell3)
                            shell3.StatusMessage = $"Flattened to 0 at vertex [{vx},{vy}] (brush {vm.BrushSize}×{vm.BrushSize})";

                        e.Handled = true;
                        return;
                    }

                case EditorTool.DitchTemplate:
                    {
                        ApplyDitchTemplate(baseTx, baseTy);
                        e.Handled = true;
                        return;
                    }
                case EditorTool.SetAltitude:
                    {
                        Debug.WriteLine($"[MapView] SetAltitude click at pixel ({e.GetPosition(Surface).X:F0}, {e.GetPosition(Surface).Y:F0})");
                        Debug.WriteLine($"[MapView] Calculated tile: baseTx={baseTx}, baseTy={baseTy}, vx={vx}, vy={vy}");
                        Debug.WriteLine($"[MapView] Target altitude: {vm.TargetAltitude}");

                        if (_altitude == null)
                        {
                            Debug.WriteLine("[MapView] ERROR: _altitude is null! Initializing...");
                            _altitude = new AltitudeAccessor(MapDataService.Instance);
                        }

                        int targetAlt = vm.TargetAltitude;
                        int cellsModified = 0;

                        // Use baseTx, baseTy for cell coordinates (not vertex)
                        ForEachTileInBrushCentered(baseTx, baseTy, vm.BrushSize, (tx, ty) =>
                        {
                            Debug.WriteLine($"[MapView] Writing altitude {targetAlt} to cell ({tx}, {ty})");
                            int oldAlt = _altitude.ReadWorldAltitude(tx, ty);
                            _altitude.WriteWorldAltitude(tx, ty, targetAlt);
                            int newAlt = _altitude.ReadWorldAltitude(tx, ty);
                            Debug.WriteLine($"[MapView] Cell ({tx}, {ty}): old={oldAlt}, new={newAlt}");
                            cellsModified++;
                        });

                        Debug.WriteLine($"[MapView] Modified {cellsModified} cells");

                        // Refresh display
                        HeightsOverlay?.InvalidateVisual();
                        // Also invalidate altitude hover layer if you have one
                        // AltitudeHoverLayer?.InvalidateVisual();

                        if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                            shell.StatusMessage = $"Set altitude to {targetAlt} at cell [{baseTx},{baseTy}] (brush {vm.BrushSize}×{vm.BrushSize}, {cellsModified} cells)";

                        e.Handled = true;
                        return;
                    }

                case EditorTool.SampleAltitude:
                    {
                        Debug.WriteLine($"[MapView] SampleAltitude click at baseTx={baseTx}, baseTy={baseTy}");

                        if (_altitude == null)
                        {
                            Debug.WriteLine("[MapView] ERROR: _altitude is null! Initializing...");
                            _altitude = new AltitudeAccessor(MapDataService.Instance);
                        }

                        int sampledAlt = _altitude.ReadWorldAltitude(baseTx, baseTy);
                        Debug.WriteLine($"[MapView] Sampled altitude: {sampledAlt} (raw: {sampledAlt >> 3})");

                        vm.TargetAltitude = sampledAlt;

                        if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                            shell.StatusMessage = $"Sampled altitude {sampledAlt} (raw: {sampledAlt >> 3}) from cell [{baseTx},{baseTy}]";

                        e.Handled = true;
                        return;
                    }

                case EditorTool.ResetAltitude:
                    {
                        Debug.WriteLine($"[MapView] ResetAltitude click at baseTx={baseTx}, baseTy={baseTy}");

                        if (_altitude == null)
                        {
                            Debug.WriteLine("[MapView] ERROR: _altitude is null! Initializing...");
                            _altitude = new AltitudeAccessor(MapDataService.Instance);
                        }

                        int cellsModified = 0;
                        ForEachTileInBrushCentered(baseTx, baseTy, vm.BrushSize, (tx, ty) =>
                        {
                            Debug.WriteLine($"[MapView] Resetting altitude to 0 at cell ({tx}, {ty})");
                            _altitude.WriteWorldAltitude(tx, ty, 0);
                            cellsModified++;
                        });

                        Debug.WriteLine($"[MapView] Reset {cellsModified} cells to altitude 0");

                        HeightsOverlay?.InvalidateVisual();

                        if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                            shell.StatusMessage = $"Reset altitude to 0 at cell [{baseTx},{baseTy}] (brush {vm.BrushSize}×{vm.BrushSize})";

                        e.Handled = true;
                        return;
                    }

                case EditorTool.DetectRoof:
                    {
                        Debug.WriteLine($"[MapView] DetectRoof click at baseTx={baseTx}, baseTy={baseTy}");

                        // Convert pixel position to block coordinates
                        int blockX = baseTx;
                        int blockZ = baseTy;

                        if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                            shell.StatusMessage = $"Detect roof at block [{blockX},{blockZ}] - implement HeightsTab.OnDetectRoofClick()";

                        e.Handled = true;
                        return;
                    }

                default:
                    // No active tool that reacts to left-click
                    return;
            }
        }
        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Only handle when Ctrl is held
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                return;

            if (DataContext is not MapViewModel vm || Surface == null || Scroller == null)
                return;

            e.Handled = true;

            // Current zoom and target zoom
            var current = vm.Zoom;
            var factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
            var target = Math.Max(MinZoom, Math.Min(MaxZoom, current * factor));
            if (Math.Abs(target - current) < 0.0001) return;

            // Mouse position relative to content (unscaled 8192 space) and to viewport
            Point mouseOnContent = e.GetPosition(Surface);
            Point mouseOnViewport = e.GetPosition(Scroller);

            // Apply zoom
            vm.Zoom = target;

            // After layout updates with new zoom, set offsets so the content point under the cursor stays put
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Offsets are in (transformed) content pixels. With LayoutTransform,
                // ScrollViewer measures scrolled positions in scaled coordinates.
                double newOffsetX = mouseOnContent.X * target - mouseOnViewport.X;
                double newOffsetY = mouseOnContent.Y * target - mouseOnViewport.Y;

                // Clamp to scrollable range
                newOffsetX = Clamp(newOffsetX, 0, Math.Max(0, Scroller.ExtentWidth - Scroller.ViewportWidth));
                newOffsetY = Clamp(newOffsetY, 0, Math.Max(0, Scroller.ExtentHeight - Scroller.ViewportHeight));

                Scroller.ScrollToHorizontalOffset(newOffsetX);
                Scroller.ScrollToVerticalOffset(newOffsetY);
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// Iterate over all tiles in a brush area CENTERED on (centerTx, centerTy).
        /// This is different from ForEachTileInBrush which uses origin-based iteration.
        /// </summary>
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

        // Existing pointer inversion:
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

            if (_isLeveling && e.LeftButton == MouseButtonState.Pressed && Surface != null)
            {
                if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                    return; // ignore drag over scrollbar to avoid painting

                Point mouseMovePos = e.GetPosition(Surface);
                if (!TryGetVertexIndexFromHit(mouseMovePos, out int vx2, out int vy2))
                    return;

                // Paint the brush area with the sampled level height
                ForEachVertexInBrush(vx2, vy2, (DataContext as MapViewModel)?.BrushSize ?? 1, (tx, ty) =>
                {
                    ApplyHeightToTile(tx, ty, _levelSource);
                });

                // Status
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

                // Calculate selection size for status
                var rect = vm.GetWalkableSelectionRect();
                if (rect.HasValue)
                {
                    int width = rect.Value.MaxX - rect.Value.MinX + 1;
                    int height = rect.Value.MaxY - rect.Value.MinY + 1;
                    int tileCount = width * height;

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = $"Walkable region: {width}×{height} ({tileCount} tiles)";
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

                // Clamp to valid range
                tx = Math.Clamp(tx, 0, MapConstants.TilesPerSide - 1);
                ty = Math.Clamp(ty, 0, MapConstants.TilesPerSide - 1);

                // Update end corner (triggers overlay repaint via PropertyChanged)
                vm.AltitudeSelectionEndX = tx;
                vm.AltitudeSelectionEndY = ty;

                // Calculate selection size for status
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

            // Texture paint rectangle selection - update end corner
            if (vm.IsPaintingTexture && e.LeftButton == MouseButtonState.Pressed && Surface != null)
            {
                if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                    return;

                Point mouseMovePos = e.GetPosition(Surface);
                int tx = (int)Math.Floor(mouseMovePos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseMovePos.Y / MapConstants.TileSize);

                // Clamp to valid range
                tx = Math.Clamp(tx, 0, MapConstants.TilesPerSide - 1);
                ty = Math.Clamp(ty, 0, MapConstants.TilesPerSide - 1);

                // Update end corner (triggers overlay repaint via PropertyChanged)
                vm.TextureSelectionEndX = tx;
                vm.TextureSelectionEndY = ty;

                // Calculate selection size for status
                var rect = vm.GetTextureSelectionRect();
                if (rect.HasValue)
                {
                    int width = rect.Value.MaxX - rect.Value.MinX + 1;
                    int height = rect.Value.MaxY - rect.Value.MinY + 1;
                    int tileCount = width * height;

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    {
                        shell.StatusMessage = $"Paint {vm.SelectedTextureGroup} #{vm.SelectedTextureNumber:000}: {width}×{height} ({tileCount} tiles)";
                    }
                }
            }

            UpdateGhostHover(e.GetPosition(Surface));

            // ---- Prim placement ghost ----
            if (vm.IsPlacingPrim && Surface != null)
            {
                Point pos = e.GetPosition(Surface);
                int clampedX = Math.Clamp((int)pos.X, 0, MapConstants.MapPixels - 1);
                int clampedZ = Math.Clamp((int)pos.Y, 0, MapConstants.MapPixels - 1);

                // NEW: Ctrl → snap ghost to nearest 64×64 vertex (matches commit behaviour)
                SnapUiToVertexIfCtrl(ref clampedX, ref clampedZ);

                // Use different variable names to avoid shadowing gameX/gameZ above
                ObjectSpace.UiPixelsToGamePrim(clampedX, clampedZ, out int mapWhoIndex, out byte cellX, out byte cellZ);
                ObjectSpace.GamePrimToUiPixels(mapWhoIndex, cellX, cellZ, out int uiX, out int uiZ);
                ObjectSpace.GameIndexToUiRowCol(mapWhoIndex, out int uiRow, out int uiCol);

                vm.DragPreviewPrim = new PrimListItem
                {
                    Index = -1, // new
                    MapWhoIndex = mapWhoIndex,
                    MapWhoRow = uiRow,
                    MapWhoCol = uiCol,
                    PrimNumber = (byte)Math.Clamp(vm.PrimNumberToPlace, 0, 255),
                    Name = PrimCatalog.GetName(vm.PrimNumberToPlace),
                    Y = 0,      // default height
                    X = cellX,  // <— use cell-local coords (0..255)
                    Z = cellZ,  // <— use cell-local coords (0..255)
                    Yaw = 0,    // default yaw
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
                return; // nothing to finish if release on scrollbar

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

                // Raise the event so BuildingsTab can show the dialog
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

                    // Apply to all tiles in the rectangle
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
            // Handle texture rectangle selection completion
            if (DataContext is MapViewModel vmTex && vmTex.IsPaintingTexture)
            {
                var rect = vmTex.GetTextureSelectionRect();
                if (rect.HasValue)
                {
                    var acc = new TexturesAccessor(MapDataService.Instance);
                    int world = vmTex.TextureWorld;
                    int tileCount = 0;

                    // Apply texture to all tiles in the rectangle
                    for (int ty = rect.Value.MinY; ty <= rect.Value.MaxY; ty++)
                    {
                        for (int tx = rect.Value.MinX; tx <= rect.Value.MaxX; tx++)
                        {
                            acc.WriteTileTexture(tx, ty, vmTex.SelectedTextureGroup, vmTex.SelectedTextureNumber, vmTex.SelectedRotationIndex, world);
                            tileCount++;
                        }
                    }

                    // Redraw textures layer
                    TexturesChangeBus.Instance.NotifyChanged();

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    {
                        int width = rect.Value.MaxX - rect.Value.MinX + 1;
                        int height = rect.Value.MaxY - rect.Value.MinY + 1;
                        shell.StatusMessage = $"Painted {width}×{height} ({tileCount} tiles) with {vmTex.SelectedTextureGroup} #{vmTex.SelectedTextureNumber:000} rot {vmTex.SelectedRotationIndex}";
                    }
                }

                vmTex.ClearTextureSelection();
                ReleaseMouseCapture();
                e.Handled = true;
            }
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
            // Escape cancels walkable drawing
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
            // Escape cancels facet redraw
            if (e.Key == Key.Escape && vm.IsRedrawingFacet)
            {
                vm.CancelFacetRedraw();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Facet redraw cancelled.";
                e.Handled = true;
                return;
            }
            // Escape cancels facet redraw
            if (e.Key == Key.Escape && vm.IsPlacingLadder)
            {
                vm.CancelLadderPlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Ladder placement cancelled.";
                e.Handled = true;
                return;
            }
            // Escape cancels door placement
            if (e.Key == Key.Escape && vm.IsPlacingDoor)
            {
                vm.CancelDoorPlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Door placement cancelled.";
                e.Handled = true;
                return;
            }
                       // Escape cancels cable placement
            if (e.Key == Key.Escape && vm.IsPlacingCable)
            {
                vm.CancelCablePlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Cable placement cancelled.";
                e.Handled = true;
                return;
            }

            // Escape cancels facet multi-draw
            if (e.Key == Key.Escape && vm.IsMultiDrawingFacets)
            {
                vm.CancelFacetMultiDraw();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Facet drawing cancelled.";
                e.Handled = true;
                return;
            }
        }

        private void OnLostMouseCapture(object? sender, MouseEventArgs e)
        {
            _isLeveling = false;
            _lastLeveledTile = null;
            if (_isDrawingWalkableRect)
            {
                _isDrawingWalkableRect = false;
                if (DataContext is MapViewModel vm)
                    vm.ClearWalkableSelection();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            GhostLayer?.SetHoverTile(null, null);
            // (keep your existing logic if any)
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
            // ignore if right-clicking on scrollbar
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

            // stop any leveling drag
            if (_isLeveling)
            {
                _isLeveling = false;
                _lastLeveledTile = null;
                if (IsMouseCaptured) ReleaseMouseCapture();
            }

            // Cancel facet redraw mode
            if (DataContext is MapViewModel vmFacet && vmFacet.IsRedrawingFacet)
            {
                vmFacet.CancelFacetRedraw();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shellFacet)
                    shellFacet.StatusMessage = "Facet redraw cancelled.";
                e.Handled = true;
                return;
            }

            // Finish/cancel facet multi-draw mode
            if (DataContext is MapViewModel vmMultiDraw && vmMultiDraw.IsMultiDrawingFacets)
            {
                vmMultiDraw.FinishFacetMultiDraw();
                e.Handled = true;
                return;
            }

            // Cancel ladder placement mode
            if (DataContext is MapViewModel vmLadder && vmLadder.IsPlacingLadder)
            {
                vmLadder.CancelLadderPlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shellLadder)
                    shellLadder.StatusMessage = "Ladder placement cancelled.";
                e.Handled = true;
                return;
            }

            // Cancel door placement mode
            if (DataContext is MapViewModel vmDoor && vmDoor.IsPlacingDoor)
            {
                vmDoor.CancelDoorPlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shellDoor)
                    shellDoor.StatusMessage = "Door placement cancelled.";
                e.Handled = true;
                return;
            }

            // Cancel cable placement mode
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

            // clear the tool
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel vm)
            {
                vm.Map.SelectedTool = EditorTool.None;
                // NEW: clear any selection (prim or light)
                bool cleared = false;
                if (vm.Map.SelectedPrim != null) { vm.Map.SelectedPrim = null; cleared = true; }

                vm.StatusMessage = cleared ? "Selection cleared." : "Action cleared.";
            }
            e.Handled = true;

            GhostLayer?.SetHoverTile(null, null);

            e.Handled = true;
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

            // 1) Core ditch body: 6 columns wide (dx = -4..+1), 9 rows tall (dy = -4..+4), all -32
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 1; dx++)
                {
                    SetIfInBounds(cx + dx, cy + dy, -32);
                }
            }

            // 2) Right-side 2-column ramp at dx = +2 and +3, only for dy in [-2..+2]
            // dy → value: -2:-26, -1:-20, 0:-13, +1:-7, +2:0
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

            // repaint once
            HeightsOverlay?.InvalidateVisual();

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                shell.StatusMessage = $"Stamped Ditch at [{cx},{cy}]";
        }

        public void GoToTileCenter(int tx, int ty)
        {
            if (Scroller == null || Surface == null) return;
            if (DataContext is not MapViewModel vm) return;

            // Center of the tile in content (unscaled) pixels
            double cx = (tx + 0.5) * MapConstants.TileSize;
            double cy = (ty + 0.5) * MapConstants.TileSize;

            double z = vm.Zoom;
            // Convert to scaled coordinates (ScrollViewer measures in scaled units because of LayoutTransform)
            double sx = cx * z;
            double sy = cy * z;

            // Target offsets so that (sx, sy) is centered in the viewport
            double targetX = sx - Scroller.ViewportWidth / 2.0;
            double targetY = sy - Scroller.ViewportHeight / 2.0;

            // Clamp to scrollable range
            targetX = Clamp(targetX, 0, System.Math.Max(0, Scroller.ExtentWidth - Scroller.ViewportWidth));
            targetY = Clamp(targetY, 0, System.Math.Max(0, Scroller.ExtentHeight - Scroller.ViewportHeight));

            // Apply after layout has current zoom/extent
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                Scroller.ScrollToHorizontalOffset(targetX);
                Scroller.ScrollToVerticalOffset(targetY);
            }), System.Windows.Threading.DispatcherPriority.Render);
        }
        private const double VertexHitRadius = 16.0; // px, slightly bigger than drawn 14px circle

        /// <summary>
        /// If the mouse is close to a height vertex circle, map it to the corresponding tile (tx,ty).
        /// Circles are centered at (tx+1, ty+1)*64 for tx,ty in [0..127].
        /// </summary>
        private static bool TryGetTileFromVertexHit(Point p, out int tx, out int ty)
        {
            tx = ty = -1;

            // nearest vertex indices in grid space (1..128)
            int vx = (int)Math.Round(p.X / MapConstants.TileSize);
            int vy = (int)Math.Round(p.Y / MapConstants.TileSize);

            if (vx < 1 || vy < 1 || vx > MapConstants.TilesPerSide || vy > MapConstants.TilesPerSide)
                return false;

            // center of that vertex in pixels
            double cx = vx * MapConstants.TileSize;
            double cy = vy * MapConstants.TileSize;

            // only accept if within circle-ish radius
            double dx = p.X - cx, dy = p.Y - cy;
            if ((dx * dx + dy * dy) > (VertexHitRadius * VertexHitRadius))
                return false;

            // this vertex belongs to tile (vx-1, vy-1)
            tx = vx - 1;
            ty = vy - 1;
            return true;
        }

        // Returns true and (vx,vy) in the vertex grid (1..128) if near a vertex circle
        private static bool TryGetVertexIndexFromHit(Point p, out int vx, out int vy)
        {
            vx = vy = -1;

            int candVx = (int)Math.Round(p.X / MapConstants.TileSize); // 1..128
            int candVy = (int)Math.Round(p.Y / MapConstants.TileSize); // 1..128

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
            // vx,vy are vertex indices (1..128). The tile directly "under" this vertex is (vx-1, vy-1).
            // Center an N×N brush around that tile.
            int size = Math.Clamp(brushSize, 1, 10);
            int half = size / 2; // floor

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

        /// <summary>
        /// Iterate over all tiles in a brush area centered on (centerTx, centerTy).
        /// </summary>
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
            if (GhostLayer == null) return; // x:Name your ghost overlay in XAML as GhostLayer
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
            // Only snap when Ctrl is held
            if (!IsCtrlDown())
                return;

            int size = MapConstants.TileSize; // 64

            uiX = (int)(Math.Round(uiX / (double)size) * size);
            uiZ = (int)(Math.Round(uiZ / (double)size) * size);

            uiX = Math.Clamp(uiX, 0, MapConstants.MapPixels - 1);
            uiZ = Math.Clamp(uiZ, 0, MapConstants.MapPixels - 1);
        }
    }
}
