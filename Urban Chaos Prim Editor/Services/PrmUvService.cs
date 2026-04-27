using System;
using System.Windows;

namespace UrbanChaosPrimEditor.Services
{
    // Reproduces the legacy viewer's PRM UV/texture mapping exactly.
    //
    // For each face we average the raw UVs to find which 32×32 tile in the
    // 256×256 page they belong to. The tile origin (baseU, baseV) is then
    // subtracted from each vertex UV and the result is normalised to [0,1]
    // by dividing by 32. TgaLoader normalizes TGA origin for WPF, so V is
    // left in local tile space here.
    //
    // Texture id is derived from the same tile coordinates plus the face's
    // TexturePage:
    //
    //   page         = tileU + tileV*8 + texturePage*64
    //   textureImgNo = page - 64*11        (resolves to TexXXXhi.tga)
    public static class PrmUvService
    {
        private const int TextureNormSize    = 32;
        private const int TextureNormSquares = 8;
        private const int FacePageOffset     = 64 * 11; // 704

        public readonly record struct PrmFaceTextureMapping(
            int TextureId,
            int Page,
            double AverageU,
            double AverageV,
            int TileU,
            int TileV,
            int BaseU,
            int BaseV,
            Point UV0,
            Point UV1,
            Point UV2,
            Point? UV3);

        public static PrmFaceTextureMapping CalculateTriangle(
            byte u0, byte v0,
            byte u1, byte v1,
            byte u2, byte v2,
            byte texturePage)
        {
            double avU = (u0 + u1 + u2) / 3.0;
            double avV = (v0 + v1 + v2) / 3.0;

            int tileU = (int)(avU / TextureNormSize);
            int tileV = (int)(avV / TextureNormSize);

            int baseU = tileU * TextureNormSize;
            int baseV = tileV * TextureNormSize;

            double finalU0 = (u0 - baseU) / 32.0;
            double finalV0 = (v0 - baseV) / 32.0;

            double finalU1 = (u1 - baseU) / 32.0;
            double finalV1 = (v1 - baseV) / 32.0;

            double finalU2 = (u2 - baseU) / 32.0;
            double finalV2 = (v2 - baseV) / 32.0;

            int page =
                tileU +
                tileV * TextureNormSquares +
                texturePage * TextureNormSquares * TextureNormSquares;

            int textureImgNo = page - FacePageOffset;

            return new PrmFaceTextureMapping(
                textureImgNo,
                page,
                avU,
                avV,
                tileU,
                tileV,
                baseU,
                baseV,
                new Point(finalU0, finalV0),
                new Point(finalU1, finalV1),
                new Point(finalU2, finalV2),
                null);
        }

        public static PrmFaceTextureMapping CalculateQuad(
            byte u0, byte v0,
            byte u1, byte v1,
            byte u2, byte v2,
            byte u3, byte v3,
            byte texturePage)
        {
            double avU = (u0 + u1 + u2 + u3) / 4.0;
            double avV = (v0 + v1 + v2 + v3) / 4.0;

            int tileU = (int)(avU / TextureNormSize);
            int tileV = (int)(avV / TextureNormSize);

            int baseU = tileU * TextureNormSize;
            int baseV = tileV * TextureNormSize;

            double finalU0 = (u0 - baseU) / 32.0;
            double finalV0 = (v0 - baseV) / 32.0;

            double finalU1 = (u1 - baseU) / 32.0;
            double finalV1 = (v1 - baseV) / 32.0;

            double finalU2 = (u2 - baseU) / 32.0;
            double finalV2 = (v2 - baseV) / 32.0;

            double finalU3 = (u3 - baseU) / 32.0;
            double finalV3 = (v3 - baseV) / 32.0;

            int page =
                tileU +
                tileV * TextureNormSquares +
                texturePage * TextureNormSquares * TextureNormSquares;

            int textureImgNo = page - FacePageOffset;

            return new PrmFaceTextureMapping(
                textureImgNo,
                page,
                avU,
                avV,
                tileU,
                tileV,
                baseU,
                baseV,
                new Point(finalU0, finalV0),
                new Point(finalU1, finalV1),
                new Point(finalU2, finalV2),
                new Point(finalU3, finalV3));
        }
    }
}
