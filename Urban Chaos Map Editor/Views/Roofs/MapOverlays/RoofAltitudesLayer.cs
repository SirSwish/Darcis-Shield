// Views/Roofs/MapOverlays/RoofAltitudesLayer.cs
// Displays PAP_HI altitude values for non-zero cells and walkable regions.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosEditor.Shared.Models;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.ViewModels.Core;
using HeightSettings = UrbanChaosMapEditor.Models.Core.HeightDisplaySettings;

namespace UrbanChaosMapEditor.Views.Roofs.MapOverlays
{
    public sealed class RoofAltitudesLayer : FrameworkElement
    {
        private const int TilesPerSide = SharedMapConstants.TilesPerSide;
        private const int PixelsPerTile = SharedMapConstants.PixelsPerTile;
        private const int HeaderBytes = TextureFormatConstants.HeaderBytes;
        private const int BytesPerTile = TextureFormatConstants.BytesPerTile;
        private const int OffAlt = MapFormatConstants.PapAltitudeByteIndex;
        private const double LabelFontSize = 10.0;

        private static readonly Typeface LabelTypeface = new("Segoe UI");

        private static readonly Brush CellFillBrush;
        private static readonly Brush CellFillZeroBrush;
        private static readonly Pen CellBorderPen;

        private static readonly Brush TextBrush;
        private static readonly Brush TextShadowBrush;
        private static readonly Brush TextZeroBrush;
        private static readonly Brush TextNegativeBrush;

        private readonly List<AltitudeCell> _cells = new();
        private readonly Dictionary<TextCacheKey, FormattedText> _textCache = new();
        private readonly DispatcherTimer _viewportDebounceTimer;
        private bool _cacheDirty = true;
        private double _cachedPixelsPerDip;
        private ScrollViewer? _scrollViewer;
        private MapViewModel? _vm;

        private readonly record struct AltitudeCell(int UiTileX, int UiTileZ, sbyte Alt);
        private readonly record struct TextCacheKey(string Text, int BrushKind, double PixelsPerDip);

