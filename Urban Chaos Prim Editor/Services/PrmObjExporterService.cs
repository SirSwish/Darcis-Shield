using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using UrbanChaosPrimEditor.Models;

namespace UrbanChaosPrimEditor.Services
{
    public sealed class PrmObjExporterService
    {
        private readonly PrimTextureResolverService? _textureResolver;
        private readonly Dictionary<int, (int Width, int Height)?> _textureSizeCache = new();

        private readonly record struct TextureSurfaceKey(int TextureId, int X, int Y, int Width, int Height);

        public PrmObjExporterService(PrimTextureResolverService? textureResolver)
        {
            _textureResolver = textureResolver;
        }

        public void Export(PrmModel model, string objPath)
        {
            ArgumentNullException.ThrowIfNull(model);
            if (string.IsNullOrWhiteSpace(objPath))
                throw new ArgumentException("OBJ export path is empty.", nameof(objPath));

            string directory = Path.GetDirectoryName(objPath) ?? Environment.CurrentDirectory;
            Directory.CreateDirectory(directory);

            string baseName = Path.GetFileNameWithoutExtension(objPath);
            string mtlFileName = baseName + ".mtl";
            string textureDirectory = Path.Combine(directory, baseName + "_textures");

            var pointLookup = model.Points.ToDictionary(p => p.GlobalId);
            var vertexIndexByPointId = model.Points
                .Select((p, i) => new { p.GlobalId, Index = i + 1 })
                .ToDictionary(x => x.GlobalId, x => x.Index);

            var obj = new StringBuilder();
            var mtl = new StringBuilder();
            var materialNames = new Dictionary<TextureSurfaceKey, string>();
            int vtIndex = 1;

            obj.AppendLine("# Exported from Urban Chaos Prim Editor");
            obj.AppendLine("# Source: " + model.FileName);
            obj.AppendLine("mtllib " + EscapeObjPath(mtlFileName));
            obj.AppendLine();

            foreach (PrmPoint p in model.Points)
                obj.AppendLine(FormattableString.Invariant($"v {p.X} {p.Y} {p.Z}"));

            obj.AppendLine();

            for (int faceIndex = 0; faceIndex < model.Triangles.Count; faceIndex++)
            {
                PrmTriangle tri = model.Triangles[faceIndex];
                if (!pointLookup.ContainsKey(tri.PointAId)) continue;
                if (!pointLookup.ContainsKey(tri.PointBId)) continue;
                if (!pointLookup.ContainsKey(tri.PointCId)) continue;

                var mapping = PrmUvService.CalculateTriangle(
                    tri.UA, tri.VA,
                    tri.UB, tri.VB,
                    tri.UC, tri.VC,
                    tri.TexturePage);

                Point[] uvs = { mapping.UV0, mapping.UV1, mapping.UV2 };
                TextureSurfaceKey surfaceKey = NormalizeFaceTextureSurface(mapping.TextureId, uvs);
                string materialName = GetOrCreateMaterial(materialNames, mtl, surfaceKey, textureDirectory);

                int vt0 = AppendTextureCoordinate(obj, uvs[0], ref vtIndex);
                int vt1 = AppendTextureCoordinate(obj, uvs[1], ref vtIndex);
                int vt2 = AppendTextureCoordinate(obj, uvs[2], ref vtIndex);

                obj.AppendLine("usemtl " + materialName);
                AppendFace(obj,
                    vertexIndexByPointId[tri.PointCId], vt2,
                    vertexIndexByPointId[tri.PointBId], vt1,
                    vertexIndexByPointId[tri.PointAId], vt0);
            }

            for (int faceIndex = 0; faceIndex < model.Quadrangles.Count; faceIndex++)
            {
                PrmQuadrangle quad = model.Quadrangles[faceIndex];
                if (!pointLookup.ContainsKey(quad.PointAId)) continue;
                if (!pointLookup.ContainsKey(quad.PointBId)) continue;
                if (!pointLookup.ContainsKey(quad.PointCId)) continue;
                if (!pointLookup.ContainsKey(quad.PointDId)) continue;

                var mapping = PrmUvService.CalculateQuad(
                    quad.UA, quad.VA,
                    quad.UB, quad.VB,
                    quad.UC, quad.VC,
                    quad.UD, quad.VD,
                    quad.TexturePage);

                Point uv0 = mapping.UV0;
                Point uv1 = mapping.UV1;
                Point uv2 = mapping.UV2;
                Point uv3 = mapping.UV3!.Value;
                Point[] uvs = { uv0, uv1, uv2, uv3 };
                TextureSurfaceKey surfaceKey = NormalizeFaceTextureSurface(mapping.TextureId, uvs);
                string materialName = GetOrCreateMaterial(materialNames, mtl, surfaceKey, textureDirectory);
                uv0 = uvs[0];
                uv1 = uvs[1];
                uv2 = uvs[2];
                uv3 = uvs[3];

                int vt0 = AppendTextureCoordinate(obj, uv0, ref vtIndex);
                int vt1 = AppendTextureCoordinate(obj, uv1, ref vtIndex);
                int vt2 = AppendTextureCoordinate(obj, uv2, ref vtIndex);
                int vt3 = AppendTextureCoordinate(obj, uv3, ref vtIndex);

                obj.AppendLine("usemtl " + materialName);
                AppendFace(obj,
                    vertexIndexByPointId[quad.PointDId], vt3,
                    vertexIndexByPointId[quad.PointBId], vt1,
                    vertexIndexByPointId[quad.PointAId], vt0);
                AppendFace(obj,
                    vertexIndexByPointId[quad.PointCId], vt2,
                    vertexIndexByPointId[quad.PointDId], vt3,
                    vertexIndexByPointId[quad.PointAId], vt0);
            }

            File.WriteAllText(objPath, obj.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, mtlFileName), mtl.ToString(), Encoding.UTF8);
        }

