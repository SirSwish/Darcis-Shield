// ============================================================
// LightEditor/Views/MapOverlays/PrimGraphicsLayer.cs
// ============================================================
using System;
using System.Collections.Generic;
using UrbanChaosLightEditor.Services;
using UrbanChaosEditor.Shared.Views.MapOverlays;
// CRITICAL: Use alias to disambiguate from local UrbanChaosLightEditor.Services.PrimDisplayInfo
using SharedPrimDisplayInfo = UrbanChaosEditor.Shared.Views.MapOverlays.PrimDisplayInfo;

namespace UrbanChaosLightEditor.Views.MapOverlays
{
    /// <summary>
    /// Prim graphics layer for Light Editor - Read-only display.
    /// </summary>
    public sealed class PrimGraphicsLayer : SharedPrimGraphicsLayer
    {
        public PrimGraphicsLayer()
        {
            // Use shared resources
            ResourceAssemblyName = "UrbanChaosEditor.Shared";

            // Set up read-only data provider
            SetDataProvider(new LightEditorPrimProvider());
        }
    }

    /// <summary>
    /// Adapter to connect ReadOnlyMapDataService to IPrimDataProvider.
    /// </summary>
    internal class LightEditorPrimProvider : IPrimDataProvider
    {
        public bool IsLoaded => ReadOnlyMapDataService.Instance.IsLoaded;

        // IMPORTANT: Return type MUST be List<SharedPrimDisplayInfo> (the shared type)
        // NOT the local UrbanChaosLightEditor.Services.PrimDisplayInfo
        public List<SharedPrimDisplayInfo> ReadAllPrims()
        {
            var accessor = new ReadOnlyObjectsAccessor(ReadOnlyMapDataService.Instance);
            // ReadOnlyObjectsAccessor.ReadAllPrims() returns local PrimDisplayInfo
            // We must convert to the shared type
            var localPrims = accessor.ReadAllPrims();
            var result = new List<SharedPrimDisplayInfo>();
            foreach (var p in localPrims)
            {
                result.Add(new SharedPrimDisplayInfo
                {
                    PixelX = p.PixelX,
                    PixelZ = p.PixelZ,
                    Y = p.Y,
                    PrimNumber = p.PrimNumber,
                    Yaw = p.Yaw,
                    Index = p.Index
                });
            }
            return result;
        }

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