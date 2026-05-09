using System.IO;
using System.Text.Json;

namespace UrbanChaosMissionEditor.Services;

/// <summary>
/// Persists per-user editor settings (last-used directories, etc.)
/// to %AppData%\UrbanChaosMissionEditor\settings.json.
/// </summary>
public sealed class EditorSettingsService
{
    private static readonly Lazy<EditorSettingsService> _lazy = new(() => new EditorSettingsService());
    public static EditorSettingsService Instance => _lazy.Value;

    private readonly object _lock = new();
    private SettingsData _data = new();
    private bool _loaded;

    private EditorSettingsService() { }

    private static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UrbanChaosMissionEditor");

    private static string StorePath => Path.Combine(AppDataDir, "settings.json");

    public string? LastUcmDirectory
    {
        get { EnsureLoaded(); return _data.LastUcmDirectory; }
        set { EnsureLoaded(); _data.LastUcmDirectory = value; Save(); }
    }

    public string? LastMapDirectory
    {
        get { EnsureLoaded(); return _data.LastMapDirectory; }
        set { EnsureLoaded(); _data.LastMapDirectory = value; Save(); }
    }

    public string? LastLightsDirectory
    {
        get { EnsureLoaded(); return _data.LastLightsDirectory; }
        set { EnsureLoaded(); _data.LastLightsDirectory = value; Save(); }
    }

    public string? LastDebugSearchDirectory
    {
        get { EnsureLoaded(); return _data.LastDebugSearchDirectory; }
        set { EnsureLoaded(); _data.LastDebugSearchDirectory = value; Save(); }
    }

    private void EnsureLoaded()
    {
        lock (_lock)
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                if (!File.Exists(StorePath)) return;
                var json = File.ReadAllText(StorePath);
                _data = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
            catch
            {
                _data = new SettingsData();
            }
        }
    }

    private void Save()
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StorePath, json);
            }
            catch
            {
                // non-fatal
            }
        }
    }

    private sealed class SettingsData
    {
        public string? LastUcmDirectory { get; set; }
        public string? LastMapDirectory { get; set; }
        public string? LastLightsDirectory { get; set; }
        public string? LastDebugSearchDirectory { get; set; }
    }
}
