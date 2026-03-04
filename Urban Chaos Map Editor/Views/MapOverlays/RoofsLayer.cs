// /Views/MapOverlays/RoofsLayer.cs
// Visualizes RoofFace4 entries for the SELECTED building only
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;

namespace UrbanChaosMapEditor.Views.MapOverlays
{
    /// <summary>
    /// Visualizes RoofFace4 tiles for the selected building only.
    /// </summary>
    public sealed class RoofsLayer : Canvas
    {
        private static readonly Brush TileFillBrush;
        private static readonly Pen TileBorderPen;
        private static readonly Brush InputBgBrush;
        private static readonly Brush InputBgHoverBrush;
        private static readonly Pen InputBorderPen;
        private static readonly Pen InputBorderHoverPen;
        private static readonly Typeface LabelTypeface;

        private TextBox? _editBox;
        private int _editingRf4Idx;
        private EditField _editingField;
        private int _hoverRf4Idx = -1;
        private EditField _hoverField = EditField.None;

        // Cached data for fast hit testing
        private record struct TileInfo(int Rf4Idx, double UiX, double UiZ, short Y, sbyte DY0, sbyte DY1, sbyte DY2);
        private List<TileInfo>? _cachedTiles;
        private int _cacheWalkablesStart;
        private int _cacheNextRf4;
        private int _cachedBuildingId = -1;

        private enum EditField { None, Y, DY0, DY1, DY2 }

        #region SelectedBuildingId Dependency Property

        public static readonly DependencyProperty SelectedBuildingIdProperty =
            DependencyProperty.Register(
                nameof(SelectedBuildingId),
                typeof(int),
                typeof(RoofsLayer),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedBuildingChanged));

        public int SelectedBuildingId
        {
            get => (int)GetValue(SelectedBuildingIdProperty);
            set => SetValue(SelectedBuildingIdProperty, value);
        }

