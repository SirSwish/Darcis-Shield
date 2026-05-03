using System.IO;
using System.Text;
using System.Threading.Tasks;
using UrbanChaosLightEditor.Services;

namespace UrbanChaosLightEditor.Services.Export
{
    public static class LightStatsExporter
    {
        public static async Task ExportAsync(LightsDataService data, string outputPath)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (!data.IsLoaded) throw new InvalidOperationException("No lights file loaded.");
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path is required.", nameof(outputPath));

            var acc = new LightsAccessor(data);
            var hdr = acc.ReadHeader();
            var entries = acc.ReadAllEntries();
            var props = acc.ReadProperties();
            var sky = acc.ReadNightColour();

            int usedCount = 0;
            for (int i = 1; i < entries.Count; i++) // skip sentinel at index 0
                if (entries[i].Used == 1) usedCount++;

            bool dayTime = (props.NightFlag & LightsAccessor.NIGHT_FLAG_DAYTIME) != 0;
            bool lampsOn = (props.NightFlag & LightsAccessor.NIGHT_FLAG_LIGHTS_UNDER_LAMPOSTS) != 0;
            bool darken  = (props.NightFlag & LightsAccessor.NIGHT_FLAG_DARKEN_BUILDING_POINTS) != 0;

            byte lampR = unchecked((byte)(props.NightLampostRed   + 128));
            byte lampG = unchecked((byte)(props.NightLampostGreen + 128));
            byte lampB = unchecked((byte)(props.NightLampostBlue  + 128));

            var sb = new StringBuilder();
            string fileName = string.IsNullOrEmpty(data.CurrentPath) ? "(unsaved lights)" : Path.GetFileName(data.CurrentPath);

            sb.AppendLine($"File:                    {fileName}");
            if (!string.IsNullOrEmpty(data.CurrentPath))
                sb.AppendLine($"Path:                    {data.CurrentPath}");
            sb.AppendLine();

            sb.AppendLine($"File Version:            {hdr.Version}");
            sb.AppendLine($"Light Entry Size:        {hdr.SizeOfEdLightLower}");
            sb.AppendLine($"Lights:                  {hdr.EdMaxLights}");
            sb.AppendLine($"Number Of Lights:        {usedCount}");
            sb.AppendLine();

            sb.AppendLine($"Flags:                   {props.NightFlag}");
            sb.AppendLine($"Lamps On:                {(lampsOn ? "On" : "Off")}");
            sb.AppendLine($"Darken Buildings:        {(darken ? "Yes" : "No")}");
            sb.AppendLine($"Day Time:                {(dayTime ? "Day" : "Night")}");
            sb.AppendLine();

            sb.AppendLine($"D3D Color:               ({props.D3DRed}, {props.D3DGreen}, {props.D3DBlue}, {props.D3DAlpha})");
            sb.AppendLine($"Ambient (Rgba):          ({props.D3DRed}, {props.D3DGreen}, {props.D3DBlue}, {props.D3DAlpha})");
            sb.AppendLine($"Specular (Rgba):         ({props.SpecularRed}, {props.SpecularGreen}, {props.SpecularBlue}, {props.SpecularAlpha})");
            sb.AppendLine($"Night Ambient (Rgb):     ({props.NightAmbRed}, {props.NightAmbGreen}, {props.NightAmbBlue})");
            sb.AppendLine();

            sb.AppendLine("Color Of Prim Lights");
            sb.AppendLine($"  Rgb:                   ({lampR}, {lampG}, {lampB})");
            sb.AppendLine($"  Radius:                {props.NightLampostRadius}");
            sb.AppendLine();

            sb.AppendLine($"Night Sky:               {DominantChannelName(sky.Red, sky.Green, sky.Blue)}");
            sb.AppendLine($"Night Sky Rgb:           ({sky.Red}, {sky.Green}, {sky.Blue})");

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(outputPath, sb.ToString());
        }

        public static string BuildOutputFileName(string lightsPath, string outputDirectory)
        {
            var name = Path.GetFileNameWithoutExtension(lightsPath);
            return Path.Combine(outputDirectory, $"{name}-Stats.txt");
        }

        private static string DominantChannelName(byte r, byte g, byte b)
        {
            if (r == g && g == b) return "Neutral";
            if (r >= g && r >= b) return "Red";
            if (g >= r && g >= b) return "Green";
            return "Blue";
        }
    }
}
