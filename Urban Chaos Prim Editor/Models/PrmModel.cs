using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanChaosPrimEditor.Models
{
    public sealed class PrmModel
    {
        public string FileName { get; init; } = string.Empty;
        public int Signature { get; init; }
        public string Name { get; init; } = string.Empty;
        public int FirstPointId { get; init; }
        public int LastPointId { get; init; }
        public int FirstQuadrangleId { get; init; }
        public int LastQuadrangleId { get; init; }
        public int FirstTriangleId { get; init; }
        public int LastTriangleId { get; init; }
        public int CollisionType { get; init; }
        public int ReactionToImpactByVehicle { get; init; }
        public int ShadowType { get; init; }
        public int VariousProperties { get; init; }

        public List<PrmPoint> Points { get; } = new();
        public List<PrmTriangle> Triangles { get; } = new();
        public List<PrmQuadrangle> Quadrangles { get; } = new();
    }

    public readonly record struct PrmPoint(int GlobalId, short X, short Y, short Z);

    public readonly record struct PrmTriangle(
        byte TexturePage,
        byte Properties,
        short PointAId,
        short PointBId,
        short PointCId,
        byte UA,
        byte VA,
        byte UB,
        byte VB,
        byte UC,
        byte VC,
        byte BrightA,
        byte BrightB,
        byte BrightC);

    public readonly record struct PrmQuadrangle(
        byte TexturePage,
        byte Properties,
        short PointAId,
        short PointBId,
        short PointCId,
        short PointDId,
        byte UA,
        byte VA,
        byte UB,
        byte VB,
        byte UC,
        byte VC,
        byte UD,
        byte VD,
        byte BrightA,
        byte BrightB,
        byte BrightC,
        byte BrightD);

}
