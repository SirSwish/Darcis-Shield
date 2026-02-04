// /Services/ReadOnlyMapDataService.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace UrbanChaosLightEditor.Services
{
    /// <summary>
    /// Read-only service for loading .iam map files for display purposes.
    /// Does not support editing, saving, or change tracking.
    /// </summary>
    public sealed class ReadOnlyMapDataService
    {
        private static readonly Lazy<ReadOnlyMapDataService> _lazy = new(() => new ReadOnlyMapDataService());
        public static ReadOnlyMapDataService Instance => _lazy.Value;

        private readonly object _sync = new();

        private ReadOnlyMapDataService()
        {
            Debug.WriteLine("[ReadOnlyMapDataService] Singleton instance created");
        }

        public byte[]? MapBytes { get; private set; }
        public string? CurrentPath { get; private set; }
        public bool IsLoaded => MapBytes is not null;
        public long SizeBytes => MapBytes?.LongLength ?? 0;

        // Cached building region
        private (int Start, int Length) _buildingRegion = (-1, 0);

        // Events
        public event EventHandler? MapLoaded;
        public event EventHandler? MapCleared;

        /// <summary>Load a .iam map file for display.</summary>
        public async Task LoadAsync(string path)
        {
            Debug.WriteLine($"[ReadOnlyMapDataService.LoadAsync] Loading: {path}");

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", nameof(path));

            var full = Path.GetFullPath(path);

            byte[] bytes = await File.ReadAllBytesAsync(full).ConfigureAwait(false);
            Debug.WriteLine($"[ReadOnlyMapDataService.LoadAsync] Read {bytes.Length} bytes");

            lock (_sync)
            {
                MapBytes = bytes;
                CurrentPath = full;
                _buildingRegion = (-1, 0);
            }

            // Compute building region for BuildingLayer
            ComputeAndCacheBuildingRegion();

            Debug.WriteLine($"[ReadOnlyMapDataService.LoadAsync] Complete");
            MapLoaded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Clear the loaded map.</summary>
        public void Clear()
        {
            lock (_sync)
            {
                MapBytes = null;
                CurrentPath = null;
                _buildingRegion = (-1, 0);
            }
            MapCleared?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Return a defensive copy of the current bytes.</summary>
        public byte[] GetBytesCopy()
        {
            if (!IsLoaded) throw new InvalidOperationException("No map loaded.");
            lock (_sync) { return (byte[])MapBytes!.Clone(); }
        }

        /// <summary>Read-only view without copying.</summary>
        public ReadOnlyMemory<byte> AsReadOnlyMemory() => new(MapBytes ?? Array.Empty<byte>());

        // ===== Building Region (for BuildingLayer) =====

        public void ComputeAndCacheBuildingRegion()
        {
            _buildingRegion = (-1, 0);

            if (!IsLoaded) return;

            var bytes = GetBytesCopy();
            if (bytes.Length < 12) return;

            int saveType = BitConverter.ToInt32(bytes, 0);
            int objectBytesFromHeader = BitConverter.ToInt32(bytes, 4);
            int sizeAdjustment = saveType >= 25 ? 2000 : 0;

            int objectOffset = bytes.Length - 12 - sizeAdjustment - objectBytesFromHeader + 8;

            // Try strict finder first
            if (objectOffset > 0 && objectOffset <= bytes.Length &&
                TryFindBuildingRegion(bytes, objectOffset, out int headerOff, out int regionLen))
            {
                _buildingRegion = (headerOff, regionLen);
                Debug.WriteLine($"[ReadOnlyMapDataService] Building region (strict): start=0x{headerOff:X} len={regionLen}");
                return;
            }

            // Fallback to V1 heuristic
            const int tileBytes = 128 * 128 * 6;
            int buildingStart = 8 + tileBytes;
            int buildingEnd = Math.Clamp(objectOffset, 0, bytes.Length);
            int buildingLen = buildingEnd - buildingStart;

            if (buildingStart >= 0 && buildingEnd <= bytes.Length && buildingLen > 0)
            {
                _buildingRegion = (buildingStart, buildingLen);
                Debug.WriteLine($"[ReadOnlyMapDataService] Building region (fallback): start=0x{buildingStart:X} len={buildingLen}");
            }
        }

        public bool TryGetBuildingRegion(out int start, out int length)
        {
            start = _buildingRegion.Start;
            length = _buildingRegion.Length;
            return start >= 0 && length > 0;
        }

        /// <summary>
        /// Scans backwards from objectOffset for the 0xFC09F00D signature to find building block.
        /// </summary>
        private static bool TryFindBuildingRegion(byte[] bytes, int objectOffset, out int headerOffset, out int regionLength)
        {
            headerOffset = -1;
            regionLength = 0;

            // Signature bytes: 0D F0 09 FC (little-endian for 0xFC09F00D)
            byte[] sig = { 0x0D, 0xF0, 0x09, 0xFC };

            int searchStart = Math.Max(0, objectOffset - 500000);
            for (int i = objectOffset - 4; i >= searchStart; i--)
            {
                if (bytes[i] == sig[0] && bytes[i + 1] == sig[1] &&
                    bytes[i + 2] == sig[2] && bytes[i + 3] == sig[3])
                {
                    headerOffset = i;
                    regionLength = objectOffset - i;
                    return true;
                }
            }
            return false;
        }
    }
}