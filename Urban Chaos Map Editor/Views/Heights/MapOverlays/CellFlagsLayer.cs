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
    /// Draws a small flag marker in each PAP_HI cell.
    /// Grey = no flags, Red = one or more flags set.
    /// Double-click a cell to open the PAP HI flag editor.
    /// </summary>
    public sealed class CellFlagsLayer : FrameworkElement
    {
        private const double IconInset = 4.0;
        private const double PoleHeight = 12.0;
        private const double PoleWidth = 1.5;
        private const double FlagWidth = 8.0;
        private const double FlagHeight = 6.0;
        private const double HitSize = 18.0;

        private ushort[,]? _flags;

        private static readonly Brush PoleBrush = new SolidColorBrush(Color.FromArgb(210, 120, 120, 120));
        private static readonly Brush GreyFlagBrush = new SolidColorBrush(Color.FromArgb(210, 110, 110, 110));
        private static readonly Brush RedFlagBrush = new SolidColorBrush(Color.FromArgb(235, 210, 40, 40));
        private static readonly Pen GreyPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 145, 145, 145)), 1.0);
        private static readonly Pen RedPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 110, 110)), 1.0);

        static CellFlagsLayer()
        {
            if (PoleBrush.CanFreeze) PoleBrush.Freeze();
            if (GreyFlagBrush.CanFreeze) GreyFlagBrush.Freeze();
            if (RedFlagBrush.CanFreeze) RedFlagBrush.Freeze();
            if (GreyPen.CanFreeze) GreyPen.Freeze();
            if (RedPen.CanFreeze) RedPen.Freeze();
        }

        public CellFlagsLayer()
        {
            Width = MapConstants.MapPixels;
            Height = MapConstants.MapPixels;
            IsHitTestVisible = true;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshFlags();

            MapDataService.Instance.MapLoaded += OnMapChanged;
            MapDataService.Instance.MapCleared += OnMapChanged;
            MapDataService.Instance.MapBytesReset += OnMapChanged;

            AltitudeChangeBus.Instance.TileChanged += OnAltitudeTileChanged;
            AltitudeChangeBus.Instance.RegionChanged += OnAltitudeRegionChanged;
            AltitudeChangeBus.Instance.AllChanged += OnAltitudeAllChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            MapDataService.Instance.MapLoaded -= OnMapChanged;
            MapDataService.Instance.MapCleared -= OnMapChanged;
            MapDataService.Instance.MapBytesReset -= OnMapChanged;

            AltitudeChangeBus.Instance.TileChanged -= OnAltitudeTileChanged;
            AltitudeChangeBus.Instance.RegionChanged -= OnAltitudeRegionChanged;
            AltitudeChangeBus.Instance.AllChanged -= OnAltitudeAllChanged;
        }

        private void OnMapChanged(object? sender, EventArgs e)
        {
            RefreshFlags();
        }

        private void OnAltitudeTileChanged(int tx, int ty)
        {
            RefreshFlags();
        }

        private void OnAltitudeRegionChanged(int minTx, int minTy, int maxTx, int maxTy)
        {
            RefreshFlags();
        }

        private void OnAltitudeAllChanged()
        {
            RefreshFlags();
        }

        public void RefreshFlags()
        {
            _flags = CellFlagsLayerService.ReadAllFlags();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_flags == null)
                return;

            int tileSize = MapConstants.TileSize;
            int tiles = MapConstants.TilesPerSide;

            for (int tx = 0; tx < tiles; tx++)
            {
                for (int ty = 0; ty < tiles; ty++)
                {
                    // UI tile -> game tile
                    int gameX = 127 - tx;
                    int gameZ = 127 - ty;

                    ushort flags = _flags[gameX, gameZ];
                    bool active = flags != 0;

                    double x = tx * tileSize + IconInset;
                    double y = ty * tileSize + IconInset;

                    DrawFlag(dc, x, y, active);
                }
            }
        }

        private static void DrawFlag(DrawingContext dc, double x, double y, bool active)
        {
            Brush fill = active ? RedFlagBrush : GreyFlagBrush;
            Pen pen = active ? RedPen : GreyPen;

            dc.DrawRectangle(PoleBrush, null, new Rect(x, y, PoleWidth, PoleHeight));

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(x + PoleWidth, y), isFilled: true, isClosed: true);
                ctx.LineTo(new Point(x + PoleWidth + FlagWidth, y + 2), true, false);
                ctx.LineTo(new Point(x + PoleWidth, y + FlagHeight), true, false);
            }
            geo.Freeze();

            dc.DrawGeometry(fill, pen, geo);
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2)
                return;

            Point p = e.GetPosition(this);

            int tx = (int)(p.X / MapConstants.TileSize);
            int ty = (int)(p.Y / MapConstants.TileSize);

            if (tx < 0 || tx >= MapConstants.TilesPerSide ||
                ty < 0 || ty >= MapConstants.TilesPerSide)
                return;

            double localX = p.X - (tx * MapConstants.TileSize);
            double localY = p.Y - (ty * MapConstants.TileSize);
            if (localX > HitSize || localY > HitSize)
                return;

            int gameX = 127 - tx;
            int gameZ = 127 - ty;

            var dlg = new PapHiCellEditorDialog(gameX, gameZ)
            {
                Owner = Window.GetWindow(this)
            };

            dlg.ShowDialog();

            if (dlg.Applied)
            {
                RefreshFlags();
            }

            e.Handled = true;
        }
    }
}