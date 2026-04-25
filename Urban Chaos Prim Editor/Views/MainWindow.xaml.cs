// Views/MainWindow.xaml.cs

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using UrbanChaosPrimEditor.ViewModels;
using UrbanChaosPrimEditor.Views.Dialogs;

namespace UrbanChaosPrimEditor.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel Vm => (MainWindowViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            SourceInitialized += OnSourceInitialized;
        }

        // ── Dark title bar (DWM) ──────────────────────────────────────────────

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                int darkMode = 1;
                int result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE,
                                                   ref darkMode, sizeof(int));
                if (result != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1,
                                          ref darkMode, sizeof(int));

                int captionColor = 0x001E1E1E;
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

                int textColor = 0x00FFFFFF;
                DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
            }
            catch { }
        }

        // ── 3D Camera state ───────────────────────────────────────────────────
        // Camera convention matches the Map Editor's Viewport3D:
        //   Yaw = 0   → Forward = (0, 0, +1)  (+Z)
        //   Pitch = 0 → level; positive tilts up
        //   WASD + QE to fly; arrow keys to look; scroll wheel adjusts speed.

        private double _camX, _camY, _camZ = -400;
        private double _yawRad   = 0.0;   // yaw=0 looks toward +Z
        private double _pitchRad = 0.0;

        private double _speed = 200.0;
        private const double MinSpeed    = 5.0;
        private const double MaxSpeed    = 10000.0;
        private const double LookRateRad = 1.6;
        private const double PitchLimit  = Math.PI / 2.0 * 0.98; // ~89°

        // Model rotation (applied to SceneVisual, independent of camera)
        private double _modelYawDeg   = 0.0;
        private double _modelPitchDeg = 0.0;
        private const double ModelRotRate = 90.0; // degrees per second
        private readonly AxisAngleRotation3D _modelYawRotation   = new(new Vector3D(0, 1, 0), 0);
        private readonly AxisAngleRotation3D _modelPitchRotation = new(new Vector3D(1, 0, 0), 0);

        private DateTime _lastTick;
        private readonly HashSet<Key> _held = new();

        // ── Window lifetime ───────────────────────────────────────────────────

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Wire model-rotation transform onto the scene visual (outside the frozen Model3DGroup).
            var rotGroup = new Transform3DGroup();
            rotGroup.Children.Add(new RotateTransform3D(_modelPitchRotation));
            rotGroup.Children.Add(new RotateTransform3D(_modelYawRotation));
            SceneVisual.Transform = rotGroup;

            // Subscribe to ViewModel so we can reset camera when a new model loads.
            Vm.PropertyChanged += OnVmPropertyChanged;
            if (Vm.ThreeD != null)
                Vm.ThreeD.PropertyChanged += OnThreeDPropertyChanged;

            _lastTick = DateTime.UtcNow;
            CompositionTarget.Rendering += OnRender;
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRender;

            Vm.PropertyChanged -= OnVmPropertyChanged;
            if (Vm.ThreeD != null)
                Vm.ThreeD.PropertyChanged -= OnThreeDPropertyChanged;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ThreeD))
            {
                // ThreeD instance was replaced — rewire the property-changed subscription.
                if (Vm.ThreeD != null)
                    Vm.ThreeD.PropertyChanged += OnThreeDPropertyChanged;
            }
        }

        private void OnThreeDPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ThreeDViewModel.ModelRadius))
                ResetCamera(Vm.ThreeD.ModelRadius);
        }

        // ── Camera helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Place the camera at (0, 0, -radius*2.5) looking toward +Z so the
        /// centred model fills a comfortable portion of the view.
        /// </summary>
        private void ResetCamera(double radius)
        {
            _camX    = 0;
            _camY    = 0;
            _camZ    = -(radius * 2.5);
            _yawRad  = 0;
            _pitchRad = 0;
            _speed   = Math.Max(MinSpeed, Math.Min(MaxSpeed, radius * 0.5));

            _modelYawDeg   = 0;
            _modelPitchDeg = 0;
            _modelYawRotation.Angle   = 0;
            _modelPitchRotation.Angle = 0;

            PushCameraToWpf();
            UpdateHud();
        }

        private void PushCameraToWpf()
        {
            double cp = Math.Cos(_pitchRad);
            double sp = Math.Sin(_pitchRad);
            double sy = Math.Sin(_yawRad);
            double cy = Math.Cos(_yawRad);

            Camera.Position      = new Point3D(_camX, _camY, _camZ);
            Camera.LookDirection = new Vector3D(cp * sy, sp, cp * cy);
            Camera.UpDirection   = new Vector3D(0, 1, 0);
        }

        private void UpdateHud()
        {
            HudCoords.Text    = $"X: {_camX,8:F1}   Y: {_camY,8:F1}   Z: {_camZ,8:F1}";
            HudOrient.Text    = $"Yaw: {_yawRad * 180 / Math.PI,6:F1}°   " +
                                $"Pitch: {_pitchRad * 180 / Math.PI,5:F1}°   " +
                                $"Speed: {_speed:F0}";
            HudModelRot.Text  = $"Model Yaw: {_modelYawDeg % 360,6:F1}°   " +
                                $"Pitch: {_modelPitchDeg % 360,5:F1}°";
        }

        // ── Render loop ───────────────────────────────────────────────────────

        private void OnRender(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            double dt = (now - _lastTick).TotalSeconds;
            _lastTick = now;

            // Guard against huge or negative deltas (e.g. first frame, tab-switch).
            if (dt <= 0 || dt > 0.25)
            {
                dt = 0.016;
                if (dt <= 0) return;
            }

            bool moved = false;

            // ── Model rotation ([ ] for yaw, ; ' for pitch, R to reset) ─────
            double modelYawDelta = 0, modelPitchDelta = 0;
            if (_held.Contains(Key.OemOpenBrackets))  modelYawDelta   -= 1; // [  → spin left
            if (_held.Contains(Key.OemCloseBrackets)) modelYawDelta   += 1; // ]  → spin right
            if (_held.Contains(Key.OemSemicolon))     modelPitchDelta -= 1; // ;  → tilt down
            if (_held.Contains(Key.OemQuotes))        modelPitchDelta += 1; // '  → tilt up

            if (_held.Contains(Key.R))
            {
                _modelYawDeg              = 0;
                _modelPitchDeg            = 0;
                _modelYawRotation.Angle   = 0;
                _modelPitchRotation.Angle = 0;
                moved = true;
            }
            else if (modelYawDelta != 0 || modelPitchDelta != 0)
            {
                double rotRate = ModelRotRate;
                if (_held.Contains(Key.LeftShift) || _held.Contains(Key.RightShift))
                    rotRate *= 2.0;

                _modelYawDeg              += modelYawDelta   * rotRate * dt;
                _modelPitchDeg            += modelPitchDelta * rotRate * dt;
                _modelYawRotation.Angle    = _modelYawDeg;
                _modelPitchRotation.Angle  = _modelPitchDeg;
                moved = true;
            }

            // ── Look (arrow keys) ────────────────────────────────────────────
            double yawDelta = 0, pitchDelta = 0;
            if (_held.Contains(Key.Left))  yawDelta   += 1;
            if (_held.Contains(Key.Right)) yawDelta   -= 1;
            if (_held.Contains(Key.Up))    pitchDelta += 1;
            if (_held.Contains(Key.Down))  pitchDelta -= 1;

            if (yawDelta != 0 || pitchDelta != 0)
            {
                double rate = LookRateRad;
                if (_held.Contains(Key.LeftShift) || _held.Contains(Key.RightShift))
                    rate *= 2.0;

                _yawRad   += yawDelta   * rate * dt;
                _pitchRad += pitchDelta * rate * dt;

                // Normalise yaw to [-π, π]
                const double twoPi = 2.0 * Math.PI;
                _yawRad %= twoPi;
                if (_yawRad >  Math.PI) _yawRad -= twoPi;
                if (_yawRad < -Math.PI) _yawRad += twoPi;

                // Clamp pitch
                _pitchRad = Math.Max(-PitchLimit, Math.Min(PitchLimit, _pitchRad));

                moved = true;
            }

            // ── Move (WASD + QE) ─────────────────────────────────────────────
            double mForward = 0, mRight = 0, mUp = 0;
            if (_held.Contains(Key.W)) mForward += 1;
            if (_held.Contains(Key.S)) mForward -= 1;
            if (_held.Contains(Key.A)) mRight   -= 1;
            if (_held.Contains(Key.D)) mRight   += 1;
            if (_held.Contains(Key.E)) mUp      += 1;
            if (_held.Contains(Key.Q)) mUp      -= 1;

            if (mForward != 0 || mRight != 0 || mUp != 0)
            {
                double speed = _speed;
                if (_held.Contains(Key.LeftShift) || _held.Contains(Key.RightShift))
                    speed *= 3.0;

                double sy = Math.Sin(_yawRad);
                double cy = Math.Cos(_yawRad);

                // Flat forward (ignores pitch so Q/E provides independent vertical)
                var fwdFlat = new Vector3D(sy, 0, cy);
                var right   = new Vector3D(cy, 0, -sy);
                var up      = new Vector3D(0, 1, 0);

                Vector3D delta = fwdFlat * mForward + right * mRight + up * mUp;
                double len = delta.Length;
                if (len > 0) delta *= (speed * dt) / len;

                _camX += delta.X;
                _camY += delta.Y;
                _camZ += delta.Z;

                moved = true;
            }

            if (moved)
            {
                PushCameraToWpf();
                UpdateHud();
            }
        }

        // ── Input handlers ────────────────────────────────────────────────────

        private void Window_KeyDown(object sender, KeyEventArgs e) => _held.Add(e.Key);
        private void Window_KeyUp(object sender, KeyEventArgs e)   => _held.Remove(e.Key);

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Viewport.Focus();
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.25 : 0.8;
            _speed = Math.Max(MinSpeed, Math.Min(MaxSpeed, _speed * factor));
            UpdateHud();
            e.Handled = true;
        }

        // ── Menu / button handlers ────────────────────────────────────────────

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AboutWindow { Owner = this };
            dlg.ShowDialog();
        }

        private void ToggleLeftPanel_Click(object sender, RoutedEventArgs e)
        {
            Vm.IsFileListPanelOpen = !Vm.IsFileListPanelOpen;
            LeftPanelColumn.Width = Vm.IsFileListPanelOpen
                ? new GridLength(240)
                : new GridLength(0);
        }

        private void ToggleRightPanel_Click(object sender, RoutedEventArgs e)
        {
            Vm.IsPrimInfoPanelOpen = !Vm.IsPrimInfoPanelOpen;
            RightPanelColumn.Width = Vm.IsPrimInfoPanelOpen
                ? new GridLength(260)
                : new GridLength(0);
        }
    }
}
