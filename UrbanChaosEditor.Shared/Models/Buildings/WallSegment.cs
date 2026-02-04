// /Models/Buildings/WallSegment.cs
namespace UrbanChaosMapEditor.Models
{
    public enum WallKind
    {
        Unknown = 0,
        Cable,
        Ladder,
        Fence,
        FenceBrick,
        FenceFlat,
        JumpableFence,
        UnclimbableBar,
        Door,
        Wall,
        Roof,
        Skylight,
        Trench,
        Partition,
        Inside,
        OutsideDoor,
        InsideDoor
    }

    public readonly struct WallSegment
    {
        public readonly int X1Ui;   // UI pixels
        public readonly int Z1Ui;   // UI pixels
        public readonly int X2Ui;   // UI pixels
        public readonly int Z2Ui;   // UI pixels
        public readonly WallKind Kind;

        public WallSegment(int x1Ui, int z1Ui, int x2Ui, int z2Ui, WallKind kind)
        {
            X1Ui = x1Ui; Z1Ui = z1Ui;
            X2Ui = x2Ui; Z2Ui = z2Ui;
            Kind = kind;
        }
    }
}
