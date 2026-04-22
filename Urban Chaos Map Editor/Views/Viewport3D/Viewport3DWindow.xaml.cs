// Views/Viewport3D/Viewport3DWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Windows;
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

namespace UrbanChaosMapEditor.Views.Viewport3D
{
    public partial class Viewport3DWindow : Window
    {
        // Hold-to-move key state.
        private readonly HashSet<Key> _held = new();

        // Movement speed (world units per second). Tuned roughly: 1 storey = 64 units.
        private double _speed = 512.0;
        private const double MinSpeed = 64.0;
        private const double MaxSpeed = 8192.0;

        // Arrow-key look sensitivity (radians per second when held).
        private const double LookRateRad = 1.6;

        // Scene root containers — the scene builder fills these in.
        private readonly ModelVisual3D _terrainVisual = new();
        private readonly ModelVisual3D _facetsVisual = new();
        private readonly ModelVisual3D _primsVisual = new();

        private SceneBuilder? _builder;
        private DateTime _lastTick;

        public Viewport3DWindow()
        {
            InitializeComponent();
            SceneRoot.Children.Add(_terrainVisual);
            SceneRoot.Children.Add(_facetsVisual);
            SceneRoot.Children.Add(_primsVisual);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Focus();
            Viewport.Focus();

            _builder = new SceneBuilder();

            // Wire change events first so a newly-loaded map also rebuilds the scene.
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
            RoofsChangeBus.Instance.Changed += OnFacetsChanged;

            ObjectsChangeBus.Instance.Changed += OnPrimsChanged;

            // Initial push: sync camera and build scene (if a map is loaded).
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
            RoofsChangeBus.Instance.Changed -= OnFacetsChanged;

            ObjectsChangeBus.Instance.Changed -= OnPrimsChanged;

            if (IsMouseCaptured) ReleaseMouseCapture();
        }

        // ---- Change-event handlers ----
        private void OnCameraChanged(object? sender, EventArgs e) => SyncCameraFromService();

        private void OnMapClearedHandler(object? sender, EventArgs e)
        {
            _terrainVisual.Content = null;
            _facetsVisual.Content = null;
            _primsVisual.Content = null;
            HudStatus.Text = "(no map loaded)";
        }

        private void OnMapWholesaleChanged(object? sender, EventArgs e) => RebuildAll();
        private void OnTerrainChanged(object? sender, EventArgs e) => RebuildTerrain();
        private void OnFacetsChanged(object? sender, EventArgs e) => RebuildFacets();
        private void OnPrimsChanged(object? sender, EventArgs e) => RebuildPrims();

        // AltitudeChangeBus has different delegate signatures, so each needs its own handler.
        private void OnAltitudeTileChanged(int tx, int ty) => Dispatcher.Invoke(RebuildTerrain);
        private void OnAltitudeRegionChanged(int minTx, int minTy, int maxTx, int maxTy) => Dispatcher.Invoke(RebuildTerrain);
        private void OnAltitudeAllChanged() => Dispatcher.Invoke(RebuildTerrain);

        // ---- Rebuilds ----
        private void RebuildAll()
        {
            RebuildTerrain();
            RebuildFacets();
            RebuildPrims();
        }

        private void RebuildTerrain()
        {
            if (_builder is null) return;
            try
            {
                _terrainVisual.Content = _builder.BuildTerrain();
                UpdateStatus();
            }
            catch (Exception ex) { HudStatus.Text = $"terrain error: {ex.Message}"; }
        }

        private void RebuildFacets()
        {
            if (_builder is null) return;
            try
            {
                _facetsVisual.Content = _builder.BuildFacets();
                UpdateStatus();
            }
            catch (Exception ex) { HudStatus.Text = $"facets error: {ex.Message}"; }
        }

        private void RebuildPrims()
        {
            if (_builder is null) return;
            try
            {
                _primsVisual.Content = _builder.BuildPrims();
                UpdateStatus();
            }
            catch (Exception ex) { HudStatus.Text = $"prims error: {ex.Message}"; }
        }

