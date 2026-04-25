using System;
using System.IO;

namespace UrbanChaosPrimEditor.Services
{
    public sealed class PrimTextureResolverService
    {
        private readonly string _primTextureDirectory;

        public PrimTextureResolverService(string primTextureDirectory)
        {
            if (string.IsNullOrWhiteSpace(primTextureDirectory))
                throw new ArgumentException("Prim texture directory cannot be empty.", nameof(primTextureDirectory));

            _primTextureDirectory = primTextureDirectory;
        }

        public string PrimTextureDirectory => _primTextureDirectory;

        public static int GetPrimTextureId(byte firstU, byte texturePage)
        {
            // enginePage encodes the two high bits of the first vertex's U byte
            // plus the face's texture-page byte. This is the direct texture file index.
            return ((firstU & 0xC0) << 2) | texturePage;
        }

        public string? ResolveTexturePath(int primTextureId)
        {
            string path = Path.Combine(_primTextureDirectory, $"Tex{primTextureId:D3}hi.tga");
            return File.Exists(path) ? path : null;
        }

        public string? ResolveTexturePath(byte firstU, byte texturePage)
        {
            return ResolveTexturePath(GetPrimTextureId(firstU, texturePage));
        }

        public bool TextureExists(int primTextureId)
        {
            return ResolveTexturePath(primTextureId) != null;
        }

        public string BuildExpectedTexturePath(int primTextureId)
        {
            return Path.Combine(_primTextureDirectory, $"Tex{primTextureId:D3}hi.tga");
        }
    }
}