using System.Diagnostics;
using System.IO;
using System.Windows;
using UrbanChaosMissionEditor.Models;

namespace UrbanChaosMissionEditor.Services;

/// <summary>
/// Read-only service for loading .lgt light files for display purposes.
/// </summary>
public sealed class ReadOnlyLightsDataService
{
    private static readonly Lazy<ReadOnlyLightsDataService> _lazy = new(() => new ReadOnlyLightsDataService());
    public static ReadOnlyLightsDataService Instance => _lazy.Value;

    private readonly object _sync = new();

    // Layout constants
    private const int HeaderSize = 12;
    private const int ReservedPad = 20;
    private const int EntrySize = 20;
    private const int EntryCount = 255;
    private const int EntriesOffset = HeaderSize + ReservedPad;
    private const int TotalMinSize = 5171;

    private ReadOnlyLightsDataService()
    {
        Debug.WriteLine("[ReadOnlyLightsDataService] Singleton instance created");
    }

    public byte[]? LightBytes { get; private set; }
    public string? CurrentPath { get; private set; }
    public bool IsLoaded => LightBytes is not null;

    public event EventHandler? LightsLoaded;
    public event EventHandler? LightsCleared;

    /// <summary>Load a .lgt light file for display.</summary>
    public async Task LoadAsync(string path)
    {
        Debug.WriteLine($"[ReadOnlyLightsDataService.LoadAsync] Loading: {path}");

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));

        var full = Path.GetFullPath(path);
        byte[] bytes = await File.ReadAllBytesAsync(full).ConfigureAwait(false);
        Debug.WriteLine($"[ReadOnlyLightsDataService.LoadAsync] Read {bytes.Length} bytes");

        if (bytes.Length < TotalMinSize)
            throw new InvalidDataException($".lgt file too small (got {bytes.Length}, expected >= {TotalMinSize}).");

        lock (_sync)
        {
            LightBytes = bytes;
            CurrentPath = full;
        }

        Debug.WriteLine($"[ReadOnlyLightsDataService.LoadAsync] Complete");
        RaiseEventOnUIThread(LightsLoaded);
    }

    public void Clear()
    {
        lock (_sync)
        {
            LightBytes = null;
            CurrentPath = null;
        }
        RaiseEventOnUIThread(LightsCleared);
    }

    private void RaiseEventOnUIThread(EventHandler? handler)
    {
        if (handler == null) return;

        if (Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            handler.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Application.Current?.Dispatcher?.BeginInvoke(() => handler.Invoke(this, EventArgs.Empty));
        }
    }

    public byte[] GetBytesCopy()
    {
        if (!IsLoaded) throw new InvalidOperationException("No lights loaded.");
        lock (_sync) { return (byte[])LightBytes!.Clone(); }
    }

    /// <summary>Read all light entries from the buffer.</summary>
    public List<LightEntry> ReadAllEntries()
    {
        var list = new List<LightEntry>(EntryCount);
        if (!IsLoaded) return list;

        var bytes = GetBytesCopy();
        int usedCount = 0;

        for (int i = 0; i < EntryCount; i++)
        {
            int off = EntriesOffset + i * EntrySize;
            if (off + EntrySize > bytes.Length) break;

            var e = new LightEntry
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
            list.Add(e);
            if (e.Used == 1) usedCount++;
        }

        Debug.WriteLine($"[ReadOnlyLightsDataService] Read {list.Count} entries, {usedCount} used");
        return list;
    }

    // Coordinate mapping (8192 UI px <-> 32768 world units)
    // Inverted to match engine rendering
    public static int WorldXToUiX(int worldX) => (32768 - worldX) / 4;
    public static int WorldZToUiZ(int worldZ) => (32768 - worldZ) / 4;
}