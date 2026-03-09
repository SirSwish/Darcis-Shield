// /Services/LightsDataService.cs
// FIXED: Correct layout based on C++ source code analysis
using System.Diagnostics;
using System.IO;
using System.Windows;
using UrbanChaosLightEditor.Models;

namespace UrbanChaosLightEditor.Services
{
    /// <summary>
    /// Manages the in-memory .lgt bytes and basic load/save/dirty state.
    /// 
    /// CORRECTED Layout (from C++ ed.h/ed.cpp):
    ///   0x0000: Header (12 bytes)
    ///   0x000C: ED_Light[0..255] (256 × 20 = 5120 bytes) - index 0 is sentinel
    ///   0x140C: Properties (36 bytes)
    ///   0x1430: NightColour (3 bytes)
    ///   Total = 5171 bytes
    /// </summary>
    public sealed class LightsDataService
    {
        public static LightsDataService Instance { get; } = new LightsDataService();

        private LightsDataService()
        {
            Debug.WriteLine("[LightsDataService] Singleton instance created");
        }

        // ---- Constants (CORRECTED from C++ source) ----
        private const int HeaderSize = 12;
        private const int EntrySize = 20;
        private const int EntryCount = 256;  // ED_MAX_LIGHTS = 256 (index 0 is sentinel)
        private const int EntriesOffset = HeaderSize;  // NO padding! Entries at offset 12
        private const int PropertiesOffset = EntriesOffset + EntrySize * EntryCount;  // 5132
        private const int PropertiesSize = 36;
        private const int NightColourOffset = PropertiesOffset + PropertiesSize;  // 5168
        private const int NightColourSize = 3;
        private const int TotalSize = NightColourOffset + NightColourSize;  // 5171

        // ---- State ----
        private byte[] _bytes = Array.Empty<byte>();
        private bool _isLoaded;
        private bool _hasChanges;

        public string? CurrentPath { get; private set; }
        public string? CurrentFilePath => CurrentPath;
        public bool IsLoaded => _isLoaded;
        public bool HasChanges => _hasChanges;

        public byte[] GetBytesCopy() => (byte[])_bytes.Clone();

        // ---- Parsed Model ----
        public LightHeader Header { get; private set; } = new();
        public List<LightEntry> Entries { get; private set; } = new(EntryCount);
        public LightProperties Properties { get; set; }
        public LightNightColour NightColour { get; set; }

        // ---- Events ----
        public event EventHandler<PathEventArgs>? LightsLoaded;
        public event EventHandler<PathEventArgs>? LightsSaved;
        public event EventHandler? LightsCleared;
        public event EventHandler? LightsBytesReset;
        public event EventHandler? DirtyStateChanged;

        // ---------------------------------------------------------------------
        // Load / Save
        // ---------------------------------------------------------------------

        public async Task LoadAsync(string path)
        {
            Debug.WriteLine($"[LightsDataService.LoadAsync] Loading: {path}");

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is null/empty.", nameof(path));

            _bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            Debug.WriteLine($"[LightsDataService.LoadAsync] Read {_bytes.Length} bytes (expected >= {TotalSize})");

            if (_bytes.Length < TotalSize)
                throw new InvalidDataException($".lgt file too small (got {_bytes.Length}, expected ≥ {TotalSize}).");

            ParseFromBytes(_bytes);

            _isLoaded = true;
            _hasChanges = false;
            CurrentPath = path;

            LightsBytesReset?.Invoke(this, EventArgs.Empty);
            LightsLoaded?.Invoke(this, new PathEventArgs(path));
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);

            Debug.WriteLine($"[LightsDataService.LoadAsync] Complete - {Entries.Count} entries");
        }

        public void NewFromTemplate(byte[] bytes)
        {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < TotalSize)
                throw new InvalidDataException($"Template too small (got {bytes.Length}, expected ≥ {TotalSize}).");

            _bytes = (byte[])bytes.Clone();
            ParseFromBytes(_bytes);

            _isLoaded = true;
            _hasChanges = false;
            CurrentPath = null;

