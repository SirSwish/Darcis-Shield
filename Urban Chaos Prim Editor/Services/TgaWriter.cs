// Services/TgaWriter.cs
// Writes a BitmapSource as an uncompressed 32-bit BGRA TGA (type 2).
// Compatible with TgaLoader which reads the same format back.

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UrbanChaosPrimEditor.Services
{
    public static class TgaWriter
    {
        public static void Write(string path, BitmapSource source)
        {
            // Normalise to Bgra32 so the byte layout is always predictable.
            BitmapSource bitmap = source.Format == PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            int w      = bitmap.PixelWidth;
            int h      = bitmap.PixelHeight;
            int stride = w * 4;

            byte[] pixels = new byte[h * stride];
            bitmap.CopyPixels(pixels, stride, 0);

            using FileStream   fs = File.Create(path);
            using BinaryWriter bw = new(fs);

            // ── 18-byte TGA header ────────────────────────────────────────
            bw.Write((byte)0);       // ID field length
            bw.Write((byte)0);       // colour-map type: none
            bw.Write((byte)2);       // image type: uncompressed true-colour
            // Colour-map spec (5 bytes, unused)
            bw.Write((short)0);      // first entry index
            bw.Write((short)0);      // colour-map length
            bw.Write((byte)0);       // colour-map entry size
            // Image spec
            bw.Write((short)0);      // x origin
            bw.Write((short)0);      // y origin
            bw.Write((short)w);
            bw.Write((short)h);
            bw.Write((byte)32);      // bits per pixel (BGRA)
            bw.Write((byte)0x08);    // image descriptor: 8 alpha bits, bottom-left origin

            // ── Pixel data ────────────────────────────────────────────────
            // TGA origin is bottom-left; WPF bitmaps are top-left — reverse rows.
            for (int y = h - 1; y >= 0; y--)
                bw.Write(pixels, y * stride, stride);
        }
    }
}
