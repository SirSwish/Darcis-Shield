// /Services/TexTypeService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UrbanChaosStyleEditor.Models;

namespace UrbanChaosStyleEditor.Services
{
    public static class TexTypeService
    {
        private static readonly Regex PageLineRegex = new(
            @"^\s*Page\s+(\d+)\s*:\s*([TWAISFDM]+)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static void LoadIntoSlots(string filePath, IList<TextureSlot> slots)
        {
            if (!File.Exists(filePath) || slots == null) return;

            foreach (var line in File.ReadLines(filePath))
            {
                var match = PageLineRegex.Match(line);
                if (!match.Success) continue;

                if (!int.TryParse(match.Groups[1].Value, out int pageIndex)) continue;
                if (pageIndex < 0 || pageIndex >= slots.Count) continue;

                string flags = match.Groups[2].Value.ToUpperInvariant();
                var slot = slots[pageIndex];

                slot.Transparent = flags.Contains('T');
                slot.Wrapping = flags.Contains('W');
                slot.AdditiveAlpha = flags.Contains('A');
                slot.IlluminationMap = flags.Contains('I');
                slot.SelfIlluminating = flags.Contains('S');
                slot.ExcludeFadeout = flags.Contains('F');
                slot.AlphaMasked = flags.Contains('D');
                slot.AlphaTransparent = flags.Contains('M');
            }
        }

        public static void WriteFromSlots(string filePath, IList<TextureSlot> slots)
        {
            var sb = new StringBuilder();

            sb.AppendLine("#");
            sb.AppendLine("# The properties of the texture pages...");
            sb.AppendLine("#");
            sb.AppendLine("# \t'T' means that black is transparent");
            sb.AppendLine("#\t'W' means that the texture wraps");
            sb.AppendLine("#\t'A' means additive alpha");
            sb.AppendLine("#\t'I' means the next page is the self-illumination map");
            sb.AppendLine("#\t'S' means that whole page is self illuminating");
            sb.AppendLine("#\t'F' means that texture is excluded from fadeout.");
            sb.AppendLine("#\t'D' means the texture is alpha masked. second texture is solid..");
            sb.AppendLine("#\t'M' means the texture uses alpha blending");
            sb.AppendLine("");
            sb.AppendLine("");

            for (int i = 0; i < slots.Count && i < 256; i++)
            {
                var slot = slots[i];
                if (!slot.HasAnyFlags) continue;

                sb.AppendLine($"Page {i,3}: {slot.FlagsSummary}");
            }

            File.WriteAllText(filePath, sb.ToString(), new System.Text.ASCIIEncoding());
        }
    }
}