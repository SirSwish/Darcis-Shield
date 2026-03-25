// /Services/TmaWriter.cs
using System;
using System.IO;
using System.Text;
using UrbanChaosStyleEditor.Models;

namespace UrbanChaosStyleEditor.Services
{
    public static class TmaWriter
    {
        private const int EntriesPerStyle = 5;
        private const int NameLength = 32;
        private const uint SaveType = 3;

        public static void Write(string outputPath, StyleProject project)
        {
            using var writer = new BinaryWriter(File.Create(outputPath));

            // Row 0 is a dummy — the engine skips it.
            // Total rows = 1 (dummy) + user styles
            int totalRows = 1 + project.Styles.Count;

            // Header
            writer.Write(SaveType);

            // TEXTURES_XY section
            writer.Write((ushort)totalRows);
            writer.Write((ushort)EntriesPerStyle);

            // Row 0: dummy (all zeroes)
            for (int j = 0; j < EntriesPerStyle; j++)
            {
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
            }

            // Rows 1+: actual styles
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
                        writer.Write((byte)0);
                        writer.Write((byte)0);
                        writer.Write((byte)0);
                        writer.Write((byte)0);
                    }
                }
            }

            // TEXTURE_STYLE_NAMES section
            writer.Write((ushort)totalRows);
            writer.Write((ushort)NameLength);

            // Row 0: dummy name
            writer.Write(new byte[NameLength]);

            // Rows 1+: actual names
            foreach (var style in project.Styles)
            {
                var nameBytes = new byte[NameLength];
                var raw = Encoding.ASCII.GetBytes(style.Name ?? "");
                int copyLen = Math.Min(raw.Length, NameLength - 1);
                Buffer.BlockCopy(raw, 0, nameBytes, 0, copyLen);
                writer.Write(nameBytes);
            }

            // TEXTURES_FLAGS section (SaveType > 2)
            writer.Write((ushort)totalRows);
            writer.Write((ushort)EntriesPerStyle);

            // Row 0: dummy flags
            for (int j = 0; j < EntriesPerStyle; j++)
                writer.Write((byte)0);

            // Rows 1+: actual flags
            foreach (var style in project.Styles)
            {
                for (int j = 0; j < EntriesPerStyle; j++)
                {
                    writer.Write((byte)0x02); // Default: Textured
                }
            }
        }
    }
}