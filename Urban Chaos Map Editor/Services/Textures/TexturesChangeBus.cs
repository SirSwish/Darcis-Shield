// /Services/ChangeBus/TexturesChangeBus.cs
namespace UrbanChaosMapEditor.Services.Textures
{
    public sealed class TexturesChangeBus
    {
        private static readonly Lazy<TexturesChangeBus> _lazy = new(() => new TexturesChangeBus());
        public static TexturesChangeBus Instance => _lazy.Value;
        private TexturesChangeBus() { }

        public event EventHandler? Changed;
        public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
