// /Services/ChangeBus/HeightsChangeBus.cs
namespace UrbanChaosMapEditor.Services.Heights
{
    /// Central bus for height edit notifications.
    public sealed class HeightsChangeBus
    {
        private static readonly Lazy<HeightsChangeBus> _lazy = new(() => new HeightsChangeBus());
        public static HeightsChangeBus Instance => _lazy.Value;
        private HeightsChangeBus() { }

        // Tile-local change
        public event EventHandler<TileChangedEventArgs>? TileChanged;

        // Region/bulk change (inclusive tile coordinates)
        public event EventHandler<RegionChangedEventArgs>? RegionChanged;

        public void NotifyTile(int tx, int ty) =>
            TileChanged?.Invoke(this, new TileChangedEventArgs(tx, ty));

        public void NotifyRegion(int x0, int y0, int x1, int y1) =>
            RegionChanged?.Invoke(this, new RegionChangedEventArgs(x0, y0, x1, y1));
    }

    public sealed class TileChangedEventArgs : EventArgs
    {
        public int Tx { get; }
        public int Ty { get; }
        public TileChangedEventArgs(int tx, int ty) { Tx = tx; Ty = ty; }
    }

    public sealed class RegionChangedEventArgs : EventArgs
    {
        public int X0 { get; }
        public int Y0 { get; }
        public int X1 { get; }
        public int Y1 { get; }
        public RegionChangedEventArgs(int x0, int y0, int x1, int y1)
        { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
    }
}
