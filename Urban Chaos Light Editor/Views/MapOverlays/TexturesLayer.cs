// ============================================================
// LightEditor/Views/MapOverlays/TexturesLayer.cs
// ============================================================
using System;
using UrbanChaosLightEditor.Services;
using UrbanChaosEditor.Shared.Views.MapOverlays;

namespace UrbanChaosLightEditor.Views.MapOverlays
{
    /// <summary>
    /// Textures layer for Light Editor - Direct rendering (read-only).
    /// </summary>
    public sealed class TexturesLayer : SharedTexturesLayer
    {
        public TexturesLayer()
        {
            // Light Editor uses direct rendering
            RenderMode = TextureRenderMode.Direct;

            // Set up data provider
            SetTextureProvider(new LightEditorTextureProvider());
        }
    }

    /// <summary>
    /// Adapter to connect ReadOnlyMapDataService to ITextureDataProvider.
    /// </summary>
    internal class LightEditorTextureProvider : ITextureDataProvider
    {
        private readonly ReadOnlyTexturesAccessor _tex;

        public LightEditorTextureProvider()
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