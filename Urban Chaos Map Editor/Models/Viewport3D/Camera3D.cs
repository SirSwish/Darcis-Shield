// Models/Viewport3D/Camera3D.cs
using System.Windows.Media.Media3D;

namespace UrbanChaosMapEditor.Models.Viewport3D
{
    /// <summary>
    /// Camera state used by the 3D viewport and the purple map-overlay marker.
    /// Position is in world pixels (same scale as the 2D map: 0..8192 on X/Z).
    /// Y is world-up (same Y units as facet Y0/Y1: 1 storey = 64).
    /// Yaw/Pitch are in radians.
    ///   Yaw = 0 looks down +Z (south on the 2D map).
    ///   Yaw increases counter-clockwise looking down from +Y.
    ///   Pitch = 0 is level; positive tilts up; clamped to [-89°, +89°].
    /// </summary>
    public sealed class Camera3D
    {
        public const double DefaultHeight = 192.0;  // ~3 storeys above ground
        public const double PitchLimitRad = 1.5533; // 89° in radians

        public double X { get; set; }
        public double Y { get; set; } = DefaultHeight;
        public double Z { get; set; }
        public double YawRad { get; set; }
        public double PitchRad { get; set; }

        public Point3D Position => new Point3D(X, Y, Z);

        /// <summary>Unit vector the camera looks along (based on yaw + pitch).</summary>
        public Vector3D Forward
        {
            get
            {
                double cp = System.Math.Cos(PitchRad);
                double sp = System.Math.Sin(PitchRad);
                double sy = System.Math.Sin(YawRad);
                double cy = System.Math.Cos(YawRad);
                // Yaw=0 -> +Z; rotating by yaw around +Y.
                return new Vector3D(cp * sy, sp, cp * cy);
            }
        }

        /// <summary>Unit vector pointing to the camera's right.</summary>
        public Vector3D Right
        {
            get
            {
                double sy = System.Math.Sin(YawRad);
                double cy = System.Math.Cos(YawRad);
                // 90° clockwise from Forward in the XZ plane.
                return new Vector3D(cy, 0.0, -sy);
            }
        }

        public static Vector3D Up => new Vector3D(0, 1, 0);
    }
}
