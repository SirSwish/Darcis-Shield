using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UrbanChaosMapEditor.Models.Styles
{
    public class TMAFile
    {
        public uint SaveType { get; set; }
        public List<TextureStyle> TextureStyles { get; set; } = new();

        public static TMAFile ReadTMAFile(string filePath)
        {
            var tma = new TMAFile();
            using var reader = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));

            tma.SaveType = reader.ReadUInt32();                        // little-endian

            // TEXTURES_XY
            ushort stylesCount = reader.ReadUInt16();               // rows (styles)
            ushort entriesPerStyle = reader.ReadUInt16();               // columns (entries per style) — usually 5
            tma.TextureStyles = new List<TextureStyle>(stylesCount);
            for (int i = 0; i < stylesCount; i++)
            {
                var style = new TextureStyle { Entries = new List<TextureEntry>(entriesPerStyle) };
                for (int j = 0; j < entriesPerStyle; j++)
                {
                    style.Entries.Add(new TextureEntry
                    {
                        Page = reader.ReadByte(),
                        Tx = reader.ReadByte(),
                        Ty = reader.ReadByte(),
                        Flip = reader.ReadByte() // opaque bitfield for now
                    });
                }
                tma.TextureStyles.Add(style);
            }

            // TEXTURE_STYLE_NAMES
            ushort nameRows = reader.ReadUInt16();
            ushort nameLen = reader.ReadUInt16();
            for (int i = 0; i < nameRows; i++)
            {
                var nameBytes = reader.ReadBytes(nameLen);
                var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                if (i < tma.TextureStyles.Count) tma.TextureStyles[i].Name = name;
            }

            // TEXTURES_FLAGS (optional)
            if (tma.SaveType > 2)
            {
                ushort flagRows = reader.ReadUInt16();
                ushort flagCols = reader.ReadUInt16();
                for (int i = 0; i < flagRows; i++)
                {
                    var flags = new List<TextureFlag>(flagCols);
                    for (int j = 0; j < flagCols; j++)
                        flags.Add((TextureFlag)reader.ReadByte());
                    if (i < tma.TextureStyles.Count) tma.TextureStyles[i].Flags = flags;
                }
            }

            // IMPORTANT: do NOT drop/shift any row here.
            // dstyles raw values are treated as zero-based indices into TextureStyles.

            return tma;
        }

        /// <summary>
        /// Fetch (Page,Tx,Ty,Flip) for a raw 0-based style id from the map and an entry slot (0..4).
        /// Returns false if OOB.
        /// </summary>
        public bool TryGetEntry(int rawStyleId, int slot, out TextureEntry entry)
        {
            entry = default;
            if (rawStyleId < 0 || rawStyleId >= TextureStyles.Count) return false;
            var s = TextureStyles[rawStyleId];
            if (s.Entries == null || slot < 0 || slot >= s.Entries.Count) return false;
            entry = s.Entries[slot];
            return true;
        }

        /// <summary>
        /// Convenience label for UI: "Style #N: Name" (where N = rawStyleId+1).
        /// </summary>
        public string GetDisplayLabel(int rawStyleId)
        {
            if (rawStyleId < 0 || rawStyleId >= TextureStyles.Count)
                return $"Style #{rawStyleId + 1}";
            var s = TextureStyles[rawStyleId];
            return string.IsNullOrWhiteSpace(s.Name)
                 ? $"Style #{rawStyleId + 1}"
                 : $"Style #{rawStyleId + 1}: {s.Name}";
        }
    }

    public class TextureStyle
    {
        public string Name { get; set; } = "";
        public List<TextureEntry> Entries { get; set; } = new();
        public List<TextureFlag> Flags { get; set; } = new();
    }

    public struct TextureEntry
    {
        public byte Page { get; set; }
        public byte Tx { get; set; }
        public byte Ty { get; set; }
        public byte Flip { get; set; } // opaque bitfield for now
    }

    [Flags]
    public enum TextureFlag : byte
    {
        Gouraud = 0x01,
        Textured = 0x02,
        Masked = 0x04,
        Transparent = 0x08,
        Alpha = 0x10,
        Tiled = 0x20,
        TwoSided = 0x40
        // bit7 unused
    }
}
