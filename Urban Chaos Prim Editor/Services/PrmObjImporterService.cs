using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UrbanChaosPrimEditor.Models;

namespace UrbanChaosPrimEditor.Services
{
    public sealed class PrmObjImporterService
    {
        private const int MaxVertexCount = 5000;

        public PrmObjImportResult Import(string objPath)
        {
            if (string.IsNullOrWhiteSpace(objPath))
                throw new ArgumentException("OBJ import path is empty.", nameof(objPath));

            var vertices = new List<PrmPoint>();
            var triangles = new List<PrmTriangle>();
            var quadrangles = new List<PrmQuadrangle>();

            foreach (string rawLine in File.ReadLines(objPath))
            {
                string line = StripComment(rawLine).Trim();
                if (line.Length == 0) continue;

                string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                if (parts[0] == "v" && parts.Length >= 4)
                {
                    if (vertices.Count >= MaxVertexCount)
                        throw new InvalidDataException($"OBJ import limit exceeded: PRM editor supports at most {MaxVertexCount:N0} vertices.");

                    vertices.Add(new PrmPoint(
                        vertices.Count,
                        ParseCoordinate(parts[1], "X"),
                        ParseCoordinate(parts[2], "Y"),
                        ParseCoordinate(parts[3], "Z")));
                }
                else if (parts[0] == "f" && parts.Length >= 4)
                {
                    int[] pointIds = parts
                        .Skip(1)
                        .Select(part => ParseFaceVertex(part, vertices.Count))
                        .ToArray();

                    if (pointIds.Length == 3)
                    {
                        triangles.Add(CreateTriangle(pointIds[0], pointIds[1], pointIds[2]));
                    }
                    else if (pointIds.Length == 4)
                    {
                        quadrangles.Add(CreateQuadrangle(pointIds[0], pointIds[1], pointIds[2], pointIds[3]));
                    }
                    else
                    {
                        for (int i = 1; i < pointIds.Length - 1; i++)
                            triangles.Add(CreateTriangle(pointIds[0], pointIds[i], pointIds[i + 1]));
                    }
                }
            }

            if (vertices.Count == 0)
                throw new InvalidDataException("OBJ contains no vertices.");

            string name = Path.GetFileNameWithoutExtension(objPath);
            string fileName = name + ".prm";
            var model = PrmTemplateService.CreateEmptyModel(fileName, name);
            model.LastPointId = vertices.Count;
            model.LastQuadrangleId = quadrangles.Count;
            model.LastTriangleId = triangles.Count;

            model.Points.AddRange(vertices);
            model.Triangles.AddRange(triangles);
            model.Quadrangles.AddRange(quadrangles);

            return new PrmObjImportResult(model, PrmTemplateService.CreateNprimHeader(model.Name));
        }

        private static PrmTriangle CreateTriangle(int a, int b, int c)
        {
            return new PrmTriangle(
                TexturePage: 0,
                Properties: 0,
                PointAId: CheckedPointId(a),
                PointBId: CheckedPointId(b),
                PointCId: CheckedPointId(c),
                UA: 0, VA: 0,
                UB: 31, VB: 0,
                UC: 0, VC: 31,
                BrightA: 200,
                BrightB: 200,
                BrightC: 200);
        }

        private static PrmQuadrangle CreateQuadrangle(int a, int b, int c, int d)
        {
            return new PrmQuadrangle(
                TexturePage: 0,
                Properties: 0,
                PointAId: CheckedPointId(a),
                PointBId: CheckedPointId(b),
                PointCId: CheckedPointId(c),
                PointDId: CheckedPointId(d),
                UA: 0, VA: 0,
                UB: 31, VB: 0,
                UC: 31, VC: 31,
                UD: 0, VD: 31,
                BrightA: 200,
                BrightB: 200,
                BrightC: 200,
                BrightD: 200);
        }

        private static short ParseCoordinate(string value, string axis)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                throw new InvalidDataException($"Invalid OBJ {axis} coordinate: {value}");

            int rounded = (int)Math.Round(parsed, MidpointRounding.AwayFromZero);
            if (rounded is < short.MinValue or > short.MaxValue)
                throw new InvalidDataException($"OBJ {axis} coordinate {parsed} is outside PRM Int16 range.");

            return (short)rounded;
        }

        private static int ParseFaceVertex(string token, int vertexCount)
        {
            string vertexPart = token.Split('/')[0];
            if (!int.TryParse(vertexPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int objIndex))
                throw new InvalidDataException($"Invalid OBJ face vertex: {token}");

            int zeroBased = objIndex > 0 ? objIndex - 1 : vertexCount + objIndex;
            if (zeroBased < 0 || zeroBased >= vertexCount)
                throw new InvalidDataException($"OBJ face vertex {objIndex} is outside the vertex list.");

            return zeroBased;
        }

        private static short CheckedPointId(int pointId)
        {
            if (pointId is < short.MinValue or > short.MaxValue)
                throw new InvalidDataException($"Point id {pointId} is outside PRM Int16 range.");

            return (short)pointId;
        }

        private static string StripComment(string line)
        {
            int comment = line.IndexOf('#');
            return comment >= 0 ? line[..comment] : line;
        }
    }

    public sealed record PrmObjImportResult(PrmModel Model, byte[] TemplatePrmBytes);
}