        private string GetOrCreateMaterial(
            Dictionary<TextureSurfaceKey, string> materialNames,
            StringBuilder mtl,
            TextureSurfaceKey surfaceKey,
            string textureDirectory)
        {
            if (materialNames.TryGetValue(surfaceKey, out string? existing))
                return existing;

            string materialName = $"tex_{surfaceKey.TextureId}_{surfaceKey.X}_{surfaceKey.Y}_{surfaceKey.Width}_{surfaceKey.Height}";
            materialName = materialName.Replace('-', 'n');
            materialNames[surfaceKey] = materialName;

            string? exportedTexturePath = ExportTexture(surfaceKey, textureDirectory);

            mtl.AppendLine("newmtl " + materialName);
            mtl.AppendLine("Ka 1.000000 1.000000 1.000000");
            mtl.AppendLine("Kd 1.000000 1.000000 1.000000");
            mtl.AppendLine("Ks 0.000000 0.000000 0.000000");
            mtl.AppendLine("d 1.000000");
            mtl.AppendLine("illum 1");
            if (exportedTexturePath is not null)
                mtl.AppendLine("map_Kd " + EscapeObjPath(exportedTexturePath));
            mtl.AppendLine();

            return materialName;
        }

        private string? ExportTexture(TextureSurfaceKey surfaceKey, string textureDirectory)
        {
            if (_textureResolver is null)
                return null;

            string? sourcePath = _textureResolver.ResolveTexturePath(surfaceKey.TextureId);
            if (sourcePath is null || !File.Exists(sourcePath))
                return null;

            BitmapSource? bitmap = LoadBitmap(sourcePath);
            if (bitmap is null)
                return null;

            bitmap = CropSurface(bitmap, surfaceKey);

            Directory.CreateDirectory(textureDirectory);
            string fileName = $"tex_{surfaceKey.TextureId}_{surfaceKey.X}_{surfaceKey.Y}_{surfaceKey.Width}_{surfaceKey.Height}.png";
            fileName = fileName.Replace('-', 'n');
            string outputPath = Path.Combine(textureDirectory, fileName);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream stream = File.Create(outputPath);
            encoder.Save(stream);

            return Path.Combine(Path.GetFileName(textureDirectory), fileName).Replace('\\', '/');
        }

