using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Models.Prims;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Heights;
using UrbanChaosMapEditor.Services.Prims;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.Services.Textures;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Views.Buildings;
using UrbanChaosMapEditor.Views.Buildings.Dialogs;

namespace UrbanChaosMapEditor.Views.Core
{
    public partial class MapView : UserControl
    {
        private const double ZoomStep = 1.10;
        private const double MinZoom = 0.10;
        private const double MaxZoom = 8.00;

        private bool _isSettingAltitude;
        private HashSet<(int, int)>? _altitudePaintedTiles;

        private bool _isDrawingWalkableRect;

        private bool _isTextureDragging;
        private bool _isTextureStrokePainting;
        private Point _textureMouseDownPos;
        private HashSet<(int, int)>? _texturePaintedTiles;
        private const double TextureDragThreshold = 16.0;

        private readonly HeightsAccessor _heights = new HeightsAccessor(MapDataService.Instance);
        private AltitudeAccessor? _altitude = new AltitudeAccessor(MapDataService.Instance);

        private bool _isLeveling = false;
        private sbyte _levelSource;
        private (int tx, int ty)? _lastLeveledTile;

        private MapViewModel? _hookedVm;

        public event EventHandler? WalkableDrawingCompleted;

        private sealed class FacetHitCandidate
        {
            public int FacetId1 { get; init; }
            public int BuildingId1 { get; init; }
            public int StoreyId { get; init; }
            public FacetType Type { get; init; }
            public byte X0 { get; init; }
            public byte Z0 { get; init; }
            public byte X1 { get; init; }
            public byte Z1 { get; init; }
            public double DistancePx { get; init; }

            public string Display =>
                $"Facet #{FacetId1} | Building #{BuildingId1} | Storey {StoreyId} | {Type} | ({X0},{Z0}) -> ({X1},{Z1})";
        }

        private static double DistancePointToSegment(
            double px, double py,
            double x1, double y1,
            double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;

            if (dx == 0 && dy == 0)
            {
                double ox = px - x1;
                double oy = py - y1;
                return Math.Sqrt(ox * ox + oy * oy);
            }

            double t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));

            double cx = x1 + t * dx;
            double cy = y1 + t * dy;

