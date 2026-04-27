// Models/PrimFile.cs

using System.IO;
// Stub model representing a loaded .prm file.
// Binary parsing will be implemented once the PRM format is documented.

namespace UrbanChaosPrimEditor.Models
{
    public sealed class PrimFile
    {
        /// <summary>Full path to the source .prm file.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Display/default name for a new unsaved PRM document.</summary>
        public string SuggestedFileName { get; set; } = "untitled.prm";

        /// <summary>File name without path, displayed in the UI.</summary>
        public string FileName => string.IsNullOrWhiteSpace(FilePath)
            ? SuggestedFileName
            : Path.GetFileName(FilePath);

        /// <summary>Raw bytes of the file, retained for round-trip saving.</summary>
        public byte[] RawBytes { get; set; } = [];

        // ── Parsed header fields (placeholders — fill in once format is known) ──

        public int VertexCount  { get; set; }
        public int FaceCount    { get; set; }
        public int MaterialCount { get; set; }

        /// <summary>True when the model has been modified since the last save.</summary>
        public bool IsDirty { get; set; }

        public static PrimFile FromPath(string path)
        {
            var raw = File.ReadAllBytes(path);
            return new PrimFile
            {
                FilePath  = path,
                RawBytes  = raw,
                // TODO: parse header fields from raw bytes
            };
        }
    }
}
