// Services/Textures/TextureLocator.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UrbanChaosMapEditor.Services.Textures
{
    public sealed class TextureLocator
    {
        private readonly int _worldNumber;
        private readonly string _variant; // "Release" or "Beta"
        private readonly Dictionary<string, BitmapSource> _cache = new(StringComparer.OrdinalIgnoreCase);

        public TextureLocator(int worldNumber, string variant = "Release")
        {
            _worldNumber = worldNumber;
            _variant = variant;
        }

        public bool TryGetTile(byte page, byte tx, byte ty, byte flip, out BitmapSource? bitmap)
        {
            bitmap = null;

            var path = GetTilePath(page, tx, ty);
            if (path == null || !File.Exists(path))
                return false;

            var key = $"{path}|flip={flip}";
            if (_cache.TryGetValue(key, out var cached))
            {
                bitmap = cached;
                return true;
            }

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();

                // Apply flip if needed
                bool fx = (flip & 0x01) != 0;
                bool fy = (flip & 0x02) != 0;
                if (fx || fy)
                {
                    var tb = new TransformedBitmap(bmp, new ScaleTransform(fx ? -1 : 1, fy ? -1 : 1));
                    tb.Freeze();
                    _cache[key] = tb;
                    bitmap = tb;
                }
                else
                {
                    _cache[key] = bmp;
                    bitmap = bmp;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string? GetTilePath(byte page, byte tx, byte ty)
        {
            // index = page * 64 + (ty * 8 + tx)
            int indexInPage = ty * 8 + tx;
            int totalImageIndex = page * 64 + indexInPage;
            string fileName = $"tex{totalImageIndex:D3}hi.bmp";

            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Textures", _variant);
            string folder;

            if (page <= 3)
            {
                folder = Path.Combine(basePath, $"world{_worldNumber}");
            }
            else if (page <= 7)
            {
                folder = Path.Combine(basePath, "shared");
            }
            else if (page == 8)
            {
                folder = Path.Combine(basePath, $"world{_worldNumber}", "insides");
            }
            else
            {
                return null; // out of known range
            }

            return Path.Combine(folder, fileName);
        }
    }
}
