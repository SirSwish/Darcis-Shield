// /Services/TextureCacheService.cs
// Updated to load textures from UrbanChaosEditor.Shared assembly
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace UrbanChaosLightEditor.Services
{
    /// <summary>
    /// Caches texture images loaded from embedded resources.
    /// Textures are loaded from the UrbanChaosEditor.Shared assembly.
    /// </summary>
    public sealed class TextureCacheService
    {
        private static readonly Lazy<TextureCacheService> _lazy = new(() => new TextureCacheService());
        public static TextureCacheService Instance => _lazy.Value;

        private readonly Dictionary<string, BitmapSource> _byKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new();

        private static readonly Regex IdRegex = new(@"(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public event EventHandler<TextureProgressEventArgs>? Progress;
        public event EventHandler? Completed;

        private TextureCacheService()
        {
            Debug.WriteLine("[TextureCacheService] Singleton instance created");
        }

        /// <summary>Which texture set to use: "release" or "beta"</summary>
        public string ActiveSet { get; set; } = "release";

        /// <summary>
        /// The assembly name containing the textures.
        /// Default: "UrbanChaosEditor.Shared"
        /// </summary>
        public string TextureAssemblyName { get; set; } = "UrbanChaosEditor.Shared";

        public async Task PreloadAllAsync(int decodeSize = 64)
        {
            Debug.WriteLine("[TextureCacheService.PreloadAllAsync] Starting...");

            // Try to load the Shared assembly containing textures
            Assembly? asm = null;
            string? gResName = null;

            // Try multiple assembly name formats
            var assemblyNamesToTry = new[]
            {
                TextureAssemblyName,
                "Urban Chaos Editor.Shared",  // With spaces
                "UrbanChaosEditor.Shared"
            };

            foreach (var asmName in assemblyNamesToTry)
            {
                try
                {
                    asm = Assembly.Load(asmName);
                    if (asm != null)
                    {
                        gResName = asm.GetManifestResourceNames()
                                      .FirstOrDefault(n => n.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase));

                        if (gResName != null)
                        {
                            Debug.WriteLine($"[TextureCacheService] Found assembly '{asmName}' with resource: {gResName}");
                            break;
                        }
                        else
                        {
                            Debug.WriteLine($"[TextureCacheService] Assembly '{asmName}' loaded but no .g.resources found");
                            Debug.WriteLine($"[TextureCacheService] Available resources: {string.Join(", ", asm.GetManifestResourceNames())}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TextureCacheService] Could not load assembly '{asmName}': {ex.Message}");
                }
            }

            if (asm == null || gResName == null)
            {
                Debug.WriteLine("[TextureCacheService] No assembly with .g.resources found - textures will not load");
                Progress?.Invoke(this, new(0, 0));
                Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

            using var resStream = asm.GetManifestResourceStream(gResName);
            if (resStream == null)
            {
                Debug.WriteLine($"[TextureCacheService] Could not open resource stream for {gResName}");
                Progress?.Invoke(this, new(0, 0));
                Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

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

            Debug.WriteLine($"[TextureCacheService] Found {total} texture resources in {asm.GetName().Name}");

            // Store the assembly name for URI construction
            var loadedAsmName = asm.GetName().Name;

            await Task.Run(() =>
            {
                foreach (var resourceKey in texKeys)
                {
                    try
                    {
                        var relPath = resourceKey.Substring("assets/textures/".Length);
                        var parts = relPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 1) { done++; RaiseProgress(done, total); continue; }

                        var set = parts[0].ToLowerInvariant();
                        var folderParts = parts.Skip(1).ToArray();
                        if (folderParts.Length == 0) { done++; RaiseProgress(done, total); continue; }

                        var fileName = folderParts[^1];
                        var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                        var matches = IdRegex.Matches(nameNoExt);
                        if (matches.Count == 0) { done++; RaiseProgress(done, total); continue; }
                        var numericId = matches[^1].Value;

                        var folderKey = string.Join("_", folderParts.Take(folderParts.Length - 1));
                        var relativeKey = string.IsNullOrEmpty(folderKey)
                            ? numericId
                            : $"{folderKey}_{numericId}";

                        var finalKey = $"{set}_{relativeKey}";

                        // Use the Shared assembly in the pack URI
                        var uri = new Uri($"pack://application:,,,/{loadedAsmName};component/{resourceKey.Replace('\\', '/')}", UriKind.Absolute);

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
                        if (done < 5) // Only log first few errors
                            Debug.WriteLine($"[TextureCacheService] Error loading texture: {ex.Message}");
                    }
                    finally { done++; RaiseProgress(done, total); }
                }
            });

            Debug.WriteLine($"[TextureCacheService] Loaded {Count} textures");
            Completed?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseProgress(int done, int total)
            => Progress?.Invoke(this, new TextureProgressEventArgs(done, total));

        /// <summary>Exact key lookup (e.g., "release_world20_018")</summary>
        public bool TryGet(string key, out BitmapSource? bitmap)
        {
            lock (_sync) return _byKey.TryGetValue(key, out bitmap);
        }

        /// <summary>Lookup by relative key using ActiveSet (e.g., "world20_018")</summary>
        public bool TryGetRelative(string relativeKey, out BitmapSource? bitmap)
        {
            var full = $"{ActiveSet}_{relativeKey}";
            lock (_sync) return _byKey.TryGetValue(full, out bitmap);
        }

        public int Count { get { lock (_sync) return _byKey.Count; } }
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