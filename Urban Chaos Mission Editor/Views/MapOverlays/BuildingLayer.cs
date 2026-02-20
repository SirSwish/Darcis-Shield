// ============================================================
// MissionEditor/Views/MapOverlays/BuildingLayer.cs
// ============================================================
using System;
using UrbanChaosMissionEditor.Services;
using UrbanChaosEditor.Shared.Views.MapOverlays;

namespace UrbanChaosMissionEditor.Views.MapOverlays
{
    /// <summary>
    /// Building layer for Mission Editor - Read-only display.
    /// </summary>
    public sealed class BuildingLayer : SharedBuildingLayer
    {
        public BuildingLayer()
        {
            // Set up read-only data provider
            SetDataProvider(new MissionEditorBuildingProvider());
        }
    }

    /// <summary>
    /// Adapter to connect ReadOnlyMapDataService to IBuildingDataProvider.
    /// </summary>
    internal class MissionEditorBuildingProvider : IBuildingDataProvider
    {
        public bool IsLoaded => ReadOnlyMapDataService.Instance.IsLoaded;
        public byte[]? GetBytesCopy() => ReadOnlyMapDataService.Instance.GetBytesCopy();

        public void ComputeAndCacheBuildingRegion()
            => ReadOnlyMapDataService.Instance.ComputeAndCacheBuildingRegion();

        public bool TryGetBuildingRegion(out int start, out int length)
            => ReadOnlyMapDataService.Instance.TryGetBuildingRegion(out start, out length);

        public void SubscribeMapLoaded(Action callback)
        {
            ReadOnlyMapDataService.Instance.MapLoaded += (sender, args) => callback();
        }

        public void SubscribeMapCleared(Action callback)
        {
            ReadOnlyMapDataService.Instance.MapCleared += (sender, args) => callback();
        }
    }
}