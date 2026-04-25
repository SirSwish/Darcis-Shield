// Services/PrimDirectoryService.cs
// Singleton that manages the selected PRM directory and the prim texture
// directory, persisting both across sessions.

using System.IO;
using System.Text.Json;

namespace UrbanChaosPrimEditor.Services
{
    public sealed class PrimDirectoryService
    {
        private static readonly Lazy<PrimDirectoryService> _lazy = new(() => new PrimDirectoryService());
        public static PrimDirectoryService Instance => _lazy.Value;

        private PrimDirectoryService() { }

        // ── Persistence ──────────────────────────────────────────────────────

        private string AppDataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "UrbanChaosPrimEditor");

        private string StorePath => Path.Combine(AppDataDir, "settings.json");

        private sealed class PersistedSettings
        {
            public string? PrmDirectory     { get; set; }
            public string? TextureDirectory { get; set; }
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(StorePath)) return;
                var json     = File.ReadAllText(StorePath);
                var settings = JsonSerializer.Deserialize<PersistedSettings>(json);
                if (settings?.PrmDirectory is { } dir && Directory.Exists(dir))
                    _prmDirectory = dir;
                if (settings?.TextureDirectory is { } tex && Directory.Exists(tex))
                    _textureDirectory = tex;
            }
            catch { /* start fresh on corrupt settings */ }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                var json = JsonSerializer.Serialize(new PersistedSettings
                {
                    PrmDirectory     = _prmDirectory,
                    TextureDirectory = _textureDirectory
                });
                File.WriteAllText(StorePath, json);
            }
            catch { }
        }

        // ── PRM directory ─────────────────────────────────────────────────────

        private string? _prmDirectory;

        /// <summary>Currently selected directory scanned for .prm files.</summary>
        public string? PrmDirectory
        {
            get => _prmDirectory;
            set
            {
                if (_prmDirectory == value) return;
                _prmDirectory = value;
                Save();
                DirectoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? DirectoryChanged;

        // ── Prim texture directory ────────────────────────────────────────────

        private string? _textureDirectory;

        /// <summary>
        /// Directory containing prim texture files (Tex###hi.tga).
        /// Set explicitly by the user via File → Set Texture Directory…
        /// </summary>
        public string? TextureDirectory
        {
            get => _textureDirectory;
            set
            {
                if (_textureDirectory == value) return;
                _textureDirectory = value;
                Save();
                TextureDirectoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? TextureDirectoryChanged;

        // ── File scanning ─────────────────────────────────────────────────────

        public IReadOnlyList<string> ScanForPrmFiles()
        {
            if (_prmDirectory is null || !Directory.Exists(_prmDirectory))
                return [];

            try
            {
                return Directory.GetFiles(_prmDirectory, "*.prm", SearchOption.TopDirectoryOnly)
                                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                .ToList();
            }
            catch
            {
                return [];
            }
        }
    }
}
