// /Views/MapOverlays/AltitudeHoverLayer.cs
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.ViewModels.Core;
using HeightSettings = UrbanChaosMapEditor.Models.Core.HeightDisplaySettings;

namespace UrbanChaosMapEditor.Views.Heights.MapOverlays
{
    /// <summary>
    /// Overlay that shows a green hover highlight when altitude tools are selected.
    /// Also displays the rectangle selection box during drag operations.
    /// Additionally displays orange rectangle during walkable region drawing.
    /// </summary>
    public sealed class AltitudeHoverLayer : FrameworkElement
    {
        private MapViewModel? _vm;
        private AltitudeAccessor? _altitude;
        private readonly DispatcherTimer _debounceTimer;

        // Brushes for hover (single tile or brush area)
        private static readonly Brush HoverFillBrush = new SolidColorBrush(Color.FromArgb(80, 0, 200, 0)); // semi-transparent green
        private static readonly Brush HoverStrokeBrush = new SolidColorBrush(Color.FromArgb(200, 0, 255, 0)); // bright green
        private static readonly Pen HoverPen = new Pen(HoverStrokeBrush, 2);

        // Brushes for selection rectangle (more prominent)
        private static readonly Brush SelectionFillBrush = new SolidColorBrush(Color.FromArgb(100, 0, 255, 100)); // brighter green fill
        private static readonly Brush SelectionStrokeBrush = new SolidColorBrush(Color.FromArgb(255, 0, 255, 0)); // solid bright green
        private static readonly Pen SelectionPen = new Pen(SelectionStrokeBrush, 3);

        // Brushes for walkable selection rectangle (orange)
        private static readonly Brush WalkableFillBrush = new SolidColorBrush(Color.FromArgb(80, 255, 165, 0)); // semi-transparent orange
        private static readonly Brush WalkableStrokeBrush = new SolidColorBrush(Color.FromArgb(255, 255, 140, 0)); // solid orange
        private static readonly Pen WalkablePen = new Pen(WalkableStrokeBrush, 3);

        private static readonly Brush TextBrush = Brushes.White;
        private static readonly Brush TextShadowBrush = Brushes.Black;

        static AltitudeHoverLayer()
        {
            HoverFillBrush.Freeze();
            HoverStrokeBrush.Freeze();
            HoverPen.Freeze();
            SelectionFillBrush.Freeze();
            SelectionStrokeBrush.Freeze();
            SelectionPen.Freeze();
            WalkableFillBrush.Freeze();
            WalkableStrokeBrush.Freeze();
            WalkablePen.Freeze();
        }

        public AltitudeHoverLayer()
        {
            Width = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = false;

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _debounceTimer.Tick += (_, __) =>
            {
                _debounceTimer.Stop();
                InvalidateVisual();
            };

            DataContextChanged += (_, __) => HookVm();

            // Listen for altitude changes
            AltitudeChangeBus.Instance.TileChanged += (tx, ty) => KickRepaint();
            AltitudeChangeBus.Instance.RegionChanged += (_, __, ___, ____) => KickRepaint();
            AltitudeChangeBus.Instance.AllChanged += () => KickRepaint();

            // Listen for display mode toggle
            HeightSettings.DisplayModeChanged += (_, __) => KickRepaint();
        }

        private void HookVm()
        {
            if (_vm is not null)
                _vm.PropertyChanged -= OnVmChanged;

            _vm = DataContext as MapViewModel;

            if (_vm is not null)
                _vm.PropertyChanged += OnVmChanged;

            // Initialize accessor
            if (MapDataService.Instance.IsLoaded)
                _altitude = new AltitudeAccessor(MapDataService.Instance);

            InvalidateVisual();
        }

