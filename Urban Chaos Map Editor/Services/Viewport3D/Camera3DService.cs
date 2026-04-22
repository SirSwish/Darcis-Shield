// Services/Viewport3D/Camera3DService.cs
using System;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Models.Viewport3D;

namespace UrbanChaosMapEditor.Services.Viewport3D
{
    /// <summary>
    /// Singleton holding the single 3D-view Camera3D shared by the 3D window
    /// and the purple camera marker overlay on the 2D map.
    /// </summary>
    public sealed class Camera3DService
    {
        private static readonly Lazy<Camera3DService> _lazy = new(() => new Camera3DService());
        public static Camera3DService Instance => _lazy.Value;

        private Camera3DService()
        {
            // Default: centre of map, looking roughly "north" (-Z).
            Camera = new Camera3D
            {
                X = MapConstants.MapPixels / 2.0,
                Z = MapConstants.MapPixels / 2.0,
                Y = Camera3D.DefaultHeight,
                YawRad = Math.PI,   // look toward -Z
                PitchRad = 0.0
            };
        }

        public Camera3D Camera { get; }

        /// <summary>Fired whenever X/Y/Z changes.</summary>
        public event EventHandler? PositionChanged;

        /// <summary>Fired whenever yaw or pitch changes.</summary>
        public event EventHandler? OrientationChanged;

        public void SetPosition(double x, double y, double z)
        {
            // Clamp XZ to map bounds so the marker icon never falls off the 2D overlay.
            double maxXZ = MapConstants.MapPixels;
            x = Math.Max(0, Math.Min(maxXZ, x));
            z = Math.Max(0, Math.Min(maxXZ, z));
            // Y allowed to roam a bit above/below world zero.
            if (double.IsNaN(y) || double.IsInfinity(y)) y = Camera.Y;

            if (Camera.X == x && Camera.Y == y && Camera.Z == z) return;
            Camera.X = x;
            Camera.Y = y;
            Camera.Z = z;
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetPositionXZ(double x, double z)
            => SetPosition(x, Camera.Y, z);

        public void SetYaw(double yawRad)
        {
            yawRad = Normalize(yawRad);
            if (Camera.YawRad == yawRad) return;
            Camera.YawRad = yawRad;
            OrientationChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetPitch(double pitchRad)
        {
            pitchRad = Math.Max(-Camera3D.PitchLimitRad, Math.Min(Camera3D.PitchLimitRad, pitchRad));
            if (Camera.PitchRad == pitchRad) return;
            Camera.PitchRad = pitchRad;
            OrientationChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetOrientation(double yawRad, double pitchRad)
        {
            yawRad = Normalize(yawRad);
            pitchRad = Math.Max(-Camera3D.PitchLimitRad, Math.Min(Camera3D.PitchLimitRad, pitchRad));
            bool changed = Camera.YawRad != yawRad || Camera.PitchRad != pitchRad;
            if (!changed) return;
            Camera.YawRad = yawRad;
            Camera.PitchRad = pitchRad;
            OrientationChanged?.Invoke(this, EventArgs.Empty);
        }

        private static double Normalize(double rad)
        {
            const double two = 2.0 * Math.PI;
            rad %= two;
            if (rad > Math.PI) rad -= two;
            else if (rad < -Math.PI) rad += two;
            return rad;
        }
    }
}
