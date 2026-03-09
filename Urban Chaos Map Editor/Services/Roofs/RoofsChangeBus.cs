// Services/Roofs/RoofsChangeBus.cs
// Central bus for walkable, RoofFace4, and cell altitude change notifications.
// Layers (RoofsLayer, WalkablesLayer) and the RoofsTab subscribe to refresh on changes.

namespace UrbanChaosMapEditor.Services.Roofs
{
    public sealed class RoofsChangeBus
    {
        private static readonly Lazy<RoofsChangeBus> _lazy = new(() => new RoofsChangeBus());
        public static RoofsChangeBus Instance => _lazy.Value;
        private RoofsChangeBus() { }

        /// <summary>
        /// Fired when any walkable, RF4, or cell altitude data changes.
        /// Subscribers should invalidate their cached roof data and repaint.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Fired when a specific walkable is added, removed, or modified.
        /// </summary>
        public event EventHandler<WalkableChangedEventArgs>? WalkableChanged;

        /// <summary>
        /// Fired when a specific RoofFace4 is added, removed, or modified.
        /// </summary>
        public event EventHandler<RoofFace4ChangedEventArgs>? RoofFace4Changed;

        /// <summary>
        /// Fired when cell altitudes are modified (single tile or region).
        /// </summary>
        public event EventHandler<CellAltitudeChangedEventArgs>? CellAltitudeChanged;

        public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

        public void NotifyWalkableChanged(int walkableId1, RoofChangeType changeType) =>
            WalkableChanged?.Invoke(this, new WalkableChangedEventArgs(walkableId1, changeType));

        public void NotifyRoofFace4Changed(int faceId, RoofChangeType changeType) =>
            RoofFace4Changed?.Invoke(this, new RoofFace4ChangedEventArgs(faceId, changeType));

        public void NotifyCellAltitudeChanged(int tx, int ty) =>
            CellAltitudeChanged?.Invoke(this, new CellAltitudeChangedEventArgs(tx, ty, tx, ty));

        public void NotifyCellAltitudeRegionChanged(int minTx, int minTy, int maxTx, int maxTy) =>
            CellAltitudeChanged?.Invoke(this, new CellAltitudeChangedEventArgs(minTx, minTy, maxTx, maxTy));
    }

    public enum RoofChangeType
    {
        Added,
        Removed,
        Modified
    }

    public sealed class WalkableChangedEventArgs : EventArgs
    {
        public int WalkableId1 { get; }
        public RoofChangeType ChangeType { get; }
        public WalkableChangedEventArgs(int walkableId1, RoofChangeType changeType)
        {
            WalkableId1 = walkableId1;
            ChangeType = changeType;
        }
    }

    public sealed class RoofFace4ChangedEventArgs : EventArgs
    {
        public int FaceId { get; }
        public RoofChangeType ChangeType { get; }
        public RoofFace4ChangedEventArgs(int faceId, RoofChangeType changeType)
        {
            FaceId = faceId;
            ChangeType = changeType;
        }
    }

    public sealed class CellAltitudeChangedEventArgs : EventArgs
    {
        public int MinTx { get; }
        public int MinTy { get; }
        public int MaxTx { get; }
        public int MaxTy { get; }
        public CellAltitudeChangedEventArgs(int minTx, int minTy, int maxTx, int maxTy)
        {
            MinTx = minTx; MinTy = minTy;
            MaxTx = maxTx; MaxTy = maxTy;
        }
    }
}