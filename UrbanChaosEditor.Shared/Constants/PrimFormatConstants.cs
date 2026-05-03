// ============================================================
// UrbanChaosEditor.Shared/Constants/PrimFormatConstants.cs
// ============================================================
// Binary layout constants for the prim/mesh (.prm) file format.

namespace UrbanChaosEditor.Shared.Constants;

/// <summary>
/// Binary layout constants for .prm prim/mesh files.
/// </summary>
public static class PrimFormatConstants
{
    /// <summary>Size of one Point record (3 × int16).</summary>
    public const int PointSize = 6;

    /// <summary>Size of one Triangle face record.</summary>
    public const int TriangleSize = 28;

    /// <summary>Size of one Quadrangle face record.</summary>
    public const int QuadrangleSize = 34;

    /// <summary>Size of the standard prim header.</summary>
    public const int PrimHeaderSize = 56;

    /// <summary>Size of the nprim header.</summary>
    public const int NprimHeaderSize = 50;

    /// <summary>Magic number identifying valid .prm files.</summary>
    public const ushort PrmSignature = 0x16A2;

    /// <summary>Maximum vertex count supported by the engine.</summary>
    public const int MaxVertexCount = 5000;

    /// <summary>Texture page atlas square size (pixels).</summary>
    public const int TextureNormSize = 32;

    /// <summary>Number of texture norm squares per side.</summary>
    public const int TextureNormSquares = 8;

    /// <summary>Offset of the face-page index in texture data (64 * 11).</summary>
    public const int FacePageOffset = 704;
}
