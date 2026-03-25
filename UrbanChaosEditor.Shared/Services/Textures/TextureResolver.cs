// /Services/Textures/TextureResolver.cs
// Shared helper for resolving textures from cache, embedded resources, or disk.
// Use this instead of direct pack:// URIs to support custom textures.

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UrbanChaosEditor.Shared.Services.Textures
{
    public static class TextureResolver
    {
        private const string TexturesAsm = "UrbanChaosEditor.Shared";

        /// <summary>
        /// Resolve a texture by page/tx/ty/flip for a given world and variant.
        /// Tries: TextureCacheService -> embedded resource -> CustomTextures on disk.
        /// </summary>
        public static bool TryResolve(int page, byte tx, byte ty, byte flip, int worldNumber,
            string variant, out BitmapSource? bmp)
        {
            bmp = null;
            if (worldNumber <= 0) return false;

            string subfolder;
            if (page <= 3) subfolder = $"world{worldNumber}";
            else if (page <= 7) subfolder = "shared";
            else subfolder = $"world{worldNumber}";

            int totalIndex = page * 64 + ty * 8 + tx;

            // Build the cache key
            string cacheFolder;
            if (page <= 3) cacheFolder = $"world{worldNumber}";
            else if (page <= 7) cacheFolder = "shared";
            else cacheFolder = $"shared_prims";

            string relKey = $"{cacheFolder}_{totalIndex:D3}";

            // 1. Try TextureCacheService (covers both embedded and custom textures)
            var cache = TextureCacheService.Instance;
            if (cache.TryGetRelative(relKey, out var cached) && cached != null)
            {
                bmp = ApplyFlip(cached, flip);
                return true;
            }

            // 2. Try embedded resource (pack URI)
            string setName = variant?.ToLowerInvariant() ?? "release";
            string packUri = $"pack://application:,,,/{TexturesAsm};component/Assets/Textures/{setName}/{subfolder}/tex{totalIndex:D3}hi.png";
            try
            {
                var sri = Application.GetResourceStream(new Uri(packUri));
                if (sri?.Stream != null)
                {
                    var baseBmp = new BitmapImage();
                    baseBmp.BeginInit();
                    baseBmp.CacheOption = BitmapCacheOption.OnLoad;
                    baseBmp.StreamSource = sri.Stream;
                    baseBmp.EndInit();
                    baseBmp.Freeze();

                    bmp = ApplyFlip(baseBmp, flip);
                    return true;
                }
            }
            catch { }

            // 3. Try CustomTextures on disk
            var diskPath = ResolveCustomTexturePath(worldNumber, totalIndex);
            if (diskPath != null)
            {
                try
                {
                    var diskBmp = new BitmapImage();
                    diskBmp.BeginInit();
                    diskBmp.CacheOption = BitmapCacheOption.OnLoad;
                    diskBmp.UriSource = new Uri(diskPath, UriKind.Absolute);
                    diskBmp.EndInit();
                    diskBmp.Freeze();

                    bmp = ApplyFlip(diskBmp, flip);
                    return true;
                }
                catch { }
            }

            return false;
        }

        /// <summary>
        /// Resolve a texture by total index (0-255 for world, 256-511 for shared) for a given world.
        /// </summary>
        public static bool TryResolveByIndex(int totalIndex, int worldNumber, string variant, out BitmapSource? bmp)
        {
            byte page = (byte)(totalIndex / 64);
            byte remainder = (byte)(totalIndex % 64);
            byte ty = (byte)(remainder / 8);
            byte tx = (byte)(remainder % 8);
            return TryResolve(page, tx, ty, 0, worldNumber, variant, out bmp);
        }

        /// <summary>
        /// Resolve a texture for the paint palette (indices 0-127, world textures only).
        /// </summary>
        public static bool TryResolvePalette(int textureIndex, int worldNumber, string variant, out BitmapSource? bmp)
        {
            bmp = null;
            if (worldNumber <= 0 || textureIndex < 0) return false;

            string relKey = $"world{worldNumber}_{textureIndex:D3}";

            var cache = TextureCacheService.Instance;
            if (cache.TryGetRelative(relKey, out var cached) && cached != null)
            {
                bmp = cached;
                return true;
            }

            // Fallback to pack URI
            string setName = variant?.ToLowerInvariant() ?? "release";
            string packUri = $"pack://application:,,,/{TexturesAsm};component/Assets/Textures/{setName}/world{worldNumber}/tex{textureIndex:D3}hi.png";
            try
            {
                var sri = Application.GetResourceStream(new Uri(packUri));
                if (sri?.Stream != null)
                {
                    var baseBmp = new BitmapImage();
                    baseBmp.BeginInit();
                    baseBmp.CacheOption = BitmapCacheOption.OnLoad;
                    baseBmp.StreamSource = sri.Stream;
                    baseBmp.EndInit();
                    baseBmp.Freeze();
                    bmp = baseBmp;
                    return true;
                }
            }
            catch { }

            // Fallback to disk
            var diskPath = ResolveCustomTexturePath(worldNumber, textureIndex);
            if (diskPath != null)
            {
                try
                {
                    var diskBmp = new BitmapImage();
                    diskBmp.BeginInit();
                    diskBmp.CacheOption = BitmapCacheOption.OnLoad;
                    diskBmp.UriSource = new Uri(diskPath, UriKind.Absolute);
                    diskBmp.EndInit();
                    diskBmp.Freeze();
                    bmp = diskBmp;
                    return true;
                }
                catch { }
            }

            return false;
        }

        private static BitmapSource ApplyFlip(BitmapSource source, byte flip)
        {
            bool flipX = (flip & 0x01) != 0;
            bool flipY = (flip & 0x02) != 0;

            if (!flipX && !flipY) return source;

            var tb = new TransformedBitmap(source, new ScaleTransform(flipX ? -1 : 1, flipY ? -1 : 1));
            tb.Freeze();
            return tb;
        }

        private static string? ResolveCustomTexturePath(int worldNumber, int textureIndex)
        {
            var customRoot = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "CustomTextures", $"world{worldNumber}");

            if (!Directory.Exists(customRoot)) return null;

            string baseName = $"tex{textureIndex:D3}hi";
            foreach (var ext in new[] { ".png", ".bmp", ".tga" })
            {
                var path = Path.Combine(customRoot, baseName + ext);
                if (File.Exists(path)) return path;
            }

            return null;
        }
    }
}