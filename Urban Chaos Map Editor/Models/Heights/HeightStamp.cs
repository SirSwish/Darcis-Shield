// /Models/Heights/HeightStamp.cs
using System.Text.Json.Serialization;

namespace UrbanChaosMapEditor.Models.Heights
{
    /// <summary>
    /// A named height stamp that can be applied to the terrain centred on a tile.
    /// Values are row-major: Values[row * Width + col] = height at (col, row) relative to top-left.
    /// The stamp origin (click point) corresponds to (Width/2, Height/2) — integer division.
    /// </summary>
    public sealed class HeightStamp
    {
        public string Name { get; set; } = "Unnamed";
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// Flat, row-major array of signed byte height values.
        /// Length must equal Width * Height.
        /// </summary>
        public sbyte[] Values { get; set; } = Array.Empty<sbyte>();

        /// <summary>Not serialised — set to true for stamps that ship with the editor.</summary>
        [JsonIgnore]
        public bool IsBuiltIn { get; set; }

        /// <summary>Returns the value at (col, row), or 0 if out of range.</summary>
        public sbyte GetValue(int col, int row)
        {
            if (col < 0 || col >= Width || row < 0 || row >= Height) return 0;
            return Values[row * Width + col];
        }

        public void SetValue(int col, int row, sbyte value)
        {
            if (col < 0 || col >= Width || row < 0 || row >= Height) return;
            Values[row * Width + col] = value;
        }
    }
}
