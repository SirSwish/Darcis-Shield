// ============================================================
// MapEditor/Views/MapOverlays/BuildingLayer.cs
// ============================================================
using System;
using UrbanChaosMapEditor.Services.DataServices;
using UrbanChaosEditor.Shared.Views.MapOverlays;

namespace UrbanChaosMapEditor.Views.MapOverlays
{
    /// <summary>
    /// Building layer for Map Editor - Read-only display.
    /// </summary>
    public sealed class BuildingLayer : SharedBuildingLayer
    {
        public BuildingLayer()
        {
            // Set up data provider
            SetDataProvider(new MapEditorBuildingProvider());
        }
    }

    /// <summary>
    /// Adapter to connect MapDataService to IBuildingDataProvider.
    /// </summary>
    internal class MapEditorBuildingProvider : IBuildingDataProvider
    {
        public bool IsLoaded => MapDataService.Instance.IsLoaded;
        public byte[]? GetBytesCopy() => MapDataService.Instance.MapBytes;

        public void ComputeAndCacheBuildingRegion()
            => MapDataService.Instance.ComputeAndCacheBuildingRegion();

        public bool TryGetBuildingRegion(out int start, out int length)
            => MapDataService.Instance.TryGetBuildingRegion(out start, out length);

        public void SubscribeMapLoaded(Action callback)
        {
            // Map Editor uses EventHandler<MapLoadedEventArgs>
            MapDataService.Instance.MapLoaded += (sender, args) => callback();
        }

        public void SubscribeMapCleared(Action callback)
        {
            MapDataService.Instance.MapCleared += (sender, args) => callback();
        }
    }
}