        private TextureSurfaceKey NormalizeFaceTextureSurface(int textureId, Point[] uvs)
        {
            (int Width, int Height)? size = GetTextureSize(textureId);
            if (size is not { } textureSize)
                return new TextureSurfaceKey(textureId, 0, 0, 0, 0);

            double minU = uvs.Min(uv => uv.X);
            double maxU = uvs.Max(uv => uv.X);
            double minV = uvs.Min(uv => uv.Y);
            double maxV = uvs.Max(uv => uv.Y);

            minU = SnapUvEdge(minU);
            maxU = SnapUvEdge(maxU);
            minV = SnapUvEdge(minV);
            maxV = SnapUvEdge(maxV);

            if (NearlyEqual(minU, maxU) || NearlyEqual(minV, maxV))
                return new TextureSurfaceKey(textureId, 0, 0, textureSize.Width, textureSize.Height);

            for (int i = 0; i < uvs.Length; i++)
            {
                double u = (SnapUvEdge(uvs[i].X) - minU) / (maxU - minU);
                double v = (SnapUvEdge(uvs[i].Y) - minV) / (maxV - minV);
                uvs[i] = new Point(u, v);
            }

            int x = ClampPixel((int)Math.Floor(minU * textureSize.Width), textureSize.Width);
            int y = ClampPixel((int)Math.Floor(minV * textureSize.Height), textureSize.Height);
            int right = ClampPixel((int)Math.Ceiling(maxU * textureSize.Width), textureSize.Width);
            int bottom = ClampPixel((int)Math.Ceiling(maxV * textureSize.Height), textureSize.Height);

            if (right <= x) right = Math.Min(textureSize.Width, x + 1);
            if (bottom <= y) bottom = Math.Min(textureSize.Height, y + 1);

            return new TextureSurfaceKey(textureId, x, y, right - x, bottom - y);
        }

        private (int Width, int Height)? GetTextureSize(int textureId)
        {
            if (_textureSizeCache.TryGetValue(textureId, out (int Width, int Height)? cached))
                return cached;

            (int Width, int Height)? size = null;
            if (_textureResolver is not null)
            {
                string? path = _textureResolver.ResolveTexturePath(textureId);
                if (path is not null && File.Exists(path))
                {
                    BitmapSource? bitmap = LoadBitmap(path);
                    if (bitmap is not null)
                        size = (bitmap.PixelWidth, bitmap.PixelHeight);
                }
            }

            _textureSizeCache[textureId] = size;
            return size;
        }

        private static BitmapSource? LoadBitmap(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".tga")
                return TgaLoader.Load(path);

            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(path, UriKind.Absolute);
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource CropSurface(BitmapSource bitmap, TextureSurfaceKey surfaceKey)
        {
            if (surfaceKey.Width <= 0 || surfaceKey.Height <= 0)
                return bitmap;

            if (surfaceKey.X == 0 &&
                surfaceKey.Y == 0 &&
                surfaceKey.Width == bitmap.PixelWidth &&
                surfaceKey.Height == bitmap.PixelHeight)
            {
                return bitmap;
            }

            int width = Math.Min(surfaceKey.Width, bitmap.PixelWidth - surfaceKey.X);
            int height = Math.Min(surfaceKey.Height, bitmap.PixelHeight - surfaceKey.Y);
            if (width <= 0 || height <= 0)
                return bitmap;

            var cropped = new CroppedBitmap(bitmap, new Int32Rect(surfaceKey.X, surfaceKey.Y, width, height));
            cropped.Freeze();
            return cropped;
        }

        private static int AppendTextureCoordinate(StringBuilder obj, Point uv, ref int vtIndex)
        {
            int index = vtIndex++;
            obj.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"vt {uv.X:0.######} {1.0 - uv.Y:0.######}"));
            return index;
        }

        private static void AppendFace(
            StringBuilder obj,
            int v0,
            int vt0,
            int v1,
            int vt1,
            int v2,
            int vt2)
        {
            obj.AppendLine(FormattableString.Invariant($"f {v0}/{vt0} {v1}/{vt1} {v2}/{vt2}"));
        }

        private static bool NearlyEqual(double a, double b)
        {
            return Math.Abs(a - b) < 0.00001;
        }

        private static double SnapUvEdge(double value)
        {
            if (Math.Abs(value) < 0.00001)
                return 0.0;

            if (Math.Abs(value - (31.0 / 32.0)) < 0.00001 || Math.Abs(value - 1.0) < 0.00001)
                return 1.0;

            return value;
        }

        private static int ClampPixel(int value, int max)
        {
            return Math.Clamp(value, 0, max);
        }

        private static string EscapeObjPath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