        private static void OnSelectedBuildingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoofsLayer layer)
            {
                layer._cachedTiles = null; // Invalidate cache
                layer.CancelEdit();
                layer.InvalidateVisual();
            }
        }

        #endregion

        static RoofsLayer()
        {
            TileFillBrush = new SolidColorBrush(Color.FromArgb(50, 0, 200, 200));
            TileFillBrush.Freeze();

            TileBorderPen = new Pen(Brushes.Cyan, 2.0);
            TileBorderPen.Freeze();

            InputBgBrush = new SolidColorBrush(Color.FromRgb(25, 25, 30));
            InputBgBrush.Freeze();

            InputBgHoverBrush = new SolidColorBrush(Color.FromRgb(45, 45, 55));
            InputBgHoverBrush.Freeze();

            InputBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 90)), 1);
            InputBorderPen.Freeze();

            InputBorderHoverPen = new Pen(Brushes.Yellow, 2);
            InputBorderHoverPen.Freeze();

            LabelTypeface = new Typeface("Consolas");
        }

        public RoofsLayer()
        {
            Width = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            ClipToBounds = true;
            Background = Brushes.Transparent;

            MapDataService.Instance.MapLoaded += (s, e) => RefreshOnUiThread();
            MapDataService.Instance.MapCleared += (s, e) => RefreshOnUiThread();
            MapDataService.Instance.MapSaved += (s, e) => RefreshOnUiThread();
            BuildingsChangeBus.Instance.Changed += (s, e) => RefreshOnUiThread();

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeave += OnMouseLeave;
        }

        private void RefreshOnUiThread()
        {
            Dispatcher.BeginInvoke(() =>
            {
                CancelEdit();
                _cachedTiles = null;
                InvalidateVisual();
            }, DispatcherPriority.Render);
        }

        private void RebuildCache()
        {
            _cachedTiles = new List<TileInfo>();
            _cachedBuildingId = SelectedBuildingId;

            var svc = MapDataService.Instance;
            if (!svc.IsLoaded) return;

            // No building selected = nothing to show
            if (_cachedBuildingId < 1) return;

            var snap = new BuildingsAccessor(svc).ReadSnapshot();
            if (snap.Walkables == null || snap.RoofFaces4 == null) return;

            _cacheWalkablesStart = snap.WalkablesStart;
            _cacheNextRf4 = snap.NextDWalkable;

            // Only process walkables that belong to the selected building
            for (int wIdx = 1; wIdx < snap.Walkables.Length; wIdx++)
            {
                var w = snap.Walkables[wIdx];
                if (w.Building != _cachedBuildingId) continue; // Filter by selected building

                for (int i = w.StartFace4; i < w.EndFace4 && i < snap.RoofFaces4.Length; i++)
                {
                    if (i < 1) continue;
                    var rf4 = snap.RoofFaces4[i];

                    // RF4 coordinates are ABSOLUTE:
                    // RX = absolute tile X
                    // RZ = absolute tile Z + 128
                    int tileX = rf4.RX;
                    int tileZ = rf4.RZ - 128;

                    // Skip invalid entries
                    if (tileX < 0 || tileX > 127 || tileZ < 0 || tileZ > 127)
                        continue;

                    double uiX = (128 - tileX - 1) * 64.0;
                    double uiZ = (128 - tileZ - 1) * 64.0;

                    _cachedTiles.Add(new TileInfo(i, uiX, uiZ, rf4.Y, rf4.DY0, rf4.DY1, rf4.DY2));
                }
            }
        }

        protected override Size MeasureOverride(Size s) => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override Size ArrangeOverride(Size s)
        {
            foreach (UIElement child in Children)
            {
                double x = GetLeft(child); if (double.IsNaN(x)) x = 0;
                double y = GetTop(child); if (double.IsNaN(y)) y = 0;
                child.Arrange(new Rect(new Point(x, y), child.DesiredSize));
            }
            return new Size(MapConstants.MapPixels, MapConstants.MapPixels);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // Rebuild cache if needed or if building selection changed
            if (_cachedTiles == null || _cachedBuildingId != SelectedBuildingId)
                RebuildCache();

            if (_cachedTiles == null || _cachedTiles.Count == 0) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (var tile in _cachedTiles)
            {
                DrawTile(dc, tile, dpi);
            }
        }

        private void DrawTile(DrawingContext dc, TileInfo tile, double dpi)
        {
            double uiX = tile.UiX;
            double uiZ = tile.UiZ;

            // Tile background
            dc.DrawRectangle(TileFillBrush, TileBorderPen, new Rect(uiX, uiZ, 64, 64));

            // Define input field rectangles
            var yRect = new Rect(uiX + 12, uiZ + 24, 40, 16);
            var dy0Rect = new Rect(uiX + 2, uiZ + 2, 24, 14);
            var dy1Rect = new Rect(uiX + 38, uiZ + 2, 24, 14);
            var dy2Rect = new Rect(uiX + 38, uiZ + 48, 24, 14);
            var swRect = new Rect(uiX + 2, uiZ + 48, 24, 14);

            // Draw input fields with hover state
            DrawInputField(dc, yRect, $"{tile.Y}", tile.Rf4Idx, EditField.Y, dpi, Brushes.White);
            DrawInputField(dc, dy0Rect, $"{tile.DY0}", tile.Rf4Idx, EditField.DY0, dpi, GetDyColor(tile.DY0));
            DrawInputField(dc, dy1Rect, $"{tile.DY1}", tile.Rf4Idx, EditField.DY1, dpi, GetDyColor(tile.DY1));
            DrawInputField(dc, dy2Rect, $"{tile.DY2}", tile.Rf4Idx, EditField.DY2, dpi, GetDyColor(tile.DY2));

            // SW is read-only
            DrawReadOnlyValue(dc, swRect, "0", dpi);

            // Label
            DrawLabel(dc, "Y:", uiX + 4, uiZ + 26, 8, dpi);
        }

        private void DrawInputField(DrawingContext dc, Rect rect, string value, int rf4Idx, EditField field, double dpi, Brush textBrush)
        {
            bool isHover = _hoverRf4Idx == rf4Idx && _hoverField == field;

            dc.DrawRectangle(
                isHover ? InputBgHoverBrush : InputBgBrush,
                isHover ? InputBorderHoverPen : InputBorderPen,
                rect);

            var ft = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                LabelTypeface, 9, textBrush, dpi);
            dc.DrawText(ft, new Point(rect.X + (rect.Width - ft.Width) / 2, rect.Y + (rect.Height - ft.Height) / 2));
        }

        private void DrawReadOnlyValue(DrawingContext dc, Rect rect, string value, double dpi)
        {
            var ft = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                LabelTypeface, 9, Brushes.DarkGray, dpi);
            dc.DrawText(ft, new Point(rect.X + (rect.Width - ft.Width) / 2, rect.Y + (rect.Height - ft.Height) / 2));
        }

        private void DrawLabel(DrawingContext dc, string text, double x, double y, double size, double dpi)
        {
            var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                LabelTypeface, size, Brushes.Gray, dpi);
            dc.DrawText(ft, new Point(x, y));
        }

        private static Brush GetDyColor(int dy) =>
            dy == 0 ? Brushes.Gray : (dy > 0 ? Brushes.LimeGreen : Brushes.OrangeRed);

        #region Mouse

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var hit = HitTestCached(e.GetPosition(this));
            int newIdx = hit?.idx ?? -1;
            EditField newField = hit?.field ?? EditField.None;

            if (newIdx != _hoverRf4Idx || newField != _hoverField)
            {
                _hoverRf4Idx = newIdx;
                _hoverField = newField;
                Cursor = newField != EditField.None ? Cursors.IBeam : Cursors.Arrow;
                InvalidateVisual();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoverRf4Idx != -1)
            {
                _hoverRf4Idx = -1;
                _hoverField = EditField.None;
                Cursor = Cursors.Arrow;
                InvalidateVisual();
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hit = HitTestCached(e.GetPosition(this));
            if (hit == null) { CancelEdit(); return; }
            StartEdit(hit.Value.idx, hit.Value.field, hit.Value.value, hit.Value.rect);
            e.Handled = true;
        }

        private (int idx, EditField field, int value, Rect rect)? HitTestCached(Point pos)
        {
            if (_cachedTiles == null || _cachedTiles.Count == 0) return null;

            foreach (var tile in _cachedTiles)
            {
                double uiX = tile.UiX;
                double uiZ = tile.UiZ;

                if (pos.X < uiX || pos.X > uiX + 64 || pos.Y < uiZ || pos.Y > uiZ + 64)
                    continue;

                var yRect = new Rect(uiX + 12, uiZ + 24, 40, 16);
                var dy0Rect = new Rect(uiX + 2, uiZ + 2, 24, 14);
                var dy1Rect = new Rect(uiX + 38, uiZ + 2, 24, 14);
                var dy2Rect = new Rect(uiX + 38, uiZ + 48, 24, 14);

                if (yRect.Contains(pos)) return (tile.Rf4Idx, EditField.Y, tile.Y, yRect);
                if (dy0Rect.Contains(pos)) return (tile.Rf4Idx, EditField.DY0, tile.DY0, dy0Rect);
                if (dy1Rect.Contains(pos)) return (tile.Rf4Idx, EditField.DY1, tile.DY1, dy1Rect);
                if (dy2Rect.Contains(pos)) return (tile.Rf4Idx, EditField.DY2, tile.DY2, dy2Rect);
            }
            return null;
        }

        #endregion

        #region Edit

        private void StartEdit(int idx, EditField field, int val, Rect rect)
        {
            CancelEdit();
            _editingRf4Idx = idx;
            _editingField = field;

            _editBox = new TextBox
            {
                Text = val.ToString(),
                Width = rect.Width + 4,
                Height = rect.Height + 4,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Padding = new Thickness(1),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Yellow,
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 25)),
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            SetLeft(_editBox, rect.X - 2);
            SetTop(_editBox, rect.Y - 2);

            _editBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
                else if (e.Key == Key.Escape) { CancelEdit(); e.Handled = true; }
            };
            _editBox.LostFocus += (s, e) => { if (_editBox != null) Commit(); };

            Children.Add(_editBox);
            _editBox.Focus();
            _editBox.SelectAll();
        }

        private void Commit()
        {
            if (_editBox == null || _editingField == EditField.None) return;

            string txt = _editBox.Text.Trim();

            if (_editingField == EditField.Y && short.TryParse(txt, out short y))
                WriteField(_editingRf4Idx, _editingField, y);
            else if (_editingField != EditField.Y && sbyte.TryParse(txt, out sbyte dy))
                WriteField(_editingRf4Idx, _editingField, dy);

            CancelEdit();
            // Cache will be invalidated by BuildingsChangeBus
        }

        private void WriteField(int idx, EditField field, int val)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded) return;

            if (_cachedTiles == null) return;

            int off = _cacheWalkablesStart + 4 + _cacheNextRf4 * 22 + idx * 10;
            var bytes = svc.GetBytesCopy();

            switch (field)
            {
                case EditField.Y:
                    bytes[off] = (byte)(val & 0xFF);
                    bytes[off + 1] = (byte)((val >> 8) & 0xFF);
                    break;
                case EditField.DY0: bytes[off + 2] = (byte)(sbyte)val; break;
                case EditField.DY1: bytes[off + 3] = (byte)(sbyte)val; break;
                case EditField.DY2: bytes[off + 4] = (byte)(sbyte)val; break;
            }

            svc.ReplaceBytes(bytes);
            BuildingsChangeBus.Instance.NotifyChanged();
        }

        private void CancelEdit()
        {
            if (_editBox != null) { Children.Remove(_editBox); _editBox = null; }
            _editingField = EditField.None;
        }

        #endregion
    }
}