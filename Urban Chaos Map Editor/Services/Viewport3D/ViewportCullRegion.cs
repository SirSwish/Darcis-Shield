using System;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosEditor.Shared.Models;
using UrbanChaosMapEditor.Models.Viewport3D;

namespace UrbanChaosMapEditor.Services.Viewport3D
{
    public readonly struct ViewportCullRegion
    {
        private readonly double _originX;
        private readonly double _originZ;
        private readonly double _forwardX;
        private readonly double _forwardZ;
        private readonly double _cosHalfAngle;
        private readonly double _farDistance;
        private readonly double _farDistanceSq;
        private readonly double _margin;

        private ViewportCullRegion(
            double originX,
            double originZ,
            double forwardX,
            double forwardZ,
            double cosHalfAngle,
            double farDistance,
            double margin,
            int signatureX,
            int signatureZ,
            int signatureYaw)
        {
            _originX = originX;
            _originZ = originZ;
            _forwardX = forwardX;
            _forwardZ = forwardZ;
            _cosHalfAngle = cosHalfAngle;
            _farDistance = farDistance;
            _farDistanceSq = farDistance * farDistance;
            _margin = margin;
            SignatureX = signatureX;
            SignatureZ = signatureZ;
            SignatureYaw = signatureYaw;
        }

        public int SignatureX { get; }
        public int SignatureZ { get; }
        public int SignatureYaw { get; }

        public static ViewportCullRegion FromCamera(Camera3D camera, double verticalFovDegrees, double aspectRatio)
            => FromCamera(
                camera,
                verticalFovDegrees,
                aspectRatio,
                CameraConstants.ViewportCullDistance,
                CameraConstants.ViewportCullMargin);

        public static ViewportCullRegion FromCamera(
            Camera3D camera,
            double verticalFovDegrees,
            double aspectRatio,
            double cullDistance,
            double cullMargin)
        {
            aspectRatio = double.IsFinite(aspectRatio) && aspectRatio > 0.01 ? aspectRatio : 1.0;
            cullDistance = double.IsFinite(cullDistance) && cullDistance > 0.0
                ? cullDistance
                : CameraConstants.ViewportCullDistance;
            cullMargin = double.IsFinite(cullMargin) && cullMargin >= 0.0
                ? cullMargin
                : CameraConstants.ViewportCullMargin;

            double fovYRad = DegreesToRadians(verticalFovDegrees);
            double halfHorizontalRad = Math.Atan(Math.Tan(fovYRad * 0.5) * aspectRatio);
            double halfAngle = halfHorizontalRad + DegreesToRadians(CameraConstants.ViewportCullAngleMarginDegrees);

            var forward = camera.Forward;
            double fx = forward.X;
            double fz = forward.Z;
            double len = Math.Sqrt(fx * fx + fz * fz);
            if (len < 1e-6)
            {
                fx = Math.Sin(camera.YawRad);
                fz = Math.Cos(camera.YawRad);
                len = Math.Sqrt(fx * fx + fz * fz);
            }

            fx /= len;
            fz /= len;

            double signatureStep = Math.Max(SharedMapConstants.TileSize * 4.0, cullMargin);
            int signatureX = (int)Math.Floor(camera.X / signatureStep);
            int signatureZ = (int)Math.Floor(camera.Z / signatureStep);
            int signatureYaw = (int)Math.Floor(NormalizePositive(camera.YawRad) / DegreesToRadians(12.0));

            return new ViewportCullRegion(
                camera.X,
                camera.Z,
                fx,
                fz,
                Math.Cos(halfAngle),
                cullDistance + cullMargin,
                cullMargin,
                signatureX,
                signatureZ,
                signatureYaw);
        }

        public bool IsSameBucket(ViewportCullRegion other)
        {
            return SignatureX == other.SignatureX
                && SignatureZ == other.SignatureZ
                && SignatureYaw == other.SignatureYaw;
        }

        public double DistanceSquaredToPoint(double x, double z)
        {
            double dx = x - _originX;
            double dz = z - _originZ;
            return dx * dx + dz * dz;
        }

        public bool IntersectsBounds(double x0, double z0, double x1, double z1)
        {
            double minX = Math.Min(x0, x1) - _margin;
            double maxX = Math.Max(x0, x1) + _margin;
            double minZ = Math.Min(z0, z1) - _margin;
            double maxZ = Math.Max(z0, z1) + _margin;

            if (_originX >= minX && _originX <= maxX && _originZ >= minZ && _originZ <= maxZ)
                return true;

            double nearestX = Math.Clamp(_originX, minX, maxX);
            double nearestZ = Math.Clamp(_originZ, minZ, maxZ);
            double nearestDx = nearestX - _originX;
            double nearestDz = nearestZ - _originZ;
            if (nearestDx * nearestDx + nearestDz * nearestDz > _farDistanceSq)
                return false;

            double centerX = (minX + maxX) * 0.5;
            double centerZ = (minZ + maxZ) * 0.5;

            return IsPointInCone(centerX, centerZ)
                || IsPointInCone(minX, minZ)
                || IsPointInCone(maxX, minZ)
                || IsPointInCone(minX, maxZ)
                || IsPointInCone(maxX, maxZ)
                || IsPointInCone(nearestX, nearestZ);
        }

        private bool IsPointInCone(double x, double z)
        {
            double dx = x - _originX;
            double dz = z - _originZ;
            double distSq = dx * dx + dz * dz;

            if (distSq <= _margin * _margin)
                return true;
            if (distSq > _farDistanceSq)
                return false;

            double dist = Math.Sqrt(distSq);
            double dot = (dx * _forwardX + dz * _forwardZ) / dist;
            return dot >= _cosHalfAngle || dist <= _farDistance * 0.15;
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

        private static double NormalizePositive(double radians)
        {
            double twoPi = Math.PI * 2.0;
            radians %= twoPi;
            if (radians < 0) radians += twoPi;
            return radians;
        }
    }
}
