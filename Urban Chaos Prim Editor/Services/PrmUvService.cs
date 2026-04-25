using System;
using System.Windows;

namespace UrbanChaosPrimEditor.Services
{
    public static class PrmUvService
    {
        private const int TextureTileSize = 32;
        private const int TilesPerPage = 8;
        private const int FacePageOffset = 64 * 11; // 704

        public readonly record struct FaceTextureMapping(
            int TextureId,
            int GlobalTexturePage,
            int TileU,
            int TileV);

        public static FaceTextureMapping GetTriangleTexture(
            byte ua, byte va,
            byte ub, byte vb,
            byte uc, byte vc,
            byte texturePage)
        {
            double avgU = (ua + ub + uc) / 3.0;
            double avgV = (va + vb + vc) / 3.0;

            return BuildMapping(avgU, avgV, texturePage);
        }

        public static FaceTextureMapping GetQuadTexture(
            byte ua, byte va,
            byte ub, byte vb,
            byte uc, byte vc,
            byte ud, byte vd,
            byte texturePage)
        {
            double avgU = (ua + ub + uc + ud) / 4.0;
            double avgV = (va + vb + vc + vd) / 4.0;

            return BuildMapping(avgU, avgV, texturePage);
        }

        private static FaceTextureMapping BuildMapping(double avgU, double avgV, byte texturePage)
        {
            int tileU = (int)(avgU / TextureTileSize);
            int tileV = (int)(avgV / TextureTileSize);

            int globalTexturePage =
                tileU +
                tileV * TilesPerPage +
                texturePage * TilesPerPage * TilesPerPage;

            int textureId = globalTexturePage - FacePageOffset;

            return new FaceTextureMapping(
                textureId,
                globalTexturePage,
                tileU,
                tileV);
        }

        public static Point ToLocalUv(byte u, byte v, FaceTextureMapping mapping)
        {
            double localU = (u - mapping.TileU * TextureTileSize) / (double)TextureTileSize;
            double localV = (v - mapping.TileV * TextureTileSize) / (double)TextureTileSize;

            return new Point(localU, 1.0 - localV);
        }
    }
}