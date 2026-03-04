// /Views/MapOverlays/GhostTexturePreviewLayer.cs

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Services;
using UrbanChaosEditor.Shared.Services.Textures;
using UrbanChaosMapEditor.ViewModels;
using static UrbanChaosMapEditor.Services.TexturesAccessor;

namespace UrbanChaosMapEditor.Views.MapOverlays
{
    public sealed class GhostTexturePreviewLayer : FrameworkElement
    {
        private MapViewModel? _vm;

        // Selection rectangle border
        private static readonly Brush SelectionStrokeBrush = new SolidColorBrush(Color.FromArgb(255, 50, 150, 255)); // bright blue
        private static readonly Pen SelectionPen = new Pen(SelectionStrokeBrush, 3);

        // Brush outline (for hover preview)
        private static readonly Brush BrushOutlineBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 0)); // yellow
        private static readonly Pen BrushOutlinePen = new Pen(BrushOutlineBrush, 2);

        static GhostTexturePreviewLayer()
        {
            SelectionStrokeBrush.Freeze();
            SelectionPen.Freeze();
            BrushOutlineBrush.Freeze();
            BrushOutlinePen.Freeze();
        }

        public GhostTexturePreviewLayer()
        {
            Width = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = false;

            // repaint when textures change globally
            TextureCacheService.Instance.Completed += (_, __) => Dispatcher.Invoke(InvalidateVisual);
            TexturesChangeBus.Instance.Changed += (_, __) => Dispatcher.Invoke(InvalidateVisual);

            DataContextChanged += (_, __) => HookVm();
        }

        private void HookVm()
        {
            if (_vm is not null)
                _vm.PropertyChanged -= OnVmChanged;

            _vm = DataContext as MapViewModel;

            if (_vm is not null)
                _vm.PropertyChanged += OnVmChanged;

            InvalidateVisual();
        }

        private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapViewModel.SelectedTool) ||
                e.PropertyName == nameof(MapViewModel.SelectedTextureGroup) ||
                e.PropertyName == nameof(MapViewModel.SelectedTextureNumber) ||
                e.PropertyName == nameof(MapViewModel.SelectedRotationIndex) ||
                e.PropertyName == nameof(MapViewModel.TextureWorld) ||
                e.PropertyName == nameof(MapViewModel.UseBetaTextures) ||
                e.PropertyName == nameof(MapViewModel.BrushSize) ||
                // Rectangle selection properties
                e.PropertyName == nameof(MapViewModel.IsPaintingTexture) ||
                e.PropertyName == nameof(MapViewModel.TextureSelectionStartX) ||
                e.PropertyName == nameof(MapViewModel.TextureSelectionStartY) ||
                e.PropertyName == nameof(MapViewModel.TextureSelectionEndX) ||
                e.PropertyName == nameof(MapViewModel.TextureSelectionEndY))
            {
                Dispatcher.Invoke(InvalidateVisual);
            }
        }

        private (int tx, int ty)? _hoverTile;   // cursor tile position
        public void SetHoverTile(int? tx, int? ty)
        {
            _hoverTile = (tx.HasValue && ty.HasValue) ? (tx.Value, ty.Value) : ((int, int)?)null;
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            if (_vm is null || _vm.SelectedTool != EditorTool.PaintTexture) return;
            if (_vm.SelectedTextureNumber <= 0) return;

            var cache = TextureCacheService.Instance;

            string relKey = _vm.SelectedTextureGroup switch
            {
                TextureGroup.World => $"world{_vm.TextureWorld}_{_vm.SelectedTextureNumber:000}",
                TextureGroup.Shared => $"shared_{_vm.SelectedTextureNumber:000}",
                TextureGroup.Prims => $"shared_prims_{_vm.SelectedTextureNumber:000}",
                _ => ""
            };
            if (string.IsNullOrEmpty(relKey)) return;

            if (!cache.TryGetRelative(relKey, out var bmp) || bmp is null) return;

            int rotIndex = ((_vm.SelectedRotationIndex % 4) + 4) % 4;
            double angle = rotIndex switch { 0 => 180, 1 => 90, 2 => 0, 3 => 270, _ => 0 };

            // Check if we're in rectangle selection mode
            if (_vm.IsPaintingTexture)
            {
                var rect = _vm.GetTextureSelectionRect();
                if (rect.HasValue)
                {
                    DrawSelectionPreview(dc, bmp, angle, rect.Value);
                    return;
                }
            }

            // Normal hover preview (brush centered on cursor)
            if (_hoverTile is null) return;

            int brushSize = Math.Max(1, _vm.BrushSize);
            int cursorTx = _hoverTile.Value.tx;
            int cursorTy = _hoverTile.Value.ty;

            // Calculate brush bounds CENTERED on cursor
            int half = (brushSize - 1) / 2;
            int startTx = cursorTx - half;
            int startTy = cursorTy - half;

            // Clamp to valid tile range
            int endTx = startTx + brushSize - 1;
            int endTy = startTy + brushSize - 1;

            // Adjust if we go out of bounds
            if (startTx < 0) startTx = 0;
            if (startTy < 0) startTy = 0;
            if (endTx >= MapConstants.TilesPerSide) endTx = MapConstants.TilesPerSide - 1;
            if (endTy >= MapConstants.TilesPerSide) endTy = MapConstants.TilesPerSide - 1;

            int width = endTx - startTx + 1;
            int height = endTy - startTy + 1;

            if (width > 0 && height > 0)
            {
                DrawTexturePreview(dc, bmp, angle, startTx, startTy, width, height);

                // Draw brush outline
                double x = startTx * MapConstants.TileSize;
                double y = startTy * MapConstants.TileSize;
                double w = width * MapConstants.TileSize;
                double h = height * MapConstants.TileSize;
                dc.DrawRectangle(null, BrushOutlinePen, new Rect(x, y, w, h));

                // Show brush size label if > 1
                if (brushSize > 1)
                {
                    DrawBrushLabel(dc, x, y, brushSize);
                }
            }
        }

        /// <summary>
        /// Draw texture preview for rectangle selection mode.
        /// </summary>
        private void DrawSelectionPreview(DrawingContext dc, ImageSource bmp, double angle,
            (int MinX, int MinY, int MaxX, int MaxY) rect)
        {
            int width = rect.MaxX - rect.MinX + 1;
            int height = rect.MaxY - rect.MinY + 1;

            // Draw texture preview across entire selection
            DrawTexturePreview(dc, bmp, angle, rect.MinX, rect.MinY, width, height);

            // Draw selection border
            double x = rect.MinX * MapConstants.TileSize;
            double y = rect.MinY * MapConstants.TileSize;
            double w = width * MapConstants.TileSize;
            double h = height * MapConstants.TileSize;
            dc.DrawRectangle(null, SelectionPen, new Rect(x, y, w, h));

            // Draw size label
            DrawSelectionLabel(dc, x, y, width, height);
        }

        /// <summary>
        /// Draw texture preview for a rectangular area.
        /// </summary>
        private void DrawTexturePreview(DrawingContext dc, ImageSource bmp, double angle,
            int startX, int startY, int width, int height)
        {
            dc.PushOpacity(0.55);
            for (int dy = 0; dy < height; dy++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    int tx = startX + dx;
                    int ty = startY + dy;
                    if (tx < 0 || tx >= MapConstants.TilesPerSide ||
                        ty < 0 || ty >= MapConstants.TilesPerSide) continue;

                    double x = tx * MapConstants.TileSize;
                    double y = ty * MapConstants.TileSize;
                    var rect = new Rect(x, y, MapConstants.TileSize, MapConstants.TileSize);

                    if (angle == 0)
                    {
                        dc.DrawImage(bmp, rect);
                    }
                    else
                    {
                        dc.PushTransform(new RotateTransform(angle, x + rect.Width / 2.0, y + rect.Height / 2.0));
                        dc.DrawImage(bmp, rect);
                        dc.Pop();
                    }
                }
            }
            dc.Pop();
        }

        /// <summary>
        /// Draw size label above selection rectangle.
        /// </summary>
        private void DrawSelectionLabel(DrawingContext dc, double x, double y, int width, int height)
        {
            int tileCount = width * height;
            string sizeText = $"{width}×{height} ({tileCount})";

            var typeface = new Typeface("Segoe UI");
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            var ft = new FormattedText(sizeText, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 12, Brushes.Yellow, ppd);

            // Draw with shadow for visibility
            dc.DrawText(new FormattedText(sizeText, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 12, Brushes.Black, ppd),
                new Point(x + 5, y - 17));
            dc.DrawText(ft, new Point(x + 4, y - 18));
        }

        /// <summary>
        /// Draw brush size label above brush preview.
        /// </summary>
        private void DrawBrushLabel(DrawingContext dc, double x, double y, int brushSize)
        {
            string sizeText = $"{brushSize}×{brushSize}";

            var typeface = new Typeface("Segoe UI");
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            var ft = new FormattedText(sizeText, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 11, Brushes.Yellow, ppd);

            // Draw with shadow for visibility
            dc.DrawText(new FormattedText(sizeText, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 11, Brushes.Black, ppd),
                new Point(x + 3, y - 15));
            dc.DrawText(ft, new Point(x + 2, y - 16));
        }
    }
}