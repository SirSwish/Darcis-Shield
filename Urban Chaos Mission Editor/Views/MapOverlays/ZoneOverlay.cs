using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.ViewModels;

namespace UrbanChaosMissionEditor.Views.MapOverlays;

public class ZoneOverlay : FrameworkElement
{
    private const int GridSize = 128;
    private const double TileSize = 64.0; // 8192 / 128

    private MainViewModel? _viewModel;
    private WriteableBitmap? _zoneBitmap;

    public ZoneOverlay()
    {
        Width = 8192;
        Height = 8192;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;

        if (e.NewValue is MainViewModel newVm)
        {
            _viewModel = newVm;
            newVm.PropertyChanged += ViewModel_PropertyChanged;
            Refresh();
        }
        else
        {
            _viewModel = null;
            Refresh();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // NOTE: your pasted code had a syntax bug here (nameof(MainViewModel.HasZoneHover || ...)
        if (e.PropertyName == nameof(MainViewModel.ShowZones) ||
            e.PropertyName == nameof(MainViewModel.CurrentMission) ||
            e.PropertyName == nameof(MainViewModel.SelectedZoneType) ||
            e.PropertyName == nameof(MainViewModel.IsZonePaintMode) ||
            e.PropertyName == nameof(MainViewModel.IsZoneEraseMode) ||
            e.PropertyName == nameof(MainViewModel.HoverZoneX) ||
            e.PropertyName == nameof(MainViewModel.HoverZoneZ) ||
            e.PropertyName == nameof(MainViewModel.HasZoneHover) ||
            e.PropertyName == nameof(MainViewModel.IsZoneRectPreviewActive) ||
            e.PropertyName == nameof(MainViewModel.ZoneRectStartX) ||
            e.PropertyName == nameof(MainViewModel.ZoneRectStartZ) ||
            e.PropertyName == nameof(MainViewModel.ZoneRectEndX) ||
            e.PropertyName == nameof(MainViewModel.ZoneRectEndZ))
        {
            Refresh();
        }
    }

    public void Refresh() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        var vm = _viewModel;
        if (vm == null || !vm.ShowZones) return;

        var zones = vm.GetZoneArray();
        if (zones == null) return;

        var selected = vm.SelectedZoneType;

        if (_zoneBitmap == null || _zoneBitmap.PixelWidth != GridSize)
            _zoneBitmap = new WriteableBitmap(GridSize, GridSize, 96, 96, PixelFormats.Bgra32, null);

        int stride = GridSize * 4;
        byte[] pixels = new byte[GridSize * GridSize * 4];

        var onColor = ZoneColors.GetColor(selected);

        for (int z = 0; z < GridSize; z++)
            for (int x = 0; x < GridSize; x++)
            {
                int displayX = GridSize - 1 - x;
                int displayZ = GridSize - 1 - z;

                bool on = selected != ZoneType.None && ((zones[x, z] & (byte)selected) != 0);
                var c = on ? onColor : Colors.Transparent;

                int idx = (displayZ * GridSize + displayX) * 4;
                pixels[idx + 0] = c.B;
                pixels[idx + 1] = c.G;
                pixels[idx + 2] = c.R;
                pixels[idx + 3] = c.A;
            }

        _zoneBitmap.WritePixels(new Int32Rect(0, 0, GridSize, GridSize), pixels, stride, 0);
        dc.DrawImage(_zoneBitmap, new Rect(0, 0, 8192, 8192));

        // Single-tile ghost preview
        DrawGhostPreview(dc, vm, zones);

        // Rectangle “square drag” ghost preview
        DrawRectGhostPreview(dc, vm);
    }

