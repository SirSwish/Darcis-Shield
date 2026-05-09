using UrbanChaosEditor.Shared.Constants;
using UrbanChaosEditor.Shared.Models;

namespace UrbanChaosEditor.Shared.Views.MapOverlays;

internal static class SharedRoofDataParser
{
    private const int HeaderSize = BuildingFormatConstants.HeaderSize;
    private const int DBuildingSize = BuildingFormatConstants.DBuildingSize;
    private const int DFacetSize = BuildingFormatConstants.DFacetSize;
    private const int AfterBuildingsPad = BuildingFormatConstants.AfterBuildingsPad;
    private const int InsideStoreySize = BuildingFormatConstants.InsideStoreySize;
    private const int StaircaseSize = BuildingFormatConstants.StaircaseSize;
    private const int DWalkableSize = BuildingFormatConstants.DWalkableSize;
    private const int RoofFace4Size = BuildingFormatConstants.RoofFace4Size;
    private const int DStoreySize = BuildingFormatConstants.DStoreySize;

    public static bool TryReadRoofData(
        IRoofDataProvider provider,
        out DWalkableRec[] walkables,
        out RoofFace4Rec[] roofFaces4)
    {
        walkables = Array.Empty<DWalkableRec>();
        roofFaces4 = Array.Empty<RoofFace4Rec>();

        if (!provider.IsLoaded)
            return false;

        provider.ComputeAndCacheBuildingRegion();
        if (!provider.TryGetBuildingRegion(out int start, out int len))
            return false;

        byte[]? bytes = provider.GetBytesCopy();
        if (bytes == null || bytes.Length < 12)
            return false;

        long blockEnd = (long)start + len;
        if (start < 0 || len <= HeaderSize || blockEnd > bytes.Length)
            return false;

        int saveType = BitConverter.ToInt32(bytes, 0);
        ushort nextDBuilding = ReadU16(bytes, start + 2);
        ushort nextDFacet = ReadU16(bytes, start + 4);
        ushort nextDStyle = ReadU16(bytes, start + 6);
        ushort nextPaintMem = saveType >= 17 ? ReadU16(bytes, start + 8) : (ushort)0;
        ushort nextDStorey = saveType >= 17 ? ReadU16(bytes, start + 10) : (ushort)0;

        int totalBuildings = Math.Max(0, nextDBuilding - 1);
        int totalFacets = Math.Max(0, nextDFacet - 1);
        long cursor = start + HeaderSize;
        cursor += (long)totalBuildings * DBuildingSize + AfterBuildingsPad;
        cursor += (long)totalFacets * DFacetSize;

        if (cursor > blockEnd)
            return false;

        cursor += (long)nextDStyle * sizeof(short);
        if (saveType >= 17)
        {
            cursor += nextPaintMem;
            cursor += (long)nextDStorey * DStoreySize;
        }

        if (cursor > blockEnd)
            return false;

        if (saveType >= 21)
        {
            if (cursor + 8 > blockEnd)
                return false;

            int nextInsideStorey = ReadU16(bytes, (int)cursor + 0);
            int nextInsideStair = ReadU16(bytes, (int)cursor + 2);
            int nextInsideBlock = ReadU16(bytes, (int)cursor + 4);
            cursor += 8;
            cursor += (long)nextInsideStorey * InsideStoreySize;
            cursor += (long)nextInsideStair * StaircaseSize;
            cursor += nextInsideBlock;

            if (cursor > blockEnd)
                return false;
        }

        if (cursor + 4 > blockEnd)
            return false;

        ushort nextDWalkable = ReadU16(bytes, (int)cursor + 0);
        ushort nextRoofFace4 = ReadU16(bytes, (int)cursor + 2);
        cursor += 4;

        long dwBytes = (long)nextDWalkable * DWalkableSize;
        long rfBytes = (long)nextRoofFace4 * RoofFace4Size;
        if (cursor + dwBytes + rfBytes > blockEnd)
            return false;

        walkables = new DWalkableRec[nextDWalkable];
        for (int i = 0; i < nextDWalkable; i++)
        {
            int off = (int)cursor + i * DWalkableSize;
            walkables[i] = new DWalkableRec(
                ReadU16(bytes, off + 0),
                ReadU16(bytes, off + 2),
                ReadU16(bytes, off + 4),
                ReadU16(bytes, off + 6),
                ReadU16(bytes, off + 8),
                ReadU16(bytes, off + 10),
                bytes[off + 12],
                bytes[off + 13],
                bytes[off + 14],
                bytes[off + 15],
                bytes[off + 16],
                bytes[off + 17],
                ReadU16(bytes, off + 18),
                ReadU16(bytes, off + 20));
        }

        cursor += dwBytes;

        roofFaces4 = new RoofFace4Rec[nextRoofFace4];
        for (int i = 0; i < nextRoofFace4; i++)
        {
            int off = (int)cursor + i * RoofFace4Size;
            roofFaces4[i] = new RoofFace4Rec(
                ReadS16(bytes, off + 0),
                unchecked((sbyte)bytes[off + 2]),
                unchecked((sbyte)bytes[off + 3]),
                unchecked((sbyte)bytes[off + 4]),
                bytes[off + 5],
                bytes[off + 6],
                bytes[off + 7],
                ReadS16(bytes, off + 8));
        }

        return true;
    }

    private static ushort ReadU16(byte[] bytes, int offset)
        => (ushort)(bytes[offset] | (bytes[offset + 1] << 8));

    private static short ReadS16(byte[] bytes, int offset)
        => unchecked((short)(bytes[offset] | (bytes[offset + 1] << 8)));
}
