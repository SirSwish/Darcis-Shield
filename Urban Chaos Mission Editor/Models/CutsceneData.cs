using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using UrbanChaosMissionEditor.Services;

namespace UrbanChaosMissionEditor.Models;

/// <summary>
/// Constants for cutscene data version
/// </summary>
public static class CutsceneConstants
{
    public const byte CurrentVersion = 2;

    // Channel Types (CT_*)
    public const byte CT_UNUSED = 0;
    public const byte CT_CHAR = 1;      // Character
    public const byte CT_CAM = 2;       // Camera
    public const byte CT_WAVE = 3;      // Sound/Wave
    public const byte CT_FX = 4;        // Visual FX
    public const byte CT_TEXT = 5;      // Subtitles

    // Packet Types (PT_*)
    public const byte PT_UNUSED = 0;
    public const byte PT_ANIM = 1;      // Animation
    public const byte PT_ACTION = 2;    // Action/Task
    public const byte PT_WAVE = 3;      // Sound
    public const byte PT_CAM = 4;       // Camera keyframe
    public const byte PT_TEXT = 5;      // Subtitle text

    // Packet Flags (PF_*)
    public const byte PF_BACKWARDS = 1;         // Reverse animation / Securicam for cameras
    public const byte PF_INTERPOLATE_MOVE = 2;  // Linear position interpolation
    public const byte PF_SMOOTH_MOVE_IN = 4;    // Ease in position
    public const byte PF_SMOOTH_MOVE_OUT = 8;   // Ease out position
    public const byte PF_SMOOTH_MOVE_BOTH = PF_SMOOTH_MOVE_IN | PF_SMOOTH_MOVE_OUT;
    public const byte PF_INTERPOLATE_ROT = 16;  // Linear rotation interpolation
    public const byte PF_SMOOTH_ROT_IN = 32;    // Ease in rotation
    public const byte PF_SMOOTH_ROT_OUT = 64;   // Ease out rotation
    public const byte PF_SMOOTH_ROT_BOTH = PF_SMOOTH_ROT_IN | PF_SMOOTH_ROT_OUT;
    public const byte PF_SLOMO = 128;           // Slow motion
}

/// <summary>
/// Channel type enumeration for cleaner code
/// </summary>
public enum CutsceneChannelType : byte
{
    Unused = 0,
    Character = 1,
    Camera = 2,
    Sound = 3,
    VisualFX = 4,
    Subtitles = 5
}

/// <summary>
/// Packet type enumeration
/// </summary>
public enum CutscenePacketType : byte
{
    Unused = 0,
    Animation = 1,
    Action = 2,
    Sound = 3,
    Camera = 4,
    Text = 5
}

/// <summary>
/// Interpolation mode for packet transitions
/// </summary>
[Flags]
public enum PacketFlags : byte
{
    None = 0,
    Backwards = 1,          // For anims: play backwards. For cameras: securicam mode
    InterpolateMove = 2,    // Linear position interpolation between keyframes
    SmoothMoveIn = 4,       // Ease-in for position
    SmoothMoveOut = 8,      // Ease-out for position
    InterpolateRot = 16,    // Linear rotation interpolation
    SmoothRotIn = 32,       // Ease-in for rotation
    SmoothRotOut = 64,      // Ease-out for rotation
    SlowMotion = 128        // Slow-mo playback
}

/// <summary>
/// A single packet/event on a cutscene channel timeline.
/// Represents an animation, camera position, sound, or subtitle at a specific time.
/// </summary>
/// <remarks>
/// Binary layout (24 bytes fixed + variable text):
/// - type: 1 byte
/// - flags: 1 byte  
/// - index: 2 bytes (animation/sound index)
/// - start: 2 bytes (timeline position)
/// - length: 2 bytes (duration)
/// - pos.X: 4 bytes
/// - pos.Y: 4 bytes
/// - pos.Z: 4 bytes
/// - angle: 2 bytes
/// - pitch: 2 bytes
/// For PT_TEXT type, if pos.X != 0, a length-prefixed string follows.
/// </remarks>
public class CutscenePacket
{
    /// <summary>
    /// Type of packet (animation, camera, sound, etc.)
    /// </summary>
    public CutscenePacketType Type { get; set; }

    /// <summary>
    /// Packet flags (interpolation mode, backwards, etc.)
    /// </summary>
    public PacketFlags Flags { get; set; }

    /// <summary>
    /// Index of animation, sound, or other resource
    /// </summary>
    public ushort Index { get; set; }

    /// <summary>
    /// Start time on the timeline (in timeline units)
    /// </summary>
    public ushort Start { get; set; }

