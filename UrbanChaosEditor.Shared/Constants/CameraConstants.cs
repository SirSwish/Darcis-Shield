// ============================================================
// UrbanChaosEditor.Shared/Constants/CameraConstants.cs
// ============================================================
// Defaults for 3D camera and viewport navigation.

namespace UrbanChaosEditor.Shared.Constants;

/// <summary>
/// Defaults for the 3D camera and viewport navigation.
/// </summary>
public static class CameraConstants
{
    /// <summary>Default camera elevation in world units.</summary>
    public const double DefaultHeight = 192.0;

    /// <summary>Pitch limit in radians (~89°).</summary>
    public const double PitchLimitRad = 1.5533;

    /// <summary>Minimum camera move speed.</summary>
    public const double MinSpeed = 5.0;

    /// <summary>Maximum camera move speed.</summary>
    public const double MaxSpeed = 10000.0;

    /// <summary>Mouse-look sensitivity (radians per pixel).</summary>
    public const double MouseLookSensitivity = 0.006;

    /// <summary>Model rotation rate in degrees per second.</summary>
    public const double ModelRotRate = 90.0;

    /// <summary>Conversion factor from yaw byte (0..255) to degrees.</summary>
    public const double DegreesPerYaw = 360.0 / 256.0;

    /// <summary>Maximum horizontal distance rendered ahead of the 3D viewport camera.</summary>
    public const double ViewportCullDistance = 3072.0;

    /// <summary>Extra horizontal padding around viewport culling bounds.</summary>
    public const double ViewportCullMargin = 256.0;

    /// <summary>Extra angle added to the camera frustum cull so edge geometry does not pop aggressively.</summary>
    public const double ViewportCullAngleMarginDegrees = 10.0;

    /// <summary>Minimum delay between camera-driven viewport cull rebuilds.</summary>
    public const int ViewportCullRefreshMilliseconds = 150;
}
