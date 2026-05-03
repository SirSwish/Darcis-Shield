using System;
using System.IO;
using System.Text;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosPrimEditor.Models;

namespace UrbanChaosPrimEditor.Services
{
    public sealed class PrmParserService
    {
        // ── Struct sizes (bytes) ──────────────────────────────────────────────
        private const int PointSize       = PrimFormatConstants.PointSize;   // Int16 X, Y, Z
        private const int TriangleSize    = PrimFormatConstants.TriangleSize;  // see layout in Decode()
        private const int QuadrangleSize  = PrimFormatConstants.QuadrangleSize;  // see layout in Decode()

        // ── Header sizes ─────────────────────────────────────────────────────
        // NPRIM: 2-byte signature at byte 0, then 32-byte name → header = 50
        // PRIM : 32-byte name first, 6 dummy bytes + 2-byte signature at end → header = 56
        private const int NprimHeaderSize = PrimFormatConstants.NprimHeaderSize;
        private const int PrimHeaderSize  = PrimFormatConstants.PrimHeaderSize;

        public PrmModel Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is empty.", nameof(path));
            return Decode(Path.GetFileName(path), File.ReadAllBytes(path));
        }

        public PrmModel Decode(string fileName, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (data.Length < NprimHeaderSize)
                throw new InvalidDataException($"{fileName} is too small to be a PRM file.");

            // Detect format by magic bytes — NPRIM has signature 0x16A2 at offset 0 (little-endian).
            bool isNprim = data.Length >= 2 && data[0] == 0xA2 && data[1] == 0x16;

            // Header field offsets differ between the two formats.
            int nameOffset       = isNprim ? 2  : 0;
            int firstPointOffset = isNprim ? 34 : 32;
            int firstQuadOffset  = isNprim ? 38 : 36;
            int firstTriOffset   = isNprim ? 42 : 40;
            int collisionOffset  = isNprim ? 46 : 44;
            int headerSize       = isNprim ? NprimHeaderSize : PrimHeaderSize;

            var model = new PrmModel
            {
                FileName   = fileName,
                Signature  = isNprim ? ReadUInt16(data, 0) : ReadUInt16(data, 54),
                Name       = isNprim
                             ? ReadNullTerminatedString(data, nameOffset, 32)
                             : ReadFixedString(data, nameOffset, 32),
                FirstPointId       = ReadInt16(data, firstPointOffset),
                LastPointId        = ReadInt16(data, firstPointOffset + 2),
                FirstQuadrangleId  = ReadInt16(data, firstQuadOffset),
                LastQuadrangleId   = ReadInt16(data, firstQuadOffset + 2),
                FirstTriangleId    = ReadInt16(data, firstTriOffset),
                LastTriangleId     = ReadInt16(data, firstTriOffset + 2),
                CollisionType              = data[collisionOffset],
                ReactionToImpactByVehicle  = data[collisionOffset + 1],
                ShadowType                 = data[collisionOffset + 2],
                VariousProperties          = data[collisionOffset + 3]
            };

            int pointCount    = model.LastPointId       - model.FirstPointId;
            int triangleCount = model.LastTriangleId    - model.FirstTriangleId;
            int quadCount     = model.LastQuadrangleId  - model.FirstQuadrangleId;

            if (pointCount < 0 || triangleCount < 0 || quadCount < 0)
                throw new InvalidDataException(
                    $"{fileName} has invalid first/last index ranges: " +
                    $"points={model.FirstPointId}..{model.LastPointId}, " +
                    $"tris={model.FirstTriangleId}..{model.LastTriangleId}, " +
                    $"quads={model.FirstQuadrangleId}..{model.LastQuadrangleId}");

            // ── On-disk section order: points → triangles → quads ─────────────
            int pointsOffset    = headerSize;
            int trianglesOffset = pointsOffset    + pointCount    * PointSize;
            int quadsOffset     = trianglesOffset + triangleCount * TriangleSize;

            RequireLength(data, pointsOffset,    pointCount    * PointSize,      fileName, "points");
            RequireLength(data, trianglesOffset, triangleCount * TriangleSize,   fileName, "triangles");
            RequireLength(data, quadsOffset,     quadCount     * QuadrangleSize, fileName, "quadrangles");

            // ── Points ────────────────────────────────────────────────────────
            // GlobalId kept in the struct so the mesh builder can look them up
            // by the global IDs stored in face records.
            for (int i = 0, cur = pointsOffset; i < pointCount; i++, cur += PointSize)
            {
                model.Points.Add(new PrmPoint(
                    model.FirstPointId + i,   // GlobalId
                    ReadInt16(data, cur),      // X
                    ReadInt16(data, cur + 2),  // Y
                    ReadInt16(data, cur + 4))); // Z
            }

            // ── Triangles (28 bytes each) ─────────────────────────────────────
            // Byte layout:
            //  [0]    TexturePage
            //  [1]    DrawFlags
            //  [2-7]  PointAId, PointBId, PointCId  (3 × Int16)
            //  [8-13] UA, VA, UB, VB, UC, VC        (6 bytes)
            //  [14-19] BrightA, BrightB, BrightC    (3 × UInt16 — not used for rendering)
            //  [20-23] ThingIndex                   (Int32)
            //  [24]   FaceFlags/Col2
            //  [25]   Type
            //  [26-27] Padding/ID
            for (int i = 0, cur = trianglesOffset; i < triangleCount; i++, cur += TriangleSize)
            {
                model.Triangles.Add(new PrmTriangle(
                    data[cur],               // TexturePage
                    data[cur + 1],           // Properties/DrawFlags
                    ReadInt16(data, cur + 2), // PointAId
                    ReadInt16(data, cur + 4), // PointBId
                    ReadInt16(data, cur + 6), // PointCId
                    data[cur + 8],  data[cur + 9],   // UA, VA
                    data[cur + 10], data[cur + 11],  // UB, VB
                    data[cur + 12], data[cur + 13],  // UC, VC
                    data[cur + 14],  // BrightA (low byte of UInt16 at +14)
                    data[cur + 16],  // BrightB (low byte of UInt16 at +16)
                    data[cur + 18])); // BrightC (low byte of UInt16 at +18)
            }

            // ── Quadrangles (34 bytes each) ───────────────────────────────────
            // Byte layout:
            //  [0]     TexturePage
            //  [1]     DrawFlags
            //  [2-9]   PointAId, PointBId, PointCId, PointDId  (4 × Int16)
            //  [10-17] UA,VA, UB,VB, UC,VC, UD,VD              (8 bytes)
            //  [18-25] BrightA, BrightB, BrightC, BrightD      (4 × UInt16 — not used)
            //  [26-29] ThingIndex                               (Int32)
            //  [30]    FaceFlags/Col2
            //  [31]    Type
            //  [32-33] Padding/ID
            for (int i = 0, cur = quadsOffset; i < quadCount; i++, cur += QuadrangleSize)
            {
                model.Quadrangles.Add(new PrmQuadrangle(
                    data[cur],               // TexturePage
                    data[cur + 1],           // Properties/DrawFlags
                    ReadInt16(data, cur + 2), // PointAId
                    ReadInt16(data, cur + 4), // PointBId
                    ReadInt16(data, cur + 6), // PointCId
                    ReadInt16(data, cur + 8), // PointDId
                    data[cur + 10], data[cur + 11],  // UA, VA
                    data[cur + 12], data[cur + 13],  // UB, VB
                    data[cur + 14], data[cur + 15],  // UC, VC
                    data[cur + 16], data[cur + 17],  // UD, VD
                    data[cur + 18],  // BrightA (low byte of UInt16 at +18)
                    data[cur + 20],  // BrightB (low byte of UInt16 at +20)
                    data[cur + 22],  // BrightC (low byte of UInt16 at +22)
                    data[cur + 24])); // BrightD (low byte of UInt16 at +24)
            }

            return model;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static short  ReadInt16 (byte[] data, int offset) => BitConverter.ToInt16 (data, offset);
        private static ushort ReadUInt16(byte[] data, int offset) => BitConverter.ToUInt16(data, offset);

        private static string ReadFixedString(byte[] data, int offset, int length) =>
            Encoding.UTF8.GetString(data, offset, Math.Min(length, data.Length - offset))
                         .Trim('\0', '\n', '\r', '\t', ' ');

        private static string ReadNullTerminatedString(byte[] data, int offset, int maxLength)
        {
            int end = offset;
            int max = Math.Min(data.Length, offset + maxLength);
            while (end < max && data[end] != 0) end++;
            return Encoding.UTF8.GetString(data, offset, end - offset)
                               .Trim('\0', '\n', '\r', '\t', ' ');
        }

        private static void RequireLength(byte[] data, int offset, int length, string fileName, string section)
        {
            if (offset < 0 || length < 0 || offset + length > data.Length)
                throw new InvalidDataException(
                    $"{fileName} truncated reading {section}: " +
                    $"offset={offset}, need={length}, file={data.Length}");
        }
    }
}