    /// <summary>
    /// Duration/length of the packet
    /// For cameras: low byte is lens, high byte is fade level
    /// </summary>
    public ushort Length { get; set; }

    /// <summary>
    /// World X position (in game units, shifted by 8)
    /// For PT_TEXT: stores pointer to text string (handled specially)
    /// </summary>
    public int PosX { get; set; }

    /// <summary>
    /// World Y position (height)
    /// </summary>
    public int PosY { get; set; }

    /// <summary>
    /// World Z position
    /// </summary>
    public int PosZ { get; set; }

    /// <summary>
    /// Facing angle (0-2047)
    /// </summary>
    public ushort Angle { get; set; }

    /// <summary>
    /// Pitch angle (for cameras)
    /// </summary>
    public ushort Pitch { get; set; }

    /// <summary>
    /// Text content (only used for PT_TEXT packets)
    /// </summary>
    public string? Text { get; set; }

    // === Computed Properties ===

    /// <summary>
    /// Effective length for timeline display purposes.
    /// Camera packets use Length field for other data, so return fixed value.
    /// </summary>
    public int EffectiveLength
    {
        get
        {
            // Camera packets store lens/fade in Length field, not duration
            if (Type == CutscenePacketType.Camera)
                return 10; // Camera keyframes display as small fixed-width blocks

            // Sanity cap for other packet types
            return Math.Min((int)Length, 1000);
        }
    }

    /// <summary>
    /// End frame of this packet (Start + effective length)
    /// Note: Camera packets use Length for lens/fade, not duration
    /// </summary>
    public int End => Start + EffectiveLength;

    /// <summary>
    /// Position interpolation enabled
    /// </summary>
    public bool InterpolatePosition
    {
        get => Flags.HasFlag(PacketFlags.InterpolateMove);
        set => Flags = value ? Flags | PacketFlags.InterpolateMove : Flags & ~PacketFlags.InterpolateMove;
    }

    /// <summary>
    /// Rotation interpolation enabled
    /// </summary>
    public bool InterpolateRotation
    {
        get => Flags.HasFlag(PacketFlags.InterpolateRot);
        set => Flags = value ? Flags | PacketFlags.InterpolateRot : Flags & ~PacketFlags.InterpolateRot;
    }

    /// <summary>
    /// Position interpolation mode description
    /// </summary>
    public string PositionInterpolationMode
    {
        get
        {
            if (!InterpolatePosition) return "Snap";
            bool smoothIn = Flags.HasFlag(PacketFlags.SmoothMoveIn);
            bool smoothOut = Flags.HasFlag(PacketFlags.SmoothMoveOut);
            if (smoothIn && smoothOut) return "Smooth Both";
            if (smoothIn) return "Smooth In";
            if (smoothOut) return "Smooth Out";
            return "Linear";
        }
    }

    /// <summary>
    /// Camera lens/zoom value (low byte of Length for camera packets)
    /// </summary>
    public byte CameraLens
    {
        get => (byte)(Length & 0xFF);
        set => Length = (ushort)((Length & 0xFF00) | value);
    }

    /// <summary>
    /// Camera fade value (high byte of Length for camera packets)
    /// </summary>
    public byte CameraFade
    {
        get => (byte)((Length >> 8) & 0xFF);
        set => Length = (ushort)((Length & 0x00FF) | (value << 8));
    }

    /// <summary>
    /// Clone this packet
    /// </summary>
    public CutscenePacket Clone()
    {
        return new CutscenePacket
        {
            Type = Type,
            Flags = Flags,
            Index = Index,
            Start = Start,
            Length = Length,
            PosX = PosX,
            PosY = PosY,
            PosZ = PosZ,
            Angle = Angle,
            Pitch = Pitch,
            Text = Text
        };
    }
}

/// <summary>
/// A channel/track in a cutscene.
/// Contains a sequence of packets for a character, camera, sound source, etc.
/// </summary>
/// <remarks>
/// Binary layout (8 bytes header + packets):
/// - type: 1 byte
/// - flags: 1 byte
/// - pad1, pad2: 2 bytes
/// - index: 2 bytes (person type, sound source, etc.)
/// - packetcount: 2 bytes
/// - [packets follow inline]
/// </remarks>
public class CutsceneChannel
{
    /// <summary>
    /// Type of channel
    /// </summary>
    public CutsceneChannelType Type { get; set; }

    /// <summary>
    /// Channel flags (reserved)
    /// </summary>
    public byte Flags { get; set; }

