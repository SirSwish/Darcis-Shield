using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosMapEditor.Models.Viewport3D;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Heights;
using UrbanChaosMapEditor.Services.Prims;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.Services.Textures;
using UrbanChaosMapEditor.Services.Viewport3D;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Viewport3D
{
    public sealed class Viewport3DOverlayLayer
    {
        public Viewport3DOverlayLayer(string name, ModelVisual3D visual, bool isVisible = true)
        {
            Name = name;
            Visual = visual;
            IsVisible = isVisible;
        }

        public Viewport3DOverlayLayer(
            string name,
            ModelVisual3D visual,
            Func<ViewportCullRegion, Model3D?> rebuildContent,
            bool isVisible = true)
            : this(name, visual, isVisible)
        {
            RebuildContent = rebuildContent;
        }

        public string Name { get; }
        public ModelVisual3D Visual { get; }
        public bool IsVisible { get; set; }
        public Func<ViewportCullRegion, Model3D?>? RebuildContent { get; }
    }

    public partial class Viewport3DWindow : Window
    {
        private enum LayerKind
        {
            Textures,
            Objects,
            Buildings,
            RoofTiles
        }

        private static readonly string[] TexturePropCandidates = { "ShowTextures" };
        private static readonly string[] ObjectPropCandidates = { "ShowObjects", "ShowPrims" };
        private static readonly string[] BuildingPropCandidates = { "ShowBuildings" };
        private static readonly string[] RoofTilePropCandidates = { "ShowRoofTiles", "ShowRoofs", "ShowRoofFace4", "ShowRf4" };

        private readonly Dictionary<LayerKind, bool> _localLayerState = new()
        {
            [LayerKind.Textures] = true,
            [LayerKind.Objects] = true,
            [LayerKind.Buildings] = true,
            [LayerKind.RoofTiles] = true
        };

        private readonly HashSet<Key> _held = new();

        private double _speed = 512.0;
        private const double MinSpeed = 64.0;
        private const double MaxSpeed = 8192.0;
        private const double LookRateRad = 1.6;

        private readonly ModelVisual3D _terrainVisual = new();
        private readonly ModelVisual3D _facetsVisual = new();
        private readonly ModelVisual3D _cablesVisual = new();
        private readonly ModelVisual3D _primsVisual = new();
        private readonly ModelVisual3D _roofTilesVisual = new();
        private readonly List<Viewport3DOverlayLayer> _overlayLayers = new();

        private SceneBuilder? _builder;
        private DateTime _lastTick;
        private readonly DispatcherTimer _cullRefreshTimer;
        private ViewportCullRegion? _lastBuiltCull;
        private readonly double? _cullDistance;
        private readonly double? _cullMargin;

        private object? _mapVmObject;
        private INotifyPropertyChanged? _mapVmNotify;
        private bool _syncingLayerButtons;
        private CheckBox? _ambientFilterCheckBox;
        private Color _ambientFilterColor = Color.FromArgb(0, 0, 0, 0);

        public Viewport3DWindow()
            : this(null)
        {
        }

        public Viewport3DWindow(
            IEnumerable<Viewport3DOverlayLayer>? overlayLayers,
            double? cullDistance = null,
            double? cullMargin = null)
        {
            InitializeComponent();

            _cullDistance = cullDistance;
            _cullMargin = cullMargin;
            if (_cullDistance.HasValue)
                Camera.FarPlaneDistance = Math.Max(256.0, _cullDistance.Value + (_cullMargin ?? CameraConstants.ViewportCullMargin) * 2.0);

            _cullRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(CameraConstants.ViewportCullRefreshMilliseconds)
            };
            _cullRefreshTimer.Tick += OnCullRefreshTimerTick;

            SceneRoot.Children.Add(_terrainVisual);
            SceneRoot.Children.Add(_facetsVisual);
            SceneRoot.Children.Add(_cablesVisual);
            SceneRoot.Children.Add(_primsVisual);
            SceneRoot.Children.Add(_roofTilesVisual);

            if (overlayLayers != null)
            {
                foreach (var layer in overlayLayers)
                    AddOverlayLayer(layer);
            }
        }

        private void AddOverlayLayer(Viewport3DOverlayLayer layer)
        {
            _overlayLayers.Add(layer);

            if (layer.IsVisible && !SceneRoot.Children.Contains(layer.Visual))
                SceneRoot.Children.Add(layer.Visual);

            var checkBox = new CheckBox
            {
                Content = layer.Name,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 2, 0, 2),
                IsChecked = layer.IsVisible,
                Tag = layer
            };
            checkBox.Checked += OverlayLayerToggle_Changed;
            checkBox.Unchecked += OverlayLayerToggle_Changed;
            LayerPanel.Children.Add(checkBox);
        }

        private void OverlayLayerToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox { Tag: Viewport3DOverlayLayer layer })
                return;

            layer.IsVisible = ((CheckBox)sender).IsChecked == true;

            if (layer.IsVisible)
            {
                if (layer.RebuildContent != null)
                    layer.Visual.Content = layer.RebuildContent(CreateCullRegion());

                if (!SceneRoot.Children.Contains(layer.Visual))
                    SceneRoot.Children.Add(layer.Visual);
            }
            else
            {
                SceneRoot.Children.Remove(layer.Visual);
            }
        }

        public void ConfigureAmbientFilter(Color color, bool isEnabled = true)
        {
            _ambientFilterColor = color;

            if (_ambientFilterCheckBox == null)
            {
                _ambientFilterCheckBox = new CheckBox
                {
                    Content = "Ambient Filter",
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 8, 0, 2),
                    IsChecked = isEnabled
                };
                _ambientFilterCheckBox.Checked += AmbientFilterToggle_Changed;
                _ambientFilterCheckBox.Unchecked += AmbientFilterToggle_Changed;
                LayerPanel.Children.Add(_ambientFilterCheckBox);
            }
            else
            {
                _ambientFilterCheckBox.IsChecked = isEnabled;
            }

            ApplyAmbientFilter(isEnabled);
        }

        public bool IsAmbientFilterEnabled => _ambientFilterCheckBox?.IsChecked == true;

        private void AmbientFilterToggle_Changed(object sender, RoutedEventArgs e)
        {
            ApplyAmbientFilter(_ambientFilterCheckBox?.IsChecked == true);
        }

        private void ApplyAmbientFilter(bool enabled)
        {
            if (!enabled)
            {
                AmbientSceneLight.Color = Color.FromRgb(0x60, 0x60, 0x60);
                PrimaryDirectionalLight.Color = Color.FromRgb(0xC8, 0xC8, 0xC8);
                SecondaryDirectionalLight.Color = Color.FromRgb(0x40, 0x40, 0x40);
                AmbientFilterOverlay.Visibility = Visibility.Collapsed;
                AmbientFilterOverlay.Fill = null;
                return;
            }

            // The Light Editor's Preview D3D Lighting path multiplies source pixels by
            // the ambient swatch colour. In WPF 3D the closest cheap approximation is to
            // let ambient light carry the colour and remove white directional fill.
            AmbientSceneLight.Color = Color.FromRgb(_ambientFilterColor.R, _ambientFilterColor.G, _ambientFilterColor.B);
            PrimaryDirectionalLight.Color = Colors.Black;
            SecondaryDirectionalLight.Color = Colors.Black;
            AmbientFilterOverlay.Visibility = Visibility.Collapsed;
            AmbientFilterOverlay.Fill = null;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Focus();
            Viewport.Focus();

            _builder = new SceneBuilder();

            HookMapVm();
            SyncLayerButtonsFromSource();

            Camera3DService.Instance.PositionChanged += OnCameraChanged;
            Camera3DService.Instance.OrientationChanged += OnCameraChanged;

            MapDataService.Instance.MapLoaded += OnMapWholesaleChanged;
            MapDataService.Instance.MapCleared += OnMapClearedHandler;
            MapDataService.Instance.MapBytesReset += OnMapWholesaleChanged;

            HeightsChangeBus.Instance.TileChanged += OnTerrainChanged;
            HeightsChangeBus.Instance.RegionChanged += OnTerrainChanged;
            TexturesChangeBus.Instance.Changed += OnTerrainChanged;

            AltitudeChangeBus.Instance.TileChanged += OnAltitudeTileChanged;
            AltitudeChangeBus.Instance.RegionChanged += OnAltitudeRegionChanged;
            AltitudeChangeBus.Instance.AllChanged += OnAltitudeAllChanged;

            BuildingsChangeBus.Instance.Changed += OnFacetsChanged;
            RoofsChangeBus.Instance.Changed += OnRoofTilesChanged;

            ObjectsChangeBus.Instance.Changed += OnPrimsChanged;

            SyncCameraFromService();
            RebuildAll();

            _lastTick = DateTime.UtcNow;
            CompositionTarget.Rendering += OnRender;
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            _cullRefreshTimer.Stop();
            _cullRefreshTimer.Tick -= OnCullRefreshTimerTick;

            CompositionTarget.Rendering -= OnRender;

            Camera3DService.Instance.PositionChanged -= OnCameraChanged;
            Camera3DService.Instance.OrientationChanged -= OnCameraChanged;

            MapDataService.Instance.MapLoaded -= OnMapWholesaleChanged;
            MapDataService.Instance.MapCleared -= OnMapClearedHandler;
            MapDataService.Instance.MapBytesReset -= OnMapWholesaleChanged;

            HeightsChangeBus.Instance.TileChanged -= OnTerrainChanged;
            HeightsChangeBus.Instance.RegionChanged -= OnTerrainChanged;
            TexturesChangeBus.Instance.Changed -= OnTerrainChanged;

            AltitudeChangeBus.Instance.TileChanged -= OnAltitudeTileChanged;
            AltitudeChangeBus.Instance.RegionChanged -= OnAltitudeRegionChanged;
            AltitudeChangeBus.Instance.AllChanged -= OnAltitudeAllChanged;

            BuildingsChangeBus.Instance.Changed -= OnFacetsChanged;
            RoofsChangeBus.Instance.Changed -= OnRoofTilesChanged;

            ObjectsChangeBus.Instance.Changed -= OnPrimsChanged;

            UnhookMapVm();

            if (IsMouseCaptured) ReleaseMouseCapture();
        }

        private void HookMapVm()
        {
            UnhookMapVm();

            try
            {
                if (Application.Current?.MainWindow?.DataContext is MainWindowViewModel shell &&
                    shell.Map is INotifyPropertyChanged notify)
                {
                    _mapVmObject = shell.Map;
                    _mapVmNotify = notify;
                    _mapVmNotify.PropertyChanged += OnMapVmPropertyChanged;
                }
            }
            catch
            {
                _mapVmObject = null;
                _mapVmNotify = null;
            }
        }

        private void UnhookMapVm()
        {
            if (_mapVmNotify != null)
            {
                try { _mapVmNotify.PropertyChanged -= OnMapVmPropertyChanged; }
                catch { }
            }

            _mapVmNotify = null;
            _mapVmObject = null;
        }

        private void OnMapVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!IsLayerPropertyName(e.PropertyName))
                return;

            Dispatcher.BeginInvoke(() =>
            {
                SyncLayerButtonsFromSource();
                RebuildAll();
            }, DispatcherPriority.Render);
        }

        private static bool IsLayerPropertyName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            return MatchesAny(name, TexturePropCandidates)
                || MatchesAny(name, ObjectPropCandidates)
                || MatchesAny(name, BuildingPropCandidates)
                || MatchesAny(name, RoofTilePropCandidates);
        }

        private static bool MatchesAny(string name, string[] candidates)
        {
            foreach (string c in candidates)
            {
                if (string.Equals(name, c, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private bool IsLayerEnabled(LayerKind kind)
        {
            var (obj, candidates) = GetLayerSource(kind);

            if (obj != null)
            {
                foreach (string propName in candidates)
                {
                    var prop = obj.GetType().GetProperty(propName);
                    if (prop != null && prop.PropertyType == typeof(bool))
                    {
                        try
                        {
                            return (bool)(prop.GetValue(obj) ?? false);
                        }
                        catch
                        {
                            break;
                        }
                    }
                }
            }

            return _localLayerState[kind];
        }

        private void SetLayerEnabled(LayerKind kind, bool value)
        {
            var (obj, candidates) = GetLayerSource(kind);

            if (obj != null)
            {
                foreach (string propName in candidates)
                {
                    var prop = obj.GetType().GetProperty(propName);
                    if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
                    {
                        try
                        {
                            prop.SetValue(obj, value);
                            _localLayerState[kind] = value;
                            return;
                        }
                        catch
                        {
                            // fall through to next candidate/local fallback
                        }
                    }
                }
            }

            _localLayerState[kind] = value;
        }

        private (object? obj, string[] candidates) GetLayerSource(LayerKind kind)
        {
            return kind switch
            {
                LayerKind.Textures => (_mapVmObject, TexturePropCandidates),
                LayerKind.Objects => (_mapVmObject, ObjectPropCandidates),
                LayerKind.Buildings => (_mapVmObject, BuildingPropCandidates),
                LayerKind.RoofTiles => (_mapVmObject, RoofTilePropCandidates),
                _ => (null, Array.Empty<string>())
            };
        }

        private void SyncLayerButtonsFromSource()
        {
            _syncingLayerButtons = true;
            try
            {
                ChkTextures.IsChecked = IsLayerEnabled(LayerKind.Textures);
                ChkObjects.IsChecked = IsLayerEnabled(LayerKind.Objects);
                ChkBuildings.IsChecked = IsLayerEnabled(LayerKind.Buildings);
                ChkRoofTiles.IsChecked = IsLayerEnabled(LayerKind.RoofTiles);
            }
            finally
            {
                _syncingLayerButtons = false;
            }
        }

        private void LayerToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncingLayerButtons)
                return;

            if (sender is not CheckBox cb)
                return;

            bool value = cb.IsChecked == true;

            if (cb == ChkTextures)
                SetLayerEnabled(LayerKind.Textures, value);
            else if (cb == ChkObjects)
                SetLayerEnabled(LayerKind.Objects, value);
            else if (cb == ChkBuildings)
                SetLayerEnabled(LayerKind.Buildings, value);
            else if (cb == ChkRoofTiles)
                SetLayerEnabled(LayerKind.RoofTiles, value);
            else
                return;

            SyncLayerButtonsFromSource();
            RebuildAll();
        }

        private void OnCameraChanged(object? sender, EventArgs e)
        {
            SyncCameraFromService();
            QueueCullRefreshIfNeeded();
        }

        private void QueueCullRefreshIfNeeded()
        {
            if (_builder is null)
                return;

            var current = CreateCullRegion();
            if (_lastBuiltCull.HasValue && current.IsSameBucket(_lastBuiltCull.Value))
                return;

            if (!_cullRefreshTimer.IsEnabled)
                _cullRefreshTimer.Start();
        }

        private void OnCullRefreshTimerTick(object? sender, EventArgs e)
        {
            _cullRefreshTimer.Stop();
            RebuildAll();
        }

        private void OnMapClearedHandler(object? sender, EventArgs e)
        {
            _terrainVisual.Content = null;
            _facetsVisual.Content = null;
            _cablesVisual.Content = null;
            _primsVisual.Content = null;
            _roofTilesVisual.Content = null;
            HudStatus.Text = "(no map loaded)";
        }

        private void OnMapWholesaleChanged(object? sender, EventArgs e) => RebuildAll();
        private void OnTerrainChanged(object? sender, EventArgs e) => RebuildTerrain();
        private void OnFacetsChanged(object? sender, EventArgs e) { RebuildFacets(); RebuildCables(); }
        private void OnPrimsChanged(object? sender, EventArgs e) => RebuildPrims();
        private void OnRoofTilesChanged(object? sender, EventArgs e) => RebuildRoofTiles();

        private void OnAltitudeTileChanged(int tx, int ty) => Dispatcher.Invoke(RebuildTerrain);
        private void OnAltitudeRegionChanged(int minTx, int minTy, int maxTx, int maxTy) => Dispatcher.Invoke(RebuildTerrain);
        private void OnAltitudeAllChanged() => Dispatcher.Invoke(RebuildTerrain);

        private void RebuildAll()
        {
            var cull = CreateCullRegion();
            _lastBuiltCull = cull;

            RebuildTerrain(cull);
            RebuildFacets(cull);
            RebuildCables(cull);
            RebuildPrims(cull);
            RebuildRoofTiles(cull);
            RebuildOverlayLayers(cull);
        }

        private void RebuildOverlayLayers(ViewportCullRegion cull)
        {
            foreach (var layer in _overlayLayers)
            {
                if (!layer.IsVisible || layer.RebuildContent == null)
                    continue;

                layer.Visual.Content = layer.RebuildContent(cull);
            }
        }

        public void RebuildCullAwareOverlayLayers()
        {
            RebuildOverlayLayers(CreateCullRegion());
        }

        private void RebuildTerrain(ViewportCullRegion? cull = null)
        {
            if (_builder is null) return;

            if (!IsLayerEnabled(LayerKind.Textures))
            {
                _terrainVisual.Content = null;
                UpdateStatus();
                return;
            }

            try
            {
                cull ??= CreateCullRegion();
                _lastBuiltCull = cull;
                _terrainVisual.Content = _builder.BuildTerrain(cull);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                HudStatus.Text = $"terrain error: {ex.Message}";
            }
        }

        private void RebuildFacets(ViewportCullRegion? cull = null)
        {
            if (_builder is null) return;

            if (!IsLayerEnabled(LayerKind.Buildings))
            {
                _facetsVisual.Content = null;
                UpdateStatus();
                return;
            }

            try
            {
                cull ??= CreateCullRegion();
                _lastBuiltCull = cull;
                _facetsVisual.Content = _builder.BuildFacets(cull);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                HudStatus.Text = $"facets error: {ex.Message}";
            }
        }

        private void RebuildCables(ViewportCullRegion? cull = null)
        {
            if (_builder is null) return;

            if (!IsLayerEnabled(LayerKind.Buildings))
            {
                _cablesVisual.Content = null;
                return;
            }

            try
            {
                cull ??= CreateCullRegion();
                _lastBuiltCull = cull;
                _cablesVisual.Content = _builder.BuildCables(cull);
            }
            catch (Exception ex)
            {
                HudStatus.Text = $"cables error: {ex.Message}";
            }
        }

        private void RebuildPrims(ViewportCullRegion? cull = null)
        {
            if (_builder is null) return;

            if (!IsLayerEnabled(LayerKind.Objects))
            {
                _primsVisual.Content = null;
                UpdateStatus();
                return;
            }

            try
            {
                cull ??= CreateCullRegion();
                _lastBuiltCull = cull;
                _primsVisual.Content = _builder.BuildPrims(cull);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                HudStatus.Text = $"prims error: {ex.Message}";
            }
        }

        private void RebuildRoofTiles(ViewportCullRegion? cull = null)
        {
            if (_builder is null) return;

            if (!IsLayerEnabled(LayerKind.RoofTiles))
            {
                _roofTilesVisual.Content = null;
                UpdateStatus();
                return;
            }

            try
            {
                cull ??= CreateCullRegion();
                _lastBuiltCull = cull;
                _roofTilesVisual.Content = _builder.BuildRoofTiles(cull);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                HudStatus.Text = $"roof tiles error: {ex.Message}";
            }
        }

        private void UpdateStatus()
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                HudStatus.Text = "(no map loaded)";
                return;
            }

            HudStatus.Text =
                $"layers: tex={(IsLayerEnabled(LayerKind.Textures) ? "on" : "off")}  " +
                $"obj={(IsLayerEnabled(LayerKind.Objects) ? "on" : "off")}  " +
                $"bld={(IsLayerEnabled(LayerKind.Buildings) ? "on" : "off")}  " +
                $"roof={(IsLayerEnabled(LayerKind.RoofTiles) ? "on" : "off")}";
        }

        private ViewportCullRegion CreateCullRegion()
        {
            double aspect = Viewport.ActualHeight > 1.0
                ? Viewport.ActualWidth / Viewport.ActualHeight
                : 1.0;

            if (_cullDistance.HasValue || _cullMargin.HasValue)
            {
                return ViewportCullRegion.FromCamera(
                    Camera3DService.Instance.Camera,
                    Camera.FieldOfView,
                    aspect,
                    _cullDistance ?? CameraConstants.ViewportCullDistance,
                    _cullMargin ?? CameraConstants.ViewportCullMargin);
            }

            return ViewportCullRegion.FromCamera(
                    Camera3DService.Instance.Camera,
                    Camera.FieldOfView,
                    aspect);
        }

        private void SyncCameraFromService()
        {
            var cam = Camera3DService.Instance.Camera;
            Camera.Position = new Point3D(cam.X, cam.Y, cam.Z);
            Camera.LookDirection = cam.Forward;
            Camera.UpDirection = new Vector3D(0, 1, 0);
            UpdateHud(cam);
        }

        private void UpdateHud(Camera3D cam)
        {
            HudCoords.Text = $"X: {cam.X,7:F1}   Y: {cam.Y,7:F1}   Z: {cam.Z,7:F1}";
            HudOrient.Text = $"Yaw: {cam.YawRad * 180.0 / Math.PI,6:F1}°   Pitch: {cam.PitchRad * 180.0 / Math.PI,5:F1}°   Speed: {_speed:F0}";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Close(); return; }
            _held.Add(e.Key);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e) => _held.Remove(e.Key);

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Viewport.Focus();
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e) { }
        private void Viewport_MouseMove(object sender, MouseEventArgs e) { }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double step = e.Delta > 0 ? 1.25 : 0.8;
            _speed = Math.Max(MinSpeed, Math.Min(MaxSpeed, _speed * step));
            UpdateHud(Camera3DService.Instance.Camera);
            e.Handled = true;
        }

        private void OnRender(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            double dt = (now - _lastTick).TotalSeconds;
            _lastTick = now;
            if (dt <= 0 || dt > 0.25)
            {
                dt = Math.Min(dt, 0.016);
                if (dt <= 0) return;
            }

            var cam = Camera3DService.Instance.Camera;

            double yawDelta = 0, pitchDelta = 0;
            if (_held.Contains(Key.Left)) yawDelta += 1;
            if (_held.Contains(Key.Right)) yawDelta -= 1;
            if (_held.Contains(Key.Up)) pitchDelta += 1;
            if (_held.Contains(Key.Down)) pitchDelta -= 1;

            if (yawDelta != 0 || pitchDelta != 0)
            {
                double rate = LookRateRad;
                if (_held.Contains(Key.LeftShift) || _held.Contains(Key.RightShift)) rate *= 2.0;

                double newYaw = cam.YawRad + yawDelta * rate * dt;
                double newPitch = cam.PitchRad + pitchDelta * rate * dt;
                Camera3DService.Instance.SetOrientation(newYaw, newPitch);
                cam = Camera3DService.Instance.Camera;
            }

            double mx = 0, mz = 0, my = 0;
            if (_held.Contains(Key.W)) mz += 1;
            if (_held.Contains(Key.S)) mz -= 1;
            if (_held.Contains(Key.A)) mx -= 1;
            if (_held.Contains(Key.D)) mx += 1;
            if (_held.Contains(Key.Q)) my -= 1;
            if (_held.Contains(Key.E)) my += 1;
            if (mx == 0 && mz == 0 && my == 0) return;

            double speed = _speed;
            if (_held.Contains(Key.LeftShift) || _held.Contains(Key.RightShift)) speed *= 3.0;

            double sy = Math.Sin(cam.YawRad);
            double cy = Math.Cos(cam.YawRad);
            Vector3D fwdFlat = new Vector3D(sy, 0, cy);
            Vector3D right = new Vector3D(-cy, 0, sy);
            Vector3D up = new Vector3D(0, 1, 0);

            Vector3D delta = fwdFlat * mz + right * mx + up * my;
            double len = delta.Length;
            if (len > 0) delta *= (speed * dt) / len;

            Camera3DService.Instance.SetPosition(cam.X + delta.X, cam.Y + delta.Y, cam.Z + delta.Z);
        }
    }
}
