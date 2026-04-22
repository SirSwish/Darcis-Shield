// Views/Core/MapOverlays/CameraMarkerLayer.cs
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Viewport3D;

namespace UrbanChaosMapEditor.Views.Core.MapOverlays
{
    /// <summary>
    /// Draws the 3D viewport's camera as a purple cone on the 2D map.
    /// - Left-click-drag: reposition the camera on the map (XZ only, Y preserved).
    /// - Right-click-drag: rotate yaw so it points toward the cursor.
    /// </summary>
    public sealed class CameraMarkerLayer : FrameworkElement
    {
        // ---- visuals ----
        private static readonly Brush FillBrush;
        private static readonly Brush OutlineBrush;
        private static readonly Pen OutlinePen;
        private static readonly Brush DotBrush;

        // Canvas-pixel size of the cone.
        private const double Radius = 90.0;       // length from apex/origin to back corners
        private const double HalfAngleDeg = 28.0; // FOV-like half-angle
        private const double PickRadius = 110.0;  // generous hit circle for dragging

        static CameraMarkerLayer()
        {
            FillBrush = new SolidColorBrush(Color.FromArgb(150, 180, 80, 220));   // translucent purple
            OutlineBrush = new SolidColorBrush(Color.FromRgb(180, 80, 220));      // solid purple
            OutlinePen = new Pen(OutlineBrush, 3.0);
            DotBrush = new SolidColorBrush(Color.FromRgb(230, 170, 255));         // light purple dot
            FillBrush.Freeze();
            OutlineBrush.Freeze();
            OutlinePen.Freeze();
            DotBrush.Freeze();
        }

        public CameraMarkerLayer()
        {
            IsHitTestVisible = true;
            Cursor = Cursors.Hand;

            Loaded += (_, __) =>
            {
                Camera3DService.Instance.PositionChanged += OnCameraChanged;
                Camera3DService.Instance.OrientationChanged += OnCameraChanged;
                InvalidateVisual();
            };
            Unloaded += (_, __) =>
            {
                Camera3DService.Instance.PositionChanged -= OnCameraChanged;
                Camera3DService.Instance.OrientationChanged -= OnCameraChanged;
            };
        }

        private void OnCameraChanged(object? sender, EventArgs e) => InvalidateVisual();

        protected override Size MeasureOverride(Size _)
            => new(MapConstants.MapPixels, MapConstants.MapPixels);

        protected override void OnRender(DrawingContext dc)
        {
            var cam = Camera3DService.Instance.Camera;

            // Full-surface transparent hit background so we can still receive clicks on the marker only.
            // We only draw the cone; background hit testing is handled manually in OnHitTestCore.

            double cx = cam.X;
            double cz = cam.Z;

            double yaw = cam.YawRad;
            double half = HalfAngleDeg * Math.PI / 180.0;

            // Yaw=0 points toward +Z on the 2D map (down). Forward unit vector in canvas XZ.
            double fx = Math.Sin(yaw);
            double fz = Math.Cos(yaw);
            // Right-hand perpendicular in canvas space.
            double px = fz;
            double pz = -fx;

            // Two back corners of the cone, offset from the apex along Forward +/- Right by half-angle.
            double backLen = Radius;
            double sideTan = Math.Tan(half);

            Point apex = new Point(cx, cz);
            Point left = new Point(
                cx + fx * backLen - px * backLen * sideTan,
                cz + fz * backLen - pz * backLen * sideTan);
            Point right = new Point(
                cx + fx * backLen + px * backLen * sideTan,
                cz + fz * backLen + pz * backLen * sideTan);

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(apex, isFilled: true, isClosed: true);
                ctx.LineTo(left, isStroked: true, isSmoothJoin: false);
                ctx.LineTo(right, isStroked: true, isSmoothJoin: false);
            }
            geom.Freeze();

            dc.DrawGeometry(FillBrush, OutlinePen, geom);

            // Centre dot (draggable handle).
            dc.DrawEllipse(DotBrush, null, apex, 12.0, 12.0);
        }

        // Only pick-hit within the cone's pick radius so the rest of the map
        // (textures, heights, etc.) stays interactable underneath.
        protected override HitTestResult? HitTestCore(PointHitTestParameters p)
        {
            var cam = Camera3DService.Instance.Camera;
            var pt = p.HitPoint;
            double dx = pt.X - cam.X;
            double dz = pt.Y - cam.Z;
            if (dx * dx + dz * dz <= PickRadius * PickRadius)
                return new PointHitTestResult(this, pt);
            return null;
        }

        // ---- Drag state ----
        private enum DragMode { None, Move, Rotate }
        private DragMode _mode = DragMode.None;

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.ChangedButton == MouseButton.Left)
            {
                _mode = DragMode.Move;
                CaptureMouse();
                UpdateFromMouse(e.GetPosition(this));
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                _mode = DragMode.Rotate;
                CaptureMouse();
                UpdateFromMouse(e.GetPosition(this));
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_mode == DragMode.None) return;
            UpdateFromMouse(e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_mode != DragMode.None && !IsMouseCaptured) return;
            _mode = DragMode.None;
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
        }

        private void UpdateFromMouse(Point p)
        {
            var svc = Camera3DService.Instance;
            if (_mode == DragMode.Move)
            {
                svc.SetPositionXZ(p.X, p.Y);
            }
            else if (_mode == DragMode.Rotate)
            {
                var cam = svc.Camera;
                double dx = p.X - cam.X;
                double dz = p.Y - cam.Z;
                if (dx * dx + dz * dz < 1) return;
                // Yaw where 0 -> +Z, increasing CCW looking down from +Y.
                double yaw = Math.Atan2(dx, dz);
                svc.SetYaw(yaw);
            }
        }
    }
}
