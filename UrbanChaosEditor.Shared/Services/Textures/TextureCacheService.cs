// /Services/Textures/TextureCacheService.cs
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;


namespace UrbanChaosEditor.Shared.Services.Textures
{
    public sealed class TextureCacheService
    {
        private static readonly Lazy<TextureCacheService> _lazy = new(() => new TextureCacheService());
        public static TextureCacheService Instance => _lazy.Value;

        private readonly Dictionary<string, BitmapSource> _byKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new();

        private static readonly Regex IdRegex = new(@"(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public event EventHandler<TextureProgressEventArgs>? Progress;
        public event EventHandler? Completed;

        private TextureCacheService() { }

        // NEW: which set to prefer when callers don't include it in the key
        public string ActiveSet { get; set; } = "release"; // "release" or "beta"

        public async Task PreloadAllAsync(int decodeSize = 64)
        {
            // CRITICAL: Use the Shared assembly where textures are embedded, NOT Application.ResourceAssembly
            var asm = typeof(TextureCacheService).Assembly;

            Debug.WriteLine($"[TextureCacheService] Loading from assembly: {asm.FullName}");
            Debug.WriteLine($"[TextureCacheService] Assembly location: {asm.Location}");

            var manifestNames = asm.GetManifestResourceNames();
            Debug.WriteLine($"[TextureCacheService] Manifest resource names ({manifestNames.Length}):");
            foreach (var name in manifestNames)
            {
                Debug.WriteLine($"  - {name}");
            }

            string? gResName = manifestNames
                                  .FirstOrDefault(n => n.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase));

            if (gResName is null)
            {
                Debug.WriteLine("[TextureCacheService] ERROR: No .g.resources found!");
                Progress?.Invoke(this, new(0, 0));
                Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

            Debug.WriteLine($"[TextureCacheService] Using .g.resources: {gResName}");

            using var resStream = asm.GetManifestResourceStream(gResName);
            if (resStream == null)
            {
                Debug.WriteLine("[TextureCacheService] ERROR: Could not open resource stream!");
                Progress?.Invoke(this, new(0, 0));
                Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

            var allResKeys = new List<string>();
            using (var reader = new ResourceReader(resStream))
            {
                Debug.WriteLine("[TextureCacheService] All resources in .g.resources:");
                int count = 0;
                foreach (DictionaryEntry entry in reader)
                {
                    if (entry.Key is string k)
                    {
                        // Log first 50 resources to see the pattern
                        if (count < 50)
                        {
                            Debug.WriteLine($"  [{count}] {k}");
                        }
                        count++;

                        // Check for textures - try multiple possible paths
                        if ((k.Contains("textures", StringComparison.OrdinalIgnoreCase) ||
                             k.Contains("world", StringComparison.OrdinalIgnoreCase) ||
                             k.Contains("shared", StringComparison.OrdinalIgnoreCase)) &&
                            k.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            allResKeys.Add(k);
                        }
                    }
                }
                Debug.WriteLine($"[TextureCacheService] Total resources: {count}");
            }

            Debug.WriteLine($"[TextureCacheService] Found {allResKeys.Count} texture resources");
            if (allResKeys.Count > 0)
            {
                Debug.WriteLine("[TextureCacheService] Sample texture paths:");
                foreach (var k in allResKeys.Take(10))
                {
                    Debug.WriteLine($"  - {k}");
                }
            }

            // Determine the base path pattern from the first texture
            string basePath = "";
            if (allResKeys.Count > 0)
            {
                var sample = allResKeys[0];
                // Find where "release" or "beta" starts
                int releaseIdx = sample.IndexOf("release", StringComparison.OrdinalIgnoreCase);
                int betaIdx = sample.IndexOf("beta", StringComparison.OrdinalIgnoreCase);
                int setIdx = releaseIdx >= 0 ? releaseIdx : betaIdx;
                if (setIdx > 0)
                {
                    basePath = sample.Substring(0, setIdx);
                    Debug.WriteLine($"[TextureCacheService] Detected base path: '{basePath}'");
                }
            }

            var texKeys = allResKeys.ToList();
            int total = texKeys.Count;
            int done = 0;

            await Task.Run(() =>
            {
                foreach (var resourceKey in texKeys)
                {
                    try
                    {
                        // Strip the base path to get: release/world20/tex018hi.png
                        var relPath = string.IsNullOrEmpty(basePath)
                            ? resourceKey
                            : resourceKey.Substring(basePath.Length);

                        var parts = relPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 1) { done++; RaiseProgress(done, total); continue; }

                        // FIRST part is the set: "release" or "beta"
                        var set = parts[0].ToLowerInvariant(); // release | beta

                        // Remaining folders (may be 0+): world20 / shared / prims / ...
                        var folderParts = parts.Skip(1).ToArray();
                        if (folderParts.Length == 0) { done++; RaiseProgress(done, total); continue; }

                        var fileName = folderParts[^1]; // e.g., tex018hi.png
                        var nameNoExt = Path.GetFileNameWithoutExtension(fileName); // tex018hi
                        var matches = IdRegex.Matches(nameNoExt);
                        if (matches.Count == 0) { done++; RaiseProgress(done, total); continue; }
                        var numericId = matches[^1].Value; // last number group, e.g., 018

                        // build folder key from everything BEFORE filename
                        var folderKey = string.Join("_", folderParts.Take(folderParts.Length - 1));
                        var relativeKey = string.IsNullOrEmpty(folderKey)
                            ? numericId
                            : $"{folderKey}_{numericId}";

                        // final key disambiguated by set
                        var finalKey = $"{set}_{relativeKey}";  // e.g., release_world20_018

                        // Build the pack URI using the SHARED assembly name
                        var uri = new Uri($"pack://application:,,,/UrbanChaosEditor.Shared;component/{resourceKey.Replace('\\', '/')}", UriKind.Absolute);

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.DecodePixelWidth = decodeSize;
                        bmp.UriSource = uri;
                        bmp.EndInit();
                        bmp.Freeze();

                        lock (_sync) { _byKey[finalKey] = bmp; }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[TextureCacheService] Error loading {resourceKey}: {ex.Message}");
                    }
                    finally { done++; RaiseProgress(done, total); }
                }
            });

            Debug.WriteLine($"[TextureCacheService] Preload complete. Loaded {Count} embedded textures.");
            if (Count > 0)
            {
                Debug.WriteLine("[TextureCacheService] Sample keys in cache:");
                lock (_sync)
                {
                    foreach (var k in _byKey.Keys.Take(10))
                    {
                        Debug.WriteLine($"  - {k}");
                    }
                }
            }

            // Load custom textures from disk (CustomTextures/ folder next to .exe)
            await LoadCustomTexturesFromDiskAsync(decodeSize);

            Completed?.Invoke(this, EventArgs.Empty);
        }

        private async Task LoadCustomTexturesFromDiskAsync(int decodeSize)
        {
            var customRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomTextures");
            Debug.WriteLine($"[CustomTex] Looking for custom textures at: {customRoot}");
            Debug.WriteLine($"[CustomTex] BaseDirectory = {AppDomain.CurrentDomain.BaseDirectory}");

            if (!Directory.Exists(customRoot))
            {
                Debug.WriteLine($"[CustomTex] FOLDER NOT FOUND: {customRoot}");
                return;
            }

            Debug.WriteLine($"[CustomTex] Folder exists. Scanning...");

            var dirs = Directory.GetDirectories(customRoot);
            Debug.WriteLine($"[CustomTex] Found {dirs.Length} subdirectories:");
            foreach (var d in dirs)
                Debug.WriteLine($"[CustomTex]   dir: {d}");

            var pngFiles = new List<(string fullPath, string set, string folder, string fileName)>();

            foreach (var dir in dirs)
            {
                var dirName = Path.GetFileName(dir).ToLowerInvariant();
                Debug.WriteLine($"[CustomTex] Processing directory: {dirName}");

                if (dirName == "release" || dirName == "beta")
                {
                    foreach (var subDir in Directory.GetDirectories(dir))
                    {
                        var folderName = Path.GetFileName(subDir).ToLowerInvariant();
                        var pngs = Directory.GetFiles(subDir, "*.png");
                        Debug.WriteLine($"[CustomTex]   {dirName}/{folderName}: {pngs.Length} PNGs");
                        foreach (var png in pngs)
                            pngFiles.Add((png, dirName, folderName, Path.GetFileName(png)));
                    }
                }
                else
                {
                    var pngs = Directory.GetFiles(dir, "*.png");
                    var tgas = Directory.GetFiles(dir, "*.tga");
                    var bmps = Directory.GetFiles(dir, "*.bmp");
                    Debug.WriteLine($"[CustomTex]   {dirName}: {pngs.Length} PNGs, {tgas.Length} TGAs, {bmps.Length} BMPs");

                    // Load PNGs
                    foreach (var png in pngs)
                    {
                        pngFiles.Add((png, "release", dirName, Path.GetFileName(png)));
                        pngFiles.Add((png, "beta", dirName, Path.GetFileName(png)));
                    }

                    // Also try BMPs (in case user only has BMPs)
                    foreach (var bmp in bmps)
                    {
                        var bmpName = Path.GetFileName(bmp);
                        if (bmpName.Equals("sky.bmp", StringComparison.OrdinalIgnoreCase))
                            continue; // skip sky
                        pngFiles.Add((bmp, "release", dirName, bmpName));
                        pngFiles.Add((bmp, "beta", dirName, bmpName));
                    }
                }
            }

            if (pngFiles.Count == 0)
            {
                Debug.WriteLine("[CustomTex] NO texture files found in any subdirectory.");
                Debug.WriteLine("[CustomTex] Expected structure: CustomTextures/world21/tex000hi.png");
                return;
            }

            Debug.WriteLine($"[CustomTex] Found {pngFiles.Count} texture file entries to load.");
            foreach (var f in pngFiles.Take(10))
                Debug.WriteLine($"[CustomTex]   sample: set={f.set} folder={f.folder} file={f.fileName}");

            int loaded = 0;
            int failed = 0;

            await Task.Run(() =>
            {
                foreach (var (fullPath, set, folder, fileName) in pngFiles)
                {
                    try
                    {
                        var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                        var matches = IdRegex.Matches(nameNoExt);
                        if (matches.Count == 0)
                        {
                            Debug.WriteLine($"[CustomTex] SKIP (no numeric id): {fileName}");
                            continue;
                        }

                        var numericId = matches[^1].Value;
                        var finalKey = $"{set}_{folder}_{numericId}";

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.DecodePixelWidth = decodeSize;
                        bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                        bmp.EndInit();
                        bmp.Freeze();

                        lock (_sync) { _byKey[finalKey] = bmp; }
                        loaded++;

                        if (loaded <= 5)
                            Debug.WriteLine($"[CustomTex] Loaded: {fileName} -> key='{finalKey}'");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Debug.WriteLine($"[CustomTex] FAILED: {fullPath} -> {ex.Message}");
                    }
                }
            });

            Debug.WriteLine($"[CustomTex] Complete. Loaded={loaded}, Failed={failed}");

            // Verify what's in the cache for custom worlds
            lock (_sync)
            {
                var customKeys = _byKey.Keys
                    .Where(k => k.Contains("world2", StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .ToList();

                Debug.WriteLine($"[CustomTex] Sample custom world keys in cache ({customKeys.Count}):");
                foreach (var k in customKeys)
                    Debug.WriteLine($"[CustomTex]   '{k}'");
            }
        }

        private void RaiseProgress(int done, int total)
            => Progress?.Invoke(this, new TextureProgressEventArgs(done, total));

        /// exact-key lookup: expects "release_world20_018" (or "beta_shared_prims_003")
        public bool TryGet(string key, out BitmapSource? bitmap)
        { lock (_sync) return _byKey.TryGetValue(key, out bitmap); }

        /// convenience: lookup by relative key (e.g., "world20_018") using ActiveSet
        public bool TryGetRelative(string relativeKey, out BitmapSource? bitmap)
        {
            var full = $"{ActiveSet}_{relativeKey}";
            lock (_sync) return _byKey.TryGetValue(full, out bitmap);
        }

        public int Count { get { lock (_sync) return _byKey.Count; } }

        /// Enumerate relative keys like "world20_208" filtered by folder prefix (e.g. "world20", "shared", "shared_prims")
        /// 
        public IEnumerable<string> EnumerateRelativeKeys(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) yield break;

            string setPrefix = $"{ActiveSet.ToLowerInvariant()}_"; // "release_" or "beta_"
            var results = new List<string>();

            lock (_sync)
            {
                foreach (var fullKey in _byKey.Keys) // e.g. "release_shared_prims_081"
                {
                    if (!fullKey.StartsWith(setPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // strip "release_" / "beta_"
                    string rel = fullKey.Substring(setPrefix.Length); // e.g. "shared_prims_081"

                    int us = rel.LastIndexOf('_');
                    if (us <= 0) continue;

                    string folderPart = rel.Substring(0, us); // e.g. "shared_prims"
                                                              // Exact folder match, not prefix-of
                    if (!folderPart.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    results.Add(rel); // keep relative key, e.g. "shared_prims_081"
                }
            }

            // Sort by numeric tail then alpha
            results.Sort((a, b) =>
            {
                static int TailNum(string s)
                {
                    int us = s.LastIndexOf('_');
                    return us >= 0 && int.TryParse(s[(us + 1)..], out var n) ? n : int.MinValue;
                }
                int na = TailNum(a), nb = TailNum(b);
                if (na != int.MinValue && nb != int.MinValue) return na.CompareTo(nb);
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            foreach (var r in results) yield return r;
        }

    }
    public sealed class TextureProgressEventArgs : EventArgs
    {
        public int Done { get; }
        public int Total { get; }
        public double Percent => Total == 0 ? 100.0 : 100.0 * Done / Total;

        public TextureProgressEventArgs(int done, int total)
        {
            Done = done;
            Total = total;
        }
    }
}