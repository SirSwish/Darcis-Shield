// /Views/MapOverlays/GhostTexturePreviewLayer.cs

using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using UrbanChaosEditor.Shared.Services.Textures;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Models.Textures;
using UrbanChaosMapEditor.Services.Textures;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Textures.MapOverlays
{
    public sealed class GhostTexturePreviewLayer : FrameworkElement
    {
        private MapViewModel? _vm;

        // Selection rectangle border (texture drag-paint)
        private static readonly Brush SelectionStrokeBrush = new SolidColorBrush(Color.FromArgb(255, 50, 150, 255));
        private static readonly Pen SelectionPen = new Pen(SelectionStrokeBrush, 3);

        // Brush outline (for hover preview)
        private static readonly Brush BrushOutlineBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 0));
        private static readonly Pen BrushOutlinePen = new Pen(BrushOutlineBrush, 2);

        // Texture area selection (SelectTextureArea tool)
        private static readonly Brush AreaSelectionFill = new SolidColorBrush(Color.FromArgb(50, 0, 200, 255));
        private static readonly Brush AreaSelectionStroke = new SolidColorBrush(Color.FromArgb(255, 0, 220, 255));
        private static readonly Pen AreaSelectionPen = new Pen(AreaSelectionStroke, 2) { DashStyle = DashStyles.Dash };

        // Paste ghost border
        private static readonly Brush PasteBorderBrush = new SolidColorBrush(Color.FromArgb(255, 80, 255, 80));
        private static readonly Pen PasteBorderPen = new Pen(PasteBorderBrush, 2);

        static GhostTexturePreviewLayer()
        {
            SelectionStrokeBrush.Freeze();
            SelectionPen.Freeze();
            BrushOutlineBrush.Freeze();
            BrushOutlinePen.Freeze();
            AreaSelectionFill.Freeze();
            AreaSelectionStroke.Freeze();
            AreaSelectionPen.Freeze();
            PasteBorderBrush.Freeze();
            PasteBorderPen.Freeze();
        }

        public GhostTexturePreviewLayer()
        {
            Width = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = false;

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
                e.PropertyName == nameof(MapViewModel.IsPaintingTexture) ||
                e.PropertyName == nameof(MapViewModel.TextureSelectionStartX) ||
                e.PropertyName == nameof(MapViewModel.TextureSelectionStartY) ||
                e.PropertyName == nameof(MapViewModel.TextureSelectionEndX) ||
                e.PropertyName == nameof(MapViewModel.TextureSelectionEndY) ||
                // Texture area selection
                e.PropertyName == nameof(MapViewModel.IsSelectingTextureArea) ||
                e.PropertyName == nameof(MapViewModel.TextureAreaCommitted) ||
                e.PropertyName == nameof(MapViewModel.TexAreaStartX) ||
                e.PropertyName == nameof(MapViewModel.TexAreaStartY) ||
                e.PropertyName == nameof(MapViewModel.TexAreaEndX) ||
                e.PropertyName == nameof(MapViewModel.TexAreaEndY) ||
                e.PropertyName == nameof(MapViewModel.TextureClipboard))
            {
                Dispatcher.Invoke(InvalidateVisual);
            }
        }

        private (int tx, int ty)? _hoverTile;
        public void SetHoverTile(int? tx, int? ty)
        {
            _hoverTile = (tx.HasValue && ty.HasValue) ? (tx.Value, ty.Value) : ((int, int)?)null;
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            if (_vm is null) return;

            // ── Texture area selection highlight ──────────────────────────────
            if (_vm.SelectedTool == EditorTool.SelectTextureArea && _vm.IsSelectingTextureArea)
            {
                var areaRect = _vm.GetTextureAreaRect();
                if (areaRect.HasValue)
                    DrawAreaSelection(dc, areaRect.Value);
                return;
            }

            // ── Paste ghost ───────────────────────────────────────────────────
            if (_vm.SelectedTool == EditorTool.PasteTexture && _vm.TextureClipboard != null && _hoverTile.HasValue)
            {
                DrawPasteGhost(dc, _hoverTile.Value.tx, _hoverTile.Value.ty);
                return;
            }

            if (_vm.SelectedTool != EditorTool.PaintTexture &&
                _vm.SelectedTool != EditorTool.PaintRoofTexture) return;
            if (_vm.SelectedTextureNumber <= 0) return;

            var cache = TextureCacheService.Instance;

            string relKey = _vm.SelectedTextureGroup switch
            {
                TexturesAccessor.TextureGroup.World  => $"world{_vm.TextureWorld}_{_vm.SelectedTextureNumber:000}",
                TexturesAccessor.TextureGroup.Shared => $"shared_{_vm.SelectedTextureNumber:000}",
                TexturesAccessor.TextureGroup.Prims  => $"shared_prims_{_vm.SelectedTextureNumber:000}",
                _ => ""
            };
            if (string.IsNullOrEmpty(relKey)) return;
            if (!cache.TryGetRelative(relKey, out var bmp) || bmp is null) return;

            int rotIndex = ((_vm.SelectedRotationIndex % 4) + 4) % 4;
            double angle = rotIndex switch { 0 => 180, 1 => 90, 2 => 0, 3 => 270, _ => 0 };

            if (_vm.IsPaintingTexture)
            {
                var rect = _vm.GetTextureSelectionRect();
                if (rect.HasValue)
                {
                    DrawSelectionPreview(dc, bmp, angle, rect.Value);
                    return;
                }
            }

            if (_hoverTile is null) return;

            int brushSize = Math.Max(1, _vm.BrushSize);
            int cursorTx = _hoverTile.Value.tx;
            int cursorTy = _hoverTile.Value.ty;

            int half = (brushSize - 1) / 2;
            int startTx = cursorTx - half;
            int startTy = cursorTy - half;
            int endTx = startTx + brushSize - 1;
            int endTy = startTy + brushSize - 1;

            if (startTx < 0) startTx = 0;
            if (startTy < 0) startTy = 0;
            if (endTx >= MapConstants.TilesPerSide) endTx = MapConstants.TilesPerSide - 1;
            if (endTy >= MapConstants.TilesPerSide) endTy = MapConstants.TilesPerSide - 1;

            int width = endTx - startTx + 1;
            int height = endTy - startTy + 1;

            if (width > 0 && height > 0)
            {
                DrawTexturePreview(dc, bmp, angle, startTx, startTy, width, height);

                double x = startTx * MapConstants.TileSize;
                double y = startTy * MapConstants.TileSize;
                double w = width * MapConstants.TileSize;
                double h = height * MapConstants.TileSize;
                dc.DrawRectangle(null, BrushOutlinePen, new Rect(x, y, w, h));

                if (brushSize > 1)
                    DrawBrushLabel(dc, x, y, brushSize);
            }
        }

        private void DrawSelectionPreview(DrawingContext dc, ImageSource bmp, double angle,
            (int MinX, int MinY, int MaxX, int MaxY) rect)
        {
            int width = rect.MaxX - rect.MinX + 1;
            int height = rect.MaxY - rect.MinY + 1;

            DrawTexturePreview(dc, bmp, angle, rect.MinX, rect.MinY, width, height);

            double x = rect.MinX * MapConstants.TileSize;
            double y = rect.MinY * MapConstants.TileSize;
            double w = width * MapConstants.TileSize;
            double h = height * MapConstants.TileSize;
            dc.DrawRectangle(null, SelectionPen, new Rect(x, y, w, h));

            DrawSelectionLabel(dc, x, y, width, height);
        }

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

        private void DrawSelectionLabel(DrawingContext dc, double x, double y, int width, int height)
        {
            int tileCount = width * height;
            string sizeText = $"{width}\u00d7{height} ({tileCount})";

            var typeface = new Typeface("Segoe UI");
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            var ft = new FormattedText(sizeText, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 12, Brushes.Yellow, ppd);

            dc.DrawText(new FormattedText(sizeText, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 12, Brushes.Black, ppd),
                new Point(x + 5, y - 17));
            dc.DrawText(ft, new Point(x + 4, y - 18));
        }

        private void DrawBrushLabel(DrawingContext dc, double x, double y, int brushSize)
        {
            string sizeText = $"{brushSize}\u00d7{brushSize}";

            var typeface = new Typeface("Segoe UI");
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            var ft = new FormattedText(sizeText, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 11, Brushes.Yellow, ppd);

            dc.DrawText(new FormattedText(sizeText, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 11, Brushes.Black, ppd),
                new Point(x + 3, y - 15));
            dc.DrawText(ft, new Point(x + 2, y - 16));
        }

        // ── Texture area selection highlight ──────────────────────────────────
        private void DrawAreaSelection(DrawingContext dc, (int MinX, int MinY, int MaxX, int MaxY) rect)
        {
            double x = rect.MinX * MapConstants.TileSize;
            double y = rect.MinY * MapConstants.TileSize;
            double w = (rect.MaxX - rect.MinX + 1) * MapConstants.TileSize;
            double h = (rect.MaxY - rect.MinY + 1) * MapConstants.TileSize;
            var r = new Rect(x, y, w, h);

            dc.DrawRectangle(AreaSelectionFill, AreaSelectionPen, r);
            DrawSelectionLabel(dc, x, y, rect.MaxX - rect.MinX + 1, rect.MaxY - rect.MinY + 1);
        }

        // ── Paste ghost ───────────────────────────────────────────────────────
        private void DrawPasteGhost(DrawingContext dc, int startTx, int startTy)
        {
            var cb = _vm?.TextureClipboard;
            if (cb == null) return;

            var cache = TextureCacheService.Instance;
            int world = _vm!.TextureWorld;

            dc.PushOpacity(0.55);
            for (int row = 0; row < cb.Height; row++)
            {
                for (int col = 0; col < cb.Width; col++)
                {
                    int tx = startTx + col;
                    int ty = startTy + row;
                    if (tx < 0 || tx >= MapConstants.TilesPerSide ||
                        ty < 0 || ty >= MapConstants.TilesPerSide) continue;

                    var entry = cb.GetCell(col, row);
                    string relKey = entry.Group switch
                    {
                        TexturesAccessor.TextureGroup.World  => $"world{world}_{entry.TextureNumber:000}",
                        TexturesAccessor.TextureGroup.Shared => $"shared_{entry.TextureNumber:000}",
                        TexturesAccessor.TextureGroup.Prims  => $"shared_prims_{entry.TextureNumber:000}",
                        _ => ""
                    };
                    if (string.IsNullOrEmpty(relKey)) continue;
                    if (!cache.TryGetRelative(relKey, out var bmp) || bmp is null) continue;

                    double angle = entry.RotationIndex switch { 0 => 180, 1 => 90, 2 => 0, 3 => 270, _ => 0 };
                    double px = tx * MapConstants.TileSize;
                    double py = ty * MapConstants.TileSize;
                    var tileRect = new Rect(px, py, MapConstants.TileSize, MapConstants.TileSize);

                    if (angle == 0)
                    {
                        dc.DrawImage(bmp, tileRect);
                    }
                    else
                    {
                        dc.PushTransform(new RotateTransform(angle, px + tileRect.Width / 2.0, py + tileRect.Height / 2.0));
                        dc.DrawImage(bmp, tileRect);
                        dc.Pop();
                    }
                }
            }
            dc.Pop();

            // Green border around paste footprint
            double bx = startTx * MapConstants.TileSize;
            double by = startTy * MapConstants.TileSize;
            double bw = cb.Width  * MapConstants.TileSize;
            double bh = cb.Height * MapConstants.TileSize;
            dc.DrawRectangle(null, PasteBorderPen, new Rect(bx, by, bw, bh));
        }
    }
}
