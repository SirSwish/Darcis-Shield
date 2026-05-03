using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.Views.Buildings.MapOverlays;
using UrbanChaosMapEditor.Views.Core;
using UrbanChaosMapEditor.Views.Core.MapOverlays;
using UrbanChaosMapEditor.Views.Heights.MapOverlays;
using UrbanChaosMapEditor.Views.Prims.MapOverlays;
using UrbanChaosMapEditor.Views.Roofs.MapOverlays;
using UrbanChaosMapEditor.Views.Textures.MapOverlays;

namespace UrbanChaosMapEditor.Services.Export
{
    public sealed class ExportLayerSelection
    {
        public bool Textures           { get; set; }
        public bool RoofTextureOverlay { get; set; }
        public bool GridLines          { get; set; }
        public bool MapWho             { get; set; }
        public bool Heights            { get; set; }
        public bool PapFlags           { get; set; }
        public bool PrimGraphics       { get; set; }
        public bool Prims              { get; set; }
        public bool Buildings          { get; set; }
        public bool Walkables          { get; set; }
        public bool Roofs              { get; set; }
        public bool RoofAltitudes      { get; set; }

        /// <summary>Output image edge length in pixels (8192, 4096, or 2048).</summary>
        public int OutputSize { get; set; } = MapImageExporter.NativeSize;

        /// <summary>
        /// Drop the alpha channel and composite onto a solid white background.
        /// Significantly smaller PNG with no visual change when the map fills
        /// the whole surface.
        /// </summary>
        public bool DropAlpha { get; set; } = true;
    }

    /// <summary>
    /// Renders the live MapView Surface to an 8192×8192 PNG by temporarily
    /// toggling layer visibility based on the user's selection. The Surface
    /// has fixed Width/Height of 8192, so RenderTargetBitmap.Render captures
    /// the full map regardless of current zoom/scroll.
    /// </summary>
    public static class MapImageExporter
    {
        /// <summary>Native surface edge length — the layers render at this size.</summary>
        public const int NativeSize = 8192;

        public static async Task ExportAsync(MapView mapView, ExportLayerSelection selection, string outputPath)
        {
            ArgumentNullException.ThrowIfNull(mapView);
            ArgumentNullException.ThrowIfNull(selection);
            ArgumentNullException.ThrowIfNull(outputPath);

            int targetSize = selection.OutputSize > 0 ? selection.OutputSize : NativeSize;

            var surface = mapView.ExportSurface;
            var vm = mapView.DataContext as MapViewModel;

            var savedVisibility = SnapshotChildVisibility(surface);
            var savedBackground = surface.Background;
            double? savedZoom = vm?.Zoom;

            try
            {
                ApplySelection(surface, selection);

                // Composite onto white when alpha is being dropped, so any
                // transparent regions don't end up black after channel drop.
                if (selection.DropAlpha)
                    surface.Background = Brushes.White;

                // Several layers (RoofAltitudesLayer, HeightsLayer, CameraMarkerLayer)
                // cull rendering to the ScrollViewer's visible rect. At zoom 0.10 the
                // transformed viewport covers the full 8192×8192 surface, so they
                // render every cell. Restored in finally.
                if (vm is not null)
                    vm.Zoom = 0.10;

                surface.UpdateLayout();

                // Yield at Background priority to let any change-bus-driven cache
                // rebuilds and the layout pass finish before we snapshot.
                await surface.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                await surface.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

                // Always render at native size — RenderTargetBitmap renders elements
                // at their layout coordinates, so a smaller RTB would just crop.
                var nativeRtb = new RenderTargetBitmap(NativeSize, NativeSize, 96, 96, PixelFormats.Pbgra32);
                nativeRtb.Render(surface);
                nativeRtb.Freeze();

                BitmapSource final = nativeRtb;

                if (targetSize != NativeSize)
                {
                    double scale = (double)targetSize / NativeSize;
                    final = new TransformedBitmap(final, new ScaleTransform(scale, scale));
                    final.Freeze();
                }

                if (selection.DropAlpha)
                {
                    final = new FormatConvertedBitmap(final, PixelFormats.Bgr24, null, 0);
                    final.Freeze();
                }

                var encoder = new PngBitmapEncoder { Interlace = PngInterlaceOption.Off };
                encoder.Frames.Add(BitmapFrame.Create(final));

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await using var fs = File.Create(outputPath);
                encoder.Save(fs);
            }
            finally
            {
                if (vm is not null && savedZoom.HasValue)
                    vm.Zoom = savedZoom.Value;
                surface.Background = savedBackground;
                RestoreChildVisibility(surface, savedVisibility);
                surface.UpdateLayout();
            }
        }

        public static string BuildOutputFileName(string mapPath, string outputDirectory)
        {
            var name = Path.GetFileNameWithoutExtension(mapPath);
            return Path.Combine(outputDirectory, $"{name}-IMG.png");
        }

        private static Dictionary<UIElement, Visibility> SnapshotChildVisibility(Grid surface)
        {
            var map = new Dictionary<UIElement, Visibility>(surface.Children.Count);
            foreach (UIElement child in surface.Children)
                map[child] = child.Visibility;
            return map;
        }

        private static void RestoreChildVisibility(Grid surface, Dictionary<UIElement, Visibility> saved)
        {
            foreach (UIElement child in surface.Children)
                if (saved.TryGetValue(child, out var v))
                    child.Visibility = v;
        }

        private static void ApplySelection(Grid surface, ExportLayerSelection sel)
        {
            foreach (UIElement child in surface.Children)
            {
                Visibility? target = child switch
                {
                    TexturesLayer            => Vis(sel.Textures),
                    RoofTextureOverlayLayer  => Vis(sel.RoofTextureOverlay),
                    GridLinesLayer           => Vis(sel.GridLines),
                    MapWhoLayer              => Vis(sel.MapWho),
                    HeightsLayer             => Vis(sel.Heights),
                    CellFlagsLayer           => Vis(sel.PapFlags),
                    PrimGraphicsLayer        => Vis(sel.PrimGraphics),
                    PrimsLayer               => Vis(sel.Prims),
                    BuildingLayer            => Vis(sel.Buildings),
                    WalkablesLayer           => Vis(sel.Walkables),
                    RoofsLayer               => Vis(sel.Roofs),
                    RoofAltitudesLayer       => Vis(sel.RoofAltitudes),

                    // Editor-only overlays — always hidden in exports.
                    AltitudeHoverLayer        => Visibility.Collapsed,
                    GhostTexturePreviewLayer  => Visibility.Collapsed,
                    FacetRedrawPreviewLayer   => Visibility.Collapsed,
                    FacetHandlesLayer         => Visibility.Collapsed,
                    MoveBuildingGhostLayer    => Visibility.Collapsed,
                    CameraMarkerLayer         => Visibility.Collapsed,

                    _ => null,
                };

                if (target.HasValue)
                    child.Visibility = target.Value;
            }
        }

        private static Visibility Vis(bool on) => on ? Visibility.Visible : Visibility.Collapsed;
    }
}
