// /Models/Textures/TextureCellClipboard.cs
using UrbanChaosMapEditor.Services.Textures;

namespace UrbanChaosMapEditor.Models.Textures
{
    /// <summary>A single cell's texture data, captured for copy-paste.</summary>
    public readonly struct TextureCellEntry
    {
        public TexturesAccessor.TextureGroup Group { get; init; }
        public int TextureNumber { get; init; }
        public int RotationIndex { get; init; }
    }

    /// <summary>
    /// An in-memory clipboard holding a rectangular region of texture cells.
    /// Cells are stored in row-major order: GetCell(col, row).
    /// </summary>
    public sealed class TextureCellClipboard
    {
        private readonly TextureCellEntry[,] _cells; // [col, row]

        public int Width { get; }
        public int Height { get; }

        public TextureCellClipboard(int width, int height, TextureCellEntry[,] cells)
        {
            Width = width;
            Height = height;
            _cells = cells;
        }

        public TextureCellEntry GetCell(int col, int row) => _cells[col, row];
    }
}
