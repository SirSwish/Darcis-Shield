using System;
using System.Drawing;
using System.Runtime.InteropServices;
using MediaColor = System.Windows.Media.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using ImageLockMode = System.Drawing.Imaging.ImageLockMode;
using BitmapData = System.Drawing.Imaging.BitmapData;

namespace UrbanChaosLightEditor.Services
{
    public static class LightPreviewRenderer
    {
        public static Bitmap ApplyPreviewLighting(
            Bitmap source,
            MediaColor filterColor,
            float filterStrength = 1.0f)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            filterStrength = Clamp01(filterStrength);

            Bitmap src = EnsureArgb32(source);
            Bitmap dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            Rectangle rect = new Rectangle(0, 0, src.Width, src.Height);
            BitmapData srcData = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = dst.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                int byteCount = Math.Abs(srcData.Stride) * src.Height;
                byte[] srcBytes = new byte[byteCount];
                byte[] dstBytes = new byte[byteCount];

                Marshal.Copy(srcData.Scan0, srcBytes, 0, byteCount);

                float fR = filterColor.R / 255f;
                float fG = filterColor.G / 255f;
                float fB = filterColor.B / 255f;

                for (int y = 0; y < src.Height; y++)
                {
                    int row = y * srcData.Stride;

                    for (int x = 0; x < src.Width; x++)
                    {
                        int i = row + (x * 4);

                        float b = srcBytes[i + 0] / 255f;
                        float g = srcBytes[i + 1] / 255f;
                        float r = srcBytes[i + 2] / 255f;
                        byte a = srcBytes[i + 3];

                        float outR = r * Lerp(1.0f, fR, filterStrength);
                        float outG = g * Lerp(1.0f, fG, filterStrength);
                        float outB = b * Lerp(1.0f, fB, filterStrength);

                        dstBytes[i + 0] = (byte)(Clamp01(outB) * 255f);
                        dstBytes[i + 1] = (byte)(Clamp01(outG) * 255f);
                        dstBytes[i + 2] = (byte)(Clamp01(outR) * 255f);
                        dstBytes[i + 3] = a;
                    }
                }

                Marshal.Copy(dstBytes, 0, dstData.Scan0, byteCount);
            }
            finally
            {
                src.UnlockBits(srcData);
                dst.UnlockBits(dstData);

                if (!ReferenceEquals(src, source))
                    src.Dispose();
            }

            return dst;
        }

        private static Bitmap EnsureArgb32(Bitmap input)
        {
            if (input.PixelFormat == PixelFormat.Format32bppArgb)
                return input;

            Bitmap clone = new Bitmap(input.Width, input.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(clone))
            {
                g.DrawImage(input, 0, 0, input.Width, input.Height);
            }
            return clone;
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + ((b - a) * t);
        }
    }
}