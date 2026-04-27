using System;
using System.Text;
using UrbanChaosPrimEditor.Models;

namespace UrbanChaosPrimEditor.Services
{
    public static class PrmTemplateService
    {
        private const ushort PrmSignature = 0x16A2;
        private const int NprimHeaderSize = 50;

        public static PrmModel CreateEmptyModel(string fileName, string name)
        {
            return new PrmModel
            {
                FileName = fileName,
                Signature = PrmSignature,
                Name = string.IsNullOrWhiteSpace(name) ? "untitled" : name,
                FirstPointId = 0,
                LastPointId = 0,
                FirstQuadrangleId = 0,
                LastQuadrangleId = 0,
                FirstTriangleId = 0,
                LastTriangleId = 0,
                CollisionType = 0,
                ReactionToImpactByVehicle = 0,
                ShadowType = 0,
                VariousProperties = 0
            };
        }

        public static byte[] CreateNprimHeader(string name)
        {
            var bytes = new byte[NprimHeaderSize];
            bytes[0] = (byte)(PrmSignature & 0xFF);
            bytes[1] = (byte)((PrmSignature >> 8) & 0xFF);

            string safeName = string.IsNullOrWhiteSpace(name) ? "untitled" : name;
            byte[] nameBytes = Encoding.ASCII.GetBytes(safeName);
            Array.Copy(nameBytes, 0, bytes, 2, Math.Min(31, nameBytes.Length));
            return bytes;
        }
    }
}
