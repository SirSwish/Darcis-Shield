using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using UrbanChaosLightEditor.Models;

namespace UrbanChaosLightEditor.Services
{
    /// <summary>
    /// Manages the in-memory .lgt bytes and basic load/save/dirty state.
    /// Also exposes parsed models (Header, Entries, Properties, NightColour).
    /// </summary>
    public sealed class LightsDataService
    {
        // Singleton
        public static LightsDataService Instance { get; } = new LightsDataService();
        private LightsDataService()
        {
            Debug.WriteLine("[LightsDataService] Singleton instance created");
        }

        // ---- Constants (v1 format) ----
        private const int HeaderSize = 12;
        private const int ReservedAfterHeader = 20;
        private const int EntrySize = 20;
        private const int EntryCount = 255;
        private const int PropertiesSize = 36;
        private const int NightColourSize = 3;
        private const int TotalSize = HeaderSize + ReservedAfterHeader + EntrySize * EntryCount + PropertiesSize + NightColourSize; // 5171

        // ---- State ----
        private byte[] _bytes = Array.Empty<byte>();
        private bool _isLoaded;
        private bool _hasChanges;

        /// <summary>Full path of the current .lgt file (null if unsaved template/default).</summary>
        public string? CurrentPath { get; private set; }

        /// <summary>Alias for compatibility with ViewModels that use CurrentFilePath.</summary>
        public string? CurrentFilePath => CurrentPath;

        /// <summary>True once a lights buffer is present in memory (from load or template).</summary>
        public bool IsLoaded => _isLoaded;

        /// <summary>True if there are unsaved changes.</summary>
        public bool HasChanges => _hasChanges;

        /// <summary>Returns a copy of the current lights bytes (never null).</summary>
        public byte[] GetBytesCopy()
        {
            Debug.WriteLine($"[LightsDataService.GetBytesCopy] Returning {_bytes.Length} bytes");
            return (byte[])_bytes.Clone();
        }

        // ---- Parsed Model (kept in sync with _bytes) ----
        public LightHeader Header { get; private set; } = new();
        public List<LightEntry> Entries { get; private set; } = new(EntryCount);
        public LightProperties Properties { get; private set; }
        public LightNightColour NightColour { get; private set; }

        // ---- Events ----
        public event EventHandler<PathEventArgs>? LightsLoaded;
        public event EventHandler<PathEventArgs>? LightsSaved;
        public event EventHandler? LightsCleared;
        public event EventHandler? LightsBytesReset;
        public event EventHandler? DirtyStateChanged;

        // ---------------------------------------------------------------------
        // Load / Save (raw)
        // ---------------------------------------------------------------------

        /// <summary>Load lights from disk into memory (resets dirty) and parse models.</summary>
        public async Task LoadAsync(string path)
        {
            Debug.WriteLine($"[LightsDataService.LoadAsync] Starting for path: {path}");

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is null/empty.", nameof(path));

            Debug.WriteLine($"[LightsDataService.LoadAsync] Reading file...");
            _bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            Debug.WriteLine($"[LightsDataService.LoadAsync] Read {_bytes.Length} bytes (expected >= {TotalSize})");

            if (_bytes.Length < TotalSize)
                throw new InvalidDataException($".lgt file too small (got {_bytes.Length}, expected ≥ {TotalSize}).");

            Debug.WriteLine($"[LightsDataService.LoadAsync] Calling ParseFromBytes...");
            ParseFromBytes(_bytes);
            Debug.WriteLine($"[LightsDataService.LoadAsync] ParseFromBytes complete");

            _isLoaded = true;
            _hasChanges = false;
            CurrentPath = path;

            Debug.WriteLine($"[LightsDataService.LoadAsync] Raising events...");
            LightsBytesReset?.Invoke(this, EventArgs.Empty);
            LightsLoaded?.Invoke(this, new PathEventArgs(path));
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);

            Debug.WriteLine($"[LightsDataService.LoadAsync] Complete - Loaded {Entries.Count} entries");
        }

        /// <summary>
        /// Seeds the lights buffer from provided template bytes (not dirty) and parse models.
        /// Leaves CurrentPath = null (unsaved).
        /// </summary>
        public void NewFromTemplate(byte[] bytes)
        {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < TotalSize)
                throw new InvalidDataException($"Template .lgt is too small (got {bytes.Length}, expected ≥ {TotalSize}).");

