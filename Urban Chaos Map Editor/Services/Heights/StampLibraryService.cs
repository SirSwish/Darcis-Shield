// /Services/Heights/StampLibraryService.cs
using System.IO;
using System.Text.Json;
using UrbanChaosMapEditor.Models.Heights;

namespace UrbanChaosMapEditor.Services.Heights
{
    /// <summary>
    /// Manages the list of available height stamps (built-in + custom).
    /// Custom stamps are persisted as .stamp.json files in the CustomStamps folder
    /// next to the running executable.
    /// </summary>
    public sealed class StampLibraryService
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        private static readonly Lazy<StampLibraryService> _lazy =
            new(() => new StampLibraryService());
        public static StampLibraryService Instance => _lazy.Value;

        // ── State ──────────────────────────────────────────────────────────────
        private readonly List<HeightStamp> _stamps = new();
        public IReadOnlyList<HeightStamp> Stamps => _stamps;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            WriteIndented = true
        };

        private StampLibraryService()
        {
            LoadBuiltIn();
            LoadCustom();
        }

        // ── Paths ──────────────────────────────────────────────────────────────
        private static string CustomStampsFolder =>
            Path.Combine(
                Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "CustomStamps");

        // ── Built-in stamps ────────────────────────────────────────────────────
        private void LoadBuiltIn()
        {
            _stamps.Add(CreateDitchStamp());
        }

        /// <summary>
        /// Replicates the original ApplyDitchTemplate logic as a 9×9 HeightStamp.
        /// Origin is col=4, row=4 (centre of a 0-indexed 9×9 grid).
        ///
        /// Original logic (cx,cy = centre tile):
        ///   dx -4..1, dy -4..4 → -32
        ///   dx 2..3, dy -2     → -26
        ///   dx 2..3, dy -1     → -20
        ///   dx 2..3, dy  0     → -13
        ///   dx 2..3, dy  1     → -7
        ///   dx 2..3, dy  2     → 0
        /// All other cells in the 9×9 footprint are left as 0 (no-write).
        /// We encode them as 0 (ground level) so the stamp writes them absolutely.
        /// </summary>
        private static HeightStamp CreateDitchStamp()
        {
            const int W = 9, H = 9;
            var values = new sbyte[W * H]; // default 0

            for (int row = 0; row < H; row++)
            {
                int dy = row - 4; // dy ranges -4..4

                for (int col = 0; col < W; col++)
                {
                    int dx = col - 4; // dx ranges -4..4

                    sbyte val = 0;

                    if (dx >= -4 && dx <= 1)
                    {
                        val = -32;
                    }
                    else if (dx == 2 || dx == 3)
                    {
                        // ramp only for dy -2..2; outside that stays 0
                        val = dy switch
                        {
                            -2 => -26,
                            -1 => -20,
                             0 => -13,
                             1 => -7,
                             2 => 0,
                            _ => 0
                        };
                    }

                    values[row * W + col] = val;
                }
            }

            return new HeightStamp
            {
                Name = "Ditch",
                Width = W,
                Height = H,
                Values = values,
                IsBuiltIn = true
            };
        }

        // ── Custom stamp persistence ───────────────────────────────────────────
        private void LoadCustom()
        {
            string folder = CustomStampsFolder;
            if (!Directory.Exists(folder)) return;

            foreach (var file in Directory.EnumerateFiles(folder, "*.stamp.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var stamp = JsonSerializer.Deserialize<HeightStamp>(json, _jsonOpts);
                    if (stamp != null && stamp.Width > 0 && stamp.Height > 0 &&
                        stamp.Values.Length == stamp.Width * stamp.Height)
                    {
                        _stamps.Add(stamp);
                    }
                }
                catch
                {
                    // Skip corrupt files silently
                }
            }
        }

        /// <summary>Saves a new custom stamp to disk and adds it to the library.</summary>
        public void SaveCustomStamp(HeightStamp stamp)
        {
            string folder = CustomStampsFolder;
            Directory.CreateDirectory(folder);

            // Build a safe filename from the stamp name
            string safeName = string.Concat(stamp.Name
                .Split(Path.GetInvalidFileNameChars()))
                .Replace(" ", "_");
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "stamp";

            // Avoid collisions
            string path = Path.Combine(folder, $"{safeName}.stamp.json");
            int counter = 1;
            while (File.Exists(path))
                path = Path.Combine(folder, $"{safeName}_{counter++}.stamp.json");

            var json = JsonSerializer.Serialize(stamp, _jsonOpts);
            File.WriteAllText(path, json);

            _stamps.Add(stamp);
        }

        /// <summary>Removes a custom stamp from the library and deletes its file.</summary>
        public void DeleteCustomStamp(HeightStamp stamp)
        {
            if (stamp.IsBuiltIn) return;
            _stamps.Remove(stamp);

            string folder = CustomStampsFolder;
            if (!Directory.Exists(folder)) return;

            string safeName = string.Concat(stamp.Name
                .Split(Path.GetInvalidFileNameChars()))
                .Replace(" ", "_");

            // Try to find and remove matching file(s)
            foreach (var file in Directory.EnumerateFiles(folder, $"{safeName}*.stamp.json"))
            {
                try { File.Delete(file); } catch { }
                break;
            }
        }
    }
}
