// /Services/Accessors/StyleAccessor.cs

using UrbanChaosMapEditor.Models.Styles;
using UrbanChaosMapEditor.Services.Styles;

namespace UrbanChaosMapEditor.Services.Styles
{
    public sealed class StylesAccessor
    {
        private readonly StyleDataService _svc;
        public StylesAccessor(StyleDataService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _svc.StylesLoaded += (_, __) => StylesBytesReset?.Invoke(this, EventArgs.Empty);
            _svc.StylesCleared += (_, __) => StylesBytesReset?.Invoke(this, EventArgs.Empty);
            _svc.StylesBytesReset += (_, __) => StylesBytesReset?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? StylesBytesReset;

        public bool TryGetStyle(int styleId0Based, out TextureStyle style)
        {
            style = null!;
            var tma = _svc.TmaSnapshot;
            if (tma?.TextureStyles is not { } list) return false;
            if ((uint)styleId0Based >= (uint)list.Count) return false;
            style = list[styleId0Based];
            return true;
        }

        public string Describe(int styleId0Based, int maxEntries = 5, bool showFlags = true)
        {
            if (!TryGetStyle(styleId0Based, out var s)) return $"style {styleId0Based} (missing)";
            var name = string.IsNullOrWhiteSpace(s.Name) ? $"#{styleId0Based}" : s.Name;
            var entries = s.Entries ?? new List<TextureEntry>();
            int n = Math.Min(entries.Count, maxEntries <= 0 ? entries.Count : maxEntries);

            var parts = new List<string>(n);
            for (int i = 0; i < n; i++)
            {
                var e = entries[i];
                parts.Add($"p{e.Page}@({e.Tx},{e.Ty}){(e.Flip != 0 ? $", flip={e.Flip}" : "")}");
            }
            if (n < entries.Count) parts.Add("…");

            var flags = (showFlags && s.Flags is { Count: > 0 })
                        ? $"  flags={string.Join('|', s.Flags)}"
                        : "";

            return $"{name}  [{string.Join(" | ", parts)}]{flags}";
        }
    }
}