            _bytes = (byte[])bytes.Clone();
            ParseFromBytes(_bytes);

            _isLoaded = true;
            _hasChanges = false;
            CurrentPath = null;

            LightsBytesReset?.Invoke(this, EventArgs.Empty);
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Saves to CurrentPath. Throws if there is no current path.</summary>
        public async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentPath))
                throw new InvalidOperationException("No current lights file path. Use SaveAsAsync first.");

            _bytes = BuildBytesFromModel();
            await File.WriteAllBytesAsync(CurrentPath, _bytes).ConfigureAwait(false);
            _hasChanges = false;

            LightsSaved?.Invoke(this, new PathEventArgs(CurrentPath));
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Saves to a new path and updates CurrentPath.</summary>
        public async Task SaveAsAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is null/empty.", nameof(path));

            _bytes = BuildBytesFromModel();
            await File.WriteAllBytesAsync(path, _bytes).ConfigureAwait(false);
            CurrentPath = path;
            _hasChanges = false;

            LightsSaved?.Invoke(this, new PathEventArgs(path));
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Clears the lights buffer and resets state.</summary>
        public void Clear()
        {
            _bytes = Array.Empty<byte>();
            _isLoaded = false;
            _hasChanges = false;
            CurrentPath = null;

            Header = new LightHeader();
            Entries = new List<LightEntry>(EntryCount);
            Properties = default;
            NightColour = default;

            LightsCleared?.Invoke(this, EventArgs.Empty);
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Replace the entire lights buffer. Marks dirty by default. Parses models.</summary>
        public void ReplaceAllBytes(byte[] bytes, bool markDirty = true)
        {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < TotalSize)
                throw new InvalidDataException($".lgt buffer too small (got {bytes.Length}, expected ≥ {TotalSize}).");

            _bytes = (byte[])bytes.Clone();
            _isLoaded = true;

            ParseFromBytes(_bytes);

            if (markDirty)
            {
                _hasChanges = true;
                DirtyStateChanged?.Invoke(this, EventArgs.Empty);
            }

            LightsBytesReset?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Marks the lights as dirty.</summary>
        public void MarkDirty()
        {
            if (_hasChanges) return;
            _hasChanges = true;
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }

        // ---------------------------------------------------------------------
        // Parsed model helpers
        // ---------------------------------------------------------------------

        private void ParseFromBytes(byte[] bytes)
        {
            Debug.WriteLine($"[ParseFromBytes] Starting with {bytes.Length} bytes");

            int offset = 0;

            // Parse header
            Debug.WriteLine($"[ParseFromBytes] Reading header at offset {offset}");
            Header = new LightHeader
            {
                SizeOfEdLight = BitConverter.ToInt32(bytes, offset + 0),
                EdMaxLights = BitConverter.ToInt32(bytes, offset + 4),
                SizeOfNightColour = BitConverter.ToInt32(bytes, offset + 8)
            };
            Debug.WriteLine($"[ParseFromBytes] Header: SizeOfEdLight={Header.SizeOfEdLight} (version={Header.Version}, size={Header.SizeOfEdLightLower}), EdMaxLights={Header.EdMaxLights}, SizeOfNightColour={Header.SizeOfNightColour}");

            offset += HeaderSize + ReservedAfterHeader;
            Debug.WriteLine($"[ParseFromBytes] Entries start at offset {offset} (0x{offset:X})");

            // Parse entries
            var list = new List<LightEntry>(EntryCount);
            int usedCount = 0;

            for (int i = 0; i < EntryCount; i++)
            {
                var e = new LightEntry
                {
                    Range = bytes[offset + 0],
                    Red = unchecked((sbyte)bytes[offset + 1]),
                    Green = unchecked((sbyte)bytes[offset + 2]),
                    Blue = unchecked((sbyte)bytes[offset + 3]),
                    Next = bytes[offset + 4],
                    Used = bytes[offset + 5],
                    Flags = bytes[offset + 6],
                    Padding = bytes[offset + 7],
                    X = BitConverter.ToInt32(bytes, offset + 8),
                    Y = BitConverter.ToInt32(bytes, offset + 12),
                    Z = BitConverter.ToInt32(bytes, offset + 16)
                };
                list.Add(e);

                if (e.Used == 1)
                {
                    usedCount++;
                    if (usedCount <= 5) // Log first 5 used lights
                    {
                        Debug.WriteLine($"[ParseFromBytes] Entry[{i}]: Used=1, Range={e.Range}, RGB=({e.Red},{e.Green},{e.Blue}), Pos=({e.X},{e.Y},{e.Z})");
                    }
                }

                offset += EntrySize;
            }
            Entries = list;
            Debug.WriteLine($"[ParseFromBytes] Parsed {list.Count} entries, {usedCount} are used");

            // Parse properties
            Debug.WriteLine($"[ParseFromBytes] Properties at offset {offset} (0x{offset:X})");
            Properties = new LightProperties
            {
                EdLightFree = BitConverter.ToInt32(bytes, offset + 0),
                NightFlag = BitConverter.ToUInt32(bytes, offset + 4),
                NightAmbD3DColour = BitConverter.ToUInt32(bytes, offset + 8),
                NightAmbD3DSpecular = BitConverter.ToUInt32(bytes, offset + 12),
                NightAmbRed = BitConverter.ToInt32(bytes, offset + 16),
                NightAmbGreen = BitConverter.ToInt32(bytes, offset + 20),
                NightAmbBlue = BitConverter.ToInt32(bytes, offset + 24),
                NightLampostRed = unchecked((sbyte)bytes[offset + 28]),
                NightLampostGreen = unchecked((sbyte)bytes[offset + 29]),
                NightLampostBlue = unchecked((sbyte)bytes[offset + 30]),
                Padding = bytes[offset + 31],
                NightLampostRadius = BitConverter.ToInt32(bytes, offset + 32),
            };
            Debug.WriteLine($"[ParseFromBytes] Properties: EdLightFree={Properties.EdLightFree}, NightFlag={Properties.NightFlag}, D3DColour=0x{Properties.NightAmbD3DColour:X8}");
            offset += PropertiesSize;

            // Parse night colour
            Debug.WriteLine($"[ParseFromBytes] NightColour at offset {offset} (0x{offset:X})");
            NightColour = new LightNightColour
            {
                Red = bytes[offset + 0],
                Green = bytes[offset + 1],
                Blue = bytes[offset + 2]
            };
            Debug.WriteLine($"[ParseFromBytes] NightColour: RGB=({NightColour.Red},{NightColour.Green},{NightColour.Blue})");
            Debug.WriteLine($"[ParseFromBytes] Complete");
        }

        private byte[] BuildBytesFromModel()
        {
            if (_bytes is null || _bytes.Length < HeaderSize)
                throw new InvalidOperationException("No existing header available.");

            var outBytes = new byte[TotalSize];
            int offset = 0;

            Array.Copy(_bytes, 0, outBytes, 0, HeaderSize);
            offset += HeaderSize + ReservedAfterHeader;

            for (int i = 0; i < EntryCount; i++)
            {
                LightEntry e = i < Entries.Count ? Entries[i] : default;
                outBytes[offset + 0] = e.Range;
                outBytes[offset + 1] = unchecked((byte)e.Red);
                outBytes[offset + 2] = unchecked((byte)e.Green);
                outBytes[offset + 3] = unchecked((byte)e.Blue);
                outBytes[offset + 4] = e.Next;
                outBytes[offset + 5] = e.Used;
                outBytes[offset + 6] = e.Flags;
                outBytes[offset + 7] = e.Padding;
                Array.Copy(BitConverter.GetBytes(e.X), 0, outBytes, offset + 8, 4);
                Array.Copy(BitConverter.GetBytes(e.Y), 0, outBytes, offset + 12, 4);
                Array.Copy(BitConverter.GetBytes(e.Z), 0, outBytes, offset + 16, 4);
                offset += EntrySize;
            }

            Array.Copy(BitConverter.GetBytes(Properties.EdLightFree), 0, outBytes, offset + 0, 4);
            Array.Copy(BitConverter.GetBytes(Properties.NightFlag), 0, outBytes, offset + 4, 4);
            Array.Copy(BitConverter.GetBytes(Properties.NightAmbD3DColour), 0, outBytes, offset + 8, 4);
            Array.Copy(BitConverter.GetBytes(Properties.NightAmbD3DSpecular), 0, outBytes, offset + 12, 4);
            Array.Copy(BitConverter.GetBytes(Properties.NightAmbRed), 0, outBytes, offset + 16, 4);
            Array.Copy(BitConverter.GetBytes(Properties.NightAmbGreen), 0, outBytes, offset + 20, 4);
            Array.Copy(BitConverter.GetBytes(Properties.NightAmbBlue), 0, outBytes, offset + 24, 4);
            outBytes[offset + 28] = unchecked((byte)Properties.NightLampostRed);
            outBytes[offset + 29] = unchecked((byte)Properties.NightLampostGreen);
            outBytes[offset + 30] = unchecked((byte)Properties.NightLampostBlue);
            outBytes[offset + 31] = Properties.Padding;
            Array.Copy(BitConverter.GetBytes(Properties.NightLampostRadius), 0, outBytes, offset + 32, 4);
            offset += PropertiesSize;

            outBytes[offset + 0] = NightColour.Red;
            outBytes[offset + 1] = NightColour.Green;
            outBytes[offset + 2] = NightColour.Blue;

            return outBytes;
        }

        // ---------------------------------------------------------------------
        // Default .lgt loader (pack resource)
        // ---------------------------------------------------------------------
        public static byte[] LoadDefaultResourceBytes()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/Defaults/default.lgt", UriKind.Absolute);
                var sri = Application.GetResourceStream(uri);
                if (sri != null)
                {
                    using var s = sri.Stream;
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch
            {
                // Fall through to create empty template
            }

            // Create an empty default .lgt file if resource not found
            return CreateEmptyLgtBuffer();
        }

        /// <summary>
        /// Creates a valid empty .lgt buffer with proper header and structure.
        /// </summary>
        public static byte[] CreateEmptyLgtBuffer()
        {
            var bytes = new byte[TotalSize];
            int offset = 0;

            // Header (12 bytes)
            // SizeOfEdLight: Entry size (20) in low word, version (1) in high word
            int sizeOfEdLight = 20 | (1 << 16);
            BitConverter.GetBytes(sizeOfEdLight).CopyTo(bytes, offset);
            offset += 4;

            // EdMaxLights: 255
            BitConverter.GetBytes(255).CopyTo(bytes, offset);
            offset += 4;

            // SizeOfNightColour: 3
            BitConverter.GetBytes(3).CopyTo(bytes, offset);
            offset += 4;

            // Reserved padding (20 bytes) - leave as zeros
            offset += ReservedAfterHeader;

            // 255 entries (all unused, 20 bytes each) - leave as zeros
            offset += EntrySize * EntryCount;

            // Properties (36 bytes)
            // EdLightFree = 1 (first slot is free)
            BitConverter.GetBytes(1).CopyTo(bytes, offset);
            offset += 4;

            // NightFlag = 0
            offset += 4;

            // NightAmbD3DColour = 0xFF404040 (dark gray with full alpha)
            BitConverter.GetBytes(0xFF404040u).CopyTo(bytes, offset);
            offset += 4;

            // NightAmbD3DSpecular = 0xFF000000 (black with full alpha)
            BitConverter.GetBytes(0xFF000000u).CopyTo(bytes, offset);
            offset += 4;

            // NightAmbRed/Green/Blue = 64, 64, 64
            BitConverter.GetBytes(64).CopyTo(bytes, offset); offset += 4;
            BitConverter.GetBytes(64).CopyTo(bytes, offset); offset += 4;
            BitConverter.GetBytes(64).CopyTo(bytes, offset); offset += 4;

            // NightLampostRed/Green/Blue/Padding (4 bytes)
            bytes[offset++] = 127; // Red
            bytes[offset++] = 127; // Green
            bytes[offset++] = 100; // Blue (slightly less for warmer tone)
            bytes[offset++] = 0;   // Padding

            // NightLampostRadius = 512
            BitConverter.GetBytes(512).CopyTo(bytes, offset);
            offset += 4;

            // Night colour (3 bytes) = dark blue sky
            bytes[offset++] = 20;  // Red
            bytes[offset++] = 20;  // Green
            bytes[offset++] = 40;  // Blue

            return bytes;
        }
    }

    /// <summary>Simple event args that carry a file path.</summary>
    public sealed class PathEventArgs : EventArgs
    {
        public string Path { get; }
        public PathEventArgs(string path) => Path = path;
    }
}