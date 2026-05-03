// ============================================================
// UrbanChaosEditor.Shared/Constants/MissionFormatConstants.cs
// ============================================================
// Mission and cutscene file constants.

namespace UrbanChaosEditor.Shared.Constants;

/// <summary>
/// Constants for mission (.ucm) data.
/// </summary>
public static class MissionFormatConstants
{
    public const int MissionCurrentVersion = 10;
    public const int MaxEventPoints = 512;

    public const int SpeakerRadio = 0;
    public const int SpeakerPlaceName = 0x100;
    public const int SpeakerTutorial = 0x200;
}

/// <summary>
/// Constants for cutscene data.
/// </summary>
public static class CutsceneFormatConstants
{
    public const byte CurrentVersion = 2;

    public const byte ChannelUnused = 0;
    public const byte ChannelCharacter = 1;
    public const byte ChannelCamera = 2;
    public const byte ChannelWave = 3;
    public const byte ChannelFx = 4;
    public const byte ChannelText = 5;

    public const byte PacketUnused = 0;
    public const byte PacketAnimation = 1;
    public const byte PacketAction = 2;
    public const byte PacketWave = 3;
    public const byte PacketCamera = 4;
    public const byte PacketText = 5;

    public const byte FlagBackwards = 1;
    public const byte FlagInterpolateMove = 2;
    public const byte FlagSmoothMoveIn = 4;
    public const byte FlagSmoothMoveOut = 8;
    public const byte FlagSmoothMoveBoth = FlagSmoothMoveIn | FlagSmoothMoveOut;
    public const byte FlagInterpolateRot = 16;
    public const byte FlagSmoothRotIn = 32;
    public const byte FlagSmoothRotOut = 64;
    public const byte FlagSmoothRotBoth = FlagSmoothRotIn | FlagSmoothRotOut;
    public const byte FlagSlomo = 128;
}
