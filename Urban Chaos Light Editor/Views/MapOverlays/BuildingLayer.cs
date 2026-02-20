// ============================================================
// LightEditor/Views/MapOverlays/BuildingLayer.cs
// ============================================================
using System;
using UrbanChaosLightEditor.Services;
using UrbanChaosEditor.Shared.Views.MapOverlays;

namespace UrbanChaosLightEditor.Views.MapOverlays
{
    /// <summary>
    /// Building layer for Light Editor - Read-only display.
    /// </summary>
    public sealed class BuildingLayer : SharedBuildingLayer
    {
        public BuildingLayer()
        {
            // Set up read-only data provider
            SetDataProvider(new LightEditorBuildingProvider());
        }
    }

    /// <summary>
    /// Adapter to connect ReadOnlyMapDataService to IBuildingDataProvider.
    /// </summary>
    internal class LightEditorBuildingProvider : IBuildingDataProvider
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