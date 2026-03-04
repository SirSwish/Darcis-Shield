// /Services/Buildings/WalkableEditor.cs
// Service for editing DWalkable fields in-place

using System;
using System.Diagnostics;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;

namespace UrbanChaosMapEditor.Services.Buildings
{
    /// <summary>
    /// Edits DWalkable fields directly in the IAM file.
    /// DWalkable structure (22 bytes):
    ///   +0-1:   StartPoint (ushort)
    ///   +2-3:   EndPoint (ushort)
    ///   +4-5:   StartFace3 (ushort)
    ///   +6-7:   EndFace3 (ushort)
    ///   +8-9:   StartFace4 (ushort)
    ///   +10-11: EndFace4 (ushort)
    ///   +12:    X1 (byte)
    ///   +13:    Z1 (byte)
    ///   +14:    X2 (byte)
    ///   +15:    Z2 (byte)
    ///   +16:    Y (byte)
    ///   +17:    StoreyY (byte)
    ///   +18-19: Next (ushort)
    ///   +20-21: Building (ushort)
    /// </summary>
    public sealed class WalkableEditor
    {
        private readonly MapDataService _svc;
        private const int DWalkableSize = 22;

        public WalkableEditor(MapDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        /// <summary>
        /// Gets the file offset for a walkable entry (1-based ID).
        /// </summary>
        public bool TryGetWalkableOffset(int walkableId1, out int offset)
        {
            offset = 0;
            if (!_svc.IsLoaded) return false;

            var acc = new BuildingsAccessor(_svc);
            if (!acc.TryGetWalkablesHeaderOffset(out int headerOffset))
                return false;

            var snap = acc.ReadSnapshot();
            if (snap.Walkables == null || walkableId1 < 1 || walkableId1 >= snap.Walkables.Length)
                return false;

            // walkablesDataOff = headerOffset + 4 (for the two U16 counts)
            int walkablesDataOff = headerOffset + 4;
            offset = walkablesDataOff + (walkableId1 * DWalkableSize);
            return true;
        }

        /// <summary>
        /// Sets the StartPoint field for a walkable.
        /// </summary>
        public bool TrySetStartPoint(int walkableId1, ushort value)
        {
            if (!TryGetWalkableOffset(walkableId1, out int baseOffset))
                return false;

            var bytes = _svc.GetBytesCopy();
            int offset = baseOffset + 0; // StartPoint at +0

            bytes[offset] = (byte)(value & 0xFF);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFF);

            Debug.WriteLine($"[WalkableEditor] Set Walkable #{walkableId1} StartPoint = {value} at offset 0x{offset:X}");
            _svc.ReplaceBytes(bytes);
            return true;
        }

        /// <summary>
        /// Sets the EndPoint field for a walkable.
        /// </summary>
        public bool TrySetEndPoint(int walkableId1, ushort value)
        {
            if (!TryGetWalkableOffset(walkableId1, out int baseOffset))
                return false;

            var bytes = _svc.GetBytesCopy();
            int offset = baseOffset + 2; // EndPoint at +2

            bytes[offset] = (byte)(value & 0xFF);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFF);

            Debug.WriteLine($"[WalkableEditor] Set Walkable #{walkableId1} EndPoint = {value} at offset 0x{offset:X}");
            _svc.ReplaceBytes(bytes);
            return true;
        }

        /// <summary>
        /// Sets both StartPoint and EndPoint for a walkable.
        /// </summary>
        public bool TrySetPointRange(int walkableId1, ushort startPoint, ushort endPoint)
        {
            if (!TryGetWalkableOffset(walkableId1, out int baseOffset))
                return false;

            var bytes = _svc.GetBytesCopy();

            // StartPoint at +0
            bytes[baseOffset + 0] = (byte)(startPoint & 0xFF);
            bytes[baseOffset + 1] = (byte)((startPoint >> 8) & 0xFF);

            // EndPoint at +2
            bytes[baseOffset + 2] = (byte)(endPoint & 0xFF);
            bytes[baseOffset + 3] = (byte)((endPoint >> 8) & 0xFF);

            Debug.WriteLine($"[WalkableEditor] Set Walkable #{walkableId1} StartPoint={startPoint}, EndPoint={endPoint}");
            _svc.ReplaceBytes(bytes);
            return true;
        }

        /// <summary>
        /// Sets the Y (altitude/32) field for a walkable.
        /// </summary>
        public bool TrySetY(int walkableId1, byte value)
        {
            if (!TryGetWalkableOffset(walkableId1, out int baseOffset))
                return false;

            var bytes = _svc.GetBytesCopy();
            int offset = baseOffset + 16; // Y at +16

            bytes[offset] = value;

            Debug.WriteLine($"[WalkableEditor] Set Walkable #{walkableId1} Y = {value} at offset 0x{offset:X}");
            _svc.ReplaceBytes(bytes);
            return true;
        }

        /// <summary>
        /// Sets the StoreyY field for a walkable.
        /// </summary>
        public bool TrySetStoreyY(int walkableId1, byte value)
        {
            if (!TryGetWalkableOffset(walkableId1, out int baseOffset))
                return false;

            var bytes = _svc.GetBytesCopy();
            int offset = baseOffset + 17; // StoreyY at +17

            bytes[offset] = value;

            Debug.WriteLine($"[WalkableEditor] Set Walkable #{walkableId1} StoreyY = {value} at offset 0x{offset:X}");
            _svc.ReplaceBytes(bytes);
            return true;
        }

        /// <summary>
        /// Gets the raw bytes for a walkable entry.
        /// </summary>
        public bool TryGetRawBytes(int walkableId1, out byte[] rawBytes)
        {
            rawBytes = new byte[DWalkableSize];
            if (!TryGetWalkableOffset(walkableId1, out int offset))
                return false;

            var bytes = _svc.GetBytesCopy();
            Buffer.BlockCopy(bytes, offset, rawBytes, 0, DWalkableSize);
            return true;
        }
    }
}