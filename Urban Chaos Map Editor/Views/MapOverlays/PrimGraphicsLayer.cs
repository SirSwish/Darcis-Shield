// ============================================================
// MapEditor/Views/MapOverlays/PrimGraphicsLayer.cs
// ============================================================
using System;
using System.Collections.Generic;
using UrbanChaosEditor.Shared.Views.MapOverlays;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;
using SharedPrimDisplayInfo = UrbanChaosEditor.Shared.Views.MapOverlays.PrimDisplayInfo;

namespace UrbanChaosMapEditor.Views.MapOverlays
{
    /// <summary>
    /// Prim graphics layer for Map Editor - Read-only display.
    /// </summary>
    public sealed class PrimGraphicsLayer : SharedPrimGraphicsLayer
    {
        public PrimGraphicsLayer()
        {
            // Use Map Editor's resources
            ResourceAssemblyName = "UrbanChaosMapEditor";

            // Set up data provider
            SetDataProvider(new MapEditorPrimProvider());

            // Subscribe to prim changes
            PrimsChangeBus.Instance.Changed += (_, __) =>
            {
                RefreshPrims();
                RefreshOnUiThread();
            };
        }
    }

    /// <summary>
    /// Adapter to connect MapDataService to IPrimDataProvider.
    /// </summary>
    internal class MapEditorPrimProvider : IPrimDataProvider
    {
        public bool IsLoaded => MapDataService.Instance.IsLoaded;

        public List<SharedPrimDisplayInfo> ReadAllPrims()
        {
            var accessor = new ObjectsAccessor(MapDataService.Instance);
            // Use the Snapshot to access prims directly
            var snapshot = accessor.ReadSnapshot();
            var result = new List<SharedPrimDisplayInfo>();
            var prims = snapshot.Prims;
            for (int i = 0; i < prims.Length; i++)
            {
                var p = prims[i];
                result.Add(new SharedPrimDisplayInfo
                {
                    PixelX = p.X,
                    PixelZ = p.Z,
                    Y = p.Y,
                    PrimNumber = p.PrimNumber,
                    Yaw = p.Yaw,
                    Index = i
                });
            }
            return result;
        }

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