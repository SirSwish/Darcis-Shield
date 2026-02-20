// ============================================================
// MapEditor/Views/MapOverlays/TexturesLayer.cs
// ============================================================
using System;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;
using UrbanChaosEditor.Shared.Views.MapOverlays;

namespace UrbanChaosMapEditor.Views.MapOverlays
{
    /// <summary>
    /// Textures layer for Map Editor - Direct rendering for editing.
    /// </summary>
    public sealed class TexturesLayer : SharedTexturesLayer
    {
        public TexturesLayer()
        {
            // Map Editor uses direct rendering for fast edits
            RenderMode = TextureRenderMode.Direct;

            // Set up data provider
            SetTextureProvider(new MapEditorTextureProvider());

            // Subscribe to texture change events
            TexturesChangeBus.Instance.Changed += (_, __) => RefreshOnUiThread();
        }
    }

    /// <summary>
    /// Adapter to connect MapDataService to ITextureDataProvider.
    /// </summary>
    internal class MapEditorTextureProvider : ITextureDataProvider
    {
        private readonly TexturesAccessor _tex;

        public MapEditorTextureProvider()
        {
            _tex = new TexturesAccessor(MapDataService.Instance);
        }

        public bool IsLoaded => MapDataService.Instance.IsLoaded;
        public byte[]? GetBytesCopy() => MapDataService.Instance.MapBytes;

        public (string relativeKey, int rotationDeg) GetTileTextureKeyAndRotation(int tx, int ty)
            => _tex.GetTileTextureKeyAndRotation(tx, ty);

        public void SubscribeMapLoaded(Action callback)
        {
            // Map Editor uses EventHandler<MapLoadedEventArgs>
            MapDataService.Instance.MapLoaded += (sender, args) => callback();
        }

        public void SubscribeMapCleared(Action callback)
        {
            // Map Editor uses EventHandler<MapLoadedEventArgs> for cleared too
            MapDataService.Instance.MapCleared += (sender, args) => callback();
        }
    }
}