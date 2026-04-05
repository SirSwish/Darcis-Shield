using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Models.Prims;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Prims;
using UrbanChaosMapEditor.Views.Prims.Dialogs;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Prims.MapOverlays
{
    /// <summary>
    /// Renders prim dots and handles:
    ///  - selection + drag/move (existing behavior),
    ///  - yaw arrow rendering,
    ///  - yaw edit by dragging in a ring around the dot (new).
    /// </summary>

    public sealed class PrimsLayer : FrameworkElement
    {

        private static int _nextId;
        private readonly int _id = System.Threading.Interlocked.Increment(ref _nextId);

        private void Log(string msg)
        {
            Debug.WriteLine($"[PrimsLayer#{_id} h={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)}] {msg}");
        }

        private const double DotRadius = 4.0;
        private const double HitGrow = 4.0;
        private const double DragThreshold = 4.0; // pixels
        private const double PrimSnapGrid = 32.0; // half-tile: covers corners, edge midpoints, and cell centres

        // --- Move drag state (unchanged semantics) ---
        private bool _mouseDown;
        private bool _isDragging;
        private Point _mouseDownPos;
        private int _draggedIndex = -1;
        private PrimListItem? _ghost;   // local ghost snapshot for move commit

        // --- Yaw edit state (new) ---
        private bool _yawEditing = false;
        private int _pressedIndex = -1;        // shared with existing paths (which prim we interacted with)
        private PrimListItem? _pressedItem;    // snapshot for yaw edit or move

        private const double ArrowLength = 22.0;
        private const double YawRingInner = DotRadius + 6.0;                    // just outside the dot
        private const double YawRingOuter = DotRadius + ArrowLength + 8.0;      // encompass arrow head

        private static readonly Brush RedFill = Brushes.Red;
        private static readonly Pen BlackPen = new Pen(Brushes.Black, 1.0);
        private static readonly Pen Highlight = new Pen(Brushes.Yellow, 2.0);
        private static readonly Pen GhostStroke = new Pen(Brushes.Yellow, 2) { DashStyle = DashStyles.Dash };
        private static readonly Pen ArrowPen = new Pen(Brushes.Lime, 2.0);

        static PrimsLayer()
        {
            BlackPen.Freeze();
            Highlight.Freeze();
            GhostStroke.Freeze();
            ArrowPen.Freeze();
        }

        public PrimsLayer()
        {
            Width = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = true;
            Focusable = true;

            // repaint on map lifecycle
            MapDataService.Instance.MapLoaded += (_, __) => Dispatcher.Invoke(InvalidateVisual);
            MapDataService.Instance.MapBytesReset += (_, __) => Dispatcher.Invoke(InvalidateVisual);
            MapDataService.Instance.MapCleared += (_, __) => Dispatcher.Invoke(InvalidateVisual);

            // repaint when objects changed (add/move/delete)
            ObjectsChangeBus.Instance.Changed += (_, __) => Dispatcher.Invoke(InvalidateVisual);

            DataContextChanged += (_, __) => HookVm();
        }

        private MapViewModel? _vm;
        private void HookVm()
        {
            if (_vm != null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as MapViewModel;
            if (_vm != null) _vm.PropertyChanged += OnVmChanged;
            InvalidateVisual();
        }

        private void OnVmChanged(object? s, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapViewModel.Prims) ||
                e.PropertyName == nameof(MapViewModel.SelectedPrim) ||
                e.PropertyName == nameof(MapViewModel.SelectedPrims) ||
                e.PropertyName == nameof(MapViewModel.DragPreviewPrim))
            {
                Dispatcher.Invoke(InvalidateVisual);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            if (_vm is null) return;

            // --- local helpers ---
            static Point Dir(Point origin, double len, double radians)
                => new Point(origin.X + len * Math.Cos(radians),
                             origin.Y - len * Math.Sin(radians)); // screen Y down

            static void DrawArrow(DrawingContext d, Point from, double radians, double length)
            {
                var tip = Dir(from, length, radians);
                d.DrawLine(ArrowPen, from, tip);

                const double headLen = 7.0;
                const double headAngleDeg = 25.0;
                double a = headAngleDeg * Math.PI / 180.0;

                var left = Dir(tip, headLen, radians + Math.PI - a);
                var right = Dir(tip, headLen, radians + Math.PI + a);

                d.DrawLine(ArrowPen, tip, left);
                d.DrawLine(ArrowPen, tip, right);
            }

            const double twoPiOver256 = 2.0 * Math.PI / 256.0;
            const double yawOffset = Math.PI / 2.0; // +90- so -right = 0- points UP

            // draw all dots + yaw arrows
            var selectedSet = _vm.SelectedPrims;
            foreach (var p in _vm.Prims)
            {
                var center = new Point(p.PixelX, p.PixelZ);
                dc.DrawEllipse(RedFill, BlackPen, center, DotRadius, DotRadius);

                if (selectedSet.Contains(p))
                    dc.DrawEllipse(null, Highlight, center, DotRadius + 4, DotRadius + 4);

                double radians = p.Yaw * twoPiOver256 + yawOffset + Math.PI;   // +180-
                DrawArrow(dc, center, radians, ArrowLength);
            }

            // ghost preview (move ghost and/or yaw preview)
            if (_vm.DragPreviewPrim != null)
            {
                var g = _vm.DragPreviewPrim;
                var center = new Point(g.PixelX, g.PixelZ);

                dc.DrawEllipse(null, GhostStroke, center, DotRadius + 2, DotRadius + 2);

                double radians = g.Yaw * twoPiOver256 + yawOffset + Math.PI;   // keep in sync with main arrow
                DrawArrow(dc, center, radians, ArrowLength);
            }
        }

        // Ensure we receive hit-tests anywhere on the layer
        protected override HitTestResult HitTestCore(PointHitTestParameters p)
            => new PointHitTestResult(this, p.HitPoint);

        // -------- Additional helpers --------
        private static Point SnapToNearestVertex(Point pos)
        {
            // Map canvas uses X = PixelX, Y = PixelZ
            double snappedX = Math.Round(pos.X / PrimSnapGrid) * PrimSnapGrid;
            double snappedZ = Math.Round(pos.Y / PrimSnapGrid) * PrimSnapGrid;
            return new Point(snappedX, snappedZ);
        }

        private static double NormalizeRadians(double a)
        {
            double twoPi = 2.0 * Math.PI;
            a %= twoPi;
            return (a < 0) ? a + twoPi : a;
        }

        // Snap a raw yaw byte to the nearest 45° step (256/8 = 32 units per step).
        private static byte SnapYawTo45(byte yaw)
        {
            const int step = 256 / 8; // 32 units = 45°
            int snapped = (int)Math.Round(yaw / (double)step) * step;
            return (byte)(snapped % 256);
        }

        private static bool IsShiftDown()
            => Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        // Map a mouse position to a yaw byte (0..255) for a given prim center.
        private static byte YawFromMouse(Point mouse, Point center)
        {
            // Convert screen ? to math coords (Y up)
            double dx = mouse.X - center.X;
            double dy = -(mouse.Y - center.Y); // flip Y to math

            double angle = Math.Atan2(dy, dx); // [-p, p]
            angle = NormalizeRadians(angle);

            // Render uses: radians = yaw*(2p/256) + 90-
            // => yaw = (angle - 90-) * 256 / (2p)
            double yawOffset = Math.PI / 2.0;
            double twoPi = 2.0 * Math.PI;
            double raw = (angle - yawOffset - Math.PI) * 256.0 / twoPi;

            raw %= 256.0;
            if (raw < 0) raw += 256.0;

            int quantized = (int)Math.Round(raw);
            if (quantized == 256) quantized = 0;
            return (byte)quantized;
        }

        // -------- Existing move drag state (kept) --------
        private bool _dragStarted;
        // _pressedIndex and _pressedItem are declared above (shared with yaw)

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            if (DataContext is not MapViewModel vm) return;

            var pos = e.GetPosition(this);

            // Find the nearest prim within a generous radius
            int hit = -1;
            double bestD2 = double.MaxValue;
            for (int i = 0; i < vm.Prims.Count; i++)
            {
                var pr = vm.Prims[i];
                double dx = pos.X - pr.PixelX, dz = pos.Y - pr.PixelZ;
                double d2 = dx * dx + dz * dz;
                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    hit = i;
                }
            }

            // If nothing reasonable, bail
            double dist = Math.Sqrt(bestD2);
            double maxPick = Math.Max(YawRingOuter, DotRadius + 4); // allow arrow ring or dot
            if (hit < 0 || dist > maxPick)
            {
                Log($"[PrimsLayer] PMDown @ {pos.X},{pos.Y}, no hit");
                // Clear selection when clicking empty space (unless Ctrl is held to preserve it)
                bool isCtrlEmpty = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                if (!isCtrlEmpty)
                {
                    vm.SetSelectedPrims(Array.Empty<PrimListItem>());
                    vm.SelectedPrim = null;
                }
                return;
            }

            var prim = vm.Prims[hit];
            bool isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (isCtrl)
            {
                // Toggle this prim in the multi-selection
                var newSel = vm.SelectedPrims.ToList();
                if (newSel.Contains(prim))
                    newSel.Remove(prim);
                else
                    newSel.Add(prim);
                // Suppress list SelectionChanged from overwriting the canvas multi-select
                vm.SuppressListToCanvasSync = true;
                vm.SetSelectedPrims(newSel);
                vm.SelectedPrim = newSel.LastOrDefault();
                vm.SuppressListToCanvasSync = false;
                // Claim keyboard focus so the Delete key reaches the canvas handler
                Focus();
                Keyboard.Focus(this);
                e.Handled = true;
                return;
            }

            // Suppress list sync for single-prim canvas selection too
            vm.SuppressListToCanvasSync = true;
            vm.SelectedPrim = prim;
            vm.SetSelectedPrims(new[] { prim });
            vm.SuppressListToCanvasSync = false;

            // Double-click remains reserved for properties (your other handler also handles this)
            if (e.ClickCount == 2)
            {
                // Let the bubbling OnMouseDown do the dialog (we'll mark handled so we don't do move)
                return;
            }

            var center = new Point(prim.PixelX, prim.PixelZ);

            // --- NEW: Start yaw edit if click is in the arrow ring (outside the dot) ---
            if (dist >= YawRingInner && dist <= YawRingOuter)
            {
                _yawEditing = true;
                _pressedIndex = hit;
                _pressedItem = prim;
                _mouseDown = false;       // not a move
                _isDragging = false;
                _draggedIndex = -1;
                _ghost = null;

                CaptureMouse();
                Focus();
                Keyboard.Focus(this);

                // Seed a yaw ghost for live preview
                var yaw = YawFromMouse(pos, center);
                if (IsShiftDown()) yaw = SnapYawTo45(yaw);
                vm.DragPreviewPrim = new PrimListItem
                {
                    Index = prim.Index,
                    PrimNumber = prim.PrimNumber,
                    Name = prim.Name,
                    Y = prim.Y,
                    X = prim.X,
                    Z = prim.Z,
                    MapWhoIndex = prim.MapWhoIndex,
                    MapWhoRow = prim.MapWhoRow,
                    MapWhoCol = prim.MapWhoCol,
                    PixelX = prim.PixelX,
                    PixelZ = prim.PixelZ,
                    Yaw = yaw,
                    Flags = prim.Flags,
                    InsideIndex = prim.InsideIndex
                };

                var shell = Application.Current.MainWindow?.DataContext as MainWindowViewModel;
                if (shell != null) shell.StatusMessage = IsShiftDown()
                    ? $"Adjusting yaw... ({yaw}/255) [Shift: 45\u00b0 snap]"
                    : $"Adjusting yaw... ({yaw}/255)";

                e.Handled = true;
                return;
            }

            // --- Else: begin move drag on dot area (existing behavior) ---
            if (dist <= (DotRadius + 4))
            {
                _mouseDown = true;
                _isDragging = false;
                _mouseDownPos = pos;
                _draggedIndex = hit;
                _ghost = null;
                _pressedIndex = hit;
                _pressedItem = prim;

                bool captured = CaptureMouse();
                Focus();
                Keyboard.Focus(this);
                Log($"[PrimsLayer] Select for drag: UI-idx={hit}, captured={captured}, down={_mouseDownPos.X},{_mouseDownPos.Y}");
                e.Handled = true;
                return;
            }
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);
            if (DataContext is not MapViewModel vm) return;

            var pos = e.GetPosition(this);

            // --- Yaw editing live preview ---
            if (_yawEditing && _pressedItem != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var center = new Point(_pressedItem.PixelX, _pressedItem.PixelZ);
                var yaw = YawFromMouse(pos, center);
                bool snap = IsShiftDown();
                if (snap) yaw = SnapYawTo45(yaw);

                vm.DragPreviewPrim = new PrimListItem
                {
                    Index = _pressedItem.Index,
                    PrimNumber = _pressedItem.PrimNumber,
                    Name = _pressedItem.Name,
                    Y = _pressedItem.Y,
                    X = _pressedItem.X,
                    Z = _pressedItem.Z,
                    MapWhoIndex = _pressedItem.MapWhoIndex,
                    MapWhoRow = _pressedItem.MapWhoRow,
                    MapWhoCol = _pressedItem.MapWhoCol,
                    PixelX = _pressedItem.PixelX,
                    PixelZ = _pressedItem.PixelZ,
                    Yaw = yaw,
                    Flags = _pressedItem.Flags,
                    InsideIndex = _pressedItem.InsideIndex
                };

                var shell = Application.Current.MainWindow?.DataContext as MainWindowViewModel;
                if (shell != null) shell.StatusMessage = snap
                    ? $"Yaw: {yaw}/255 [Shift: 45\u00b0 snap]"
                    : $"Yaw: {yaw}/255";

                InvalidateVisual();
                return;
            }

            // --- Move drag path (existing behavior, now with optional snapping) ---
            if (e.LeftButton == MouseButtonState.Pressed)
                Log($"[PrimsLayer] PMove (pressed) @ {pos.X},{pos.Y}  state: down={_mouseDown} drag={_isDragging} idx={_draggedIndex}");

            if (!_mouseDown || _draggedIndex < 0 || e.LeftButton != MouseButtonState.Pressed)
                return;

            if (!_isDragging)
            {
                var d = pos - _mouseDownPos;
                if (Math.Abs(d.X) > DragThreshold || Math.Abs(d.Y) > DragThreshold)
                {
                    _isDragging = true;
                    Log($"[PrimsLayer] Drag start. down={_mouseDownPos.X},{_mouseDownPos.Y} now={pos.X},{pos.Y} ?=({d.X:0.0},{d.Y:0.0})");
                }
                else
                {
                    return;
                }
            }

            bool ctrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            if (ctrlDown)
                pos = SnapToNearestVertex(pos);

            int clampedMoveX = Math.Clamp((int)pos.X, 0, 8191);
            int clampedMoveZ = Math.Clamp((int)pos.Y, 0, 8191);

            ObjectSpace.UiPixelsToGamePrim(clampedMoveX, clampedMoveZ, out int moveTargetIndex, out byte moveGameX, out byte moveGameZ);
            ObjectSpace.GameIndexToUiRowCol(moveTargetIndex, out int moveUiRow, out int moveUiCol);
            ObjectSpace.GamePrimToUiPixels(moveTargetIndex, moveGameX, moveGameZ, out int movePx, out int movePz);

            var src = vm.Prims[_draggedIndex];

            _ghost = new PrimListItem
            {
                Index = src.Index,
                MapWhoIndex = moveTargetIndex,
                MapWhoRow = moveUiRow,
                MapWhoCol = moveUiCol,
                PrimNumber = src.PrimNumber,
                Name = src.Name,
                Y = src.Y,
                X = moveGameX,
                Z = moveGameZ,
                Yaw = src.Yaw,
                Flags = src.Flags,
                InsideIndex = src.InsideIndex,
                PixelX = movePx,
                PixelZ = movePz
            };

            vm.DragPreviewPrim = _ghost;

            Log($"[PrimsLayer] Ghost ? cell={moveTargetIndex} (r{moveUiRow},c{moveUiCol}) game X={moveGameX},Z={moveGameZ} ui=({movePx},{movePz}) from Prim.Index={src.Index}");
            InvalidateVisual();
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);
            if (DataContext is not MapViewModel vm) { Log("[PrimsLayer] PMUp: no VM"); return; }

            // --- Finish yaw edit (commit) ---
            if (_yawEditing && _pressedItem != null)
            {
                var pos = e.GetPosition(this);
                var center = new Point(_pressedItem.PixelX, _pressedItem.PixelZ);
                var finalYaw = YawFromMouse(pos, center);
                if (IsShiftDown()) finalYaw = SnapYawTo45(finalYaw);

                try
                {
                    var acc = new PrimsAccessor(MapDataService.Instance);
                    acc.EditPrim(_pressedItem.Index, prim =>
                    {
                        prim.Yaw = finalYaw;
                        return prim;
                    });

                    vm.RefreshPrimsList();

                    PrimListItem? toSelect = null;
                    if (_pressedItem.Index >= 0 && _pressedItem.Index < vm.Prims.Count)
                        toSelect = vm.Prims[_pressedItem.Index];

                    vm.SelectedPrim = toSelect;

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = $"Set yaw to {finalYaw}/255";
                }
                catch (Exception ex)
                {
                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = "Yaw update failed.";
                    Log($"[PrimsLayer] Yaw update failed: {ex}");
                }

                // cleanup yaw state
                vm.DragPreviewPrim = null;
                _yawEditing = false;
                _pressedIndex = -1;
                _pressedItem = null;

                if (IsMouseCaptured) ReleaseMouseCapture();
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // --- Move commit path (FIXED) ---
            // IMPORTANT: snapshot state BEFORE releasing capture (release can trigger LostMouseCapture)
            bool wasDown = _mouseDown;
            bool wasDragging = _isDragging;
            int draggedIdx = _draggedIndex;
            var ghost = _ghost;

            Log($"[PrimsLayer] PMUp(pre): down={wasDown}, isDragging={wasDragging}, idx={draggedIdx}, ghostLocal={ghost != null}, ghostVm={vm.DragPreviewPrim != null}");

            if (wasDragging && draggedIdx >= 0 && ghost != null)
            {
                try
                {
                    Log($"[PrimsLayer] Commit MovePrim: primEntryIdx={ghost.Index}, mapWho={ghost.MapWhoIndex}, X={ghost.X}, Z={ghost.Z}");

                    var acc = new PrimsAccessor(MapDataService.Instance);
                    acc.MovePrim(ghost.Index, ghost.MapWhoIndex, ghost.X, ghost.Z);

                    vm.RefreshPrimsList();
                    Log("[PrimsLayer] RefreshPrimsList after move.");
                }
                catch (Exception ex)
                {
                    Log($"[PrimsLayer] Move commit failed: {ex}");
                }
            }
            else
            {
                Log("[PrimsLayer] No commit.");
            }

            // cleanup move state
            vm.DragPreviewPrim = null;
            _ghost = null;
            _mouseDown = false;
            _isDragging = false;
            _draggedIndex = -1;
            _pressedIndex = -1;
            _pressedItem = null;

            if (IsMouseCaptured) ReleaseMouseCapture();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);

            // Only cancel yaw if capture is stolen mid-yaw-edit
            if (_yawEditing)
            {
                _yawEditing = false;
                _pressedIndex = -1;
                _pressedItem = null;

                if (_vm != null)
                    _vm.DragPreviewPrim = null;

                InvalidateVisual();
            }

            // DO NOT cancel move drag here.
            // MouseUp handles commit/no-commit + cleanup reliably.
        }

        // Your existing OnMouseDown handler (dialog / selection) remains;
        // note that we mark e.Handled in PM LBD when starting yaw or move, so
        // this block won-t run in those cases.
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (DataContext is not MapViewModel vm) return;

            Point pos = e.GetPosition(this);

            foreach (var prim in vm.Prims)
            {
                double dx = pos.X - prim.PixelX;
                double dz = pos.Y - prim.PixelZ;
                if ((dx * dx + dz * dz) <= DotRadius * DotRadius)
                {
                    if (e.ClickCount == 2)
                    {
                        vm.SelectedPrim = prim;

                        var dlg = new PrimPropertiesDialog(prim.Flags, prim.InsideIndex, prim.Y)
                        {
                            Owner = Application.Current.MainWindow
                        };

                        if (dlg.ShowDialog() == true)
                        {
                            var acc = new PrimsAccessor(MapDataService.Instance);
                            acc.EditPrim(prim.Index, p2 =>
                            {
                                p2.Flags = dlg.FlagsValue;
                                p2.InsideIndex = dlg.InsideIndexValue;
                                p2.Y = (short)dlg.ResultHeight;
                                return p2;
                            });

                            vm.RefreshPrimsList();

                            if (prim.Index >= 0 && prim.Index < vm.Prims.Count)
                                vm.SelectedPrim = vm.Prims[prim.Index];

                            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                                shell.StatusMessage =
                                    $"Updated {prim.Name} | Height={dlg.ResultHeight} | Flags={dlg.FlagsValue} | Inside={dlg.InsideIndexValue}";
                        }

                        e.Handled = true;
                        return;
                    }

                    if (e.ClickCount == 1)
                    {
                        vm.SelectedPrim = prim;
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
    }
}
