// ============================================================
// UrbanChaosEditor.Shared/Constants/TextureFormatConstants.cs
// ============================================================
// Constants for texture/style (.tma) file formats and tile rendering.

namespace UrbanChaosEditor.Shared.Constants;

/// <summary>
/// Binary layout and rendering constants for textures and the .tma style format.
/// </summary>
public static class TextureFormatConstants
{
    /// <summary>Size of the texture-block header in bytes.</summary>
    public const int HeaderBytes = 8;

    /// <summary>Bytes per tile entry in the texture accessor.</summary>
    public const int BytesPerTile = 6;

    /// <summary>Total rows in a .tma style file.</summary>
    public const int TotalRows = 200;

    /// <summary>Number of style entries per row.</summary>
    public const int EntriesPerStyle = 5;

    /// <summary>Length of a name field (bytes).</summary>
    public const int NameLength = 21;

    /// <summary>SaveType value used when serializing .tma files.</summary>
    public const uint SaveType = 5;

    /// <summary>Default flag value for new texture entries.</summary>
    public const byte DefaultFlag = 0x03;

    /// <summary>First valid texture index.</summary>
    public const int FirstIndex = 256;

    /// <summary>Last valid texture index.</summary>
    public const int LastIndex = 511;

    // ---- Tile rendering ----

    /// <summary>Tile width in pixels.</summary>
    public const int TileWidth = 64;

    /// <summary>Tile height in pixels.</summary>
    public const int TileHeight = 64;

    /// <summary>Bytes per pixel (BGRA).</summary>
    public const int Bpp = 4;

    /// <summary>Stride in bytes for one tile row.</summary>
    public const int Stride = TileWidth * Bpp;

    /// <summary>Alpha threshold used when exporting cut-out textures.</summary>
    public const byte CutoutAlphaThreshold = 8;
}
