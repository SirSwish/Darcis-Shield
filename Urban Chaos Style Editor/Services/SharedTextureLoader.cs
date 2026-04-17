// /Services/SharedTextureLoader.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;

namespace UrbanChaosStyleEditor.Services
{
    /// <summary>
    /// Loads the game's built-in shared textures (pages 4-7, absolute indices 256-511)
    /// from the embedded WPF resources in UrbanChaosEditor.Shared.
    /// These textures never change — they are the same for every world.
    /// Results are cached after the first call.
    /// </summary>
    public static class SharedTextureLoader
    {
        // Shared textures occupy pages 4-7.  Each page holds 64 slots.
        // Absolute index = page * 64 + ty * 8 + tx  →  256 .. 511
        private const int FirstIndex = 256;
        private const int LastIndex  = 511;

        private static Dictionary<int, BitmapSource>? _cache;

        public static IReadOnlyDictionary<int, BitmapSource> Load()
        {
            if (_cache != null) return _cache;

            _cache = new Dictionary<int, BitmapSource>();

            for (int i = FirstIndex; i <= LastIndex; i++)
            {
                // WPF Resource embedded in UrbanChaosEditor.Shared assembly.
                // GetResourceStream throws IOException for missing entries — catch it.
                var uri = new Uri(
                    $"pack://application:,,,/UrbanChaosEditor.Shared;component/" +
                    $"Assets/Textures/Release/shared/tex{i}hi.png",
                    UriKind.Absolute);

                try
                {
                    var sri = Application.GetResourceStream(uri);
                    if (sri?.Stream == null) continue;

                    using var stream = sri.Stream;
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 64;
                    bmp.DecodePixelHeight = 64;
                    bmp.StreamSource = stream;
                    bmp.EndInit();
                    bmp.Freeze();
                    _cache[i] = bmp;
                }
                catch { /* resource absent or undecodable — skip */ }
            }

            return _cache;
        }

        public static BitmapSource? TryGet(int absoluteIndex)
        {
            Load();
            return _cache!.TryGetValue(absoluteIndex, out var bmp) ? bmp : null;
        }
    }
}
