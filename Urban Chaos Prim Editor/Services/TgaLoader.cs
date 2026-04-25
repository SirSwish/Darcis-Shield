// Services/TgaLoader.cs
// Minimal TGA decoder for uncompressed (type 2) and RLE-compressed (type 10)
// 24-bit (BGR) and 32-bit (BGRA) targa files. Returns a frozen BitmapSource
// suitable for use as an ImageBrush.

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UrbanChaosPrimEditor.Services
{
    public static class TgaLoader
    {
        public static BitmapSource? Load(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                return Decode(bytes);
            }
            catch
            {
                return null;
            }
        }

        public static BitmapSource? Decode(byte[] data)
        {
            if (data is null || data.Length < 18) return null;

            int idLength    = data[0];
            int colorMapType= data[1];
            int imageType   = data[2];
            int width       = data[12] | (data[13] << 8);
            int height      = data[14] | (data[15] << 8);
            int bpp         = data[16];
            int descriptor  = data[17];

            if (colorMapType != 0) return null;
            if (width <= 0 || height <= 0) return null;
            if (bpp != 24 && bpp != 32) return null;
            if (imageType != 2 && imageType != 10) return null;

            int bytesPerPixel = bpp / 8;
            int pixelCount    = width * height;
            int dataOffset    = 18 + idLength;

            byte[] pixels = new byte[pixelCount * bytesPerPixel];

            if (imageType == 2)
            {
                if (dataOffset + pixels.Length > data.Length) return null;
                System.Buffer.BlockCopy(data, dataOffset, pixels, 0, pixels.Length);
            }
            else // RLE
            {
                if (!DecodeRle(data, dataOffset, pixels, bytesPerPixel, pixelCount))
                    return null;
            }

            // TGA origin: bit 5 of descriptor cleared = bottom-left, set = top-left.
            bool topDown = (descriptor & 0x20) != 0;
            if (!topDown) FlipVertical(pixels, width, height, bytesPerPixel);

            PixelFormat format = bpp == 32 ? PixelFormats.Bgra32 : PixelFormats.Bgr24;
            int stride = width * bytesPerPixel;

            var bitmap = BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
            bitmap.Freeze();
            return bitmap;
        }

        private static bool DecodeRle(byte[] src, int srcOffset, byte[] dst, int bpp, int pixelCount)
        {
            int written = 0;
            int cursor = srcOffset;

            while (written < pixelCount)
            {
                if (cursor >= src.Length) return false;
                byte packetHeader = src[cursor++];
                int count = (packetHeader & 0x7F) + 1;
                bool isRun = (packetHeader & 0x80) != 0;

                if (isRun)
                {
                    if (cursor + bpp > src.Length) return false;
                    for (int i = 0; i < count && written < pixelCount; i++, written++)
                    {
                        for (int b = 0; b < bpp; b++)
                            dst[written * bpp + b] = src[cursor + b];
                    }
                    cursor += bpp;
                }
                else
                {
                    int rawByteCount = count * bpp;
                    if (cursor + rawByteCount > src.Length) return false;
                    for (int i = 0; i < count && written < pixelCount; i++, written++)
                    {
                        for (int b = 0; b < bpp; b++)
                            dst[written * bpp + b] = src[cursor + i * bpp + b];
                    }
                    cursor += rawByteCount;
                }
            }
            return true;
        }

        private static void FlipVertical(byte[] pixels, int width, int height, int bpp)
        {
            int stride = width * bpp;
            byte[] row = new byte[stride];
            for (int y = 0; y < height / 2; y++)
            {
                int top = y * stride;
                int bot = (height - 1 - y) * stride;
                System.Buffer.BlockCopy(pixels, top, row, 0, stride);
                System.Buffer.BlockCopy(pixels, bot, pixels, top, stride);
                System.Buffer.BlockCopy(row, 0, pixels, bot, stride);
            }
        }
    }
}