        private void UpdateStatus()
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                HudStatus.Text = "(no map loaded)";
                return;
            }
            HudStatus.Text = $"scene ready";
        }

        // ---- Camera sync ----
        private void SyncCameraFromService()
        {
            var cam = Camera3DService.Instance.Camera;
            Camera.Position = new Point3D(cam.X, cam.Y, cam.Z);
            var fwd = cam.Forward;
            Camera.LookDirection = fwd;
            // Up vector: keep world-Y up (works fine until pitch nears 90°, which is clamped).
            Camera.UpDirection = new Vector3D(0, 1, 0);
            UpdateHud(cam);
        }

        private void UpdateHud(Camera3D cam)
        {
            HudCoords.Text = $"X: {cam.X,7:F1}   Y: {cam.Y,7:F1}   Z: {cam.Z,7:F1}";
            HudOrient.Text = $"Yaw: {cam.YawRad * 180.0 / Math.PI,6:F1}°   Pitch: {cam.PitchRad * 180.0 / Math.PI,5:F1}°   Speed: {_speed:F0}";
        }

        // ---- Input: keyboard ----
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Close(); return; }
            _held.Add(e.Key);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e) => _held.Remove(e.Key);

        // ---- Input: mouse ----
        // Mouse-look was replaced with arrow-key look. These handlers remain so
        // the XAML bindings still resolve, but they only re-focus the viewport.
        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Viewport.Focus();
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e) { }

        private void Viewport_MouseMove(object sender, MouseEventArgs e) { }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Adjust movement speed exponentially with the wheel.
            double step = e.Delta > 0 ? 1.25 : 0.8;
            _speed = Math.Max(MinSpeed, Math.Min(MaxSpeed, _speed * step));
            UpdateHud(Camera3DService.Instance.Camera);
            e.Handled = true;
        }

        // ---- Frame tick: advance camera from held keys ----
        private void OnRender(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            double dt = (now - _lastTick).TotalSeconds;
            _lastTick = now;
            if (dt <= 0 || dt > 0.25) { dt = Math.Min(dt, 0.016); if (dt <= 0) return; }

            var cam = Camera3DService.Instance.Camera;

            // Arrow-key look: Left/Right = yaw, Up/Down = pitch.
            double yawDelta = 0, pitchDelta = 0;
            if (_held.Contains(Key.Left))  yawDelta   -= 1;
            if (_held.Contains(Key.Right)) yawDelta   += 1;
            if (_held.Contains(Key.Up))    pitchDelta += 1;
            if (_held.Contains(Key.Down))  pitchDelta -= 1;
            if (yawDelta != 0 || pitchDelta != 0)
            {
                double rate = LookRateRad;
                if (_held.Contains(Key.LeftShift) || _held.Contains(Key.RightShift)) rate *= 2.0;
                double newYaw = cam.YawRad + yawDelta * rate * dt;
                double newPitch = cam.PitchRad + pitchDelta * rate * dt;
                Camera3DService.Instance.SetOrientation(newYaw, newPitch);
                cam = Camera3DService.Instance.Camera;
            }

            // WASD/QE movement.
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

            // Build a horizontal forward (ignore pitch for WASD; pitch still drives look direction).
            double sy = Math.Sin(cam.YawRad);
            double cy = Math.Cos(cam.YawRad);
            Vector3D fwdFlat = new Vector3D(sy, 0, cy);
            Vector3D right = new Vector3D(cy, 0, -sy);
            Vector3D up = new Vector3D(0, 1, 0);

            Vector3D delta = fwdFlat * mz + right * mx + up * my;
            double len = delta.Length;
            if (len > 0) delta *= (speed * dt) / len;

            Camera3DService.Instance.SetPosition(cam.X + delta.X, cam.Y + delta.Y, cam.Z + delta.Z);
        }
    }
}
