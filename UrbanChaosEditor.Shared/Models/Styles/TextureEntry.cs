// Models/TextureEntry.cs
namespace UrbanChaosMapEditor.Models
{
    public sealed class TextureEntry
    {
        public byte Page { get; init; }
        public byte Tx { get; init; }
        public byte Ty { get; init; }
        public byte Flip { get; init; } // bit0 = X, bit1 = Y (common convention)
    }
}
