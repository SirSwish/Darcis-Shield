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
using UrbanChaosPrimEditor.Models;
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
        //   WASD + QE to fly; right-drag to look; scroll wheel adjusts speed.

        private double _camX, _camY, _camZ = -400;
        private double _yawRad   = 0.0;   // yaw=0 looks toward +Z
        private double _pitchRad = 0.0;

        private double _speed = 200.0;
        private const double MinSpeed    = 5.0;
        private const double MaxSpeed    = 10000.0;
        private const double MouseLookSensitivity = 0.006; // radians per screen pixel
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
            else if (e.PropertyName == nameof(MainWindowViewModel.SelectedTexture))
            {
                // Clear the drag-selection overlay whenever a different texture is picked.
                SelectionRect.Visibility = Visibility.Collapsed;
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

        private void ClampCameraAngles()
        {
            const double twoPi = 2.0 * Math.PI;
            _yawRad %= twoPi;
            if (_yawRad >  Math.PI) _yawRad -= twoPi;
            if (_yawRad < -Math.PI) _yawRad += twoPi;

            _pitchRad = Math.Max(-PitchLimit, Math.Min(PitchLimit, _pitchRad));
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

        // ── Drag state for "Move Point" tool ─────────────────────────────────
        private bool       _isDraggingPoint;
        private bool       _isLookingAround;
        private int        _draggedPointId;
        private Vector3D   _dragPlaneNormal;
        private Point3D    _dragPlanePoint;     // a world-space point on the drag plane
        private Point      _lastLookPoint;

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Viewport.Focus();

            if (e.ChangedButton == MouseButton.Right)
            {
                _isLookingAround = true;
                _lastLookPoint = e.GetPosition(Viewport);
                Viewport.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Left) return;
            if (Vm.CurrentModel is null) return;

            Point pos = e.GetPosition(Viewport);
            PrimEditTool tool = Vm.ActiveTool;

            if (tool == PrimEditTool.AddPoint)
            {
                if (TryProjectClickToModelSpace(pos, out short mx, out short my, out short mz))
                {
                    Vm.AddPoint(mx, my, mz);
                    e.Handled = true;
                }
                else
                {
                    Vm.ReportAddPointProjectionFailed();
                    e.Handled = true;
                }
                return;
            }

            // Every other tool needs a point under the cursor.
            if (!TryPickMarker(pos, out int pointId)) return;

            if (tool == PrimEditTool.MovePoint)
            {
                if (TryGetPointWorldPosition(pointId, out Point3D worldPos))
                {
                    _isDraggingPoint  = true;
                    _draggedPointId   = pointId;
                    _dragPlanePoint   = worldPos;
                    Vector3D normal   = Camera.LookDirection; normal.Normalize();
                    _dragPlaneNormal  = normal;
                    Viewport.CaptureMouse();
                    Vm.OnPointClicked(pointId); // selects + rebuilds
                    e.Handled = true;
                    return;
                }
            }

            // Select / NewTriangle / NewQuad / DeletePoint
            Vm.OnPointClicked(pointId);
            e.Handled = true;
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isLookingAround)
            {
                if (e.RightButton != MouseButtonState.Pressed) { EndLookDrag(); return; }

                Point lookPos = e.GetPosition(Viewport);
                Vector delta = lookPos - _lastLookPoint;
                _lastLookPoint = lookPos;

                _yawRad += delta.X * MouseLookSensitivity;
                _pitchRad -= delta.Y * MouseLookSensitivity;
                ClampCameraAngles();

                PushCameraToWpf();
                UpdateHud();
                e.Handled = true;
                return;
            }

            if (!_isDraggingPoint) return;
            if (e.LeftButton != MouseButtonState.Pressed) { EndPointDrag(); return; }
            if (Vm.CurrentModel is null) { EndPointDrag(); return; }

            Point pos = e.GetPosition(Viewport);
            (Point3D rayO, Vector3D rayD) = BuildMouseRay(pos);

            if (!TryIntersectRayPlane(rayO, rayD, _dragPlanePoint, _dragPlaneNormal, out Point3D worldHit))
                return;

            if (!TryWorldToModel(worldHit, out Point3D modelHit))
                return;

            short x = ClampToShort(modelHit.X);
            short y = ClampToShort(modelHit.Y);
            short z = ClampToShort(modelHit.Z);
            Vm.MovePointTo(_draggedPointId, x, y, z);
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                if (_isLookingAround) EndLookDrag();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Left) return;
            if (_isDraggingPoint) EndPointDrag();
        }

        private void EndPointDrag()
        {
            _isDraggingPoint = false;
            if (!_isLookingAround && Viewport.IsMouseCaptured) Viewport.ReleaseMouseCapture();
        }

        private void EndLookDrag()
        {
            _isLookingAround = false;
            if (!_isDraggingPoint && Viewport.IsMouseCaptured) Viewport.ReleaseMouseCapture();
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.25 : 0.8;
            _speed = Math.Max(MinSpeed, Math.Min(MaxSpeed, _speed * factor));
            UpdateHud();
            e.Handled = true;
        }

        // ── Picking ──────────────────────────────────────────────────────────

        /// <summary>
        /// Hit-test the viewport at the given screen position, walking all hits
        /// and preferring vertex markers over face geometry.
        /// </summary>
        private bool TryPickMarker(Point screenPos, out int pointId)
        {
            pointId = -1;
            var lookup = Vm.ThreeD?.MarkerToPointId;
            if (lookup is null || lookup.Count == 0) return false;

            int? found = null;
            VisualTreeHelper.HitTest(
                Viewport,
                null,
                result =>
                {
                    if (result is RayMeshGeometry3DHitTestResult r &&
                        r.ModelHit is GeometryModel3D g &&
                        lookup.TryGetValue(g, out int id))
                    {
                        found = id;
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue;
                },
                new PointHitTestParameters(screenPos));

            if (found is null) return false;
            pointId = found.Value;
            return true;
        }

        /// <summary>
        /// Project a viewport click into model space by intersecting the camera
        /// ray with a plane through the world origin facing the camera. Returns
        /// false if the ray is parallel to the plane.
        /// </summary>
        private bool TryProjectClickToModelSpace(Point screenPos, out short x, out short y, out short z)
        {
            x = y = z = 0;
            (Point3D rayO, Vector3D rayD) = BuildMouseRay(screenPos);

            Vector3D normal = Camera.LookDirection; normal.Normalize();
            if (!TryIntersectRayPlane(rayO, rayD, new Point3D(0, 0, 0), normal, out Point3D worldHit))
                return false;
            if (!TryWorldToModel(worldHit, out Point3D modelHit))
                return false;

            x = ClampToShort(modelHit.X);
            y = ClampToShort(modelHit.Y);
            z = ClampToShort(modelHit.Z);
            return true;
        }

        /// <summary>Get the current world-space position of a model point given current transforms.</summary>
        private bool TryGetPointWorldPosition(int pointId, out Point3D worldPos)
        {
            worldPos = default;
            var model = Vm.CurrentModel;
            if (model is null) return false;

            int idx = model.Points.FindIndex(p => p.GlobalId == pointId);
            if (idx < 0) return false;

            var p = model.Points[idx];
            var localPoint = new Point3D(p.X, p.Y, p.Z);
            if (!TryGetModelToWorldMatrix(out Matrix3D m)) return false;
            worldPos = m.Transform(localPoint);
            return true;
        }

        // ── Coordinate-space helpers ─────────────────────────────────────────

        /// <summary>
        /// Build the matrix that maps a raw point coordinate (model space)
        /// to world space via the scene's centering transform and the
        /// SceneVisual's rotation transform.
        /// </summary>
        private bool TryGetModelToWorldMatrix(out Matrix3D matrix)
        {
            matrix = Matrix3D.Identity;

            // child transform: Scene's own (centering)
            Transform3D? childT = Vm.ThreeD?.Scene?.Transform;
            // parent transform: SceneVisual.Transform (model rotation)
            Transform3D? parentT = SceneVisual.Transform;

            // WPF row-vector convention: world = local * child * parent
            if (childT is not null)  matrix.Append(childT.Value);
            if (parentT is not null) matrix.Append(parentT.Value);
            return true;
        }

        private bool TryWorldToModel(Point3D worldPoint, out Point3D modelPoint)
        {
            modelPoint = default;
            if (!TryGetModelToWorldMatrix(out Matrix3D m)) return false;
            if (!m.HasInverse) return false;
            m.Invert();
            modelPoint = m.Transform(worldPoint);
            return true;
        }

        /// <summary>
        /// Build a world-space ray from the camera through the given pixel
        /// in the viewport. Uses the camera's horizontal FieldOfView and the
        /// current viewport size.
        /// </summary>
        private (Point3D origin, Vector3D direction) BuildMouseRay(Point screenPos)
        {
            double w = Viewport.ActualWidth;
            double h = Viewport.ActualHeight;
            if (w <= 0 || h <= 0) return (Camera.Position, Camera.LookDirection);

            double aspect = w / h;
            double fovHRad = Camera.FieldOfView * Math.PI / 180.0;
            double tanH = Math.Tan(fovHRad / 2.0);
            double tanV = tanH / aspect;

            // Normalised device coords: x in [-1,1] right-positive, y up-positive.
            double ndcX = (2.0 * screenPos.X / w) - 1.0;
            double ndcY = 1.0 - (2.0 * screenPos.Y / h);

            Vector3D fwd = Camera.LookDirection;   fwd.Normalize();
            Vector3D up  = Camera.UpDirection;     up.Normalize();
            Vector3D right = Vector3D.CrossProduct(fwd, up); right.Normalize();
            Vector3D trueUp = Vector3D.CrossProduct(right, fwd); trueUp.Normalize();

            Vector3D dir = fwd + right * (ndcX * tanH) + trueUp * (ndcY * tanV);
            dir.Normalize();
            return (Camera.Position, dir);
        }

        private static bool TryIntersectRayPlane(
            Point3D rayOrigin,
            Vector3D rayDir,
            Point3D planePoint,
            Vector3D planeNormal,
            out Point3D hit)
        {
            hit = default;
            double denom = Vector3D.DotProduct(rayDir, planeNormal);
            if (Math.Abs(denom) < 1e-6) return false;
            Vector3D toPlane = planePoint - rayOrigin;
            double t = Vector3D.DotProduct(toPlane, planeNormal) / denom;
            if (t <= 0) return false;
            hit = rayOrigin + rayDir * t;
            return true;
        }

        private static short ClampToShort(double v)
        {
            if (double.IsNaN(v)) return 0;
            if (v >= short.MaxValue) return short.MaxValue;
            if (v <= short.MinValue) return short.MinValue;
            return (short)Math.Round(v);
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

        // ── Texture UV region selection ───────────────────────────────────────

        private Point _texSelStart;
        private bool  _isDraggingTexSel;

        private void TextureCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            var canvas = (System.Windows.Controls.Canvas)sender;
            Rect imageRect = GetTextureImageRect(canvas);
            _texSelStart = ClampPointToRect(e.GetPosition(canvas), imageRect);
            _isDraggingTexSel = true;
            canvas.CaptureMouse();
            UpdateTexSelRect(_texSelStart, _texSelStart, canvas);
            e.Handled = true;
        }

        private void TextureCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingTexSel) return;
            var canvas = (System.Windows.Controls.Canvas)sender;
            Point cur = ClampPointToRect(e.GetPosition(canvas), GetTextureImageRect(canvas));
            UpdateTexSelRect(_texSelStart, cur, canvas);
        }

        private void TextureCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || !_isDraggingTexSel) return;
            _isDraggingTexSel = false;
            ((System.Windows.Controls.Canvas)sender).ReleaseMouseCapture();
            e.Handled = true;
        }

        private void UpdateTexSelRect(Point a, Point b, System.Windows.Controls.Canvas canvas)
        {
            double x = Math.Min(a.X, b.X);
            double y = Math.Min(a.Y, b.Y);
            double w = Math.Abs(b.X - a.X);
            double h = Math.Abs(b.Y - a.Y);

            System.Windows.Controls.Canvas.SetLeft(SelectionRect, x);
            System.Windows.Controls.Canvas.SetTop(SelectionRect,  y);
            SelectionRect.Width      = Math.Max(w, 1);
            SelectionRect.Height     = Math.Max(h, 1);
            SelectionRect.Visibility = Visibility.Visible;

            double cw = canvas.ActualWidth;
            double ch = canvas.ActualHeight;
            if (cw < 1 || ch < 1) return;

            Rect imageRect = GetTextureImageRect(canvas);
            if (imageRect.Width < 1 || imageRect.Height < 1) return;

            Vm.TextureSelection = new Rect(
                Math.Clamp((x - imageRect.X) / imageRect.Width, 0, 1),
                Math.Clamp((y - imageRect.Y) / imageRect.Height, 0, 1),
                Math.Clamp(w / imageRect.Width, 0, 1),
                Math.Clamp(h / imageRect.Height, 0, 1));
        }

        private Rect GetTextureImageRect(System.Windows.Controls.Canvas canvas)
        {
            double cw = canvas.ActualWidth;
            double ch = canvas.ActualHeight;
            var bitmap = Vm.SelectedTexture?.Thumbnail;
            if (cw <= 0 || ch <= 0 || bitmap is null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
                return new Rect(0, 0, Math.Max(cw, 0), Math.Max(ch, 0));

            double imageAspect = bitmap.PixelWidth / (double)bitmap.PixelHeight;
            double boxAspect = cw / ch;

            if (imageAspect >= boxAspect)
            {
                double height = cw / imageAspect;
                return new Rect(0, (ch - height) / 2.0, cw, height);
            }
            else
            {
                double width = ch * imageAspect;
                return new Rect((cw - width) / 2.0, 0, width, ch);
            }
        }

        private static Point ClampPointToRect(Point point, Rect rect)
        {
            return new Point(
                Math.Clamp(point.X, rect.Left, rect.Right),
                Math.Clamp(point.Y, rect.Top, rect.Bottom));
        }

        private void ResetTextureSelection_Click(object sender, RoutedEventArgs e)
        {
            SelectionRect.Visibility = Visibility.Collapsed;
            Vm.TextureSelection = new Rect(0, 0, 1, 1);
        }
    }
}
