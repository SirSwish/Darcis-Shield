using UrbanChaosEditor.Shared.Views.MapOverlays;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;

namespace UrbanChaosMapEditor.Views.Roofs.MapOverlays
{
    public sealed class RoofAltitudesLayer : SharedRoofAltitudesLayer
    {
        public RoofAltitudesLayer()
        {
            SetDataProvider(new MapEditorRoofDataProvider());
            ShowRawHeights = HeightDisplaySettings.ShowRawHeights;

            HeightDisplaySettings.DisplayModeChanged += (_, _) =>
            {
                ShowRawHeights = HeightDisplaySettings.ShowRawHeights;
                InvalidateVisual();
            };

            RoofsChangeBus.Instance.Changed += (_, _) => MarkCacheDirty();
            BuildingsChangeBus.Instance.Changed += (_, _) => MarkCacheDirty();
            AltitudeChangeBus.Instance.TileChanged += (_, _) => MarkCacheDirty();
            AltitudeChangeBus.Instance.RegionChanged += (_, _, _, _) => MarkCacheDirty();
            AltitudeChangeBus.Instance.AllChanged += MarkCacheDirty;
        }
    }

    internal sealed class MapEditorRoofDataProvider : IRoofDataProvider
    {
        public bool IsLoaded => MapDataService.Instance.IsLoaded;

        public byte[]? GetBytesCopy()
            => MapDataService.Instance.IsLoaded ? MapDataService.Instance.GetBytesCopy() : null;

        public void SubscribeMapLoaded(Action callback)
            => MapDataService.Instance.MapLoaded += (_, _) => callback();

        public void SubscribeMapCleared(Action callback)
            => MapDataService.Instance.MapCleared += (_, _) => callback();

        public void ComputeAndCacheBuildingRegion()
            => MapDataService.Instance.ComputeAndCacheBuildingRegion();

        public bool TryGetBuildingRegion(out int start, out int length)
            => MapDataService.Instance.TryGetBuildingRegion(out start, out length);
    }
}
