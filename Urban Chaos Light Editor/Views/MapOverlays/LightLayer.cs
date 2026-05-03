// ============================================================
// LightEditor/Views/MapOverlays/LightLayer.cs
// ============================================================
// NOTE: The Light Editor's LightLayer is a Canvas-based interactive layer
// with drag-to-move functionality. It should remain as the existing
// implementation since it has specialized editing behavior.
// 
// However, it could optionally extend SharedLightLayer for the
// rendering portion while keeping the interactive Canvas behavior.
// ============================================================

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosEditor.Shared.Constants;
using System.Windows.Shapes;
using UrbanChaosLightEditor.Services;
using UrbanChaosLightEditor.ViewModels;

namespace UrbanChaosLightEditor.Views.MapOverlays
{
    /// <summary>
    /// Interactive light layer for Light Editor.
    /// Uses Canvas with Shape children for hit testing and drag interactions.
    /// This is specialized for the Light Editor and not shared.
    /// </summary>
    public sealed class LightLayer : Canvas
    {
        // [Existing implementation remains unchanged]
        // This is the interactive version with drag-to-move, selection, etc.
        // See the original LightLayer.cs in the Light Editor project.

        private readonly LightsAccessor _acc = new LightsAccessor(LightsDataService.Instance);
        private MainWindowViewModel? _vm;

        // Drag state
        private bool _mouseDown;
        private Point _mouseDownPos;
        private int _pressedIndex = -1;
        private Shape? _pressedShape;
        private bool _isDragging;
        private int _dragIndex = -1;
        private Shape? _dragShape;
        private double _dragRadius;

        private const double DragThreshold = EditorUiConstants.DragThreshold;

