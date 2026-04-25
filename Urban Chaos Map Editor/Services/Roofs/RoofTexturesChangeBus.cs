// Services/Roofs/RoofTexturesChangeBus.cs
namespace UrbanChaosMapEditor.Services.Roofs
{
    /// <summary>
    /// Notification bus fired when any roof texture cell in the .MAP layer is modified.
    /// The RoofTextureOverlayLayer subscribes to this to repaint.
    /// </summary>
    public sealed class RoofTexturesChangeBus
    {
        private static readonly Lazy<RoofTexturesChangeBus> _lazy = new(() => new RoofTexturesChangeBus());
        public static RoofTexturesChangeBus Instance => _lazy.Value;
        private RoofTexturesChangeBus() { }

        public event EventHandler? Changed;
        public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
