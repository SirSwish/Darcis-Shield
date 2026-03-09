// Models/Roofs/RoofModels.cs
// View model classes for walkable and RoofFace4 data.
// Extracted from BuildingsTabViewModel nested classes.

namespace UrbanChaosMapEditor.Models.Roofs
{
    /// <summary>
    /// View model for a single walkable entry.
    /// Maps 1:1 to DWalkableRec fields plus computed display properties.
    /// </summary>
    public sealed class WalkableVM
    {
        public int WalkableId1 { get; set; }      // 1-based index into DWalkable array
        public int BuildingId { get; set; }

        public byte X1 { get; set; }
        public byte Z1 { get; set; }
        public byte X2 { get; set; }
        public byte Z2 { get; set; }

        public byte Y { get; set; }
        public byte StoreyY { get; set; }

        public ushort StartFace4 { get; set; }
        public ushort EndFace4 { get; set; }
        public ushort Next { get; set; }

        public bool HasRoofFaces4 { get; set; }

        public ushort StartPoint { get; set; }
        public ushort EndPoint { get; set; }

        // XAML display
        public string Rect => $"({X1},{Z1}) → ({X2},{Z2})";
        public string Face4Span => $"{StartFace4}..{EndFace4}  (n={Math.Max(0, EndFace4 - StartFace4)})";
        public string PointSpan => $"{StartPoint}..{EndPoint}  (n={Math.Max(0, EndPoint - StartPoint)})";
    }

    /// <summary>
    /// View model for a single RoofFace4 entry.
    /// </summary>
    public sealed class RoofFace4VM
    {
        public int FaceId { get; set; }

        // Compatibility alias used by some older XAML/code-behind.
        public int Index
        {
            get => FaceId;
            set => FaceId = value;
        }

        public short Y { get; set; }
        public sbyte DY0 { get; set; }
        public sbyte DY1 { get; set; }
        public sbyte DY2 { get; set; }

        public byte DrawFlags { get; set; }
        public byte RX { get; set; }
        public byte RZ { get; set; }
        public short Next { get; set; }

        // XAML display
        public string DY => $"{DY0},{DY1},{DY2}";
    }

    /// <summary>
    /// Simplified row VM for walkable list display (used in grids).
    /// </summary>
    public sealed class WalkableRowVM
    {
        public int WalkableId1 { get; set; }
        public string Rect { get; set; } = "";
        public byte Y { get; set; }
        public byte StoreyY { get; set; }
        public string Face4Span { get; set; } = "";
        public ushort Next { get; set; }

        // Needed for selection -> roofface list
        public ushort StartFace4 { get; set; }
        public ushort EndFace4 { get; set; }
    }

    /// <summary>
    /// Simplified row VM for RoofFace4 list display (used in grids).
    /// </summary>
    public sealed class RoofFace4RowVM
    {
        public int FaceId { get; set; }
        public short Y { get; set; }
        public string DY { get; set; } = "";
        public byte RX { get; set; }
        public string RZ { get; set; } = "";
        public string DrawFlags { get; set; } = "";
        public short Next { get; set; }
    }
}