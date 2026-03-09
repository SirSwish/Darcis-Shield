using System.IO;
using System.Text.Json;

namespace UrbanChaosMapEditor.Services.Core
{
    public sealed class RecentFilesService
    {
        private static readonly Lazy<RecentFilesService> _lazy = new(() => new RecentFilesService());
        public static RecentFilesService Instance => _lazy.Value;

        private RecentFilesService() { }

        public int MaxItems { get; set; } = 10;

        public IReadOnlyList<string> Items => _items.AsReadOnly();
        private readonly List<string> _items = new();

        private string AppDataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UrbanChaosMapEditor");

        private string StorePath => Path.Combine(AppDataDir, "recent.json");

        public void Load()
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                if (!File.Exists(StorePath)) return;
                var json = File.ReadAllText(StorePath);
                var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                _items.Clear();
                foreach (var p in list)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                        _items.Add(p);
                }
                Trim();
            }
            catch
            {
                // ignore corrupt files; start fresh
                _items.Clear();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                var json = JsonSerializer.Serialize(_items);
                File.WriteAllText(StorePath, json);
            }
            catch
            {
                // non-fatal; ignore save errors
            }
        }

        public void Add(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            path = Path.GetFullPath(path);

            _items.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            _items.Insert(0, path);
            Trim();
            Save();
            RecentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            _items.Clear();
            Save();
            RecentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Trim()
        {
            if (_items.Count > MaxItems)
                _items.RemoveRange(MaxItems, _items.Count - MaxItems);
        }

        public event EventHandler? RecentChanged;
    }
}
