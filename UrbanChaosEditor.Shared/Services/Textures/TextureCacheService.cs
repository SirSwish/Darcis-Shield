// /Services/Textures/TextureCacheService.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;


namespace UrbanChaosMapEditor.Services.Textures
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

        // NEW: which set to prefer when callers don’t include it in the key
        public string ActiveSet { get; set; } = "release"; // "release" or "beta"

        public async Task PreloadAllAsync(int decodeSize = 64)
        {
            var asm = Application.ResourceAssembly ?? typeof(TextureCacheService).Assembly;
            string? gResName = asm.GetManifestResourceNames()
                                  .FirstOrDefault(n => n.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase));
            if (gResName is null) { Progress?.Invoke(this, new(0, 0)); Completed?.Invoke(this, EventArgs.Empty); return; }

            using var resStream = asm.GetManifestResourceStream(gResName);
            if (resStream == null) { Progress?.Invoke(this, new(0, 0)); Completed?.Invoke(this, EventArgs.Empty); return; }

            var allResKeys = new List<string>();
            using (var reader = new ResourceReader(resStream))
            {
                foreach (DictionaryEntry entry in reader)
                {
                    if (entry.Key is string k &&
                        k.StartsWith("assets/textures/", StringComparison.OrdinalIgnoreCase) &&
                        k.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        allResKeys.Add(k);
                    }
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
                        // resourceKey like: assets/textures/release/world20/tex018hi.png
                        var relPath = resourceKey.Substring("assets/textures/".Length);
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

                        var uri = new Uri("pack://application:,,,/" + resourceKey.Replace('\\', '/'), UriKind.Absolute);

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.DecodePixelWidth = decodeSize;
                        bmp.UriSource = uri;
                        bmp.EndInit();
                        bmp.Freeze();

                        lock (_sync) { _byKey[finalKey] = bmp; }
                    }
                    catch { /* skip bad */ }
                    finally { done++; RaiseProgress(done, total); }
                }
            });

            Completed?.Invoke(this, EventArgs.Empty);
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
