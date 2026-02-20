// ============================================================
// MissionEditor/Views/MapOverlays/PrimGraphicsLayer.cs
// ============================================================
using System;
using System.Collections.Generic;
using UrbanChaosMissionEditor.Services;
using UrbanChaosEditor.Shared.Views.MapOverlays;
// Use alias to disambiguate from any local PrimDisplayInfo
using SharedPrimDisplayInfo = UrbanChaosEditor.Shared.Views.MapOverlays.PrimDisplayInfo;

namespace UrbanChaosMissionEditor.Views.MapOverlays
{
    /// <summary>
    /// Prim graphics layer for Mission Editor - Read-only display.
    /// </summary>
    public sealed class PrimGraphicsLayer : SharedPrimGraphicsLayer
    {
        public PrimGraphicsLayer()
        {
            // Use shared resources (PrimGraphics images should be in Shared project)
            ResourceAssemblyName = "UrbanChaosEditor.Shared";

            // Set up read-only data provider
            SetDataProvider(new MissionEditorPrimProvider());
        }
    }

    /// <summary>
    /// Adapter to connect ReadOnlyMapDataService to IPrimDataProvider.
    /// </summary>
    internal class MissionEditorPrimProvider : IPrimDataProvider
    {
        public bool IsLoaded => ReadOnlyMapDataService.Instance.IsLoaded;

        public List<SharedPrimDisplayInfo> ReadAllPrims()
        {
            var accessor = new ReadOnlyObjectsAccessor(ReadOnlyMapDataService.Instance);
            // ReadOnlyObjectsAccessor returns its own PrimDisplayInfo, convert to shared type
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