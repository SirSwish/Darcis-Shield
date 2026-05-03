using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using UrbanChaosLightEditor.ViewModels;
using UrbanChaosLightEditor.Views;

namespace UrbanChaosLightEditor.Services.Export
{
    public sealed class LightExportLayerSelection
    {
        public bool Textures     { get; set; }
        public bool GridLines    { get; set; }
        public bool Buildings    { get; set; }
        public bool PrimGraphics { get; set; }
        public bool Lights       { get; set; }
        public bool LightRanges  { get; set; }

        /// <summary>Output image edge length in pixels (8192, 4096, or 2048).</summary>
        public int OutputSize { get; set; } = LightImageExporter.NativeSize;

        /// <summary>
        /// Drop the alpha channel and composite onto a solid white background.
        /// </summary>
        public bool DropAlpha { get; set; } = true;
    }

    /// <summary>
    /// Renders the live LightEditorMapView Surface to a PNG by temporarily
    /// toggling the MainWindowViewModel's Show* flags based on the user's
    /// selection. Mirrors the Map Editor's MapImageExporter.
    /// </summary>
    public static class LightImageExporter
    {
        public const int NativeSize = 8192;

        public static async Task ExportAsync(LightEditorMapView mapView, LightExportLayerSelection selection, string outputPath)
        {
            ArgumentNullException.ThrowIfNull(mapView);
            ArgumentNullException.ThrowIfNull(selection);
            ArgumentNullException.ThrowIfNull(outputPath);

            int targetSize = selection.OutputSize > 0 ? selection.OutputSize : NativeSize;

            var surface = mapView.ExportSurface;
            var vm = mapView.DataContext as MainWindowViewModel;

            var savedBackground = surface.Background;

            bool? savedTextures     = vm?.ShowTextures;
            bool? savedGridLines    = vm?.ShowGridLines;
            bool? savedBuildings    = vm?.ShowBuildings;
            bool? savedPrimGraphics = vm?.ShowPrimGraphics;
            bool? savedLights       = vm?.ShowLights;
            bool? savedRanges       = vm?.ShowLightRanges;

            try
            {
                if (vm is not null)
                {
                    vm.ShowTextures     = selection.Textures;
                    vm.ShowGridLines    = selection.GridLines;
                    vm.ShowBuildings    = selection.Buildings;
                    vm.ShowPrimGraphics = selection.PrimGraphics;
                    vm.ShowLights       = selection.Lights;
                    vm.ShowLightRanges  = selection.LightRanges;
                }

                if (selection.DropAlpha)
                    surface.Background = Brushes.White;

                surface.UpdateLayout();

                await surface.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                await surface.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

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
                if (vm is not null)
                {
                    if (savedTextures.HasValue)     vm.ShowTextures     = savedTextures.Value;
                    if (savedGridLines.HasValue)    vm.ShowGridLines    = savedGridLines.Value;
                    if (savedBuildings.HasValue)    vm.ShowBuildings    = savedBuildings.Value;
                    if (savedPrimGraphics.HasValue) vm.ShowPrimGraphics = savedPrimGraphics.Value;
                    if (savedLights.HasValue)       vm.ShowLights       = savedLights.Value;
                    if (savedRanges.HasValue)       vm.ShowLightRanges  = savedRanges.Value;
                }
                surface.Background = savedBackground;
                surface.UpdateLayout();
            }
        }

        public static string BuildOutputFileName(string lightsOrMapPath, string outputDirectory)
        {
            var name = Path.GetFileNameWithoutExtension(lightsOrMapPath);
            return Path.Combine(outputDirectory, $"{name}-IMG.png");
        }
    }
}