    private static void DrawGhostPreview(DrawingContext dc, MainViewModel vm, byte[,] zones)
    {
        if (!vm.HasZoneHover) return;
        if (!vm.IsZonePaintMode && !vm.IsZoneEraseMode) return;

        var flag = vm.SelectedZoneType;
        if (flag == ZoneType.None) return;

        int gx = vm.HoverZoneX;
        int gz = vm.HoverZoneZ;
        if (gx < 0 || gx >= GridSize || gz < 0 || gz >= GridSize) return;

        bool hasFlag = (zones[gx, gz] & (byte)flag) != 0;

        int displayX = GridSize - 1 - gx;
        int displayZ = GridSize - 1 - gz;

        var rect = new Rect(displayX * TileSize, displayZ * TileSize, TileSize, TileSize);

        var baseColor = ZoneColors.GetColor(flag);

        // dashed outline
        Color outline = vm.IsZoneEraseMode
            ? Color.FromArgb(220, 255, 0, 0) // red for delete
            : Color.FromArgb(220, baseColor.R, baseColor.G, baseColor.B);

        var pen = new Pen(new SolidColorBrush(outline), 2)
        {
            DashStyle = new DashStyle(new double[] { 4, 2 }, 0)
        };
        pen.Freeze();

        // faint fill
        Color fillColor;
        if (vm.IsZonePaintMode)
            fillColor = Color.FromArgb(80, baseColor.R, baseColor.G, baseColor.B);
        else
            fillColor = hasFlag ? Color.FromArgb(60, 0, 0, 0) : Color.FromArgb(20, 0, 0, 0);

        var fill = new SolidColorBrush(fillColor);
        fill.Freeze();

        dc.DrawRectangle(fill, pen, rect);

        // erase "X"
        if (vm.IsZoneEraseMode)
        {
            var xPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 0, 0)), 2);
            xPen.Freeze();
            dc.DrawLine(xPen, rect.TopLeft, rect.BottomRight);
            dc.DrawLine(xPen, rect.TopRight, rect.BottomLeft);
        }
    }

    private static void DrawRectGhostPreview(DrawingContext dc, MainViewModel vm)
    {
        if (!vm.IsZoneRectPreviewActive) return;
        if (!vm.IsZonePaintMode && !vm.IsZoneEraseMode) return;

        var flag = vm.SelectedZoneType;
        if (flag == ZoneType.None) return;

        // Clamp & normalize in game coords (0..127)
        int x0 = Math.Clamp(vm.ZoneRectStartX, 0, 127);
        int z0 = Math.Clamp(vm.ZoneRectStartZ, 0, 127);
        int x1 = Math.Clamp(vm.ZoneRectEndX, 0, 127);
        int z1 = Math.Clamp(vm.ZoneRectEndZ, 0, 127);

        int minX = Math.Min(x0, x1);
        int maxX = Math.Max(x0, x1);
        int minZ = Math.Min(z0, z1);
        int maxZ = Math.Max(z0, z1);

        // Convert game coords to display coords (same inversion used elsewhere)
        // Display coordinate increases left->right/top->bottom, while game is inverted.
        int displayMinX = (GridSize - 1) - maxX;
        int displayMaxX = (GridSize - 1) - minX;
        int displayMinZ = (GridSize - 1) - maxZ;
        int displayMaxZ = (GridSize - 1) - minZ;

        var rect = new Rect(
            displayMinX * TileSize,
            displayMinZ * TileSize,
            (displayMaxX - displayMinX + 1) * TileSize,
            (displayMaxZ - displayMinZ + 1) * TileSize
        );

        var baseColor = ZoneColors.GetColor(flag);

        Color outline = vm.IsZoneEraseMode
            ? Color.FromArgb(220, 255, 0, 0)
            : Color.FromArgb(220, baseColor.R, baseColor.G, baseColor.B);

        var pen = new Pen(new SolidColorBrush(outline), 2)
        {
            DashStyle = new DashStyle(new double[] { 6, 3 }, 0)
        };
        pen.Freeze();

        Color fillColor = vm.IsZoneEraseMode
            ? Color.FromArgb(25, 255, 0, 0)
            : Color.FromArgb(40, baseColor.R, baseColor.G, baseColor.B);

        var fill = new SolidColorBrush(fillColor);
        fill.Freeze();

        dc.DrawRectangle(fill, pen, rect);

        // Optional: show an "erase" X across the rectangle
        if (vm.IsZoneEraseMode)
        {
            var xPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 0, 0)), 2);
            xPen.Freeze();
            dc.DrawLine(xPen, rect.TopLeft, rect.BottomRight);
            dc.DrawLine(xPen, rect.TopRight, rect.BottomLeft);
        }
    }
}