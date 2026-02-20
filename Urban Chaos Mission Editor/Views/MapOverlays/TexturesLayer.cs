// ============================================================
// MissionEditor/Views/MapOverlays/TexturesLayer.cs
// ============================================================
using System;
using UrbanChaosMissionEditor.Services;
using UrbanChaosEditor.Shared.Views.MapOverlays;

namespace UrbanChaosMissionEditor.Views.MapOverlays
{
    /// <summary>
    /// Textures layer for Mission Editor - Composite rendering for performance.
    /// </summary>
    public sealed class TexturesLayer : SharedTexturesLayer
    {
        public TexturesLayer()
        {
            // Mission Editor uses composite rendering for better read-only performance
            RenderMode = TextureRenderMode.Composite;

            // Set up data provider
            SetTextureProvider(new MissionEditorTextureProvider());
        }
    }

    /// <summary>
    /// Adapter to connect ReadOnlyMapDataService to ITextureDataProvider.
    /// </summary>
    internal class MissionEditorTextureProvider : ITextureDataProvider
    {
        private readonly ReadOnlyTexturesAccessor _tex;

        public MissionEditorTextureProvider()
        {
            _tex = new ReadOnlyTexturesAccessor(ReadOnlyMapDataService.Instance);
        }

        public bool IsLoaded => ReadOnlyMapDataService.Instance.IsLoaded;
        public byte[]? GetBytesCopy() => ReadOnlyMapDataService.Instance.GetBytesCopy();

        public (string relativeKey, int rotationDeg) GetTileTextureKeyAndRotation(int tx, int ty)
            => _tex.GetTileTextureKeyAndRotation(tx, ty);

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