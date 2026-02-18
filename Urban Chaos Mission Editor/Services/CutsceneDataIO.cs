using System;
using System.IO;
using System.Text;
using UrbanChaosMissionEditor.Models;

namespace UrbanChaosMissionEditor.Services;

/// <summary>
/// Service for reading and writing cutscene data in the UCM file format.
/// </summary>
/// <remarks>
/// The cutscene data format is binary and follows the original game's C++ structures.
/// Key considerations:
/// - Data is stored separately from the main EventPoint structure
/// - Preceded by a type byte (2) in the UCM file's extra data section
/// - Text packets have variable-length strings appended
/// </remarks>
public static class CutsceneDataIO
{
    /// <summary>
    /// Read cutscene data from a binary stream
    /// </summary>
    /// <param name="reader">Binary reader positioned at the start of cutscene data</param>
    /// <returns>Parsed CutsceneData object</returns>
    public static CutsceneData Read(BinaryReader reader)
    {
        var cutscene = new CutsceneData();

        // Read header
        cutscene.Version = reader.ReadByte();
        byte channelCount = reader.ReadByte();

        // Read channels
        for (int i = 0; i < channelCount; i++)
        {
            var channel = ReadChannel(reader, cutscene.Version);
            cutscene.Channels.Add(channel);
        }

        return cutscene;
    }

    /// <summary>
    /// Read a single channel from the stream
    /// </summary>
    private static CutsceneChannel ReadChannel(BinaryReader reader, byte version)
    {
        var channel = new CutsceneChannel();

        // Read CSChannel header (8 bytes)
        channel.Type = (CutsceneChannelType)reader.ReadByte();
        channel.Flags = reader.ReadByte();
        reader.ReadByte(); // pad1
        reader.ReadByte(); // pad2
        channel.Index = reader.ReadUInt16();
        ushort packetCount = reader.ReadUInt16();

        // Skip the 4-byte pointer (packets pointer in memory, not meaningful in file)
        // Actually, looking at the C++ code, it writes the struct directly which includes
        // the pointer. But when reading, the pointer is ignored and packets follow inline.
        // The C++ code uses sizeof(CSChannel) which is 8 bytes on 32-bit.
        // Let me check... the struct is: type(1) + flags(1) + pad(2) + index(2) + packetcount(2) + packets*(4) = 12 bytes
        // But in the file, the packets pointer is just written as garbage/0, and actual packets follow

        // Actually re-reading the code:
        // fwrite((void*)chan,sizeof(CSChannel),1,file_handle);
        // sizeof(CSChannel) includes the pointer... but on write, that's just whatever value is in memory
        // On read, we ignore it because we read packets separately

        // CSChannel is: UBYTE type, UBYTE flags, UBYTE pad1, pad2, UWORD index, UWORD packetcount, CSPacket* packets
        // That's 1+1+1+1+2+2+4 = 12 bytes on 32-bit
        // But wait, the struct padding might make it different...

        // Looking more carefully at the struct:
        // struct CSChannel {
        //     UBYTE type;           // 0
        //     UBYTE flags;          // 1  
        //     UBYTE pad1,pad2;      // 2-3
        //     UWORD index;          // 4-5
        //     UWORD packetcount;    // 6-7
        //     CSPacket *packets;    // 8-11 (32-bit pointer)
        // };
        // So total is 12 bytes

        // We've read 8 bytes (1+1+1+1+2+2), need to skip the pointer (4 bytes)
        reader.ReadInt32(); // Skip packets pointer

        // Read packets
        for (int i = 0; i < packetCount; i++)
        {
            var packet = ReadPacket(reader, version);
            channel.Packets.Add(packet);
        }

        return channel;
    }

