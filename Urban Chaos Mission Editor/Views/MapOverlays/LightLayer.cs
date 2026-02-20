// ============================================================
// MissionEditor/Views/MapOverlays/LightLayer.cs
// ============================================================
using System;
using System.Collections.Generic;
using UrbanChaosMissionEditor.Services;
using UrbanChaosEditor.Shared.Views.MapOverlays;
// Use alias to disambiguate from any local LightDisplayInfo
using SharedLightDisplayInfo = UrbanChaosEditor.Shared.Views.MapOverlays.LightDisplayInfo;

namespace UrbanChaosMissionEditor.Views.MapOverlays
{
    /// <summary>
    /// Light layer for Mission Editor - Read-only display.
    /// </summary>
    public sealed class LightLayer : SharedLightLayer
    {
        public LightLayer()
        {
            // Set up read-only data provider
            SetDataProvider(new MissionEditorLightProvider());
        }
    }

    /// <summary>
    /// Adapter to connect ReadOnlyLightsDataService to ILightDataProvider.
    /// </summary>
    internal class MissionEditorLightProvider : ILightDataProvider
    {
        public bool IsLoaded => ReadOnlyLightsDataService.Instance.IsLoaded;

        public List<SharedLightDisplayInfo> ReadAllLights()
        {
            var entries = ReadOnlyLightsDataService.Instance.ReadAllEntries();
            var result = new List<SharedLightDisplayInfo>();

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                result.Add(new SharedLightDisplayInfo
                {
                    Index = i,
                    Used = e.Used == 1,
                    PixelX = ReadOnlyLightsDataService.WorldXToUiX(e.X),
                    PixelZ = ReadOnlyLightsDataService.WorldZToUiZ(e.Z),
                    Range = e.Range,
                    Red = e.Red,
                    Green = e.Green,
                    Blue = e.Blue
                });
            }

            return result;
        }

        public void SubscribeLightsLoaded(Action callback)
        {
            ReadOnlyLightsDataService.Instance.LightsLoaded += (sender, args) => callback();
        }

        public void SubscribeLightsCleared(Action callback)
        {
            ReadOnlyLightsDataService.Instance.LightsCleared += (sender, args) => callback();
        }
    }
}