        static RoofAltitudesLayer()
        {
            CellFillBrush = new SolidColorBrush(Color.FromArgb(100, 0, 255, 100));
            CellFillBrush.Freeze();

            CellFillZeroBrush = new SolidColorBrush(Color.FromArgb(40, 0, 200, 0));
            CellFillZeroBrush.Freeze();

            CellBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)), 0.5);
            CellBorderPen.Freeze();

            TextBrush = Brushes.White;
            TextShadowBrush = Brushes.Black;

            TextZeroBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xAA, 0xAA, 0xAA));
            TextZeroBrush.Freeze();

            TextNegativeBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x66, 0x66));
            TextNegativeBrush.Freeze();
        }

        public RoofAltitudesLayer()
        {
            IsHitTestVisible = false;
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

            _viewportDebounceTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _viewportDebounceTimer.Tick += (_, _) =>
            {
                _viewportDebounceTimer.Stop();
                InvalidateVisual();
            };

            RoofsChangeBus.Instance.Changed += (_, _) => Dispatcher.Invoke(MarkCacheDirty);
            BuildingsChangeBus.Instance.Changed += (_, _) => Dispatcher.Invoke(MarkCacheDirty);
            AltitudeChangeBus.Instance.TileChanged += (_, _) => Dispatcher.Invoke(MarkCacheDirty);
            AltitudeChangeBus.Instance.RegionChanged += (_, __, ___, ____) => Dispatcher.Invoke(MarkCacheDirty);
            AltitudeChangeBus.Instance.AllChanged += () => Dispatcher.Invoke(MarkCacheDirty);
            HeightSettings.DisplayModeChanged += (_, __) => Dispatcher.Invoke(ClearTextCacheAndInvalidate);
            MapDataService.Instance.MapLoaded += (_, _) => Dispatcher.Invoke(MarkCacheDirty);
            MapDataService.Instance.MapCleared += (_, _) => Dispatcher.Invoke(ClearCache);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += (_, _) => QueueViewportRepaint();
            DataContextChanged += (_, _) => HookViewModel();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (!MapDataService.Instance.IsLoaded) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            if (_cachedPixelsPerDip != dpi)
            {
                _cachedPixelsPerDip = dpi;
                _textCache.Clear();
            }

            if (_cacheDirty)
                RebuildCache();

            if (_cells.Count == 0)
                return;

            Rect visible = GetVisibleMapRect();
            foreach (var cell in _cells)
            {
                double cellX = cell.UiTileX * PixelsPerTile;
                double cellZ = cell.UiTileZ * PixelsPerTile;
                var cellRect = new Rect(cellX, cellZ, PixelsPerTile, PixelsPerTile);

                if (!visible.IntersectsWith(cellRect))
                    continue;

                DrawCell(dc, cell, cellRect, dpi);
            }
        }

        private void RebuildCache()
        {
            _cells.Clear();
            _cacheDirty = false;

            var svc = MapDataService.Instance;
            if (!svc.IsLoaded) return;

            byte[] fileBytes;
            try { fileBytes = svc.GetBytesCopy(); }
            catch { return; }

            var shouldDisplay = new bool[TilesPerSide * TilesPerSide];

            if (svc.TryGetWalkables(out var walkables, out _) && walkables.Length > 1)
            {
                for (int wIdx = 1; wIdx < walkables.Length; wIdx++)
                {
                    var w = walkables[wIdx];
                    if ((w.X1 | w.Z1 | w.X2 | w.Z2) == 0) continue;

                    int minGx = Math.Clamp((int)Math.Min(w.X1, w.X2), 0, TilesPerSide - 1);
                    int maxGx = Math.Clamp((int)Math.Max(w.X1, w.X2), 0, TilesPerSide);
                    int minGz = Math.Clamp((int)Math.Min(w.Z1, w.Z2), 0, TilesPerSide - 1);
                    int maxGz = Math.Clamp((int)Math.Max(w.Z1, w.Z2), 0, TilesPerSide);

                    for (int gx = minGx; gx < maxGx; gx++)
                    {
                        for (int gz = minGz; gz < maxGz; gz++)
                        {
                            int uiTileX = TilesPerSide - 1 - gx;
                            int uiTileZ = TilesPerSide - 1 - gz;
                            shouldDisplay[uiTileZ * TilesPerSide + uiTileX] = true;
                        }
                    }
                }
            }

            int tileCount = TilesPerSide * TilesPerSide;
            for (int fileIndex = 0; fileIndex < tileCount; fileIndex++)
            {
                int offset = HeaderBytes + fileIndex * BytesPerTile + OffAlt;
                if (offset >= fileBytes.Length) break;

                sbyte alt = unchecked((sbyte)fileBytes[offset]);

                int fileY = fileIndex / TilesPerSide;
                int fileX = fileIndex % TilesPerSide;
                int uiTileX = TilesPerSide - 1 - fileY;
                int uiTileZ = TilesPerSide - 1 - fileX;

                if (alt == 0 && !shouldDisplay[uiTileZ * TilesPerSide + uiTileX])
                    continue;

                _cells.Add(new AltitudeCell(uiTileX, uiTileZ, alt));
            }
        }

        private void DrawCell(DrawingContext dc, AltitudeCell cell, Rect cellRect, double dpi)
        {
            Brush fill = cell.Alt != 0 ? CellFillBrush : CellFillZeroBrush;
            dc.DrawRectangle(fill, CellBorderPen, cellRect);

            int worldAlt = cell.Alt << MapFormatConstants.PapAltitudeShift;
            int displayAlt = HeightSettings.ShowRawHeights ? worldAlt : worldAlt / 64;
            string label = displayAlt.ToString(CultureInfo.InvariantCulture);

            int brushKind = displayAlt == 0 ? 0 : displayAlt < 0 ? 2 : 1;
            Brush textColor = brushKind switch
            {
                0 => TextBrush,
                2 => TextNegativeBrush,
                _ => TextBrush
            };

            var shadowFt = GetFormattedText(label, 3, TextShadowBrush, dpi);
            double tx = cellRect.X + (PixelsPerTile - shadowFt.Width) / 2;
            double ty = cellRect.Y + (PixelsPerTile - shadowFt.Height) / 2;
            dc.DrawText(shadowFt, new Point(tx + 1, ty + 1));

            var ft = GetFormattedText(label, brushKind, textColor, dpi);
            dc.DrawText(ft, new Point(tx, ty));
        }

        private FormattedText GetFormattedText(string text, int brushKind, Brush brush, double dpi)
        {
            var key = new TextCacheKey(text, brushKind, dpi);
            if (_textCache.TryGetValue(key, out var cached))
                return cached;

            var formatted = new FormattedText(text, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LabelTypeface, LabelFontSize, brush, dpi);
            _textCache[key] = formatted;
            return formatted;
        }

        private Rect GetVisibleMapRect()
        {
            var scrollViewer = FindAncestor<ScrollViewer>(this);
            if (scrollViewer == null || scrollViewer.ViewportWidth <= 0 || scrollViewer.ViewportHeight <= 0)
                return new Rect(0, 0, TilesPerSide * PixelsPerTile, TilesPerSide * PixelsPerTile);

            try
            {
                GeneralTransform transform = scrollViewer.TransformToDescendant(this);
                var viewport = new Rect(0, 0, scrollViewer.ViewportWidth, scrollViewer.ViewportHeight);
                var visible = transform.TransformBounds(viewport);
                visible.Inflate(PixelsPerTile, PixelsPerTile);
                return visible;
            }
            catch
            {
                return new Rect(0, 0, TilesPerSide * PixelsPerTile, TilesPerSide * PixelsPerTile);
            }
        }

        private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
        {
            while (node != null)
            {
                if (node is T match)
                    return match;

                node = VisualTreeHelper.GetParent(node);
            }

            return null;
        }

        private void MarkCacheDirty()
        {
            _cacheDirty = true;
            InvalidateVisual();
        }

        private void ClearTextCacheAndInvalidate()
        {
            _textCache.Clear();
            InvalidateVisual();
        }

        private void ClearCache()
        {
            _cells.Clear();
            _textCache.Clear();
            _cacheDirty = true;
            InvalidateVisual();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _scrollViewer = FindAncestor<ScrollViewer>(this);
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollChanged;
                _scrollViewer.SizeChanged += OnViewportSizeChanged;
            }

            HookViewModel();
            QueueViewportRepaint();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnScrollChanged;
                _scrollViewer.SizeChanged -= OnViewportSizeChanged;
                _scrollViewer = null;
            }

            if (_vm != null)
            {
                _vm.PropertyChanged -= OnViewModelPropertyChanged;
                _vm = null;
            }
        }

        private void HookViewModel()
        {
            if (_vm != null)
                _vm.PropertyChanged -= OnViewModelPropertyChanged;

            _vm = DataContext as MapViewModel;

            if (_vm != null)
                _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapViewModel.Zoom))
                QueueViewportRepaint();
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange != 0 ||
                e.VerticalChange != 0 ||
                e.ViewportWidthChange != 0 ||
                e.ViewportHeightChange != 0 ||
                e.ExtentWidthChange != 0 ||
                e.ExtentHeightChange != 0)
            {
                QueueViewportRepaint();
            }
        }

        private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            QueueViewportRepaint();
        }

        private void QueueViewportRepaint()
        {
            if (!_viewportDebounceTimer.IsEnabled)
                _viewportDebounceTimer.Start();
        }
    }
}
