using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UrbanChaosPrimEditor.Models;

namespace UrbanChaosPrimEditor.Services
{
    public sealed class PrmWriterService
    {
        private const int PointSize = 6;
        private const int TriangleSize = 28;
        private const int QuadrangleSize = 34;
        private const int NprimHeaderSize = 50;
        private const int PrimHeaderSize = 56;

        public byte[] Encode(PrmModel model, byte[] originalBytes)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(originalBytes);

            bool isNprim = originalBytes.Length >= 2 && originalBytes[0] == 0xA2 && originalBytes[1] == 0x16;
            int headerSize = isNprim ? NprimHeaderSize : PrimHeaderSize;
            if (originalBytes.Length < headerSize)
                throw new InvalidDataException($"{model.FileName} is too small to save as a PRM file.");

            int firstPointOffset = isNprim ? 34 : 32;
            int firstQuadOffset = isNprim ? 38 : 36;
            int firstTriOffset = isNprim ? 42 : 40;
            int collisionOffset = isNprim ? 46 : 44;

            var points = RepackPoints(model);
            RepackFacePointIds(model, points.IdMap);

            int firstPointId = points.FirstPointId;
            int firstTriangleId = model.FirstTriangleId;
            int firstQuadrangleId = model.FirstQuadrangleId;
            int lastPointId = firstPointId + points.Points.Count;
            int lastTriangleId = firstTriangleId + model.Triangles.Count;
            int lastQuadrangleId = firstQuadrangleId + model.Quadrangles.Count;

            ValidateInt16("first point id", firstPointId);
            ValidateInt16("last point id", lastPointId);
            ValidateInt16("first triangle id", firstTriangleId);
            ValidateInt16("last triangle id", lastTriangleId);
            ValidateInt16("first quadrangle id", firstQuadrangleId);
            ValidateInt16("last quadrangle id", lastQuadrangleId);

            int pointCount = model.LastPointId - model.FirstPointId;
            int triangleCount = model.LastTriangleId - model.FirstTriangleId;
            int quadrangleCount = model.LastQuadrangleId - model.FirstQuadrangleId;
            int originalPointsOffset = headerSize;
            int originalTrianglesOffset = originalPointsOffset + Math.Max(0, pointCount) * PointSize;
            int originalQuadsOffset = originalTrianglesOffset + Math.Max(0, triangleCount) * TriangleSize;

            int size = headerSize
                + points.Points.Count * PointSize
                + model.Triangles.Count * TriangleSize
                + model.Quadrangles.Count * QuadrangleSize;

            var output = new byte[size];
            Array.Copy(originalBytes, output, headerSize);

            WriteInt16(output, firstPointOffset, firstPointId);
            WriteInt16(output, firstPointOffset + 2, lastPointId);
            WriteInt16(output, firstQuadOffset, firstQuadrangleId);
            WriteInt16(output, firstQuadOffset + 2, lastQuadrangleId);
            WriteInt16(output, firstTriOffset, firstTriangleId);
            WriteInt16(output, firstTriOffset + 2, lastTriangleId);
            output[collisionOffset] = CheckedByte(model.CollisionType, "collision type");
            output[collisionOffset + 1] = CheckedByte(model.ReactionToImpactByVehicle, "reaction to vehicle");
            output[collisionOffset + 2] = CheckedByte(model.ShadowType, "shadow type");
            output[collisionOffset + 3] = CheckedByte(model.VariousProperties, "various properties");

            int cursor = headerSize;
            foreach (PrmPoint point in points.Points)
            {
                WriteInt16(output, cursor, point.X);
                WriteInt16(output, cursor + 2, point.Y);
                WriteInt16(output, cursor + 4, point.Z);
                cursor += PointSize;
            }

            for (int i = 0; i < model.Triangles.Count; i++, cursor += TriangleSize)
            {
                byte[] record = CopyOriginalRecord(originalBytes, originalTrianglesOffset + i * TriangleSize, TriangleSize);
                PatchTriangle(record, model.Triangles[i]);
                Array.Copy(record, 0, output, cursor, TriangleSize);
            }

            for (int i = 0; i < model.Quadrangles.Count; i++, cursor += QuadrangleSize)
            {
                byte[] record = CopyOriginalRecord(originalBytes, originalQuadsOffset + i * QuadrangleSize, QuadrangleSize);
                PatchQuadrangle(record, model.Quadrangles[i]);
                Array.Copy(record, 0, output, cursor, QuadrangleSize);
            }

            model.FirstPointId = firstPointId;
            model.LastPointId = lastPointId;
            model.FirstTriangleId = firstTriangleId;
            model.LastTriangleId = lastTriangleId;
            model.FirstQuadrangleId = firstQuadrangleId;
            model.LastQuadrangleId = lastQuadrangleId;

