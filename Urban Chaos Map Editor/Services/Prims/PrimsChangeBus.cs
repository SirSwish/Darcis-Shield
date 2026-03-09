namespace UrbanChaosMapEditor.Services.Prims
{
    public sealed class PrimsChangeBus
    {
        private static readonly Lazy<PrimsChangeBus> _lazy = new(() => new PrimsChangeBus());
        public static PrimsChangeBus Instance => _lazy.Value;

        private PrimsChangeBus() { }

        public event EventHandler? Changed;
        public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
