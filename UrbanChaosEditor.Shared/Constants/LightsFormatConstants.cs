// ============================================================
// UrbanChaosEditor.Shared/Constants/LightsFormatConstants.cs
// ============================================================
// Binary layout constants for .lgt lighting files.

namespace UrbanChaosEditor.Shared.Constants;

/// <summary>
/// Binary layout constants for the .lgt file format.
/// </summary>
public static class LightsFormatConstants
{
    /// <summary>Size of the lights header in bytes.</summary>
    public const int HeaderSize = 12;

    /// <summary>Size of one light entry in bytes.</summary>
    public const int EntrySize = 20;

    /// <summary>Number of light entries.</summary>
    public const int EntryCount = 256;

    /// <summary>Byte offset where light entries start.</summary>
    public const int EntriesOffset = HeaderSize;

    /// <summary>Byte offset of the global light properties block.</summary>
    public const int PropertiesOffset = EntriesOffset + EntrySize * EntryCount;

    /// <summary>Size of the global light properties block.</summary>
    public const int PropertiesSize = 36;

    /// <summary>Byte offset of the night-color block.</summary>
    public const int NightColourOffset = PropertiesOffset + PropertiesSize;

    /// <summary>Size of the night-color block (RGB).</summary>
    public const int NightColourSize = 3;

    /// <summary>Total size of a .lgt file in bytes.</summary>
    public const int TotalSize = NightColourOffset + NightColourSize;

    /// <summary>Read-only mission-preview padding between header and entries.</summary>
    public const int MissionPreviewReservedPad = 20;

    /// <summary>Light entries displayed by mission preview data.</summary>
    public const int MissionPreviewEntryCount = 255;

    /// <summary>Mission-preview entry offset.</summary>
    public const int MissionPreviewEntriesOffset = HeaderSize + MissionPreviewReservedPad;

    /// <summary>Minimum byte size needed for mission-preview lighting.</summary>
    public const int MissionPreviewTotalMinSize = 5171;

    // Night flag bit definitions (from C++ night.h)
    /// <summary>Bit 0 (0x01): Lamppost lights enabled.</summary>
    public const uint NIGHT_FLAG_LIGHTS_UNDER_LAMPOSTS = 1u << 0;
    /// <summary>Bit 1 (0x02): Darken wall bottoms.</summary>
    public const uint NIGHT_FLAG_DARKEN_BUILDING_POINTS = 1u << 1;
    /// <summary>Bit 2 (0x04): SET = daytime, CLEAR = night.</summary>
    public const uint NIGHT_FLAG_DAYTIME = 1u << 2;
}
