using System.IO;
using System.Text;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.Models;

namespace UrbanChaosMissionEditor.Services;

/// <summary>
/// Service for reading and writing UCM (Urban Chaos Mission) files
/// </summary>
public class UcmFileService
{
    /// <summary>
    /// Read a UCM file and return a Mission object
    /// </summary>
    public Mission ReadMission(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"UCM file not found: {filePath}");

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs, Encoding.ASCII);

        var mission = new Mission();
        mission.InitializeEventPoints();

        // Read header
        mission.Version = reader.ReadUInt32();
        mission.Flags = (MissionFlags)reader.ReadUInt32();

        // Validate version
        if (mission.Version > Mission.CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported UCM version: {mission.Version}. Maximum supported: {Mission.CurrentVersion}");
        }

        // Read path strings (260 bytes each)
        mission.BriefName = ReadFixedString(reader, 260);
        mission.LightMapName = ReadFixedString(reader, 260);
        mission.MapName = ReadFixedString(reader, 260);
        mission.MissionName = ReadFixedString(reader, 260);
        mission.CitSezMapName = ReadFixedString(reader, 260);

        // Read metadata
        mission.MapIndex = reader.ReadUInt16();
        mission.FreeEPoints = reader.ReadUInt16();
        mission.UsedEPoints = reader.ReadUInt16();
        mission.CrimeRate = reader.ReadByte();
        mission.CivsRate = reader.ReadByte();

        // Read EventPoints (512 entries)
        for (int i = 0; i < Mission.MaxEventPoints; i++)
        {
            mission.EventPoints[i] = ReadEventPoint(reader, i + 1); // 1-based index
        }

        // Read SkillLevels (254 bytes)
        mission.SkillLevels = reader.ReadBytes(254);

        // Read tail rates
        mission.BoredomRate = reader.ReadByte();
        mission.CarsRate = reader.ReadByte();
        mission.MusicWorld = reader.ReadByte();

        // Read extra data for text-based waypoints
        for (int i = 0; i < Mission.MaxEventPoints; i++)
        {
            ReadEventExtra(reader, mission.EventPoints[i], mission.Version);
        }

        // Read MissionZones (128x128)
        for (int x = 0; x < 128; x++)
        {
            for (int z = 0; z < 128; z++)
            {
                mission.MissionZones[x, z] = reader.ReadByte();
            }
        }

        return mission;
    }

    /// <summary>
    /// Write a Mission object to a UCM file
    /// </summary>
    public void WriteMission(Mission mission, string filePath)
    {
        // Create backup if file exists
        if (File.Exists(filePath))
        {
            string backupPath = filePath + ".bak";
            File.Copy(filePath, backupPath, overwrite: true);
        }

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs, Encoding.ASCII);

        // Write header
        writer.Write(mission.Version);
        writer.Write((uint)mission.Flags);

        // Write path strings (260 bytes each, null-padded)
        WriteFixedString(writer, mission.BriefName, 260);
        WriteFixedString(writer, mission.LightMapName, 260);
        WriteFixedString(writer, mission.MapName, 260);
        WriteFixedString(writer, mission.MissionName, 260);
        WriteFixedString(writer, mission.CitSezMapName, 260);

        // Write metadata
        writer.Write(mission.MapIndex);
        writer.Write(mission.FreeEPoints);
        writer.Write(mission.UsedEPoints);
        writer.Write(mission.CrimeRate);
        writer.Write(mission.CivsRate);

        // Write EventPoints (512 entries)
        for (int i = 0; i < Mission.MaxEventPoints; i++)
        {
            WriteEventPoint(writer, mission.EventPoints[i]);
        }

        // Write SkillLevels (254 bytes)
        if (mission.SkillLevels == null || mission.SkillLevels.Length < 254)
        {
            writer.Write(new byte[254]);
        }
        else
        {
            writer.Write(mission.SkillLevels, 0, 254);
        }

        // Write tail rates
        writer.Write(mission.BoredomRate);
        writer.Write(mission.CarsRate);
        writer.Write(mission.MusicWorld);

        // Write extra data for text-based waypoints
        for (int i = 0; i < Mission.MaxEventPoints; i++)
        {
            WriteEventExtra(writer, mission.EventPoints[i], mission.Version);
        }

        // Write MissionZones (128x128)
        for (int x = 0; x < 128; x++)
        {
            for (int z = 0; z < 128; z++)
            {
                writer.Write(mission.MissionZones[x, z]);
            }
        }
    }

    /// <summary>
    /// Write a single EventPoint to the binary stream
    /// </summary>
    private void WriteEventPoint(BinaryWriter writer, EventPoint ep)
    {
        // First 14 bytes
        writer.Write(ep.Colour);
        writer.Write(ep.Group);
        writer.Write((byte)ep.WaypointType);
        writer.Write(ep.Used ? (byte)1 : (byte)0);
        writer.Write((byte)ep.TriggeredBy);
        writer.Write((byte)ep.OnTrigger);
        writer.Write(ep.Direction);
        writer.Write((byte)ep.Flags);
        writer.Write(ep.EPRef);
        writer.Write(ep.EPRefBool);
        writer.Write(ep.AfterTimer);

        // Data array (10 x 4 bytes = 40 bytes)
        for (int j = 0; j < 10; j++)
        {
            writer.Write(ep.Data[j]);
        }

        // Position and links (20 bytes)
        writer.Write(ep.Radius);
        writer.Write(ep.X);
        writer.Write(ep.Y);
        writer.Write(ep.Z);
        writer.Write(ep.Next);
        writer.Write(ep.Prev);
    }

    /// <summary>
    /// Write extra data (text strings) for an EventPoint
    /// </summary>
    private void WriteEventExtra(BinaryWriter writer, EventPoint ep, uint version)
    {
        if (!ep.Used) return;

        // Check if this waypoint type has text data
        bool hasText = ep.WaypointType switch
        {
            WaypointType.Message => true,
            WaypointType.CreateMapExit => true,
            WaypointType.Shout => true,
            WaypointType.NavBeacon => true,
            WaypointType.Conversation => true,
            WaypointType.BonusPoints => true,
            _ => false
        };

        if (hasText)
        {
            WriteTextExtra(writer, ep.ExtraText, version);
        }

        // Handle cutscene data
        if (ep.WaypointType == WaypointType.CutScene)
        {
            if (version > 7)
            {
                // Write empty cutscene marker for now
                // TODO: Implement proper CUTSCENE_write equivalent
                writer.Write((byte)0);
            }
        }

        // Write trigger text for shout triggers
        if (ep.TriggeredBy == TriggerType.ShoutAll || ep.TriggeredBy == TriggerType.ShoutAny)
        {
            WriteTextExtra(writer, ep.TriggerText, version);
        }
    }

    /// <summary>
    /// Write text extra data
    /// </summary>
    private void WriteTextExtra(BinaryWriter writer, string? text, uint version)
    {
        text ??= string.Empty;

        if (version > 7)
        {
            // Write type code (0x01 = text)
            writer.Write((byte)0x01);
        }

        byte[] textBytes = Encoding.ASCII.GetBytes(text);

        if (version > 4)
        {
            // Write length-prefixed string
            writer.Write(textBytes.Length + 1); // +1 for null terminator
            writer.Write(textBytes);
            writer.Write((byte)0); // Null terminator
        }
        else
        {
            // Fixed 260 byte field
            byte[] buffer = new byte[260];
            Array.Copy(textBytes, buffer, Math.Min(textBytes.Length, 259));
            writer.Write(buffer);
        }
    }

    /// <summary>
    /// Write a fixed-length null-padded string
    /// </summary>
    private void WriteFixedString(BinaryWriter writer, string value, int length)
    {
        byte[] buffer = new byte[length];
        if (!string.IsNullOrEmpty(value))
        {
            byte[] valueBytes = Encoding.ASCII.GetBytes(value);
            Array.Copy(valueBytes, buffer, Math.Min(valueBytes.Length, length - 1));
        }
        writer.Write(buffer);
    }

    /// <summary>
    /// Read a single EventPoint from the binary stream
    /// </summary>
    private EventPoint ReadEventPoint(BinaryReader reader, int index)
    {
        var ep = new EventPoint
        {
            Index = index
        };

        // First 14 bytes (as per the original C code split read)
        ep.Colour = reader.ReadByte();
        ep.Group = reader.ReadByte();
        ep.WaypointType = (WaypointType)reader.ReadByte();
        ep.Used = reader.ReadByte() != 0;
        ep.TriggeredBy = (TriggerType)reader.ReadByte();
        ep.OnTrigger = (OnTriggerBehavior)reader.ReadByte();
        ep.Direction = reader.ReadByte();
        ep.Flags = (WaypointFlags)reader.ReadByte();
        ep.EPRef = reader.ReadUInt16();
        ep.EPRefBool = reader.ReadUInt16();
        ep.AfterTimer = reader.ReadUInt16();

        // Next 60 bytes (Data[10] = 40 bytes + Radius, X, Y, Z, Next, Prev = 20 bytes)
        for (int j = 0; j < 10; j++)
        {
            ep.Data[j] = reader.ReadInt32();
        }
        ep.Radius = reader.ReadInt32();
        ep.X = reader.ReadInt32();
        ep.Y = reader.ReadInt32();
        ep.Z = reader.ReadInt32();
        ep.Next = reader.ReadUInt16();
        ep.Prev = reader.ReadUInt16();

        return ep;
    }

    /// <summary>
    /// Read extra data (text strings, cutscene data) for an EventPoint
    /// </summary>
    private void ReadEventExtra(BinaryReader reader, EventPoint ep, uint version)
    {
        if (!ep.Used) return;

        // Check if this waypoint type has text data
        bool hasText = ep.WaypointType switch
        {
            WaypointType.Message => true,
            WaypointType.CreateMapExit => true,
            WaypointType.Shout => true,
            WaypointType.NavBeacon => true,
            WaypointType.Conversation => true,
            WaypointType.BonusPoints => true,
            _ => false
        };

        if (hasText)
        {
            ReadTextExtra(reader, ep, version, isMainText: true);
        }

        // Handle cutscene data
        if (ep.WaypointType == WaypointType.CutScene)
        {
            if (version > 7)
            {
                byte typeCode = reader.ReadByte();
                // Cutscene data format is complex - skip for now
                // TODO: Implement CUTSCENE_read equivalent
            }
        }

        // Check trigger type for shout text
        if (ep.TriggeredBy == TriggerType.ShoutAll || ep.TriggeredBy == TriggerType.ShoutAny)
        {
            ReadTextExtra(reader, ep, version, isMainText: false);
        }
    }

    /// <summary>
    /// Read text extra data
    /// </summary>
    private void ReadTextExtra(BinaryReader reader, EventPoint ep, uint version, bool isMainText)
    {
        try
        {
            // Version 8+ has a type code prefix
            if (version > 7)
            {
                byte typeCode = reader.ReadByte();
                if (typeCode != 0x01) // 0x01 = text type
                {
                    // Not text data, might be cutscene or something else
                    return;
                }
            }

            // Read string length
            int length;
            if (version > 4)
            {
                length = reader.ReadInt32();
            }
            else
            {
                length = 260; // Fixed length in older versions
            }

            if (length > 0 && length < 10000) // Sanity check
            {
                byte[] textBytes = reader.ReadBytes(length);

                // Find null terminator if present
                int nullIndex = Array.IndexOf(textBytes, (byte)0);
                int actualLength = nullIndex >= 0 ? nullIndex : length;

                string text = Encoding.ASCII.GetString(textBytes, 0, actualLength);

                if (isMainText)
                    ep.ExtraText = text;
                else
                    ep.TriggerText = text;
            }
        }
        catch (EndOfStreamException)
        {
            // End of file reached, stop reading extras
        }
    }

    /// <summary>
    /// Read a fixed-length null-terminated string
    /// </summary>
    private string ReadFixedString(BinaryReader reader, int length)
    {
        byte[] bytes = reader.ReadBytes(length);
        int nullIndex = Array.IndexOf(bytes, (byte)0);
        if (nullIndex < 0) nullIndex = length;
        return Encoding.ASCII.GetString(bytes, 0, nullIndex);
    }

    /// <summary>
    /// Get file information without fully loading the mission
    /// </summary>
    public MissionInfo GetMissionInfo(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"UCM file not found: {filePath}");

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs, Encoding.ASCII);

        var info = new MissionInfo
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            FileSize = new FileInfo(filePath).Length
        };

        // Read just the header information
        info.Version = reader.ReadUInt32();
        info.Flags = (MissionFlags)reader.ReadUInt32();

        // Read path strings
        info.BriefName = ReadFixedString(reader, 260);
        info.LightMapName = ReadFixedString(reader, 260);
        info.MapName = ReadFixedString(reader, 260);
        info.MissionName = ReadFixedString(reader, 260);
        info.CitSezMapName = ReadFixedString(reader, 260);

        // Read metadata
        info.MapIndex = reader.ReadUInt16();
        reader.ReadUInt16(); // FreeEPoints
        reader.ReadUInt16(); // UsedEPoints
        info.CrimeRate = reader.ReadByte();
        info.CivsRate = reader.ReadByte();

        // Count used EventPoints by reading the Used flag of each
        int usedCount = 0;
        for (int i = 0; i < Mission.MaxEventPoints; i++)
        {
            reader.ReadBytes(3); // Skip Colour, Group, WaypointType
            bool used = reader.ReadByte() != 0;
            if (used) usedCount++;
            reader.ReadBytes(70); // Skip rest of EventPoint (74 - 4 = 70)
        }
        info.EventPointCount = usedCount;

        return info;
    }
}

/// <summary>
/// Quick info about a mission file without full loading
/// </summary>
public class MissionInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public uint Version { get; set; }
    public MissionFlags Flags { get; set; }
    public string BriefName { get; set; } = string.Empty;
    public string LightMapName { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public string MissionName { get; set; } = string.Empty;
    public string CitSezMapName { get; set; } = string.Empty;
    public ushort MapIndex { get; set; }
    public byte CrimeRate { get; set; }
    public byte CivsRate { get; set; }
    public int EventPointCount { get; set; }
}