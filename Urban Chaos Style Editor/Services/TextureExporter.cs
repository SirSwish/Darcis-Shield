// /Services/TextureExporter.cs
using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosStyleEditor.Models;

namespace UrbanChaosStyleEditor.Services
{
    public static class TextureExporter
    {
        /// <summary>
        /// Export a full project to a target folder.
        /// Creates: tex###hi.tga files, style.tma, sky.tga, optional textype.txt and soundfx.ini
        /// </summary>
        public static ExportResult Export(StyleProject project, string outputFolder)
        {
            int texturesExported = 0;
            int stylesExported = 0;

            try
            {
                Directory.CreateDirectory(outputFolder);

                // Export texture slots as TGA
                foreach (var slot in project.Slots)
                {
                    if (!slot.IsOccupied || slot.Image == null)
                        continue;

                    string tgaPath = Path.Combine(outputFolder, $"tex{slot.Index:D3}hi.tga");
                    WriteTga(slot, tgaPath);
                    texturesExported++;

                    // Also write PNG for the Map Editor's CustomTextures folder
                    string pngPath = Path.Combine(outputFolder, $"tex{slot.Index:D3}hi.png");
                    WritePng(slot.Image, pngPath);
                }

                // Export sky as 256x256 TGA
                if (project.SkyImage != null)
                {
                    var skyResized = ResizeTo(project.SkyImage, 256, 256);
                    string skyTgaPath = Path.Combine(outputFolder, "sky.tga");

                    // Sky should stay fully opaque unless you later decide otherwise.
                    WriteTga(skyResized, skyTgaPath, transparent: false, preserveSourceAlpha: false);
                }

                // Export style.tma
                if (project.Styles.Count > 0)
                {
                    string tmaPath = Path.Combine(outputFolder, "style.tma");
                    TmaWriter.Write(tmaPath, project);
                    stylesExported = project.Styles.Count;
                }

                // Export textype.txt (if any slots have flags set)
                if (project.Slots.Any(s => s.HasAnyFlags))
                {
                    string texTypePath = Path.Combine(outputFolder, "textype.txt");
                    TexTypeService.WriteFromSlots(texTypePath, project.Slots);
                }

                // Export soundfx.ini
                var sfxData = SoundFxService.BuildFromSlots(project.Slots, project.SoundFxData);
                if (sfxData.TextureGroupMap.Count > 0 || sfxData.Groups.Count > 0)
                {
                    string sfxPath = Path.Combine(outputFolder, "soundfx.ini");
                    SoundFxService.Write(sfxPath, sfxData);
                }

                return new ExportResult(true, null, texturesExported, stylesExported);
            }
            catch (Exception ex)
            {
                return new ExportResult(false, ex.Message, texturesExported, stylesExported);
            }
        }

        /// <summary>
        /// Slot-aware TGA export.
        ///
        /// T / Transparent     => generate cutout alpha from near-black pixels.
        /// M / AlphaMasked     => preserve source alpha for blended / masked textures.
        ///
        /// If both are set:
        /// - cutout wins for near-black pixels (alpha = 0)
        /// - existing source alpha is preserved for all other pixels
        /// </summary>
        public static void WriteTga(TextureSlot slot, string path)
        {
            if (slot.Image == null)
                throw new ArgumentNullException(nameof(slot.Image));

            bool useCutoutTransparency = slot.Transparent;
            bool preserveSourceAlpha = slot.AlphaTransparent;

            WriteTga(slot.Image, path, useCutoutTransparency, preserveSourceAlpha);
        }

        /// <summary>
        /// Core TGA writer.
        /// Always writes 32-bit uncompressed BGRA TGA.
        /// </summary>
        public static void WriteTga(
            BitmapSource source,
            string path,
            bool transparent,
            bool preserveSourceAlpha)
        {
            int w = source.PixelWidth;
            int h = source.PixelHeight;

            // Convert to BGRA32
            var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            bgra.Freeze();

            int stride = w * 4;
            var pixels = new byte[stride * h];
            bgra.CopyPixels(pixels, stride, 0);

            // Tune this if needed.
            // Exact black only would be:
            // bool isCutout = (r == 0 && g == 0 && b == 0);
            //
            // Using a threshold works better for dark "almost black" art.
            const byte cutoutThreshold = 8;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i + 0];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                byte srcA = pixels[i + 3];

                bool isNearBlack =
                    r <= cutoutThreshold &&
                    g <= cutoutThreshold &&
                    b <= cutoutThreshold;

                byte outA;

                if (transparent && preserveSourceAlpha)
                {
                    // Combined mode:
                    // - near-black becomes hard cutout
                    // - other pixels keep source alpha
                    outA = isNearBlack ? (byte)0 : srcA;
                }
                else if (transparent)
                {
                    // T flag only:
                    // hard cutout from near-black
                    outA = isNearBlack ? (byte)0 : (byte)255;
                }
                else if (preserveSourceAlpha)
                {
                    // M flag only:
                    // preserve alpha exactly from source
                    outA = srcA;
                }
                else
                {
                    // Ordinary opaque texture
                    outA = 255;
                }

                pixels[i + 3] = outA;

                // IMPORTANT:
                // The engine behaves badly with fully transparent pure-black texels.
                // Working textures keep a non-black RGB even when alpha = 0.
                //
                // So if a pixel is fully transparent and still black/near-black,
                // nudge it to a dark non-black colour.
                // Earlier working examples matched the pattern (0,0,17,0) in RGB/A.
                if (outA == 0 && r == 0 && g == 0 && b == 0)
                {
                    pixels[i + 0] = 17; // B
                    pixels[i + 1] = 0;  // G
                    pixels[i + 2] = 0;  // R
                }
            }

            using var fs = File.Create(path);
            using var writer = new BinaryWriter(fs);

            // 18-byte TGA header
            writer.Write((byte)0);     // ID length
            writer.Write((byte)0);     // Color map type
            writer.Write((byte)2);     // Image type: uncompressed true-color
            writer.Write((short)0);    // Color map first entry
            writer.Write((short)0);    // Color map length
            writer.Write((byte)0);     // Color map entry size
            writer.Write((short)0);    // X origin
            writer.Write((short)0);    // Y origin
            writer.Write((short)w);    // Width
            writer.Write((short)h);    // Height
            writer.Write((byte)32);    // Bits per pixel
            writer.Write((byte)0x00);  // Image descriptor: bottom-left origin

            // Pixel data bottom-to-top
            for (int row = h - 1; row >= 0; row--)
            {
                writer.Write(pixels, row * stride, stride);
            }
        }

        public static void WritePng(BitmapSource source, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var fs = File.Create(path);
            encoder.Save(fs);
        }

        public static void WriteBmp(BitmapSource source, string path)
        {
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var fs = File.Create(path);
            encoder.Save(fs);
        }

        public static BitmapSource ResizeTo(BitmapSource source, int width, int height)
        {
            if (source.PixelWidth == width && source.PixelHeight == height)
                return source;

            double scaleX = (double)width / source.PixelWidth;
            double scaleY = (double)height / source.PixelHeight;
            var scaled = new TransformedBitmap(source, new ScaleTransform(scaleX, scaleY));
            scaled.Freeze();
            return scaled;
        }
    }

    public sealed record ExportResult(
        bool Success,
        string? Error,
        int TexturesExported,
        int StylesExported);
}