using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.Views.Roofs.Dialogs;

namespace UrbanChaosMapEditor.Views.Roofs.MapOverlays
{
    /// <summary>
    /// Visualizes RoofFace4 tiles for the selected walkable only.
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
        private static readonly Brush SelectedTileFillBrush;
        private static readonly Pen SelectedTileBorderPen;
        private static readonly Brush SelectionFill;
        private static readonly Pen SelectionPen;
        private static readonly Pen DiagonalPen;

        private TextBox? _editBox;
        private int _editingRf4Idx;
        private EditField _editingField;
        private int _hoverRf4Idx = -1;
        private EditField _hoverField = EditField.None;

        // Tile-based selection state (col/row = floor(uiPixel / 64), matches texture drag approach)
        private bool _isDraggingTileSelection;
        private int _selStartCol = -1, _selStartRow = -1;
        private int _selHoverCol = -1, _selHoverRow = -1;

        private record struct TileInfo(int Rf4Idx, double UiX, double UiZ, short Y, sbyte DY0, sbyte DY1, sbyte DY2, bool DiagonalFlag);
        private List<TileInfo>? _cachedTiles;
        private int _cacheWalkablesStart;
        private int _cacheNextRf4;
        private int _cachedWalkableId1 = -1;

        private enum EditField { None, Y, DY0, DY1, DY2 }

        #region SelectedWalkableId1 Dependency Property

