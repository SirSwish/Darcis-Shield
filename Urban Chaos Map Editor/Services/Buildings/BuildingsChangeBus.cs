// /Services/ChangeBus/BuildingsChangeBus.cs
namespace UrbanChaosMapEditor.Services.Buildings
{
    /// <summary>
    /// Central bus for building/facet edit notifications.
    /// Layers (BuildingLayer, WalkablesLayer) subscribe to refresh when buildings/facets change.
    /// </summary>
    public sealed class BuildingsChangeBus
    {
        private static readonly Lazy<BuildingsChangeBus> _lazy = new(() => new BuildingsChangeBus());
        public static BuildingsChangeBus Instance => _lazy.Value;
        private BuildingsChangeBus() { }

        /// <summary>
        /// Fired when any building or facet data changes (coords, heights, flags, type, etc.)
        /// Subscribers should invalidate their cached building data and repaint.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Fired when a specific facet is modified. Includes the 1-based facet ID.
        /// </summary>
        public event EventHandler<FacetChangedEventArgs>? FacetChanged;

        /// <summary>
        /// Fired when a building is added, removed, or modified.
        /// </summary>
        public event EventHandler<BuildingChangedEventArgs>? BuildingChanged;

        /// <summary>Notify all subscribers that building/facet data has changed.</summary>
        public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

        /// <summary>Notify that a specific facet was modified.</summary>
        public void NotifyFacetChanged(int facetId1) =>
            FacetChanged?.Invoke(this, new FacetChangedEventArgs(facetId1));

        /// <summary>Notify that a building was added, removed, or modified.</summary>
        public void NotifyBuildingChanged(int buildingId1, BuildingChangeType changeType) =>
            BuildingChanged?.Invoke(this, new BuildingChangedEventArgs(buildingId1, changeType));
    }

    public sealed class FacetChangedEventArgs : EventArgs
    {
        /// <summary>1-based facet ID that was modified.</summary>
        public int FacetId1 { get; }
        public FacetChangedEventArgs(int facetId1) { FacetId1 = facetId1; }
    }

    public enum BuildingChangeType
    {
        Added,
        Removed,
        Modified
    }

    public sealed class BuildingChangedEventArgs : EventArgs
    {
        /// <summary>1-based building ID that was affected.</summary>
        public int BuildingId1 { get; }
        public BuildingChangeType ChangeType { get; }
        public BuildingChangedEventArgs(int buildingId1, BuildingChangeType changeType)
        {
            BuildingId1 = buildingId1;
            ChangeType = changeType;
        }
    }
}