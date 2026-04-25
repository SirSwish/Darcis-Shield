using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Heights;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.Views.Heights.Dialogs;

namespace UrbanChaosMapEditor.Views.Heights.MapOverlays
{
    /// <summary>
    /// Highlights matching PAP_HI cells as full-cell overlays.
    /// FilterMask == 0  → highlight any cell with at least one flag set.
    /// FilterMask != 0  → highlight only cells where ALL selected flag bits are set (AND match).
    /// Double-click any highlighted cell to open the Cell Flags Editor.
    /// </summary>
    public sealed class CellFlagsLayer : FrameworkElement
    {
        private static readonly Brush HighlightFill;
        private static readonly Pen   HighlightBorder;

        static CellFlagsLayer()
        {
            HighlightFill = new SolidColorBrush(Color.FromArgb(100, 220, 40, 40));
            HighlightFill.Freeze();
            var stroke = new SolidColorBrush(Color.FromArgb(200, 255, 80, 80));
            stroke.Freeze();
            HighlightBorder = new Pen(stroke, 1.5);
            HighlightBorder.Freeze();
        }

        #region FilterMask dependency property

        public static readonly DependencyProperty FilterMaskProperty =
            DependencyProperty.Register(
                nameof(FilterMask),
                typeof(ushort),
                typeof(CellFlagsLayer),
                new FrameworkPropertyMetadata((ushort)0, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// AND-mask that controls which cells are highlighted.
        /// 0 = show any cell with at least one flag; non-zero = require all set bits to be present.
        /// </summary>
        public ushort FilterMask
        {
            get => (ushort)GetValue(FilterMaskProperty);
            set => SetValue(FilterMaskProperty, value);
        }

        #endregion

        private ushort[,]? _flags;

        public CellFlagsLayer()
        {
            Width  = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = true;

            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;

            MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshFlags();

            MapDataService.Instance.MapLoaded    += OnMapChanged;
            MapDataService.Instance.MapCleared   += OnMapChanged;
            MapDataService.Instance.MapBytesReset += OnMapChanged;

            AltitudeChangeBus.Instance.TileChanged    += OnAltitudeTileChanged;
            AltitudeChangeBus.Instance.RegionChanged  += OnAltitudeRegionChanged;
            AltitudeChangeBus.Instance.AllChanged     += OnAltitudeAllChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            MapDataService.Instance.MapLoaded    -= OnMapChanged;
            MapDataService.Instance.MapCleared   -= OnMapChanged;
            MapDataService.Instance.MapBytesReset -= OnMapChanged;

            AltitudeChangeBus.Instance.TileChanged    -= OnAltitudeTileChanged;
            AltitudeChangeBus.Instance.RegionChanged  -= OnAltitudeRegionChanged;
            AltitudeChangeBus.Instance.AllChanged     -= OnAltitudeAllChanged;
        }

        private void OnMapChanged(object? sender, EventArgs e) => RefreshFlags();
        private void OnAltitudeTileChanged(int tx, int ty)          => RefreshFlags();
        private void OnAltitudeRegionChanged(int a, int b, int c, int d) => RefreshFlags();
        private void OnAltitudeAllChanged()                          => RefreshFlags();

        public void RefreshFlags()
        {
            _flags = CellFlagsLayerService.ReadAllFlags();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (_flags == null) return;

            int tileSize   = MapConstants.TileSize;
            int tiles      = MapConstants.TilesPerSide;
            ushort filter  = FilterMask;

            for (int tx = 0; tx < tiles; tx++)
            {
                for (int ty = 0; ty < tiles; ty++)
                {
                    // UI tile → game tile
                    int gameX = 127 - tx;
                    int gameZ = 127 - ty;

                    ushort flags = _flags[gameX, gameZ];

                    bool matches = filter == 0
                        ? flags != 0                          // any flag set
                        : (flags & filter) == filter;         // all selected bits present

                    if (!matches) continue;

                    double x = tx * tileSize;
                    double y = ty * tileSize;
                    dc.DrawRectangle(HighlightFill, HighlightBorder,
                        new Rect(x, y, tileSize, tileSize));
                }
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2) return;

            Point p  = e.GetPosition(this);
            int tx   = (int)(p.X / MapConstants.TileSize);
            int ty   = (int)(p.Y / MapConstants.TileSize);

            if (tx < 0 || tx >= MapConstants.TilesPerSide ||
                ty < 0 || ty >= MapConstants.TilesPerSide)
                return;

            int    gameX  = 127 - tx;
            int    gameZ  = 127 - ty;
            ushort flags  = _flags?[gameX, gameZ] ?? 0;
            ushort filter = FilterMask;

            // Only intercept the double-click if this cell is currently highlighted.
            bool matches = filter == 0 ? flags != 0 : (flags & filter) == filter;
            if (!matches) return;

            var dlg = new PapHiCellEditorDialog(gameX, gameZ)
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();

            if (dlg.Applied)
                RefreshFlags();

            e.Handled = true;
        }
    }
}