        public static readonly DependencyProperty SelectedWalkableId1Property =
            DependencyProperty.Register(
                nameof(SelectedWalkableId1),
                typeof(int),
                typeof(RoofsLayer),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedWalkableChanged));

        public int SelectedWalkableId1
        {
            get => (int)GetValue(SelectedWalkableId1Property);
            set => SetValue(SelectedWalkableId1Property, value);
        }

        private static void OnSelectedWalkableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoofsLayer layer)
            {
                layer._cachedTiles = null;
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

            SelectedTileFillBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 0));
            SelectedTileFillBrush.Freeze();

            SelectedTileBorderPen = new Pen(Brushes.Yellow, 3.0);
            SelectedTileBorderPen.Freeze();

            SelectionFill = new SolidColorBrush(Color.FromArgb(50, 0, 200, 255));
            SelectionFill.Freeze();

            var selStroke = new SolidColorBrush(Color.FromArgb(255, 0, 220, 255));
            selStroke.Freeze();
            SelectionPen = new Pen(selStroke, 2) { DashStyle = DashStyles.Dash };
            SelectionPen.Freeze();

            var diagStroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
            diagStroke.Freeze();
            DiagonalPen = new Pen(diagStroke, 1) { DashStyle = DashStyles.Dot };
            DiagonalPen.Freeze();
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
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;
            MouseLeave += OnMouseLeave;
            KeyDown += OnKeyDown;
            Focusable = true;

            var sel = RoofTileSelectionService.Instance;
            sel.SelectionBegan += (_, __) => Dispatcher.BeginInvoke(() => { Cursor = Cursors.Cross; InvalidateVisual(); });
            sel.SelectionEnded += (_, __) => Dispatcher.BeginInvoke(() =>
            {
                _isDraggingTileSelection = false;
                _selStartCol = _selStartRow = _selHoverCol = _selHoverRow = -1;
                Cursor = Cursors.Arrow;
                InvalidateVisual();
            });
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
            _cachedWalkableId1 = SelectedWalkableId1;

            var svc = MapDataService.Instance;
            if (!svc.IsLoaded) return;
            if (_cachedWalkableId1 < 1) return;

            var snap = new BuildingsAccessor(svc).ReadSnapshot();
            if (snap.Walkables == null || snap.RoofFaces4 == null) return;
            if (_cachedWalkableId1 >= snap.Walkables.Length) return;

            _cacheWalkablesStart = snap.WalkablesStart;
            _cacheNextRf4 = snap.NextDWalkable;

            var w = snap.Walkables[_cachedWalkableId1];

            for (int i = w.StartFace4; i < w.EndFace4 && i < snap.RoofFaces4.Length; i++)
            {
                if (i < 1) continue;
                var rf4 = snap.RoofFaces4[i];

                int tileX = rf4.RX & 0x7F;
                int tileZ = rf4.RZ - 128;

                if (tileX < 0 || tileX > 127 || tileZ < 0 || tileZ > 127)
                    continue;

                double uiX = (128 - tileX - 1) * 64.0;
                double uiZ = (128 - tileZ - 1) * 64.0;

                _cachedTiles.Add(new TileInfo(i, uiX, uiZ, rf4.Y, rf4.DY0, rf4.DY1, rf4.DY2, (rf4.RX & 0x80) != 0));
            }
        }

        /// <summary>
        /// Only claim the hit if the point lands on an actual tile, is in selection mode,
        /// or an inline edit box is open. Otherwise return null so the click falls through
        /// to the Prims layer (or whatever is underneath).
        /// </summary>
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            // Selection mode: need cursor tracking across the whole canvas.
            if (RoofTileSelectionService.Instance.IsSelecting)
                return new PointHitTestResult(this, hitTestParameters.HitPoint);

            // Inline edit active: keep focus on the canvas.
            if (_editBox != null)
                return new PointHitTestResult(this, hitTestParameters.HitPoint);

            // Only intercept if the point is over a real tile.
            if (HitTestTile(hitTestParameters.HitPoint) != null)
                return new PointHitTestResult(this, hitTestParameters.HitPoint);

            // Nothing here — pass through to layers below.
            return null!;
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

            if (_cachedTiles == null || _cachedWalkableId1 != SelectedWalkableId1)
                RebuildCache();

            if (_cachedTiles == null || _cachedTiles.Count == 0) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (var tile in _cachedTiles)
                DrawTile(dc, tile, dpi);

            if (RoofTileSelectionService.Instance.IsSelecting && _selHoverCol >= 0 && _cachedTiles != null)
            {
                int minCol = Math.Min(_selStartCol < 0 ? _selHoverCol : _selStartCol, _selHoverCol);
                int maxCol = Math.Max(_selStartCol < 0 ? _selHoverCol : _selStartCol, _selHoverCol);
                int minRow = Math.Min(_selStartRow < 0 ? _selHoverRow : _selStartRow, _selHoverRow);
                int maxRow = Math.Max(_selStartRow < 0 ? _selHoverRow : _selStartRow, _selHoverRow);

                foreach (var t in _cachedTiles)
                {
                    int col = (int)(t.UiX / 64);
                    int row = (int)(t.UiZ / 64);
                    if (col >= minCol && col <= maxCol && row >= minRow && row <= maxRow)
                        dc.DrawRectangle(SelectionFill, SelectionPen, new Rect(t.UiX, t.UiZ, 64, 64));
                }
            }
        }

        private void DrawTile(DrawingContext dc, TileInfo tile, double dpi)
        {
            double uiX = tile.UiX;
            double uiZ = tile.UiZ;

            // Tile background � yellow highlight if selected
            bool isSelected = tile.Rf4Idx == SelectedRf4Id;
            dc.DrawRectangle(
                isSelected ? SelectedTileFillBrush : TileFillBrush,
                isSelected ? SelectedTileBorderPen : TileBorderPen,
                new Rect(uiX, uiZ, 64, 64));

            // Diagonal line showing which edge is straight on sloped tiles.
            // DiagonalFlag (RX bit 7) set = NW-SE straight edge; clear = SW-NE straight edge.
            if (tile.DiagonalFlag)
                dc.DrawLine(DiagonalPen, new Point(uiX, uiZ), new Point(uiX + 64, uiZ + 64));       // NW → SE
            else
                dc.DrawLine(DiagonalPen, new Point(uiX, uiZ + 64), new Point(uiX + 64, uiZ));       // SW → NE

            // Y (base) shown SE corner; DY0 SW, DY1 NW, DY2 NE — original screen positions
            var yRect   = new Rect(uiX + 38, uiZ + 48, 24, 14);  // SE bottom-right = Y (base)
            var dy0Rect = new Rect(uiX + 2,  uiZ + 48, 24, 14);  // SW bottom-left  = DY0
            var dy1Rect = new Rect(uiX + 2,  uiZ + 2,  24, 14);  // NW top-left     = DY1
            var dy2Rect = new Rect(uiX + 38, uiZ + 2,  24, 14);  // NE top-right    = DY2

            DrawInputField(dc, yRect,   $"{tile.Y / 64}",   tile.Rf4Idx, EditField.Y,   dpi, Brushes.White);
            DrawInputField(dc, dy0Rect, $"{tile.DY0}px",    tile.Rf4Idx, EditField.DY0, dpi, GetDyColor(tile.DY0));
            DrawInputField(dc, dy1Rect, $"{tile.DY1}px",    tile.Rf4Idx, EditField.DY1, dpi, GetDyColor(tile.DY1));
            DrawInputField(dc, dy2Rect, $"{tile.DY2}px",    tile.Rf4Idx, EditField.DY2, dpi, GetDyColor(tile.DY2));
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
            if (RoofTileSelectionService.Instance.IsSelecting)
            {
                var pos = e.GetPosition(this);
                int col = (int)Math.Floor(pos.X / 64);
                int row = (int)Math.Floor(pos.Y / 64);
                Cursor = Cursors.Cross;
                if (col != _selHoverCol || row != _selHoverRow)
                {
                    _selHoverCol = col;
                    _selHoverRow = row;
                    InvalidateVisual();
                }
                return;
            }

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
            if (RoofTileSelectionService.Instance.IsSelecting)
            {
                if (_selHoverCol >= 0) { _selHoverCol = _selHoverRow = -1; InvalidateVisual(); }
                return;
            }

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
            if (RoofTileSelectionService.Instance.IsSelecting)
            {
                var pos = e.GetPosition(this);
                _selStartCol = _selHoverCol = (int)Math.Floor(pos.X / 64);
                _selStartRow = _selHoverRow = (int)Math.Floor(pos.Y / 64);
                _isDraggingTileSelection = true;
                CaptureMouse();
                Focus();
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (e.ClickCount == 2)
            {
                var tile = HitTestTile(e.GetPosition(this));
                if (tile.HasValue)
                {
                    CancelEdit();
                    OpenRoofFace4Preview(tile.Value);
                    e.Handled = true;
                    return;
                }
            }

            var hit = HitTestCached(e.GetPosition(this));
            if (hit == null) { CancelEdit(); return; }
            StartEdit(hit.Value.idx, hit.Value.field, hit.Value.value, hit.Value.rect);
            e.Handled = true;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingTileSelection) return;

            _isDraggingTileSelection = false;
            ReleaseMouseCapture();

            var bounds = GetTileSelectionBounds();

            if (bounds.HasValue)
                RoofTileSelectionService.Instance.CompleteSelection(bounds.Value);
            else
                RoofTileSelectionService.Instance.Cancel();

            e.Handled = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && (_isDraggingTileSelection || RoofTileSelectionService.Instance.IsSelecting))
            {
                _isDraggingTileSelection = false;
                ReleaseMouseCapture();
                RoofTileSelectionService.Instance.Cancel();
                e.Handled = true;
            }
        }

        private TileInfo? HitTestTile(Point pos)
        {
            if (_cachedTiles == null) return null;
            foreach (var tile in _cachedTiles)
            {
                if (pos.X >= tile.UiX && pos.X <= tile.UiX + 64 &&
                    pos.Y >= tile.UiZ && pos.Y <= tile.UiZ + 64)
                    return tile;
            }
            return null;
        }

        private Rect? GetTileSelectionBounds()
        {
            if (_selHoverCol < 0) return null;
            int startCol = _selStartCol >= 0 ? _selStartCol : _selHoverCol;
            int startRow = _selStartRow >= 0 ? _selStartRow : _selHoverRow;
            int minCol = Math.Min(startCol, _selHoverCol);
            int maxCol = Math.Max(startCol, _selHoverCol);
            int minRow = Math.Min(startRow, _selHoverRow);
            int maxRow = Math.Max(startRow, _selHoverRow);
            return new Rect(minCol * 64, minRow * 64, (maxCol - minCol + 1) * 64, (maxRow - minRow + 1) * 64);
        }

        private void OpenRoofFace4Preview(TileInfo tile)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded) return;

            var snap = new BuildingsAccessor(svc).ReadSnapshot();
            if (snap.RoofFaces4 == null || tile.Rf4Idx < 0 || tile.Rf4Idx >= snap.RoofFaces4.Length) return;

            var dlg = new RoofFace4PreviewWindow(tile.Rf4Idx, snap.RoofFaces4[tile.Rf4Idx], SelectedWalkableId1)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            dlg.Show();
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

                // Y (base) shown SE corner; DY0 SW, DY1 NW, DY2 NE — original screen positions
                var yRect   = new Rect(uiX + 38, uiZ + 48, 24, 14);  // SE bottom-right = Y (base)
                var dy0Rect = new Rect(uiX + 2,  uiZ + 48, 24, 14);  // SW bottom-left  = DY0
                var dy1Rect = new Rect(uiX + 2,  uiZ + 2,  24, 14);  // NW top-left     = DY1
                var dy2Rect = new Rect(uiX + 38, uiZ + 2,  24, 14);  // NE top-right    = DY2

                if (yRect.Contains(pos))   return (tile.Rf4Idx, EditField.Y,   tile.Y / 64,    yRect);
                if (dy0Rect.Contains(pos)) return (tile.Rf4Idx, EditField.DY0, tile.DY0,       dy0Rect);
                if (dy1Rect.Contains(pos)) return (tile.Rf4Idx, EditField.DY1, tile.DY1,       dy1Rect);
                if (dy2Rect.Contains(pos)) return (tile.Rf4Idx, EditField.DY2, tile.DY2,       dy2Rect);
            }
            return null;
        }

        #endregion

        #region SelectedRf4Id Dependency Property

        public static readonly DependencyProperty SelectedRf4IdProperty =
            DependencyProperty.Register(
                nameof(SelectedRf4Id),
                typeof(int),
                typeof(RoofsLayer),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

        public int SelectedRf4Id
        {
            get => (int)GetValue(SelectedRf4IdProperty);
            set => SetValue(SelectedRf4IdProperty, value);
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
                    // val is in Quarter Storeys; convert to raw RF4 units (1 QS = 64 raw)
                    int rawY = val * 64;
                    bytes[off] = (byte)(rawY & 0xFF);
                    bytes[off + 1] = (byte)((rawY >> 8) & 0xFF);
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