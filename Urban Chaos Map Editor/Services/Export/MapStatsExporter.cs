using System.IO;
using System.Text;
using System.Threading.Tasks;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Prims;
using UrbanChaosMapEditor.Services.Textures;

namespace UrbanChaosMapEditor.Services.Export
{
    public readonly record struct MapStats(
        int MapVersion,
        int WorldTextureNumber,
        int Buildings,
        int Facets,
        int Walkables,
        int RoofTiles,
        int Prims);

    public static class MapStatsExporter
    {
        public static MapStats Compute(MapDataService data)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (!data.IsLoaded) throw new InvalidOperationException("No map loaded.");

            var bldSnap = new BuildingsAccessor(data).ReadSnapshot();
            var primSnap = new PrimsAccessor(data).ReadSnapshot();
            int worldTex = new TexturesAccessor(data).ReadTextureWorld();

            int walkables = Math.Max(0, bldSnap.Walkables.Length - 1);
            int roofTiles = Math.Max(0, bldSnap.RoofFaces4.Length - 1);

            return new MapStats(
                MapVersion: bldSnap.SaveType,
                WorldTextureNumber: worldTex,
                Buildings: bldSnap.Buildings.Length,
                Facets: bldSnap.Facets.Length,
                Walkables: walkables,
                RoofTiles: roofTiles,
                Prims: primSnap.Prims.Length);
        }

        public static async Task ExportAsync(MapDataService data, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path is required.", nameof(outputPath));

            var stats = Compute(data);

            var sb = new StringBuilder();
            string mapName = string.IsNullOrEmpty(data.CurrentPath)
                ? "(unsaved map)"
                : Path.GetFileName(data.CurrentPath);

            sb.AppendLine($"Map:                   {mapName}");
            if (!string.IsNullOrEmpty(data.CurrentPath))
                sb.AppendLine($"Path:                  {data.CurrentPath}");
            sb.AppendLine();
            sb.AppendLine($"Map Version:           {stats.MapVersion}");
            sb.AppendLine($"World Texture Number:  {stats.WorldTextureNumber}");
            sb.AppendLine($"Number of Buildings:   {stats.Buildings}");
            sb.AppendLine($"Number of Facets:      {stats.Facets}");
            sb.AppendLine($"Number of Walkables:   {stats.Walkables}");
            sb.AppendLine($"Number of Roof Tiles:  {stats.RoofTiles}");
            sb.AppendLine($"Number of Prims:       {stats.Prims}");

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(outputPath, sb.ToString());
        }

        public static string BuildOutputFileName(string mapPath, string outputDirectory)
        {
            var name = Path.GetFileNameWithoutExtension(mapPath);
            return Path.Combine(outputDirectory, $"{name}-Stats.txt");
        }
    }
}
