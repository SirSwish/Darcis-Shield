// /Services/TmaWriter.cs
using System;
using System.IO;
using System.Text;
using UrbanChaosStyleEditor.Models;

namespace UrbanChaosStyleEditor.Services
{
    public static class TmaWriter
    {
        private const int TotalRows = 200;     // fixed total rows expected by the game
        private const int EntriesPerStyle = 5;
        private const int NameLength = 21;
        private const uint SaveType = 5;

        // POLY_GT = POLY_FLAG_GOURAD | POLY_FLAG_TEXTURED = 0x03
        private const byte DefaultFlag = 0x03;

        public static void Write(string outputPath, StyleProject project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            // Row 0 is reserved dummy, so max editable styles = 199.
            if (project.Styles.Count > TotalRows - 1)
            {
                throw new InvalidOperationException(
                    $"TMA supports a maximum of {TotalRows - 1} editable styles (row 0 is reserved). " +
                    $"Current project has {project.Styles.Count} styles.");
            }

            using var writer = new BinaryWriter(File.Create(outputPath));

            // Header
            writer.Write(SaveType);

            // ─────────────────────────────────────────────────────────────
            // TEXTURES_XY section
            // ─────────────────────────────────────────────────────────────
            writer.Write((ushort)TotalRows);
            writer.Write((ushort)EntriesPerStyle);

            // Row 0: dummy
            WriteDummyStyleEntries(writer);

            // Rows 1..N: actual styles
            foreach (var style in project.Styles)
            {
                for (int j = 0; j < EntriesPerStyle; j++)
                {
                    if (j < style.Pieces.Count)
                    {
                        var piece = style.Pieces[j];
                        writer.Write(piece.Page);
                        writer.Write(piece.Tx);
                        writer.Write(piece.Ty);
                        writer.Write(piece.Flip);
                    }
                    else
                    {
                        WriteDummyPiece(writer);
                    }
                }
            }

            // Pad remaining rows
            int writtenRealRows = project.Styles.Count;
            int remainingRows = (TotalRows - 1) - writtenRealRows;

            for (int i = 0; i < remainingRows; i++)
            {
                WriteDummyStyleEntries(writer);
            }

            // ─────────────────────────────────────────────────────────────
            // TEXTURE_STYLE_NAMES section
            // ─────────────────────────────────────────────────────────────
            writer.Write((ushort)TotalRows);
            writer.Write((ushort)NameLength);

            // Row 0: dummy name
            writer.Write(new byte[NameLength]);

            // Rows 1..N: actual names
            foreach (var style in project.Styles)
            {
                writer.Write(BuildFixedAsciiName(style.Name, NameLength));
            }

            // Pad remaining names
            for (int i = 0; i < remainingRows; i++)
            {
                writer.Write(new byte[NameLength]);
            }

            // ─────────────────────────────────────────────────────────────
            // TEXTURES_FLAGS section
            // ─────────────────────────────────────────────────────────────
            writer.Write((ushort)TotalRows);
            writer.Write((ushort)EntriesPerStyle);

            // Row 0: dummy flags
            for (int j = 0; j < EntriesPerStyle; j++)
                writer.Write((byte)0);

            // Rows 1..N: actual flags
            foreach (var style in project.Styles)
            {
                for (int j = 0; j < EntriesPerStyle; j++)
                {
                    writer.Write(GetFlagForPiece(style, j));
                }
            }

            // Pad remaining flag rows
            for (int i = 0; i < remainingRows; i++)
            {
                for (int j = 0; j < EntriesPerStyle; j++)
                    writer.Write((byte)0);
            }
        }

        private static void WriteDummyStyleEntries(BinaryWriter writer)
        {
            for (int j = 0; j < EntriesPerStyle; j++)
                WriteDummyPiece(writer);
        }

        private static void WriteDummyPiece(BinaryWriter writer)
        {
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
        }

        private static byte[] BuildFixedAsciiName(string? name, int fixedLength)
        {
            var bytes = new byte[fixedLength];

            if (string.IsNullOrEmpty(name))
                return bytes;

            var raw = Encoding.ASCII.GetBytes(name);
            int copyLen = Math.Min(raw.Length, fixedLength - 1); // leave room for null terminator
            Buffer.BlockCopy(raw, 0, bytes, 0, copyLen);

            return bytes;
        }

        private static byte GetFlagForPiece(StyleEntry style, int pieceIndex)
        {
            if (pieceIndex >= style.Pieces.Count)
                return 0;

            var piece = style.Pieces[pieceIndex];

            // Preserve the actual per-piece TMA/poly draw flag.
            // Fall back to POLY_GT if an uninitialised piece somehow has 0.
            return piece.Flag == 0 ? DefaultFlag : piece.Flag;
        }
    }
}