            double rx = px - cx;
            double ry = py - cy;
            return Math.Sqrt(rx * rx + ry * ry);
        }

        private static List<FacetHitCandidate> FindFacetHits(double uiX, double uiZ, double maxDistancePx = 8.0)
        {
            var snap = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();
            var hits = new List<FacetHitCandidate>();

            if (snap.Facets == null || snap.Facets.Length == 0)
                return hits;

            for (int i = 0; i < snap.Facets.Length; i++)
            {
                var f = snap.Facets[i];
                int facetId1 = i + 1;

                if (f.Building <= 0)
                    continue;

                // Convert facet tile coords to UI pixel coords.
                // This matches your existing facet redraw conversion.
                double x0 = (128 - f.X0) * 64.0;
                double z0 = (128 - f.Z0) * 64.0;
                double x1 = (128 - f.X1) * 64.0;
                double z1 = (128 - f.Z1) * 64.0;

                double d = DistancePointToSegment(uiX, uiZ, x0, z0, x1, z1);
                if (d <= maxDistancePx)
                {
                    hits.Add(new FacetHitCandidate
                    {
                        FacetId1 = facetId1,
                        BuildingId1 = f.Building,
                        StoreyId = f.Storey,
                        Type = f.Type,
                        X0 = f.X0,
                        Z0 = f.Z0,
                        X1 = f.X1,
                        Z1 = f.Z1,
                        DistancePx = d
                    });
                }
            }

            return hits
                .OrderBy(h => h.DistancePx)
                .ThenBy(h => h.BuildingId1)
                .ThenBy(h => h.FacetId1)
                .ToList();
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private bool TrySelectFacetInBuildingsTab(int facetId1, int buildingId1, bool openEditor)
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
                return false;

            var buildingsTab = FindVisualChild<BuildingsTab>(mainWindow);
            if (buildingsTab == null)
                return false;

            return buildingsTab.SelectFacetFromMap(facetId1, buildingId1, openEditor);
        }

        public MapView()
        {
            InitializeComponent();

            PreviewMouseMove += OnPreviewMouseMove;
            MouseLeave += OnMouseLeave;
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            LostMouseCapture += OnLostMouseCapture;
            PreviewMouseWheel += OnPreviewMouseWheel;
            PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;

            MouseEnter += (_, __) => Focus();
            PreviewMouseLeftButtonDown += (_, __) => Focus();

            DataContextChanged += MapView_DataContextChanged;
            Loaded += (_, __) =>
            {
                HookVm();
                UpdateOverlayHitTesting();
                RefreshCellFlagsOverlay();
            };
        }

        private void MapView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            HookVm();
            UpdateOverlayHitTesting();
        }

        private void HookVm()
        {
            if (_hookedVm != null)
                _hookedVm.PropertyChanged -= OnMapVmPropertyChanged;

            _hookedVm = DataContext as MapViewModel;

            if (_hookedVm != null)
                _hookedVm.PropertyChanged += OnMapVmPropertyChanged;
        }

        private void OnMapVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapViewModel.SelectedTool) ||
                e.PropertyName == nameof(MapViewModel.ShowHeights) ||
                e.PropertyName == nameof(MapViewModel.ShowObjects))
            {
                Dispatcher.BeginInvoke(UpdateOverlayHitTesting);
            }
        }

        private void UpdateOverlayHitTesting()
        {
            if (DataContext is not MapViewModel vm)
                return;

            bool isAreaHeightTool = vm.SelectedTool == EditorTool.AreaSetHeight;

            if (HeightsOverlay != null)
            {
                HeightsOverlay.IsHitTestVisible = isAreaHeightTool && vm.ShowHeights;
            }

            if (PrimsOverlay != null)
            {
                PrimsOverlay.IsHitTestVisible = !isAreaHeightTool && vm.ShowObjects;
            }

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MapView] UpdateOverlayHitTesting: Tool={vm.SelectedTool}, HeightsHit={HeightsOverlay?.IsHitTestVisible}, PrimsHit={PrimsOverlay?.IsHitTestVisible}");
            }
        }

        private static bool IsCtrlDown()
            => Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

        private static bool IsShiftDown()
            => Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = LogicalTreeHelper.GetParent(d) ?? VisualTreeHelper.GetParent(d);
            }
            return null;
        }

        private void OnPreviewMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                return;

            if (DataContext is not MapViewModel vm || Surface == null) return;

            Point mouseDownPos = e.GetPosition(Surface);
            var mainVm = Application.Current.MainWindow?.DataContext as MainWindowViewModel;

            // Facet pick/select from map when no special placement/draw tool is active
            if (vm.ShowBuildings &&
                !vm.IsDrawingWalkable &&
                !vm.IsRedrawingFacet &&
                !vm.IsMultiDrawingFacets &&
                !vm.IsPlacingLadder &&
                !vm.IsPlacingCable &&
                !vm.IsPlacingDoor &&
                !vm.IsPlacingPrim &&
                vm.SelectedTool == EditorTool.None)
            {
                int uiX = (int)Math.Clamp(mouseDownPos.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(mouseDownPos.Y, 0, MapConstants.MapPixels);

                var hits = FindFacetHits(uiX, uiZ, maxDistancePx: 8.0);
                if (hits.Count > 0)
                {
                    FacetHitCandidate chosen;
                    bool openEditor = false;

                    if (hits.Count == 1)
                    {
                        chosen = hits[0];
                        openEditor = e.ClickCount >= 2;
                    }
                    else
                    {
                        var dialogItems = hits.Select(h => new FacetSelectionDialog.CandidateVm
                        {
                            FacetId1 = h.FacetId1,
                            BuildingId1 = h.BuildingId1,
                            StoreyId = h.StoreyId,
                            TypeName = h.Type.ToString(),
                            Coords = $"({h.X0},{h.Z0}) -> ({h.X1},{h.Z1})"
                        }).ToList();

                        var dlg = new FacetSelectionDialog(dialogItems)
                        {
                            Owner = Application.Current.MainWindow
                        };

                        if (dlg.ShowDialog() != true || dlg.SelectedCandidate == null)
                        {
                            e.Handled = true;
                            return;
                        }

                        var sel = dlg.SelectedCandidate;

                        chosen = hits.First(h =>
                            h.FacetId1 == sel.FacetId1 &&
                            h.BuildingId1 == sel.BuildingId1);

                        // IMPORTANT: use the dialog's intent, not the map click count
                        openEditor = dlg.OpenEditor;
                    }

                    if (TrySelectFacetInBuildingsTab(chosen.FacetId1, chosen.BuildingId1, openEditor))
                    {
                        if (mainVm != null)
                        {
                            mainVm.StatusMessage = openEditor
                                ? $"Opened facet #{chosen.FacetId1} from building #{chosen.BuildingId1}."
                                : $"Selected facet #{chosen.FacetId1} from building #{chosen.BuildingId1}.";
                        }

                        e.Handled = true;
                        return;
                    }

                    if (TrySelectFacetInBuildingsTab(chosen.FacetId1, chosen.BuildingId1, openEditor))
                    {
                        if (mainVm != null)
                        {
                            mainVm.StatusMessage = openEditor
                                ? $"Opened facet #{chosen.FacetId1} from building #{chosen.BuildingId1}."
                                : $"Selected facet #{chosen.FacetId1} from building #{chosen.BuildingId1}.";
                        }

                        e.Handled = true;
                        return;
                    }
                }
            }


            if (vm.IsDrawingWalkable)
            {
                int tx = (int)Math.Floor(mouseDownPos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseDownPos.Y / MapConstants.TileSize);

                tx = Math.Clamp(tx, 0, MapConstants.TilesPerSide - 1);
                ty = Math.Clamp(ty, 0, MapConstants.TilesPerSide - 1);

                vm.WalkableSelectionStartX = tx;
                vm.WalkableSelectionStartY = ty;
                vm.WalkableSelectionEndX = tx;
                vm.WalkableSelectionEndY = ty;

                _isDrawingWalkableRect = true;
                CaptureMouse();

                if (mainVm != null)
                    mainVm.StatusMessage = "Drag to select walkable region...";

                e.Handled = true;
                return;
            }

            if (vm.IsRedrawingFacet)
            {
                Point p = e.GetPosition(Surface);
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);

                if (vm.HandleFacetRedrawClick(uiX, uiZ))
                {
                    e.Handled = true;
                    InvalidateVisual();
                    return;
                }
            }

            if (vm.IsMultiDrawingFacets)
            {
                Point p = e.GetPosition(Surface);
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);

                if (vm.HandleFacetMultiDrawClick(uiX, uiZ))
                {
                    e.Handled = true;
                    return;
                }
            }

            if (vm.IsPlacingLadder)
            {
                Point p = e.GetPosition(Surface);
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);

                if (vm.HandleLadderPlacementClick(uiX, uiZ))
                {
                    e.Handled = true;
                    return;
                }
            }

            if (vm.IsPlacingCable)
            {
                Point p = e.GetPosition(Surface);
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);

                if (vm.HandleCablePlacementClick(uiX, uiZ))
                {
                    e.Handled = true;
                    return;
                }
            }

            if (vm.IsPlacingPrim)
            {
                int clampedX = Math.Clamp((int)mouseDownPos.X, 0, MapConstants.MapPixels - 1);
                int clampedZ = Math.Clamp((int)mouseDownPos.Y, 0, MapConstants.MapPixels - 1);

                SnapUiToVertexIfCtrl(ref clampedX, ref clampedZ);

                ObjectSpace.UiPixelsToGamePrim(clampedX, clampedZ, out int mapWhoIndex, out byte gameX, out byte gameZ);

                try
                {
                    var acc = new PrimsAccessor(MapDataService.Instance);
                    var prim = new PrimsAccessor.PrimEntry
                    {
                        PrimNumber = (byte)vm.PrimNumberToPlace,
                        MapWhoIndex = mapWhoIndex,
                        X = gameX,
                        Z = gameZ,
                        Y = (short)0,
                        Yaw = (byte)0,
                        Flags = (byte)0,
                        InsideIndex = (byte)0
                    };

                    acc.AddPrim(prim);
                    vm.RefreshPrimsList();

                    var just = vm.Prims.LastOrDefault(p =>
                        p.MapWhoIndex == mapWhoIndex &&
                        p.X == gameX && p.Z == gameZ &&
                        p.PrimNumber == prim.PrimNumber);

                    vm.SelectedPrim = just;

                    if (mainVm != null)
                        mainVm.StatusMessage = $"Added {PrimCatalog.GetName(prim.PrimNumber)} ({prim.PrimNumber:000}) at cell r{just?.MapWhoRow},c{just?.MapWhoCol} ({gameX},{gameZ}).";
                }
                catch (Exception ex)
                {
                    if (mainVm != null)
                        mainVm.StatusMessage = $"Error: failed to add prim. {ex.Message}";
                }
                finally
                {
                    vm.CancelPlacePrim();
                    e.Handled = true;
                }
                return;
            }

            if (vm.SelectedTool == EditorTool.PaintTexture)
            {
                Point mousePos = e.GetPosition(Surface);
                int tx = (int)Math.Floor(mousePos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mousePos.Y / MapConstants.TileSize);

                if (tx < 0 || tx >= MapConstants.TilesPerSide || ty < 0 || ty >= MapConstants.TilesPerSide)
                    return;

                _textureMouseDownPos = mousePos;
                _isTextureDragging = false;
                _isTextureStrokePainting = false;

                vm.IsPaintingTexture = true;
                vm.TextureSelectionStartX = tx;
                vm.TextureSelectionStartY = ty;
                vm.TextureSelectionEndX = tx;
                vm.TextureSelectionEndY = ty;

                if (IsShiftDown())
                {
                    _isTextureStrokePainting = true;
                    _texturePaintedTiles = new HashSet<(int, int)>();

                    ApplyTextureBrush(tx, ty, vm);

                    if (mainVm != null)
                        mainVm.StatusMessage = $"Stroke painting {vm.SelectedTextureGroup} #{vm.SelectedTextureNumber:000} (brush {vm.BrushSize}×{vm.BrushSize})";
                }
                else
                {
                    if (mainVm != null)
                        mainVm.StatusMessage = $"Click to paint (brush {vm.BrushSize}×{vm.BrushSize}), drag for rectangle, Shift+drag for stroke";
                }

                CaptureMouse();
                e.Handled = true;
                return;
            }

            if (vm.SelectedTool == EditorTool.EyedropTexture)
            {
                Point mousePos = e.GetPosition(Surface);

                int tx = (int)Math.Floor(mousePos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mousePos.Y / MapConstants.TileSize);

                if (tx < 0 || tx >= MapConstants.TilesPerSide ||
                    ty < 0 || ty >= MapConstants.TilesPerSide)
                    return;

                int gameTx = MapConstants.TilesPerSide - 1 - tx;
                int gameTy = MapConstants.TilesPerSide - 1 - ty;

                var tex = new TexturesAccessor(MapDataService.Instance);

                // Compute offset using SAME logic as accessor
                int fx = MapConstants.TilesPerSide - 1 - ty;
                int fy = MapConstants.TilesPerSide - 1 - tx;
                int fileIndex = fy * MapConstants.TilesPerSide + fx;

                int off = 8 + (fileIndex * 6); // HeaderBytes=8, BytesPerTile=6

                byte[]? bytes = MapDataService.Instance.MapBytes;

                byte b0 = 0, b1 = 0, b2 = 0;

                if (bytes != null && off >= 0 && off + 2 < bytes.Length)
                {
                    b0 = bytes[off + 0];
                    b1 = bytes[off + 1];
                    b2 = bytes[off + 2];
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[EYEDROPPER] Mouse=({mousePos.X:0.0},{mousePos.Y:0.0}) " +
                    $"UI=({tx},{ty}) GAME=({gameTx},{gameTy}) " +
                    $"OFF={off} RAW=({b0},{b1},{b2})");

                if (tex.TryGetTileTextureSelection(tx, ty, out var g, out var n, out var r))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[EYEDROPPER] READ -> Group={g}, Tex={n}, Rot={r}");

                    vm.SelectedTextureGroup = g;
                    vm.SelectedTextureNumber = n;
                    vm.SelectedRotationIndex = r;

                    vm.SelectedTool = EditorTool.PaintTexture;

                    System.Diagnostics.Debug.WriteLine(
                        $"[EYEDROPPER] APPLY -> Group={vm.SelectedTextureGroup}, Tex={vm.SelectedTextureNumber}, Rot={vm.SelectedRotationIndex}");

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = $"Sampled tile [{gameTx},{gameTy}]";
                }

                e.Handled = true;
                return;
            }

            if (vm.SelectedTool == EditorTool.SetAltitude ||
                vm.SelectedTool == EditorTool.SampleAltitude ||
                vm.SelectedTool == EditorTool.ResetAltitude ||
                vm.SelectedTool == EditorTool.DetectRoof)
            {
                int tx = (int)Math.Floor(mouseDownPos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseDownPos.Y / MapConstants.TileSize);

                if (tx < 0 || tx >= MapConstants.TilesPerSide || ty < 0 || ty >= MapConstants.TilesPerSide)
                    return;

                if (_altitude == null)
                    _altitude = new AltitudeAccessor(MapDataService.Instance);

                switch (vm.SelectedTool)
                {
                    case EditorTool.SetAltitude:
                        vm.IsSettingAltitude = true;
                        vm.AltitudeSelectionStartX = tx;
                        vm.AltitudeSelectionStartY = ty;
                        vm.AltitudeSelectionEndX = tx;
                        vm.AltitudeSelectionEndY = ty;
                        CaptureMouse();

                        if (mainVm != null)
                            mainVm.StatusMessage = $"Drag to select area for altitude {vm.TargetAltitude}";

                        e.Handled = true;
                        return;

                    case EditorTool.SampleAltitude:
                        int sampledAlt = _altitude.ReadWorldAltitude(tx, ty);
                        vm.TargetAltitude = sampledAlt;

                        if (mainVm != null)
                            mainVm.StatusMessage = $"Sampled altitude {sampledAlt} (raw: {sampledAlt >> 3}) from cell [{tx},{ty}]";

                        e.Handled = true;
                        return;

                    case EditorTool.ResetAltitude:
                        vm.IsSettingAltitude = true;
                        vm.AltitudeSelectionStartX = tx;
                        vm.AltitudeSelectionStartY = ty;
                        vm.AltitudeSelectionEndX = tx;
                        vm.AltitudeSelectionEndY = ty;
                        CaptureMouse();

                        if (mainVm != null)
                            mainVm.StatusMessage = $"Drag to select area to clear altitude";

                        e.Handled = true;
                        return;

                    case EditorTool.DetectRoof:
                        if (mainVm != null)
                            mainVm.StatusMessage = $"Detect roof at tile [{tx},{ty}] - implement HeightsTab.OnDetectRoofClick()";

                        e.Handled = true;
                        return;
                }
            }

            if (!TryGetVertexIndexFromHit(mouseDownPos, out int vx, out int vy))
                return;

            int baseTx = vx - 1;
            int baseTy = vy - 1;

            switch (vm.SelectedTool)
            {
                case EditorTool.LevelHeight:
                    _levelSource = _heights.ReadHeight(baseTx, baseTy);
                    _isLeveling = true;
                    _lastLeveledTile = null;
                    CaptureMouse();

                    ForEachVertexInBrush(vx, vy, vm.BrushSize, (tx, ty) => ApplyHeightToTile(tx, ty, _levelSource));

                    if (mainVm != null)
                        mainVm.StatusMessage = $"Level: picked {_levelSource} at vertex [{vx},{vy}] (brush {vm.BrushSize}×{vm.BrushSize})";

                    e.Handled = true;
                    return;

                case EditorTool.RaiseHeight:
                case EditorTool.LowerHeight:
                    int step = Math.Max(1, vm.HeightStep);
                    bool isRaise = vm.SelectedTool == EditorTool.RaiseHeight;

                    ForEachVertexInBrush(vx, vy, vm.BrushSize, (tx, ty) =>
                    {
                        sbyte h = _heights.ReadHeight(tx, ty);
                        int temp = isRaise ? h + step : h - step;
                        temp = Math.Clamp(temp, sbyte.MinValue, sbyte.MaxValue);
                        if (temp != h) _heights.WriteHeight(tx, ty, (sbyte)temp);
                    });

                    HeightsOverlay?.InvalidateVisual();

                    if (mainVm != null)
                        mainVm.StatusMessage = $"Height {(isRaise ? "+=" : "-=")} {step} at vertex [{vx},{vy}] (brush {vm.BrushSize}×{vm.BrushSize})";

                    e.Handled = true;
                    return;

                case EditorTool.FlattenHeight:
                    ForEachVertexInBrush(vx, vy, vm.BrushSize, (tx, ty) =>
                    {
                        if (_heights.ReadHeight(tx, ty) != 0) _heights.WriteHeight(tx, ty, (sbyte)0);
                    });

                    HeightsOverlay?.InvalidateVisual();

                    if (mainVm != null)
                        mainVm.StatusMessage = $"Flattened to 0 at vertex [{vx},{vy}] (brush {vm.BrushSize}×{vm.BrushSize})";

                    e.Handled = true;
                    return;

                case EditorTool.DitchTemplate:
                    ApplyDitchTemplate(baseTx, baseTy);
                    e.Handled = true;
                    return;

                default:
                    return;
            }
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                return;

            if (DataContext is not MapViewModel vm || Surface == null || Scroller == null)
                return;

            e.Handled = true;

            var current = vm.Zoom;
            var factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
            var target = Math.Max(MinZoom, Math.Min(MaxZoom, current * factor));
            if (Math.Abs(target - current) < 0.0001) return;

            Point mouseOnContent = e.GetPosition(Surface);
            Point mouseOnViewport = e.GetPosition(Scroller);

            vm.Zoom = target;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                double newOffsetX = mouseOnContent.X * target - mouseOnViewport.X;
                double newOffsetY = mouseOnContent.Y * target - mouseOnViewport.Y;

                newOffsetX = Clamp(newOffsetX, 0, Math.Max(0, Scroller.ExtentWidth - Scroller.ViewportWidth));
                newOffsetY = Clamp(newOffsetY, 0, Math.Max(0, Scroller.ExtentHeight - Scroller.ViewportHeight));

                Scroller.ScrollToHorizontalOffset(newOffsetX);
                Scroller.ScrollToVerticalOffset(newOffsetY);
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is not MapViewModel vm || Surface == null) return;

            Point p = e.GetPosition(Surface);
            double gx = MapConstants.MapPixels - p.X;
            double gz = MapConstants.MapPixels - p.Y;

            int gameX = (int)Math.Max(0, Math.Min(MapConstants.MapPixels, Math.Round(gx)));
            int gameZ = (int)Math.Max(0, Math.Min(MapConstants.MapPixels, Math.Round(gz)));

            vm.CursorX = gameX;
            vm.CursorZ = gameZ;

            vm.CursorTileX = System.Math.Clamp(gameX / MapConstants.TileSize, 0, MapConstants.TilesPerSide - 1);
            vm.CursorTileZ = System.Math.Clamp(gameZ / MapConstants.TileSize, 0, MapConstants.TilesPerSide - 1);

            if (vm.IsRedrawingFacet)
            {
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);
                vm.UpdateFacetRedrawPreview(uiX, uiZ);
            }

            if (vm.IsMultiDrawingFacets)
            {
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);
                vm.UpdateFacetMultiDrawPreview(uiX, uiZ);
            }

            if (vm.IsPlacingLadder)
            {
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);
                vm.UpdateLadderPlacementPreview(uiX, uiZ);
            }

            if (vm.IsPlacingDoor)
            {
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);
                vm.UpdateDoorPlacementPreview(uiX, uiZ);
            }

            if (vm.IsPlacingCable)
            {
                int uiX = (int)Math.Clamp(p.X, 0, MapConstants.MapPixels);
                int uiZ = (int)Math.Clamp(p.Y, 0, MapConstants.MapPixels);
                vm.UpdateCablePlacementPreview(uiX, uiZ);
            }

            if (_isLeveling && e.LeftButton == MouseButtonState.Pressed && Surface != null)
            {
                if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                    return;

                Point mouseMovePos = e.GetPosition(Surface);
                if (!TryGetVertexIndexFromHit(mouseMovePos, out int vx2, out int vy2))
                    return;

                ForEachVertexInBrush(vx2, vy2, (DataContext as MapViewModel)?.BrushSize ?? 1, (tx, ty) =>
                {
                    ApplyHeightToTile(tx, ty, _levelSource);
                });

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = $"Level: set #{_levelSource} at vertex [{vx2},{vy2}] (brush {(DataContext as MapViewModel)?.BrushSize ?? 1}×{(DataContext as MapViewModel)?.BrushSize ?? 1})";
            }

            if (_isDrawingWalkableRect && e.LeftButton == MouseButtonState.Pressed && Surface != null)
            {
                if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                    return;

                Point mouseMovePos = e.GetPosition(Surface);
                int tx = (int)Math.Floor(mouseMovePos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseMovePos.Y / MapConstants.TileSize);

                tx = Math.Clamp(tx, 0, MapConstants.TilesPerSide - 1);
                ty = Math.Clamp(ty, 0, MapConstants.TilesPerSide - 1);

                vm.WalkableSelectionEndX = tx;
                vm.WalkableSelectionEndY = ty;

                var rect = vm.GetWalkableSelectionRect();
                if (rect.HasValue)
                {
                    int width = rect.Value.MaxX - rect.Value.MinX + 1;
                    int height = rect.Value.MaxY - rect.Value.MinY + 1;
                    int tileCount = width * height;

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = $"Walkable region: Max X = {rect.Value.MaxX} Min X = {rect.Value.MinX} | Max Y = {rect.Value.MaxY} Min Y = {rect.Value.MinY} | {width}×{height} ({tileCount} tiles)";
                }
            }

            if (vm.IsSettingAltitude && e.LeftButton == MouseButtonState.Pressed && Surface != null)
            {
                if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                    return;

                Point mouseMovePos = e.GetPosition(Surface);
                int tx = (int)Math.Floor(mouseMovePos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseMovePos.Y / MapConstants.TileSize);

                tx = Math.Clamp(tx, 0, MapConstants.TilesPerSide - 1);
                ty = Math.Clamp(ty, 0, MapConstants.TilesPerSide - 1);

                vm.AltitudeSelectionEndX = tx;
                vm.AltitudeSelectionEndY = ty;

                var rect = vm.GetAltitudeSelectionRect();
                if (rect.HasValue)
                {
                    int width = rect.Value.MaxX - rect.Value.MinX + 1;
                    int height = rect.Value.MaxY - rect.Value.MinY + 1;
                    int tileCount = width * height;

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    {
                        bool isReset = vm.SelectedTool == EditorTool.ResetAltitude;
                        string action = isReset ? "Clear" : $"Set altitude {vm.TargetAltitude}";
                        shell.StatusMessage = $"{action}: {width}×{height} ({tileCount} tiles)";
                    }
                }
            }

            if (vm.IsPaintingTexture && e.LeftButton == MouseButtonState.Pressed && Surface != null)
            {
                if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                    return;

                Point mouseMovePos = e.GetPosition(Surface);
                int tx = (int)Math.Floor(mouseMovePos.X / MapConstants.TileSize);
                int ty = (int)Math.Floor(mouseMovePos.Y / MapConstants.TileSize);

                tx = Math.Clamp(tx, 0, MapConstants.TilesPerSide - 1);
                ty = Math.Clamp(ty, 0, MapConstants.TilesPerSide - 1);

                if (_isTextureStrokePainting)
                {
                    ApplyTextureBrush(tx, ty, vm);

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = $"Stroke painting at [{tx},{ty}] (brush {vm.BrushSize}×{vm.BrushSize})";
                    return;
                }

                if (!_isTextureDragging)
                {
                    double dx = mouseMovePos.X - _textureMouseDownPos.X;
                    double dy = mouseMovePos.Y - _textureMouseDownPos.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist >= TextureDragThreshold)
                    {
                        _isTextureDragging = true;
                    }
                }

                if (_isTextureDragging)
                {
                    vm.TextureSelectionEndX = tx;
                    vm.TextureSelectionEndY = ty;

                    var rect = vm.GetTextureSelectionRect();
                    if (rect.HasValue)
                    {
                        int width = rect.Value.MaxX - rect.Value.MinX + 1;
                        int height = rect.Value.MaxY - rect.Value.MinY + 1;
                        int tileCount = width * height;

                        if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        {
                            shell.StatusMessage = $"Rectangle: {width}×{height} ({tileCount} tiles) with {vm.SelectedTextureGroup} #{vm.SelectedTextureNumber:000}";
                        }
                    }
                }
            }

            UpdateGhostHover(e.GetPosition(Surface));

            if (vm.IsPlacingPrim && Surface != null)
            {
                Point pos = e.GetPosition(Surface);
                int clampedX = Math.Clamp((int)pos.X, 0, MapConstants.MapPixels - 1);
                int clampedZ = Math.Clamp((int)pos.Y, 0, MapConstants.MapPixels - 1);

                SnapUiToVertexIfCtrl(ref clampedX, ref clampedZ);

                ObjectSpace.UiPixelsToGamePrim(clampedX, clampedZ, out int mapWhoIndex, out byte cellX, out byte cellZ);
                ObjectSpace.GamePrimToUiPixels(mapWhoIndex, cellX, cellZ, out int uiX, out int uiZ);
                ObjectSpace.GameIndexToUiRowCol(mapWhoIndex, out int uiRow, out int uiCol);

                vm.DragPreviewPrim = new PrimListItem
                {
                    Index = -1,
                    MapWhoIndex = mapWhoIndex,
                    MapWhoRow = uiRow,
                    MapWhoCol = uiCol,
                    PrimNumber = (byte)Math.Clamp(vm.PrimNumberToPlace, 0, 255),
                    Name = PrimCatalog.GetName(vm.PrimNumberToPlace),
                    Y = 0,
                    X = cellX,
                    Z = cellZ,
                    Yaw = 0,
                    Flags = 0,
                    InsideIndex = 0,
                    PixelX = uiX,
                    PixelZ = uiZ
                };
            }
        }

        private void OnPreviewMouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                return;

            if (_isLeveling)
            {
                _isLeveling = false;
                _lastLeveledTile = null;
                ReleaseMouseCapture();
                e.Handled = true;
            }

            if (_isDrawingWalkableRect && DataContext is MapViewModel vmWalk)
            {
                _isDrawingWalkableRect = false;
                ReleaseMouseCapture();
                WalkableDrawingCompleted?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            if (DataContext is MapViewModel vm && vm.IsSettingAltitude)
            {
                var rect = vm.GetAltitudeSelectionRect();
                if (rect.HasValue && _altitude != null)
                {
                    bool isReset = vm.SelectedTool == EditorTool.ResetAltitude;
                    int tileCount = 0;

                    for (int ty = rect.Value.MinY; ty <= rect.Value.MaxY; ty++)
                    {
                        for (int tx = rect.Value.MinX; tx <= rect.Value.MaxX; tx++)
                        {
                            if (isReset)
                            {
                                _altitude.ClearRoofTile(tx, ty);
                                ClearPapFlagsForTile(tx, ty);
                            }
                            else
                            {
                                _altitude.SetRoofTile(tx, ty, vm.TargetAltitude);

                                if (vm.TargetAltitude == 0)
                                    ClearPapFlagsForTile(tx, ty);
                            }

                            tileCount++;
                        }
                    }

                    HeightsOverlay?.InvalidateVisual();
                    CellFlagsOverlay?.RefreshFlags();

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    {
                        string action = isReset ? "Cleared" : $"Set altitude {vm.TargetAltitude} on";
                        shell.StatusMessage = $"{action} {tileCount} tiles";
                    }
                }

                vm.ClearAltitudeSelection();
                ReleaseMouseCapture();
                e.Handled = true;
            }

            if (DataContext is MapViewModel vmTex && vmTex.IsPaintingTexture)
            {
                var rect = vmTex.GetTextureSelectionRect();

                if (_isTextureStrokePainting)
                {
                    int tilesCount = _texturePaintedTiles?.Count ?? 0;
                    _texturePaintedTiles = null;
                    _isTextureStrokePainting = false;

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                        shell.StatusMessage = $"Stroke painted {tilesCount} tiles with {vmTex.SelectedTextureGroup} #{vmTex.SelectedTextureNumber:000}";
                }
                else if (_isTextureDragging && rect.HasValue)
                {
                    var acc = new TexturesAccessor(MapDataService.Instance);
                    int world = vmTex.TextureWorld;
                    int tileCount = 0;

                    for (int ty = rect.Value.MinY; ty <= rect.Value.MaxY; ty++)
                    {
                        for (int tx = rect.Value.MinX; tx <= rect.Value.MaxX; tx++)
                        {
                            acc.WriteTileTexture(tx, ty, vmTex.SelectedTextureGroup, vmTex.SelectedTextureNumber, vmTex.SelectedRotationIndex, world);
                            tileCount++;
                        }
                    }

                    TexturesChangeBus.Instance.NotifyChanged();

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    {
                        int width = rect.Value.MaxX - rect.Value.MinX + 1;
                        int height = rect.Value.MaxY - rect.Value.MinY + 1;
                        shell.StatusMessage = $"Rectangle painted {width}×{height} ({tileCount} tiles) with {vmTex.SelectedTextureGroup} #{vmTex.SelectedTextureNumber:000}";
                    }
                }
                else if (rect.HasValue)
                {
                    int centerTx = rect.Value.MinX;
                    int centerTy = rect.Value.MinY;

                    ApplyTextureBrush(centerTx, centerTy, vmTex);

                    int brushSize = vmTex.BrushSize;
                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    {
                        if (brushSize == 1)
                            shell.StatusMessage = $"Painted tile [{centerTx},{centerTy}] with {vmTex.SelectedTextureGroup} #{vmTex.SelectedTextureNumber:000}";
                        else
                            shell.StatusMessage = $"Brush painted {brushSize}×{brushSize} at [{centerTx},{centerTy}] with {vmTex.SelectedTextureGroup} #{vmTex.SelectedTextureNumber:000}";
                    }
                }

                _isTextureDragging = false;
                vmTex.ClearTextureSelection();
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void ApplyTextureBrush(int centerTx, int centerTy, MapViewModel vm)
        {
            var acc = new TexturesAccessor(MapDataService.Instance);
            int world = vm.TextureWorld;
            int brushSize = Math.Max(1, vm.BrushSize);
            int half = (brushSize - 1) / 2;

            for (int dy = -half; dy <= half; dy++)
            {
                for (int dx = -half; dx <= half; dx++)
                {
                    int tx = centerTx + dx;
                    int ty = centerTy + dy;

                    if (tx >= 0 && tx < MapConstants.TilesPerSide &&
                        ty >= 0 && ty < MapConstants.TilesPerSide)
                    {
                        if (_texturePaintedTiles != null)
                        {
                            if (_texturePaintedTiles.Contains((tx, ty)))
                                continue;
                            _texturePaintedTiles.Add((tx, ty));
                        }

                        acc.WriteTileTexture(tx, ty, vm.SelectedTextureGroup, vm.SelectedTextureNumber, vm.SelectedRotationIndex, world);
                    }
                }
            }

            TexturesChangeBus.Instance.NotifyChanged();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;

            if (vm.SelectedTool == EditorTool.PaintTexture && e.Key == Key.Space)
            {
                vm.SelectedRotationIndex = (vm.SelectedRotationIndex + 1) % 4;
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = $"Rotation: {vm.SelectedRotationIndex}  (0?180°, 1?90°, 2?0°, 3?270°)";
                e.Handled = true;
            }
            if (e.Key == Key.Escape && vm.IsPlacingPrim)
            {
                vm.CancelPlacePrim();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Add Prim canceled.";
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && vm.IsDrawingWalkable)
            {
                _isDrawingWalkableRect = false;
                vm.ClearWalkableSelection();
                ReleaseMouseCapture();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Walkable drawing cancelled.";
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && vm.IsRedrawingFacet)
            {
                vm.CancelFacetRedraw();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Facet redraw cancelled.";
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && vm.IsPlacingLadder)
            {
                vm.CancelLadderPlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Ladder placement cancelled.";
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && vm.IsPlacingCable)
            {
                vm.CancelCablePlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Cable placement cancelled.";
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && vm.IsMultiDrawingFacets)
            {
                vm.CancelFacetMultiDraw();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Facet drawing cancelled.";
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && vm.IsPaintingTexture)
            {
                _isTextureDragging = false;
                _isTextureStrokePainting = false;
                _texturePaintedTiles = null;
                vm.ClearTextureSelection();
                ReleaseMouseCapture();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Texture painting cancelled.";
                e.Handled = true;
                return;
            }
        }

        private void OnLostMouseCapture(object? sender, MouseEventArgs e)
        {
            _isLeveling = false;
            _lastLeveledTile = null;

            _isTextureDragging = false;
            _isTextureStrokePainting = false;
            _texturePaintedTiles = null;

            if (_isDrawingWalkableRect)
            {
                _isDrawingWalkableRect = false;
                if (DataContext is MapViewModel vm)
                    vm.ClearWalkableSelection();
            }

            if (DataContext is MapViewModel vmTex && vmTex.IsPaintingTexture)
            {
                vmTex.ClearTextureSelection();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            GhostLayer?.SetHoverTile(null, null);
        }

        private void ApplyHeightToTile(int tx, int ty, sbyte value)
        {
            sbyte h = _heights.ReadHeight(tx, ty);
            if (h != value)
            {
                _heights.WriteHeight(tx, ty, value);
                HeightsOverlay?.InvalidateVisual();
            }
            _lastLeveledTile = (tx, ty);
        }

        private void OnPreviewMouseRightButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                return;

            if (DataContext is MapViewModel vmWalk && vmWalk.IsDrawingWalkable)
            {
                _isDrawingWalkableRect = false;
                vmWalk.ClearWalkableSelection();
                ReleaseMouseCapture();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Walkable drawing cancelled.";
                e.Handled = true;
                return;
            }

            if (DataContext is MapViewModel vmTex && vmTex.IsPaintingTexture)
            {
                _isTextureDragging = false;
                _isTextureStrokePainting = false;
                _texturePaintedTiles = null;
                vmTex.ClearTextureSelection();
                ReleaseMouseCapture();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                    shell.StatusMessage = "Texture painting cancelled.";
                e.Handled = true;
                return;
            }

            if (_isLeveling)
            {
                _isLeveling = false;
                _lastLeveledTile = null;
                if (IsMouseCaptured) ReleaseMouseCapture();
            }

            if (DataContext is MapViewModel vmFacet && vmFacet.IsRedrawingFacet)
            {
                vmFacet.CancelFacetRedraw();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shellFacet)
                    shellFacet.StatusMessage = "Facet redraw cancelled.";
                e.Handled = true;
                return;
            }

            if (DataContext is MapViewModel vmMultiDraw && vmMultiDraw.IsMultiDrawingFacets)
            {
                vmMultiDraw.FinishFacetMultiDraw();
                e.Handled = true;
                return;
            }

            if (DataContext is MapViewModel vmLadder && vmLadder.IsPlacingLadder)
            {
                vmLadder.CancelLadderPlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shellLadder)
                    shellLadder.StatusMessage = "Ladder placement cancelled.";
                e.Handled = true;
                return;
            }

            if (DataContext is MapViewModel vmCable && vmCable.IsPlacingCable)
            {
                vmCable.CancelCablePlacement();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shellCable)
                    shellCable.StatusMessage = "Cable placement cancelled.";
                e.Handled = true;
                return;
            }

            if (DataContext is MapViewModel vm0 && vm0.IsPlacingPrim)
            {
                vm0.CancelPlacePrim();
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell0)
                    shell0.StatusMessage = "Add Prim canceled.";
                e.Handled = true;
                return;
            }

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel vm)
            {
                vm.Map.SelectedTool = EditorTool.None;
                bool cleared = false;
                if (vm.Map.SelectedPrim != null) { vm.Map.SelectedPrim = null; cleared = true; }

                UpdateOverlayHitTesting();

                vm.StatusMessage = cleared ? "Selection cleared." : "Action cleared.";
            }
            e.Handled = true;

            GhostLayer?.SetHoverTile(null, null);
        }

        private void ApplyDitchTemplate(int cx, int cy)
        {
            void SetIfInBounds(int tx, int ty, sbyte val)
            {
                if (tx >= 0 && tx < MapConstants.TilesPerSide &&
                    ty >= 0 && ty < MapConstants.TilesPerSide)
                {
                    var cur = _heights.ReadHeight(tx, ty);
                    if (cur != val) _heights.WriteHeight(tx, ty, val);
                }
            }

            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 1; dx++)
                {
                    SetIfInBounds(cx + dx, cy + dy, -32);
                }
            }

            var ramp = new[]
            {
                (-2, (sbyte)-26),
                (-1, (sbyte)-20),
                ( 0, (sbyte)-13),
                ( 1, (sbyte)-7),
                ( 2, (sbyte)0)
            };

            foreach (var (dy, val) in ramp)
            {
                SetIfInBounds(cx + 2, cy + dy, val);
                SetIfInBounds(cx + 3, cy + dy, val);
            }

            HeightsOverlay?.InvalidateVisual();

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                shell.StatusMessage = $"Stamped Ditch at [{cx},{cy}]";
        }

        public void GoToTileCenter(int tx, int ty)
        {
            if (Scroller == null || Surface == null) return;
            if (DataContext is not MapViewModel vm) return;

            double cx = (tx + 0.5) * MapConstants.TileSize;
            double cy = (ty + 0.5) * MapConstants.TileSize;

            double z = vm.Zoom;
            double sx = cx * z;
            double sy = cy * z;

            double targetX = sx - Scroller.ViewportWidth / 2.0;
            double targetY = sy - Scroller.ViewportHeight / 2.0;

            targetX = Clamp(targetX, 0, System.Math.Max(0, Scroller.ExtentWidth - Scroller.ViewportWidth));
            targetY = Clamp(targetY, 0, System.Math.Max(0, Scroller.ExtentHeight - Scroller.ViewportHeight));

            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                Scroller.ScrollToHorizontalOffset(targetX);
                Scroller.ScrollToVerticalOffset(targetY);
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private const double VertexHitRadius = 16.0;

        private static bool TryGetVertexIndexFromHit(Point p, out int vx, out int vy)
        {
            vx = vy = -1;

            int candVx = (int)Math.Round(p.X / MapConstants.TileSize);
            int candVy = (int)Math.Round(p.Y / MapConstants.TileSize);

            if (candVx < 1 || candVy < 1 || candVx > MapConstants.TilesPerSide || candVy > MapConstants.TilesPerSide)
                return false;

            double cx = candVx * MapConstants.TileSize;
            double cy = candVy * MapConstants.TileSize;

            double dx = p.X - cx, dy = p.Y - cy;
            if ((dx * dx + dy * dy) > (VertexHitRadius * VertexHitRadius))
                return false;

            vx = candVx;
            vy = candVy;
            return true;
        }

        private static void ForEachVertexInBrush(int vx, int vy, int brushSize, Action<int, int> applyByTile)
        {
            int size = Math.Clamp(brushSize, 1, 10);
            int half = size / 2;

            int startTx = (vx - 1) - half;
            int startTy = (vy - 1) - half;

            for (int dy = 0; dy < size; dy++)
            {
                for (int dx = 0; dx < size; dx++)
                {
                    int tx = startTx + dx;
                    int ty = startTy + dy;

                    if (tx >= 0 && tx < MapConstants.TilesPerSide &&
                        ty >= 0 && ty < MapConstants.TilesPerSide)
                    {
                        applyByTile(tx, ty);
                    }
                }
            }
        }

        private void UpdateGhostHover(Point p)
        {
            if (GhostLayer == null) return;
            if (DataContext is not MapViewModel vm) return;

            if (vm.SelectedTool != EditorTool.PaintTexture)
            {
                GhostLayer.SetHoverTile(null, null);
                return;
            }

            int tx = (int)Math.Floor(p.X / MapConstants.TileSize);
            int ty = (int)Math.Floor(p.Y / MapConstants.TileSize);

            if (tx < 0 || tx >= MapConstants.TilesPerSide || ty < 0 || ty >= MapConstants.TilesPerSide)
                GhostLayer.SetHoverTile(null, null);
            else
                GhostLayer.SetHoverTile(tx, ty);
        }

        public void CenterOnPixel(int px, int pz)
        {
            if (Scroller == null || Surface == null || DataContext is not MapViewModel vm) return;

            double z = vm.Zoom;
            double targetX = px * z - Scroller.ViewportWidth / 2.0;
            double targetY = pz * z - Scroller.ViewportHeight / 2.0;

            targetX = Math.Max(0, Math.Min(targetX, Scroller.ExtentWidth - Scroller.ViewportWidth));
            targetY = Math.Max(0, Math.Min(targetY, Scroller.ExtentHeight - Scroller.ViewportHeight));

            Scroller.ScrollToHorizontalOffset(targetX);
            Scroller.ScrollToVerticalOffset(targetY);
        }

        private static void SnapUiToVertexIfCtrl(ref int uiX, ref int uiZ)
        {
            if (!IsCtrlDown())
                return;

            int size = MapConstants.TileSize;

            uiX = (int)(Math.Round(uiX / (double)size) * size);
            uiZ = (int)(Math.Round(uiZ / (double)size) * size);

            uiX = Math.Clamp(uiX, 0, MapConstants.MapPixels - 1);
            uiZ = Math.Clamp(uiZ, 0, MapConstants.MapPixels - 1);
        }
        private void RefreshCellFlagsOverlay()
        {
            CellFlagsOverlay?.RefreshFlags();
        }
        private void ClearPapFlagsForTile(int tx, int ty)
        {
            byte[]? bytes = MapDataService.Instance.MapBytes;
            if (bytes == null || bytes.Length < 8)
                return;

            const int HeaderBytes = 8;
            const int BytesPerTile = 6;
            const int TilesPerSide = 128;
            const int OffFlags = 2;

            // Match the same UI->file mapping style already used elsewhere in MapView.
            int fx = TilesPerSide - 1 - ty;
            int fy = TilesPerSide - 1 - tx;
            int fileIndex = fy * TilesPerSide + fx;

            int off = HeaderBytes + (fileIndex * BytesPerTile) + OffFlags;

            if (off < 0 || off + 1 >= bytes.Length)
                return;

            bytes[off] = 0;
            bytes[off + 1] = 0;
        }
    }
}