// /Services/DataServices/StyleDataService.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UrbanChaosMapEditor.Models.Styles;

namespace UrbanChaosMapEditor.Services.DataServices
{
    public sealed class StyleDataService
    {
        private static readonly Lazy<StyleDataService> _lazy = new(() => new StyleDataService());
        public static StyleDataService Instance => _lazy.Value;

        private readonly object _sync = new();
        private TMAFile? _tma;

        private StyleDataService() { }

        public bool IsLoaded => _tma is not null;

        /// <summary>
        /// For file loads this is the full path; for resource loads this is the
        /// descriptive label you pass in (e.g. pack URI or "World3 release").
        /// </summary>
        public string? CurrentPath { get; private set; }

        public TMAFile? TmaSnapshot => _tma;

        public event EventHandler? StylesLoaded;
        public event EventHandler? StylesCleared;
        public event EventHandler? StylesBytesReset;

        /// <summary>
        /// Map RAW style id -> zero-based index in TMA table.
        /// Row 0 is dummy. 0x000 and 0x001 both map to row 1. Otherwise idx = raw.
        /// </summary>
        public static int MapRawStyleIdToTmaIndex(int raw) => (raw <= 1) ? 1 : raw;

        private void SetParsedResult(TMAFile parsed, string sourceLabel)
        {
            lock (_sync)
            {
                _tma = parsed;
                CurrentPath = sourceLabel;
            }

            StylesLoaded?.Invoke(this, EventArgs.Empty);
            StylesBytesReset?.Invoke(this, EventArgs.Empty);

            Debug.WriteLine(
                $"[StyleDataService] Loaded styles from '{sourceLabel}', count={_tma.TextureStyles.Count}");
        }

        /// <summary>
        /// Load a TMA from a file on disk.
        /// </summary>
        public async Task LoadAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path required.", nameof(path));

            var full = Path.GetFullPath(path);

            var parsed = await Task.Run(() => TMAFile.ReadTMAFile(full)).ConfigureAwait(false);
            SetParsedResult(parsed, full);
        }

        /// <summary>
        /// Load a TMA from a stream (e.g. embedded pack resource).
        /// We currently only have TMAFile.ReadTMAFile(string), so the stream is
        /// copied to a temporary file which is then parsed and deleted.
        /// The sourceLabel is just for debugging / CurrentPath.
        /// </summary>
        public async Task LoadFromResourceStreamAsync(Stream stream, string sourceLabel)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            var tempPath = Path.Combine(Path.GetTempPath(), $"style_{Guid.NewGuid():N}.tma");

            try
            {
                // Copy resource bytes to temp file
                using (var fs = File.Create(tempPath))
                {
                    await stream.CopyToAsync(fs).ConfigureAwait(false);
                }

                var parsed = await Task.Run(() => TMAFile.ReadTMAFile(tempPath)).ConfigureAwait(false);
                SetParsedResult(parsed, sourceLabel);
            }
            finally
            {
                // Best-effort cleanup; non-fatal if this fails.
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch { }
            }
        }

        /// <summary>
        /// Fetch a TMA entry (Page/Tx/Ty/Flip) for a RAW style id + slot (0..4).
        /// Applies the alias rule above.
        /// </summary>
        public bool TryGetTmaEntry(int rawStyleId, int slot, out TextureEntry entry)
        {
            entry = default;
            var tma = _tma;
            if (tma == null || slot < 0)
                return false;

            int idx = MapRawStyleIdToTmaIndex(rawStyleId);
            if (idx < 0 || idx >= tma.TextureStyles.Count)
                return false;

            var style = tma.TextureStyles[idx];
            if (style.Entries == null || slot >= style.Entries.Count)
                return false;

            entry = style.Entries[slot];
            return true;
        }

        /// <summary>
        /// Nice label for UI using the same alias rule: "Style #N: Name" where N is
        /// 1-based human index (row 0 is dummy so idx==1 => "Style 1").
        /// </summary>
        public string GetStyleDisplayLabel(int rawStyleId)
        {
            var tma = _tma;
            if (tma == null)
                return $"Style raw={rawStyleId}";

            int idx = MapRawStyleIdToTmaIndex(rawStyleId);
            if (idx < 0 || idx >= tma.TextureStyles.Count)
                return $"Style raw={rawStyleId}";

            var s = tma.TextureStyles[idx];
            int human = idx;

            return string.IsNullOrWhiteSpace(s.Name)
                ? $"Style {human}"
                : $"Style {human}: {s.Name}";
        }

        public void Clear()
        {
            lock (_sync)
            {
                _tma = null;
                CurrentPath = null;
            }

            StylesCleared?.Invoke(this, EventArgs.Empty);
        }
    }
}