    /// <summary>
    /// Index - meaning depends on type:
    /// - Character: person type (1=Darci, 2=Roper, etc.)
    /// - Sound: not used
    /// - Camera: not used
    /// </summary>
    public ushort Index { get; set; }

    /// <summary>
    /// Packets/events on this channel
    /// </summary>
    public ObservableCollection<CutscenePacket> Packets { get; } = new();

    /// <summary>
    /// Display name for the channel
    /// </summary>
    public string DisplayName => Type switch
    {
        CutsceneChannelType.Character => AnimationNameTable.GetPersonTypeName(Index),
        CutsceneChannelType.Camera => "Camera",
        CutsceneChannelType.Sound => "Sound",
        CutsceneChannelType.VisualFX => "Visual FX",
        CutsceneChannelType.Subtitles => "Subtitles",
        _ => $"Channel {Index}"
    };

    private static string GetCharacterName(ushort index)
    {
        return index switch
        {
            1 => "Darci",
            2 => "Roper",
            3 => "Cop",
            4 => "Civilian",
            5 => "Rasta Thug",
            6 => "Grey Thug",
            7 => "Red Thug",
            8 => "Prostitute",
            9 => "Fat Prostitute",
            10 => "Hostage",
            11 => "Mechanic",
            12 => "Tramp",
            13 => "MIB 1",
            14 => "MIB 2",
            15 => "MIB 3",
            _ => $"Person {index}"
        };
    }

    /// <summary>
    /// Get the packet at a specific timeline position, or null
    /// </summary>
    public CutscenePacket? GetPacketAt(int timelinePosition)
    {
        foreach (var packet in Packets)
        {
            if (packet.Start == timelinePosition)
                return packet;
        }
        return null;
    }

    /// <summary>
    /// Find the packet that contains a timeline position
    /// </summary>
    public CutscenePacket? GetPacketContaining(int timelinePosition)
    {
        foreach (var packet in Packets)
        {
            if (timelinePosition >= packet.Start && timelinePosition < packet.End)
                return packet;
        }
        return null;
    }
}

/// <summary>
/// Root container for cutscene data.
/// A cutscene is a scripted sequence with multiple channels (tracks) of events.
/// </summary>
/// <remarks>
/// Binary layout:
/// - version: 1 byte
/// - channelcount: 1 byte
/// - [channels follow inline, each with their packets]
/// </remarks>
public class CutsceneData
{
    /// <summary>
    /// Data format version
    /// </summary>
    public byte Version { get; set; } = CutsceneConstants.CurrentVersion;

    /// <summary>
    /// Channels/tracks in the cutscene
    /// </summary>
    public ObservableCollection<CutsceneChannel> Channels { get; } = new();

    /// <summary>
    /// Total duration of the cutscene (max end time across all packets)
    /// </summary>
    public int Duration
    {
        get
        {
            int maxEnd = 0;
            foreach (var channel in Channels)
            {
                foreach (var packet in channel.Packets)
                {
                    // Use EffectiveLength to avoid camera packet issues
                    int end = packet.Start + packet.EffectiveLength;
                    if (end > maxEnd)
                        maxEnd = end;
                }
            }
            return maxEnd;
        }
    }

    /// <summary>
    /// Create a new empty cutscene
    /// </summary>
    public static CutsceneData CreateNew()
    {
        return new CutsceneData
        {
            Version = CutsceneConstants.CurrentVersion
        };
    }

    /// <summary>
    /// Add a new character channel
    /// </summary>
    public CutsceneChannel AddCharacterChannel(ushort personType)
    {
        var channel = new CutsceneChannel
        {
            Type = CutsceneChannelType.Character,
            Index = personType
        };
        Channels.Add(channel);
        return channel;
    }

    /// <summary>
    /// Add a new camera channel
    /// </summary>
    public CutsceneChannel AddCameraChannel()
    {
        var channel = new CutsceneChannel
        {
            Type = CutsceneChannelType.Camera,
            Index = 0
        };
        Channels.Add(channel);
        return channel;
    }

    /// <summary>
    /// Add a new sound channel
    /// </summary>
    public CutsceneChannel AddSoundChannel()
    {
        var channel = new CutsceneChannel
        {
            Type = CutsceneChannelType.Sound,
            Index = 0
        };
        Channels.Add(channel);
        return channel;
    }

    /// <summary>
    /// Add a subtitle channel
    /// </summary>
    public CutsceneChannel AddSubtitleChannel()
    {
        var channel = new CutsceneChannel
        {
            Type = CutsceneChannelType.Subtitles,
            Index = 0
        };
        Channels.Add(channel);
        return channel;
    }
}