            return output;
        }

        private static RepackedPoints RepackPoints(PrmModel model)
        {
            int firstPointId = model.FirstPointId;
            var sorted = model.Points.OrderBy(p => p.GlobalId).ToList();
            var idMap = new Dictionary<int, short>(sorted.Count);
            var repacked = new List<PrmPoint>(sorted.Count);

            for (int i = 0; i < sorted.Count; i++)
            {
                int newId = firstPointId + i;
                ValidateInt16("point id", newId);
                idMap.Add(sorted[i].GlobalId, (short)newId);
                repacked.Add(sorted[i] with { GlobalId = newId });
            }

            model.Points.Clear();
            model.Points.AddRange(repacked);
            return new RepackedPoints(firstPointId, repacked, idMap);
        }

        private static void RepackFacePointIds(PrmModel model, IReadOnlyDictionary<int, short> idMap)
        {
            for (int i = 0; i < model.Triangles.Count; i++)
            {
                PrmTriangle t = model.Triangles[i];
                model.Triangles[i] = t with
                {
                    PointAId = RemapPointId(idMap, t.PointAId),
                    PointBId = RemapPointId(idMap, t.PointBId),
                    PointCId = RemapPointId(idMap, t.PointCId)
                };
            }

            for (int i = 0; i < model.Quadrangles.Count; i++)
            {
                PrmQuadrangle q = model.Quadrangles[i];
                model.Quadrangles[i] = q with
                {
                    PointAId = RemapPointId(idMap, q.PointAId),
                    PointBId = RemapPointId(idMap, q.PointBId),
                    PointCId = RemapPointId(idMap, q.PointCId),
                    PointDId = RemapPointId(idMap, q.PointDId)
                };
            }
        }

        private static short RemapPointId(IReadOnlyDictionary<int, short> idMap, short pointId)
        {
            if (idMap.TryGetValue(pointId, out short remapped)) return remapped;
            throw new InvalidDataException($"Face references missing point #{pointId}.");
        }

        private static byte[] CopyOriginalRecord(byte[] originalBytes, int offset, int size)
        {
            var record = new byte[size];
            if (offset >= 0 && offset + size <= originalBytes.Length)
                Array.Copy(originalBytes, offset, record, 0, size);
            return record;
        }

        private static void PatchTriangle(byte[] record, PrmTriangle triangle)
        {
            record[0] = triangle.TexturePage;
            record[1] = triangle.Properties;
            WriteInt16(record, 2, triangle.PointAId);
            WriteInt16(record, 4, triangle.PointBId);
            WriteInt16(record, 6, triangle.PointCId);
            record[8] = triangle.UA;
            record[9] = triangle.VA;
            record[10] = triangle.UB;
            record[11] = triangle.VB;
            record[12] = triangle.UC;
            record[13] = triangle.VC;
            record[14] = triangle.BrightA;
            record[16] = triangle.BrightB;
            record[18] = triangle.BrightC;
        }

        private static void PatchQuadrangle(byte[] record, PrmQuadrangle quadrangle)
        {
            record[0] = quadrangle.TexturePage;
            record[1] = quadrangle.Properties;
            WriteInt16(record, 2, quadrangle.PointAId);
            WriteInt16(record, 4, quadrangle.PointBId);
            WriteInt16(record, 6, quadrangle.PointCId);
            WriteInt16(record, 8, quadrangle.PointDId);
            record[10] = quadrangle.UA;
            record[11] = quadrangle.VA;
            record[12] = quadrangle.UB;
            record[13] = quadrangle.VB;
            record[14] = quadrangle.UC;
            record[15] = quadrangle.VC;
            record[16] = quadrangle.UD;
            record[17] = quadrangle.VD;
            record[18] = quadrangle.BrightA;
            record[20] = quadrangle.BrightB;
            record[22] = quadrangle.BrightC;
            record[24] = quadrangle.BrightD;
        }

        private static void WriteInt16(byte[] data, int offset, int value)
        {
            ValidateInt16("value", value);
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static byte CheckedByte(int value, string name)
        {
            if (value is < byte.MinValue or > byte.MaxValue)
                throw new InvalidDataException($"{name} value {value} is outside byte range.");
            return (byte)value;
        }

        private static void ValidateInt16(string name, int value)
        {
            if (value is < short.MinValue or > short.MaxValue)
                throw new InvalidDataException($"{name} value {value} is outside Int16 range.");
        }

        private sealed record RepackedPoints(
            int FirstPointId,
            List<PrmPoint> Points,
            IReadOnlyDictionary<int, short> IdMap);
    }
}