        public LightLayer()
        {
            Width = 8192;
            Height = 8192;
            Background = null;
            IsHitTestVisible = true;
            Panel.SetZIndex(this, 6);
            Loaded += (_, __) =>
            {
                Redraw();
                LightsDataService.Instance.LightsBytesReset += OnLightsBytesReset;
            };
            Unloaded += (_, __) =>
            {
                LightsDataService.Instance.LightsBytesReset -= OnLightsBytesReset;
            };

            AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnMouseLeftDown), true);
            AddHandler(UIElement.PreviewMouseMoveEvent, new MouseEventHandler(OnMouseMove), true);
            AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnMouseLeftUp), true);

            DataContextChanged += (_, __) => HookVm();
        }

        private void OnLightsBytesReset(object? sender, EventArgs e) => Dispatcher.Invoke(Redraw);

        private void Redraw()
        {
            Children.Clear();

            var svc = LightsDataService.Instance;
            var buf = svc.GetBytesCopy();

            if (!svc.IsLoaded || buf.Length < LightsAccessor.TotalSize)
                return;

            try
            {
                var entries = _acc.ReadAllEntries();
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.Used != 1) continue;

                    int uiX = LightsAccessor.WorldXToUiX(e.X);
                    int uiZ = LightsAccessor.WorldZToUiZ(e.Z);

                    // Range in world units; 4 world units = 1 UI pixel, 256 world = 1 tile = 64 UI px
                    double uiRadius = Math.Max(8.0, e.Range / 4.0);

                    byte r = unchecked((byte)(e.Red + 128));
                    byte g = unchecked((byte)(e.Green + 128));
                    byte b = unchecked((byte)(e.Blue + 128));

                    // Radial gradient: opaque at centre, fully transparent at edge
                    var fill = new RadialGradientBrush(
                        Color.FromArgb(210, r, g, b),
                        Color.FromArgb(0,   r, g, b));

                    var ellipse = new Ellipse
                    {
                        Width = uiRadius * 2,
                        Height = uiRadius * 2,
                        Fill = fill,
                        Stroke = new SolidColorBrush(Color.FromArgb(100, r, g, b)),
                        StrokeThickness = 1.0,
                        Tag = i
                    };

                    SetLeft(ellipse, uiX - uiRadius);
                    SetTop(ellipse, uiZ - uiRadius);
                    Children.Add(ellipse);

                    // Selected ring
                    if (_vm != null && _vm.SelectedLight?.Index == i)
                    {
                        double ringSize = uiRadius * 2 + 8;
                        var ring = new Ellipse
                        {
                            Width = ringSize,
                            Height = ringSize,
                            Stroke = Brushes.Yellow,
                            StrokeThickness = 2.0,
                            Fill = Brushes.Transparent,
                            IsHitTestVisible = false
                        };
                        SetLeft(ring, uiX - ringSize / 2);
                        SetTop(ring, uiZ - ringSize / 2);
                        Children.Add(ring);
                    }

                    // Range outline ring at exact world radius (no minimum clamp)
                    if (_vm?.ShowLightRanges == true)
                    {
                        double exactRadius = e.Range / 4.0;
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
                System.Diagnostics.Debug.WriteLine($"[LightLayer] Redraw skipped: {ex.Message}");
            }
        }

        private static Shape? FindAncestorShape(DependencyObject? d)
        {
            while (d != null)
            {
                if (d is Shape s) return s;
                d = VisualTreeHelper.GetParent(d);
            }
            return null;
        }

        private void OnMouseLeftDown(object? sender, MouseButtonEventArgs e)
        {
            var shape = FindAncestorShape(e.OriginalSource as DependencyObject);
            if (shape == null || shape.Tag is not int idx) return;

            // Double-click opens edit dialog
            if (e.ClickCount == 2)
            {
                if (_vm != null)
                {
                    SetSelectedByAccessorIndex(_vm, idx);
                    OpenEditDialog(idx);
                }
                e.Handled = true;
                return;
            }

            _mouseDown = true;
            _isDragging = false;
            _mouseDownPos = e.GetPosition(this);
            _pressedIndex = idx;
            _pressedShape = shape;

            var w = _pressedShape.RenderSize.Width;
            _dragRadius = (w > 0 ? w : _pressedShape.Width) / 2.0;

            CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_mouseDown || _pressedShape == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var pos = e.GetPosition(this);

            if (!_isDragging)
            {
                var d = pos - _mouseDownPos;
                if (Math.Abs(d.X) <= DragThreshold && Math.Abs(d.Y) <= DragThreshold)
                    return;

                _isDragging = true;
                _dragIndex = _pressedIndex;
                _dragShape = _pressedShape;
            }

            // Live drag
            double left = pos.X - _dragRadius;
            double top = pos.Y - _dragRadius;
            left = Math.Max(0, Math.Min(left, ActualWidth - _dragRadius * 2));
            top = Math.Max(0, Math.Min(top, ActualHeight - _dragRadius * 2));

            SetLeft(_dragShape!, left);
            SetTop(_dragShape!, top);
            e.Handled = true;
        }

        private void OnMouseLeftUp(object? sender, MouseButtonEventArgs e)
        {
            if (!_mouseDown) return;

            try
            {
                if (_isDragging && _dragShape != null && _dragIndex >= 0)
                {
                    // Commit move
                    Point p = e.GetPosition(this);
                    int uiX = (int)Math.Round(p.X);
                    int uiZ = (int)Math.Round(p.Y);

                    _vm?.MoveLightTo(_dragIndex, uiX, uiZ);
                    if (_vm != null)
                        SetSelectedByAccessorIndex(_vm, _dragIndex);
                }
                else
                {
                    // Pure click → select
                    if (_vm != null && _pressedIndex >= 0)
                    {
                        SetSelectedByAccessorIndex(_vm, _pressedIndex);
                        Redraw();
                    }
                }
            }
            finally
            {
                _mouseDown = false;
                _isDragging = false;
                _pressedIndex = -1;
                _pressedShape = null;
                _dragIndex = -1;
                _dragShape = null;
                if (IsMouseCaptured) ReleaseMouseCapture();
            }

            e.Handled = true;
        }

        private static void SetSelectedByAccessorIndex(MainWindowViewModel vm, int accessorIndex)
        {
            int collectionIdx = -1;
            for (int i = 0; i < vm.Lights.Count; i++)
            {
                if (vm.Lights[i].Index == accessorIndex)
                {
                    collectionIdx = i;
                    break;
                }
            }
            vm.SelectedLightIndex = collectionIdx;
        }

        private void HookVm()
        {
            if (_vm != null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as MainWindowViewModel;
            if (_vm != null) _vm.PropertyChanged += OnVmChanged;
            Redraw();
        }

        private void OnVmChanged(object? s, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedLightIndex) ||
                e.PropertyName == nameof(MainWindowViewModel.ShowLights) ||
                e.PropertyName == nameof(MainWindowViewModel.ShowLightRanges))
            {
                Dispatcher.Invoke(Redraw);
            }
        }
        private void OpenEditDialog(int lightIndex)
        {
            try
            {
                var entry = _acc.ReadEntry(lightIndex);

                var dlg = new Dialogs.AddEditLightDialog(
                    initialHeight: entry.Y,
                    initialRange: entry.Range,
                    initialRed: entry.Red,
                    initialGreen: entry.Green,
                    initialBlue: entry.Blue)
                {
                    Owner = Window.GetWindow(this),
                    Title = $"Edit Light #{lightIndex}"
                };

                if (dlg.ShowDialog() == true)
                {
                    // Update the light entry with new values
                    entry.Range = (byte)dlg.ResultRange;
                    entry.Red = (sbyte)dlg.ResultRed;
                    entry.Green = (sbyte)dlg.ResultGreen;
                    entry.Blue = (sbyte)dlg.ResultBlue;
                    entry.Y = dlg.ResultHeight;

                    _acc.WriteEntry(lightIndex, entry);

                    if (_vm != null)
                        _vm.StatusMessage = $"Updated light #{lightIndex}.";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LightLayer] Failed to edit light: {ex.Message}");
                if (_vm != null)
                    _vm.StatusMessage = $"Failed to edit light: {ex.Message}";
            }
        }
    }
}