    /// <summary>
    /// Read a single packet from the stream
    /// </summary>
    private static CutscenePacket ReadPacket(BinaryReader reader, byte version)
    {
        var packet = new CutscenePacket();

        // Read CSPacket (24 bytes)
        // struct CSPacket {
        //     UBYTE type;           // 0
        //     UBYTE flags;          // 1
        //     UWORD index;          // 2-3 (with padding, might be at 2)
        //     UWORD start;          // 4-5
        //     UWORD length;         // 6-7
        //     GameCoord pos;        // 8-19 (3x SLONG = 12 bytes)
        //     UWORD angle,pitch;    // 20-23
        // };

        // Wait, let me re-check the struct alignment:
        // UBYTE type (1) + UBYTE flags (1) + UWORD index (2) + UWORD start (2) + UWORD length (2) = 8
        // GameCoord pos = 3 x SLONG (4 bytes each) = 12 bytes
        // UWORD angle (2) + UWORD pitch (2) = 4 bytes
        // Total = 8 + 12 + 4 = 24 bytes

        packet.Type = (CutscenePacketType)reader.ReadByte();
        packet.Flags = (PacketFlags)reader.ReadByte();
        packet.Index = reader.ReadUInt16();
        packet.Start = reader.ReadUInt16();
        packet.Length = reader.ReadUInt16();
        packet.PosX = reader.ReadInt32();
        packet.PosY = reader.ReadInt32();
        packet.PosZ = reader.ReadInt32();
        packet.Angle = reader.ReadUInt16();
        packet.Pitch = reader.ReadUInt16();

        // Version-specific fixups
        if (version == 1)
        {
            // In version 1, camera packets need length adjusted
            if (packet.Type == CutscenePacketType.Camera)
            {
                packet.Length = 0xFF7F; // Default camera length
            }
        }

        // Handle text packets - if PosX is non-zero, text follows
        if (packet.Type == CutscenePacketType.Text && packet.PosX != 0)
        {
            int textLength = reader.ReadInt32();
            if (textLength > 0 && textLength < 65536) // Sanity check
            {
                byte[] textBytes = reader.ReadBytes(textLength);
                packet.Text = Encoding.ASCII.GetString(textBytes).TrimEnd('\0');
            }
            // Reset PosX since it was used as a flag, not actual position
            packet.PosX = 0;
        }

        return packet;
    }

    /// <summary>
    /// Write cutscene data to a binary stream
    /// </summary>
    public static void Write(BinaryWriter writer, CutsceneData cutscene)
    {
        // Write header
        writer.Write(CutsceneConstants.CurrentVersion);
        writer.Write((byte)cutscene.Channels.Count);

        // Write channels
        foreach (var channel in cutscene.Channels)
        {
            WriteChannel(writer, channel);
        }
    }

    /// <summary>
    /// Write a single channel to the stream
    /// </summary>
    private static void WriteChannel(BinaryWriter writer, CutsceneChannel channel)
    {
        // Write CSChannel header (12 bytes)
        writer.Write((byte)channel.Type);
        writer.Write(channel.Flags);
        writer.Write((byte)0); // pad1
        writer.Write((byte)0); // pad2
        writer.Write(channel.Index);
        writer.Write((ushort)channel.Packets.Count);
        writer.Write(0); // packets pointer (written as 0, meaningless in file)

        // Write packets
        foreach (var packet in channel.Packets)
        {
            WritePacket(writer, packet);
        }
    }

    /// <summary>
    /// Write a single packet to the stream
    /// </summary>
    private static void WritePacket(BinaryWriter writer, CutscenePacket packet)
    {
        // For text packets with text, we need to set PosX as a flag
        int posXToWrite = packet.PosX;
        bool hasText = packet.Type == CutscenePacketType.Text && !string.IsNullOrEmpty(packet.Text);
        if (hasText)
        {
            posXToWrite = 1; // Non-zero indicates text follows
        }

        // Write CSPacket (24 bytes)
        writer.Write((byte)packet.Type);
        writer.Write((byte)packet.Flags);
        writer.Write(packet.Index);
        writer.Write(packet.Start);
        writer.Write(packet.Length);
        writer.Write(posXToWrite);
        writer.Write(packet.PosY);
        writer.Write(packet.PosZ);
        writer.Write(packet.Angle);
        writer.Write(packet.Pitch);

        // Write text if present
        if (hasText)
        {
            byte[] textBytes = Encoding.ASCII.GetBytes(packet.Text!);
            writer.Write(textBytes.Length);
            writer.Write(textBytes);
        }
    }

    /// <summary>
    /// Calculate the size of cutscene data when written to file
    /// </summary>
    public static int CalculateSize(CutsceneData cutscene)
    {
        int size = 2; // version + channelcount

        foreach (var channel in cutscene.Channels)
        {
            size += 12; // Channel header (including pointer)

            foreach (var packet in channel.Packets)
            {
                size += 24; // Fixed packet size

                // Add text size if present
                if (packet.Type == CutscenePacketType.Text && !string.IsNullOrEmpty(packet.Text))
                {
                    size += 4; // Length prefix
                    size += Encoding.ASCII.GetByteCount(packet.Text);
                }
            }
        }

        return size;
    }
}