        private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapViewModel.SelectedTool) ||
                e.PropertyName == nameof(MapViewModel.BrushSize) ||
                e.PropertyName == nameof(MapViewModel.CursorX) ||
                e.PropertyName == nameof(MapViewModel.CursorZ) ||
                e.PropertyName == nameof(MapViewModel.TargetAltitude) ||
                e.PropertyName == nameof(MapViewModel.IsSettingAltitude) ||
                e.PropertyName == nameof(MapViewModel.AltitudeSelectionStartX) ||
                e.PropertyName == nameof(MapViewModel.AltitudeSelectionStartY) ||
                e.PropertyName == nameof(MapViewModel.AltitudeSelectionEndX) ||
                e.PropertyName == nameof(MapViewModel.AltitudeSelectionEndY) ||
                // Walkable selection properties
                e.PropertyName == nameof(MapViewModel.IsDrawingWalkable) ||
                e.PropertyName == nameof(MapViewModel.WalkableSelectionStartX) ||
                e.PropertyName == nameof(MapViewModel.WalkableSelectionStartY) ||
                e.PropertyName == nameof(MapViewModel.WalkableSelectionEndX) ||
                e.PropertyName == nameof(MapViewModel.WalkableSelectionEndY))
            {
                KickRepaint();
            }
        }

        private void KickRepaint()
        {
            if (!_debounceTimer.IsEnabled) _debounceTimer.Start();
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            if (_vm == null) return;

            // Ensure accessor is available
            if (_altitude == null && MapDataService.Instance.IsLoaded)
                _altitude = new AltitudeAccessor(MapDataService.Instance);

            // Draw walkable selection rectangle (orange) - this takes priority
            if (_vm.IsDrawingWalkable)
            {
                DrawWalkableSelectionRectangle(dc);
            }
            // Draw altitude tools
            else if (IsAltitudeTool(_vm.SelectedTool))
            {
                // Check if we're in rectangle selection mode
                if (_vm.IsSettingAltitude)
                {
                    DrawSelectionRectangle(dc);
                }
                else
                {
                    // Just draw hover highlight at cursor
                    DrawHoverHighlight(dc);
                }
            }
        }

        /// <summary>
        /// Draw the walkable selection rectangle (orange).
        /// </summary>
        private void DrawWalkableSelectionRectangle(DrawingContext dc)
        {
            var rect = _vm!.GetWalkableSelectionRect();
            if (!rect.HasValue) return;

            int minX = rect.Value.MinX;
            int minY = rect.Value.MinY;
            int maxX = rect.Value.MaxX;
            int maxY = rect.Value.MaxY;

            // Convert to pixel coordinates
            double x = minX * MapConstants.TileSize;
            double y = minY * MapConstants.TileSize;
            double w = (maxX - minX + 1) * MapConstants.TileSize;
            double h = (maxY - minY + 1) * MapConstants.TileSize;

            var selectionRect = new Rect(x, y, w, h);
            dc.DrawRectangle(WalkableFillBrush, WalkablePen, selectionRect);

            // Draw size label
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int tileCount = width * height;

            var typeface = new Typeface("Segoe UI");
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Title
            string titleText = "WALKABLE";
            var titleFt = new FormattedText(titleText, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 14, Brushes.Orange, ppd);
            titleFt.SetFontWeight(FontWeights.Bold);
            dc.DrawText(titleFt, new Point(x + 4, y - 40));

            // Size label
            string sizeText = $"{width}-{height} ({tileCount} tiles)";
            var sizeFt = new FormattedText(sizeText, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 12, Brushes.Yellow, ppd);
            dc.DrawText(sizeFt, new Point(x + 4, y - 20));

            // Draw corner markers
            var cornerPen = new Pen(Brushes.Orange, 2);
            double markerSize = 8;

            // Top-left
            dc.DrawLine(cornerPen, new Point(x, y), new Point(x + markerSize, y));
            dc.DrawLine(cornerPen, new Point(x, y), new Point(x, y + markerSize));

            // Top-right
            dc.DrawLine(cornerPen, new Point(x + w, y), new Point(x + w - markerSize, y));
            dc.DrawLine(cornerPen, new Point(x + w, y), new Point(x + w, y + markerSize));

            // Bottom-left
            dc.DrawLine(cornerPen, new Point(x, y + h), new Point(x + markerSize, y + h));
            dc.DrawLine(cornerPen, new Point(x, y + h), new Point(x, y + h - markerSize));

            // Bottom-right
            dc.DrawLine(cornerPen, new Point(x + w, y + h), new Point(x + w - markerSize, y + h));
            dc.DrawLine(cornerPen, new Point(x + w, y + h), new Point(x + w, y + h - markerSize));
        }

        /// <summary>
        /// Draw the rectangle selection box during drag operation.
        /// </summary>
        private void DrawSelectionRectangle(DrawingContext dc)
        {
            var rect = _vm!.GetAltitudeSelectionRect();
            if (!rect.HasValue) return;

            int minX = rect.Value.MinX;
            int minY = rect.Value.MinY;
            int maxX = rect.Value.MaxX;
            int maxY = rect.Value.MaxY;

            // Convert to pixel coordinates
            double x = minX * MapConstants.TileSize;
            double y = minY * MapConstants.TileSize;
            double w = (maxX - minX + 1) * MapConstants.TileSize;
            double h = (maxY - minY + 1) * MapConstants.TileSize;

            var selectionRect = new Rect(x, y, w, h);
            dc.DrawRectangle(SelectionFillBrush, SelectionPen, selectionRect);

            // Draw altitude values for each cell in the selection (not for PAP flags tool)
            if (_altitude != null && _vm.SelectedTool != EditorTool.AreaSetPapFlags)
            {
                DrawAltitudeValues(dc, minX, minY, maxX, maxY);
            }

            // Draw selection info
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int tileCount = width * height;

            var typeface = new Typeface("Segoe UI");
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Target altitude label
            if (_vm.SelectedTool == EditorTool.SetAltitude)
            {
                string targetText = $"Set: {_vm.TargetAltitudeDisplay}{(HeightSettings.ShowRawHeights ? "" : " QS")}";
                var ft = new FormattedText(targetText, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, 14, Brushes.Lime, ppd);
                dc.DrawText(ft, new Point(x + 4, y - 20));
            }
            else if (_vm.SelectedTool == EditorTool.ResetAltitude)
            {
                string clearText = "Clear";
                var ft = new FormattedText(clearText, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, 14, Brushes.Orange, ppd);
                dc.DrawText(ft, new Point(x + 4, y - 20));
            }
            else if (_vm.SelectedTool == EditorTool.AreaSetPapFlags)
            {
                string papText;
                Brush papColour;
                if (_vm.PapFlagsSetMask == 0 && _vm.PapFlagsClearMask == 0)
                {
                    papText = "(no change)";
                    papColour = Brushes.Gray;
                }
                else if (_vm.PapFlagsClearMask == 0)
                {
                    papText = $"+0x{_vm.PapFlagsSetMask:X4}";
                    papColour = Brushes.Lime;
                }
                else if (_vm.PapFlagsSetMask == 0)
                {
                    papText = $"-0x{_vm.PapFlagsClearMask:X4}";
                    papColour = Brushes.Orange;
                }
                else
                {
                    papText = $"+0x{_vm.PapFlagsSetMask:X4} -0x{_vm.PapFlagsClearMask:X4}";
                    papColour = Brushes.Cyan;
                }
                var ft = new FormattedText(papText, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, 14, papColour, ppd);
                dc.DrawText(ft, new Point(x + 4, y - 20));
            }

            // Size label
            string sizeText = $"{width}-{height} ({tileCount})";
            var sizeFt = new FormattedText(sizeText, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 12, Brushes.Yellow, ppd);
            dc.DrawText(sizeFt, new Point(x + 4, y - 38));
        }

        /// <summary>
        /// Draw hover highlight when not in selection mode.
        /// </summary>
        private void DrawHoverHighlight(DrawingContext dc)
        {
            var (tx, ty) = GetHoveredTile();
            if (tx < 0 || ty < 0) return;

            // Single tile highlight (no brush for altitude tools to keep it simple)
            double x = tx * MapConstants.TileSize;
            double y = ty * MapConstants.TileSize;
            double size = MapConstants.TileSize;

            var rect = new Rect(x, y, size, size);
            dc.DrawRectangle(HoverFillBrush, HoverPen, rect);

            // Draw current altitude value
            if (_altitude != null)
            {
                DrawAltitudeValues(dc, tx, ty, tx, ty);
            }

            // Draw target altitude indicator
            if (_vm!.SelectedTool == EditorTool.SetAltitude)
            {
                string targetText = $"Target: {_vm.TargetAltitudeDisplay}{(HeightSettings.ShowRawHeights ? "" : " QS")}";
                var ft = new FormattedText(targetText, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Lime,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(ft, new Point(x + 4, y - 16));
            }
        }

        /// <summary>
        /// Draw altitude values for tiles in the specified range.
        /// </summary>
        private void DrawAltitudeValues(DrawingContext dc, int minX, int minY, int maxX, int maxY)
        {
            var typeface = new Typeface("Segoe UI");
            double fontSize = 10;
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            for (int cty = minY; cty <= maxY; cty++)
            {
                for (int ctx = minX; ctx <= maxX; ctx++)
                {
                    if (ctx < 0 || ctx >= MapConstants.TilesPerSide ||
                        cty < 0 || cty >= MapConstants.TilesPerSide)
                        continue;

                    try
                    {
                        int alt = _altitude!.ReadWorldAltitude(ctx, cty);
                        string text = (HeightSettings.ShowRawHeights ? alt : alt / 8).ToString();

                        var ft = new FormattedText(text, CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, typeface, fontSize, TextBrush, ppd);

                        double cx = ctx * MapConstants.TileSize + (MapConstants.TileSize - ft.Width) / 2;
                        double cy = cty * MapConstants.TileSize + (MapConstants.TileSize - ft.Height) / 2;

                        // Draw shadow
                        dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, typeface, fontSize, TextShadowBrush, ppd),
                            new Point(cx + 1, cy + 1));

                        // Draw text
                        dc.DrawText(ft, new Point(cx, cy));
                    }
                    catch
                    {
                        // Ignore read errors
                    }
                }
            }
        }

        private static bool IsAltitudeTool(EditorTool t) =>
            t == EditorTool.SetAltitude ||
            t == EditorTool.SampleAltitude ||
            t == EditorTool.ResetAltitude ||
            t == EditorTool.DetectRoof ||
            t == EditorTool.AreaSetPapFlags;

        /// <summary>
        /// Returns the hovered tile indices (tx, ty) in tile coords (0..127), or (-1,-1) when no hover.
        /// </summary>
        private (int tx, int ty) GetHoveredTile()
        {
            if (_vm == null || !IsAltitudeTool(_vm.SelectedTool))
                return (-1, -1);

            // Convert game?UI (origin flip like other overlays)
            double uiX = MapConstants.MapPixels - _vm.CursorX;
            double uiZ = MapConstants.MapPixels - _vm.CursorZ;

            // Get tile at cursor position
            int tx = (int)Math.Floor(uiX / MapConstants.TileSize);
            int ty = (int)Math.Floor(uiZ / MapConstants.TileSize);

            if (tx < 0 || tx >= MapConstants.TilesPerSide ||
                ty < 0 || ty >= MapConstants.TilesPerSide)
            {
                return (-1, -1);
            }

            return (tx, ty);
        }
    }
}