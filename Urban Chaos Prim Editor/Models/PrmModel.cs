using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanChaosPrimEditor.Models
{
    public sealed class PrmModel
    {
        public string FileName { get; set; } = string.Empty;
        public int Signature { get; set; }
        public string Name { get; set; } = string.Empty;
        public int FirstPointId { get; set; }
        public int LastPointId { get; set; }
        public int FirstQuadrangleId { get; set; }
        public int LastQuadrangleId { get; set; }
        public int FirstTriangleId { get; set; }
        public int LastTriangleId { get; set; }
        public int CollisionType { get; set; }
        public int ReactionToImpactByVehicle { get; set; }
        public int ShadowType { get; set; }
        public int VariousProperties { get; set; }

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