            LightsBytesReset?.Invoke(this, EventArgs.Empty);
            LightsLoaded?.Invoke(this, new PathEventArgs("(new)"));
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentPath))
                throw new InvalidOperationException("No current path. Use SaveAsAsync first.");

            Debug.WriteLine($"[LightsDataService.SaveAsync] Saving to {CurrentPath}");
            Debug.WriteLine($"[LightsDataService.SaveAsync] NightFlag=0x{Properties.NightFlag:X8}");

            var bytesToSave = BuildBytesFromModel();
            await File.WriteAllBytesAsync(CurrentPath, bytesToSave).ConfigureAwait(false);

            _bytes = bytesToSave;
            _hasChanges = false;

            LightsSaved?.Invoke(this, new PathEventArgs(CurrentPath));
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);

            Debug.WriteLine($"[LightsDataService.SaveAsync] Saved {bytesToSave.Length} bytes");
        }

        public async Task SaveAsAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is null/empty.", nameof(path));

            var bytesToSave = BuildBytesFromModel();
            await File.WriteAllBytesAsync(path, bytesToSave).ConfigureAwait(false);

            _bytes = bytesToSave;
            CurrentPath = path;
            _hasChanges = false;

            LightsSaved?.Invoke(this, new PathEventArgs(path));
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }

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

        public void ReplaceAllBytes(byte[] bytes, bool markDirty = true)
        {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < TotalSize)
                throw new InvalidDataException($"Buffer too small (got {bytes.Length}, expected ≥ {TotalSize}).");

            _bytes = (byte[])bytes.Clone();
            _isLoaded = true;

            ParseFromBytes(_bytes);

            if (markDirty && !_hasChanges)
            {
                _hasChanges = true;
                DirtyStateChanged?.Invoke(this, EventArgs.Empty);
            }

            LightsBytesReset?.Invoke(this, EventArgs.Empty);
        }

        // ---------------------------------------------------------------------
        // Parsing
        // ---------------------------------------------------------------------

        private void ParseFromBytes(byte[] bytes)
        {
            Debug.WriteLine($"[ParseFromBytes] Buffer size = {bytes.Length}");

            // Header
            int sizeField = BitConverter.ToInt32(bytes, 0);
            Header = new LightHeader
            {
                SizeOfEdLight = sizeField,
                EdMaxLights = BitConverter.ToInt32(bytes, 4),
                SizeOfNightColour = BitConverter.ToInt32(bytes, 8)
            };
            Debug.WriteLine($"[ParseFromBytes] Header: size={Header.SizeOfEdLightLower}, ver={Header.Version}, max={Header.EdMaxLights}");

            // Entries (256 total, starting at offset 12)
            Entries = new List<LightEntry>(EntryCount);
            int usedCount = 0;

            for (int i = 0; i < EntryCount; i++)
            {
                int off = EntriesOffset + i * EntrySize;
                if (off + EntrySize > bytes.Length) break;

                var entry = new LightEntry
                {
                    Range = bytes[off + 0],
                    Red = unchecked((sbyte)bytes[off + 1]),
                    Green = unchecked((sbyte)bytes[off + 2]),
                    Blue = unchecked((sbyte)bytes[off + 3]),
                    Next = bytes[off + 4],
                    Used = bytes[off + 5],
                    Flags = bytes[off + 6],
                    Padding = bytes[off + 7],
                    X = BitConverter.ToInt32(bytes, off + 8),
                    Y = BitConverter.ToInt32(bytes, off + 12),
                    Z = BitConverter.ToInt32(bytes, off + 16),
                };
                Entries.Add(entry);
                if (entry.Used == 1) usedCount++;
            }
            Debug.WriteLine($"[ParseFromBytes] {Entries.Count} entries, {usedCount} used");

            // Properties (at offset 5132)
            Properties = new LightProperties
            {
                EdLightFree = BitConverter.ToInt32(bytes, PropertiesOffset + 0),
                NightFlag = BitConverter.ToUInt32(bytes, PropertiesOffset + 4),
                NightAmbD3DColour = BitConverter.ToUInt32(bytes, PropertiesOffset + 8),
                NightAmbD3DSpecular = BitConverter.ToUInt32(bytes, PropertiesOffset + 12),
                NightAmbRed = BitConverter.ToInt32(bytes, PropertiesOffset + 16),
                NightAmbGreen = BitConverter.ToInt32(bytes, PropertiesOffset + 20),
                NightAmbBlue = BitConverter.ToInt32(bytes, PropertiesOffset + 24),
                NightLampostRed = unchecked((sbyte)bytes[PropertiesOffset + 28]),
                NightLampostGreen = unchecked((sbyte)bytes[PropertiesOffset + 29]),
                NightLampostBlue = unchecked((sbyte)bytes[PropertiesOffset + 30]),
                Padding = bytes[PropertiesOffset + 31],
                NightLampostRadius = BitConverter.ToInt32(bytes, PropertiesOffset + 32),
            };
            Debug.WriteLine($"[ParseFromBytes] NightFlag=0x{Properties.NightFlag:X8}, D3D=0x{Properties.NightAmbD3DColour:X8}");

            // NightColour (at offset 5168)
            NightColour = new LightNightColour
            {
                Red = bytes[NightColourOffset + 0],
                Green = bytes[NightColourOffset + 1],
                Blue = bytes[NightColourOffset + 2]
            };
        }

        private byte[] BuildBytesFromModel()
        {
            var outBytes = new byte[TotalSize];

            // Header (12 bytes)
            int sizeOfEdLight = EntrySize | (1 << 16);  // size=20, version=1
            BitConverter.GetBytes(sizeOfEdLight).CopyTo(outBytes, 0);
            BitConverter.GetBytes(EntryCount).CopyTo(outBytes, 4);
            BitConverter.GetBytes(NightColourSize).CopyTo(outBytes, 8);

            // Entries (256 × 20 bytes at offset 12)
            for (int i = 0; i < EntryCount; i++)
            {
                int off = EntriesOffset + i * EntrySize;
                LightEntry e = i < Entries.Count ? Entries[i] : default;

                outBytes[off + 0] = e.Range;
                outBytes[off + 1] = unchecked((byte)e.Red);
                outBytes[off + 2] = unchecked((byte)e.Green);
                outBytes[off + 3] = unchecked((byte)e.Blue);
                outBytes[off + 4] = e.Next;
                outBytes[off + 5] = e.Used;
                outBytes[off + 6] = e.Flags;
                outBytes[off + 7] = e.Padding;
                BitConverter.GetBytes(e.X).CopyTo(outBytes, off + 8);
                BitConverter.GetBytes(e.Y).CopyTo(outBytes, off + 12);
                BitConverter.GetBytes(e.Z).CopyTo(outBytes, off + 16);
            }

            // Properties (36 bytes at offset 5132)
            BitConverter.GetBytes(Properties.EdLightFree).CopyTo(outBytes, PropertiesOffset + 0);
            BitConverter.GetBytes(Properties.NightFlag).CopyTo(outBytes, PropertiesOffset + 4);
            BitConverter.GetBytes(Properties.NightAmbD3DColour).CopyTo(outBytes, PropertiesOffset + 8);
            BitConverter.GetBytes(Properties.NightAmbD3DSpecular).CopyTo(outBytes, PropertiesOffset + 12);
            BitConverter.GetBytes(Properties.NightAmbRed).CopyTo(outBytes, PropertiesOffset + 16);
            BitConverter.GetBytes(Properties.NightAmbGreen).CopyTo(outBytes, PropertiesOffset + 20);
            BitConverter.GetBytes(Properties.NightAmbBlue).CopyTo(outBytes, PropertiesOffset + 24);
            outBytes[PropertiesOffset + 28] = unchecked((byte)Properties.NightLampostRed);
            outBytes[PropertiesOffset + 29] = unchecked((byte)Properties.NightLampostGreen);
            outBytes[PropertiesOffset + 30] = unchecked((byte)Properties.NightLampostBlue);
            outBytes[PropertiesOffset + 31] = Properties.Padding;
            BitConverter.GetBytes(Properties.NightLampostRadius).CopyTo(outBytes, PropertiesOffset + 32);

            // NightColour (3 bytes at offset 5168)
            outBytes[NightColourOffset + 0] = NightColour.Red;
            outBytes[NightColourOffset + 1] = NightColour.Green;
            outBytes[NightColourOffset + 2] = NightColour.Blue;

            Debug.WriteLine($"[BuildBytesFromModel] Built {outBytes.Length} bytes, NightFlag=0x{Properties.NightFlag:X8}");
            return outBytes;
        }

        // ---------------------------------------------------------------------
        // Default .lgt creation
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
            catch { }

            return CreateEmptyLgtBuffer();
        }

        public static byte[] CreateEmptyLgtBuffer()
        {
            var bytes = new byte[TotalSize];

            // Header
            int sizeOfEdLight = EntrySize | (1 << 16);
            BitConverter.GetBytes(sizeOfEdLight).CopyTo(bytes, 0);
            BitConverter.GetBytes(EntryCount).CopyTo(bytes, 4);
            BitConverter.GetBytes(NightColourSize).CopyTo(bytes, 8);

            // Initialize free list (entries 1-255, entry 0 is sentinel)
            for (int i = 1; i < EntryCount - 1; i++)
            {
                int off = EntriesOffset + i * EntrySize;
                bytes[off + 4] = (byte)(i + 1);  // Next
                bytes[off + 5] = 0;               // Used = false
            }
            // Last entry's next = 0
            bytes[EntriesOffset + (EntryCount - 1) * EntrySize + 4] = 0;

            // Properties
            BitConverter.GetBytes(1).CopyTo(bytes, PropertiesOffset + 0);  // EdLightFree = 1
            BitConverter.GetBytes(0u).CopyTo(bytes, PropertiesOffset + 4);  // NightFlag = 0 (night mode)
            BitConverter.GetBytes(0xFF404040u).CopyTo(bytes, PropertiesOffset + 8);   // D3D colour
            BitConverter.GetBytes(0xFF000000u).CopyTo(bytes, PropertiesOffset + 12);  // Specular
            BitConverter.GetBytes(64).CopyTo(bytes, PropertiesOffset + 16);  // AmbRed
            BitConverter.GetBytes(64).CopyTo(bytes, PropertiesOffset + 20);  // AmbGreen
            BitConverter.GetBytes(64).CopyTo(bytes, PropertiesOffset + 24);  // AmbBlue
            bytes[PropertiesOffset + 28] = 127;  // LampostRed
            bytes[PropertiesOffset + 29] = 127;  // LampostGreen
            bytes[PropertiesOffset + 30] = 100;  // LampostBlue
            bytes[PropertiesOffset + 31] = 0;    // Padding
            BitConverter.GetBytes(512).CopyTo(bytes, PropertiesOffset + 32);  // LampostRadius

            // NightColour (dark blue sky)
            bytes[NightColourOffset + 0] = 20;
            bytes[NightColourOffset + 1] = 20;
            bytes[NightColourOffset + 2] = 40;

            return bytes;
        }
    }

    public sealed class PathEventArgs : EventArgs
    {
        public string Path { get; }
        public PathEventArgs(string path) => Path = path;
    }
}