using System.Diagnostics;
using System.IO;

namespace UrbanChaosLightEditor.Services;

/// <summary>
/// Polls the currently loaded .iam map file for on-disk changes and
/// automatically reloads the map service when a change is detected.
/// Uses polling rather than FileSystemWatcher to avoid reliability issues on Windows.
/// </summary>
public sealed class ExternalFileWatcherService
{
    private static readonly Lazy<ExternalFileWatcherService> _lazy = new(() => new ExternalFileWatcherService());
    public static ExternalFileWatcherService Instance => _lazy.Value;

    private const int PollIntervalMs = 1000;

    private Timer? _timer;
    private DateTime _lastMapWriteTime = DateTime.MinValue;
    private bool _reloadingMap;

    private ExternalFileWatcherService() { }

    /// <summary>
    /// Start polling. Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    public void Start()
    {
        if (_timer != null) return;
        _timer = new Timer(Poll, null, PollIntervalMs, PollIntervalMs);
        Debug.WriteLine("[ExternalFileWatcherService] Started polling");
    }

    /// <summary>
    /// Stop polling.
    /// </summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        Debug.WriteLine("[ExternalFileWatcherService] Stopped polling");
    }

    /// <summary>
    /// Reset stored timestamp so the next poll doesn't treat the current file as changed.
    /// Call this after programmatically loading a map file to avoid a spurious reload.
    /// </summary>
    public void ResetTimestamps()
    {
        _lastMapWriteTime = GetWriteTime(ReadOnlyMapDataService.Instance.CurrentPath);
    }

    private void Poll(object? _)
    {
        CheckMap();
    }

    private void CheckMap()
    {
        var path = ReadOnlyMapDataService.Instance.CurrentPath;
        if (path == null) return;
        if (_reloadingMap) return;

        var writeTime = GetWriteTime(path);
        if (writeTime == DateTime.MinValue) return; // file gone or unreadable

        if (_lastMapWriteTime == DateTime.MinValue)
        {
            // First poll after load — record baseline, don't reload
            _lastMapWriteTime = writeTime;
            return;
        }

        if (writeTime <= _lastMapWriteTime) return;

        _lastMapWriteTime = writeTime;
        _reloadingMap = true;
        Debug.WriteLine($"[ExternalFileWatcherService] .iam changed, reloading: {path}");

        _ = ReloadMapAsync(path);
    }

    private async Task ReloadMapAsync(string path)
    {
        try
        {
            await ReadOnlyMapDataService.Instance.LoadAsync(path);
            Debug.WriteLine("[ExternalFileWatcherService] .iam reload complete");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExternalFileWatcherService] .iam reload failed: {ex.Message}");
        }
        finally
        {
            _reloadingMap = false;
        }
    }

    private static DateTime GetWriteTime(string? path)
    {
        if (path == null) return DateTime.MinValue;
        try { return File.GetLastWriteTimeUtc(path); }
        catch { return DateTime.MinValue; }
    }
}
