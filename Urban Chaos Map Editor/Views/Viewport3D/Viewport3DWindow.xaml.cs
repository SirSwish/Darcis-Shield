using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
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

        private SceneBuilder? _builder;
        private DateTime _lastTick;

        private object? _mapVmObject;
        private INotifyPropertyChanged? _mapVmNotify;
        private bool _syncingLayerButtons;

        public Viewport3DWindow()
        {
            InitializeComponent();

            SceneRoot.Children.Add(_terrainVisual);
            SceneRoot.Children.Add(_facetsVisual);
            SceneRoot.Children.Add(_cablesVisual);
            SceneRoot.Children.Add(_primsVisual);
            SceneRoot.Children.Add(_roofTilesVisual);
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

        private void OnCameraChanged(object? sender, EventArgs e) => SyncCameraFromService();

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
            RebuildTerrain();
            RebuildFacets();
            RebuildCables();
            RebuildPrims();
            RebuildRoofTiles();
        }

        private void RebuildTerrain()
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
                _terrainVisual.Content = _builder.BuildTerrain();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                HudStatus.Text = $"terrain error: {ex.Message}";
            }
        }

        private void RebuildFacets()
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
                _facetsVisual.Content = _builder.BuildFacets();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                HudStatus.Text = $"facets error: {ex.Message}";
            }
        }

        private void RebuildCables()
        {
            if (_builder is null) return;

            if (!IsLayerEnabled(LayerKind.Buildings))
            {
                _cablesVisual.Content = null;
                return;
            }

            try
            {
                _cablesVisual.Content = _builder.BuildCables();
            }
            catch (Exception ex)
            {
                HudStatus.Text = $"cables error: {ex.Message}";
            }
        }

        private void RebuildPrims()
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
                _primsVisual.Content = _builder.BuildPrims();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                HudStatus.Text = $"prims error: {ex.Message}";
            }
        }

        private void RebuildRoofTiles()
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
                _roofTilesVisual.Content = _builder.BuildRoofTiles();
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