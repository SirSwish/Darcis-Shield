// /Services/SoundFxService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UrbanChaosStyleEditor.Models;

namespace UrbanChaosStyleEditor.Services
{
    public static class SoundFxService
    {
        private static readonly Regex GroupLineRegex = new(
            @"^(.+?)=(.+)$", RegexOptions.Compiled);

        public static SoundFxData Load(string filePath)
        {
            var data = new SoundFxData();
            if (!File.Exists(filePath)) return data;

            string currentSection = "";

            foreach (var rawLine in File.ReadLines(filePath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).ToLowerInvariant();
                    continue;
                }

                var match = GroupLineRegex.Match(line);
                if (!match.Success) continue;

                string key = match.Groups[1].Value.Trim();
                string value = match.Groups[2].Value.Trim();

                if (currentSection == "groups")
                {
                    var parts = value.Split(',');
                    var group = new SoundFxGroup
                    {
                        Name = key,
                        Legacy1 = parts.Length > 0 ? ParseInt(parts[0]) : -1,
                        Legacy2 = parts.Length > 1 ? ParseInt(parts[1]) : -1,
                        Legacy3 = parts.Length > 2 ? ParseInt(parts[2]) : -1,
                        Legacy4 = parts.Length > 3 ? ParseInt(parts[3]) : -1,
                        SampleLow = parts.Length > 4 ? ParseInt(parts[4]) : -1,
                        SampleHigh = parts.Length > 5 ? ParseInt(parts[5]) : -1
                    };
                    data.Groups.Add(group);
                }
                else if (currentSection == "textures")
                {
                    var texMatch = Regex.Match(key, @"tex(\d+)", RegexOptions.IgnoreCase);
                    if (texMatch.Success && int.TryParse(texMatch.Groups[1].Value, out int texIndex)
                        && int.TryParse(value, out int groupIndex))
                    {
                        if (texIndex >= 0 && texIndex < 256)
                            data.TextureGroupMap[texIndex] = groupIndex;
                    }
                }
            }

            return data;
        }

        public static void ApplyToSlots(SoundFxData data, IList<TextureSlot> slots)
        {
            foreach (var kvp in data.TextureGroupMap)
            {
                if (kvp.Key >= 0 && kvp.Key < slots.Count)
                {
                    int groupIndex = kvp.Value;
                    if (groupIndex >= 0 && groupIndex < data.Groups.Count)
                        slots[kvp.Key].SoundGroup = data.Groups[groupIndex].Name;
                    else
                        slots[kvp.Key].SoundGroupIndex = groupIndex;
                }
            }
        }

        public static void Write(string filePath, SoundFxData data)
        {
            var sb = new StringBuilder();

            sb.AppendLine("[Groups]");
            foreach (var group in data.Groups)
            {
                sb.AppendLine($"{group.Name}={group.Legacy1},{group.Legacy2},{group.Legacy3},{group.Legacy4},{group.SampleLow},{group.SampleHigh}");
            }

            sb.AppendLine("[Textures]");
            foreach (var kvp in data.TextureGroupMap.OrderBy(k => k.Key))
            {
                sb.AppendLine($"tex{kvp.Key:D3}hi.tga={kvp.Value}");
            }

            File.WriteAllText(filePath, sb.ToString());
        }

        public static SoundFxData BuildFromSlots(IList<TextureSlot> slots, SoundFxData? existingData = null)
        {
            var data = new SoundFxData();

            if (existingData?.Groups.Count > 0)
            {
                foreach (var g in existingData.Groups)
                    data.Groups.Add(g);
            }
            else
            {
                // Default groups matching the game's standard set
                // Format: Legacy1,Legacy2,Legacy3,Legacy4,SampleLow,SampleHigh
                data.Groups.Add(new SoundFxGroup { Name = "forest", Legacy1 = 183, Legacy2 = 186, Legacy3 = -1, Legacy4 = -1, SampleLow = 284, SampleHigh = 287 });
                data.Groups.Add(new SoundFxGroup { Name = "glass", Legacy1 = 178, Legacy2 = 181, Legacy3 = -1, Legacy4 = -1, SampleLow = 279, SampleHigh = 282 });
                data.Groups.Add(new SoundFxGroup { Name = "grass", Legacy1 = 26, Legacy2 = 27, Legacy3 = -1, Legacy4 = -1, SampleLow = 46, SampleHigh = 49 });
                data.Groups.Add(new SoundFxGroup { Name = "gravel", Legacy1 = 28, Legacy2 = 29, Legacy3 = -1, Legacy4 = -1, SampleLow = 50, SampleHigh = 53 });
                data.Groups.Add(new SoundFxGroup { Name = "metal", Legacy1 = 32, Legacy2 = 33, Legacy3 = -1, Legacy4 = -1, SampleLow = 58, SampleHigh = 61 });
                data.Groups.Add(new SoundFxGroup { Name = "road (default)", Legacy1 = 30, Legacy2 = 31, Legacy3 = -1, Legacy4 = -1, SampleLow = 54, SampleHigh = 57 });
                data.Groups.Add(new SoundFxGroup { Name = "snow", Legacy1 = 195, Legacy2 = 198, Legacy3 = -1, Legacy4 = -1, SampleLow = 296, SampleHigh = 299 });
                data.Groups.Add(new SoundFxGroup { Name = "tiles/rock/ice", Legacy1 = 279, Legacy2 = 280, Legacy3 = -1, Legacy4 = -1, SampleLow = 397, SampleHigh = 400 });
                data.Groups.Add(new SoundFxGroup { Name = "water", Legacy1 = 34, Legacy2 = 35, Legacy3 = -1, Legacy4 = -1, SampleLow = 62, SampleHigh = 65 });
                data.Groups.Add(new SoundFxGroup { Name = "wood", Legacy1 = 22, Legacy2 = 23, Legacy3 = -1, Legacy4 = -1, SampleLow = 38, SampleHigh = 41 });
            }

            var groupNameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < data.Groups.Count; i++)
                groupNameToIndex[data.Groups[i].Name] = i;

            for (int i = 0; i < slots.Count && i < 256; i++)
            {
                var slot = slots[i];
                if (string.IsNullOrEmpty(slot.SoundGroup) && slot.SoundGroupIndex < 0)
                    continue;

                if (!string.IsNullOrEmpty(slot.SoundGroup) && groupNameToIndex.TryGetValue(slot.SoundGroup, out int idx))
                    data.TextureGroupMap[i] = idx;
                else if (slot.SoundGroupIndex >= 0)
                    data.TextureGroupMap[i] = slot.SoundGroupIndex;
            }

            return data;
        }

        private static int ParseInt(string s) =>
            int.TryParse(s.Trim(), out int v) ? v : -1;
    }

    public sealed class SoundFxData
    {
        public List<SoundFxGroup> Groups { get; } = new();
        public Dictionary<int, int> TextureGroupMap { get; } = new();
    }
}