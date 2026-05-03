using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Input;
using System.Windows.Interop;
using UrbanChaosEditor.Shared.Views.Help;
using UrbanChaosLightEditor.Help;
using UrbanChaosLightEditor.Services;
using UrbanChaosLightEditor.Services.Export;
using UrbanChaosLightEditor.Services.Viewport3D;
using UrbanChaosLightEditor.ViewModels;
using UrbanChaosLightEditor.Views.Dialogs;
using UrbanChaosMapEditor.Services.Styles;
using UrbanChaosMapEditor.Services.Textures;
using UrbanChaosMapEditor.Views.Viewport3D;
using MapEditorMapDataService = UrbanChaosMapEditor.Services.Core.MapDataService;
using SharedTextureCacheService = UrbanChaosEditor.Shared.Services.Textures.TextureCacheService;

namespace UrbanChaosLightEditor
{
    public partial class MainWindow : Window
    {
        public static readonly RoutedCommand Open3DViewportCommand = new(nameof(Open3DViewportCommand), typeof(MainWindow));
        private Viewport3DWindow? _viewport3DWindow;
        private ModelVisual3D? _lightOverlayVisual;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            SourceInitialized += OnSourceInitialized;
            CommandBindings.Add(new CommandBinding(Open3DViewportCommand, async (_, __) => await Open3DViewportAsync()));
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                int dark = 1;
                if (DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref dark, sizeof(int));
                int caption = 0x001E1E1E;
                DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
                int text = 0x00FFFFFF;
                DwmSetWindowAttribute(hwnd, 36, ref text, sizeof(int));
            }
            catch { }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            new Views.Dialogs.AboutWindow { Owner = this }.ShowDialog();
        }

        private void Help_Click(object sender, RoutedEventArgs e)
            => HelpViewerWindow.ShowHelp("Urban Chaos Light Editor - Help", LightEditorHelpTopics.All, this);

        private async void Open3DViewport_Click(object sender, RoutedEventArgs e)
            => await Open3DViewportAsync();

        private async Task Open3DViewportAsync()
        {
            if (_viewport3DWindow != null && _viewport3DWindow.IsLoaded)
            {
                _viewport3DWindow.Activate();
                return;
            }

            var mapPath = ReadOnlyMapDataService.Instance.CurrentPath;
            if (string.IsNullOrWhiteSpace(mapPath) || !File.Exists(mapPath))
            {
                MessageBox.Show(this, "Open a map before opening the 3D viewport.", "3D Viewport",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await LoadMapViewportResourcesAsync(mapPath);

            _lightOverlayVisual = new ModelVisual3D();
            LightsDataService.Instance.LightsBytesReset += On3DLightsChanged;

            _viewport3DWindow = new Viewport3DWindow(new[]
            {
                new Viewport3DOverlayLayer("Lights", _lightOverlayVisual, cull => Light3DOverlayBuilder.Build(cull))
            },
            cullDistance: 1536.0,
            cullMargin: 192.0)
            { Owner = this };
            _viewport3DWindow.ConfigureAmbientFilter(ReadPreviewAmbientFilterColor(), isEnabled: true);
            _viewport3DWindow.Closed += (_, __) =>
            {
                LightsDataService.Instance.LightsBytesReset -= On3DLightsChanged;
                _viewport3DWindow = null;
                _lightOverlayVisual = null;
            };
            _viewport3DWindow.Show();
        }

        private void On3DLightsChanged(object? sender, EventArgs e)
        {
            if (_lightOverlayVisual == null)
                return;

            Dispatcher.Invoke(() =>
            {
                _viewport3DWindow?.RebuildCullAwareOverlayLayers();
                if (_viewport3DWindow != null)
                    _viewport3DWindow.ConfigureAmbientFilter(
                        ReadPreviewAmbientFilterColor(),
                        _viewport3DWindow.IsAmbientFilterEnabled);
            });
        }

        private static Color ReadPreviewAmbientFilterColor()
        {
            if (!LightsDataService.Instance.IsLoaded)
                return Color.FromRgb(96, 96, 96);

            try
            {
                var props = new LightsAccessor(LightsDataService.Instance).ReadProperties();
                return Color.FromRgb(
                    (byte)Math.Clamp(props.NightAmbRed * 820 / 256, 0, 255),
                    (byte)Math.Clamp(props.NightAmbGreen * 820 / 256, 0, 255),
                    (byte)Math.Clamp(props.NightAmbBlue * 820 / 256, 0, 255));
            }
            catch
            {
                return Color.FromRgb(96, 96, 96);
            }
        }

        private static async Task LoadMapViewportResourcesAsync(string mapPath)
        {
            await MapEditorMapDataService.Instance.LoadAsync(mapPath);

            var cache = SharedTextureCacheService.Instance;
            cache.ActiveSet = "release";
            if (cache.Count == 0)
                await cache.PreloadAllAsync(64);

            int world = new TexturesAccessor(MapEditorMapDataService.Instance).ReadTextureWorld();
            await LoadStyleTmaAsync(world, useBeta: false);
        }

        private static async Task LoadStyleTmaAsync(int world, bool useBeta)
        {
            if (world <= 0)
            {
                StyleDataService.Instance.Clear();
                return;
            }

            string? customTma = ResolveCustomStyleTmaPath(world);
            if (world > 20 && customTma != null)
            {
                await StyleDataService.Instance.LoadAsync(customTma);
                return;
            }

            string set = useBeta ? "Beta" : "Release";
            var uri = new Uri(
                $"pack://application:,,,/UrbanChaosEditor.Shared;component/Assets/Textures/{set}/world{world}/style.tma",
                UriKind.Absolute);

            try
            {
                var sri = Application.GetResourceStream(uri);
                if (sri?.Stream != null)
                {
                    using (sri.Stream)
                        await StyleDataService.Instance.LoadFromResourceStreamAsync(sri.Stream, uri.ToString());
                    return;
                }
            }
            catch (IOException)
            {
            }

            if (customTma != null)
            {
                await StyleDataService.Instance.LoadAsync(customTma);
                return;
            }

            StyleDataService.Instance.Clear();
        }

        private static string? ResolveCustomStyleTmaPath(int world)
        {
            var customRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomTextures", $"world{world}");
            if (!Directory.Exists(customRoot))
                return null;

            var tmaPath = Path.Combine(customRoot, "style.tma");
            if (File.Exists(tmaPath))
                return tmaPath;

            return Directory.GetFiles(customRoot, "*.tma").FirstOrDefault();
        }

        private async void ExportCurrentLightMap_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null || !vm.IsMapViewLoaded)
            {
                MessageBox.Show(this, "No map is loaded — open a map (Ctrl+Shift+O) before exporting.",
                    "Export Light Map", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new ExportLightMapDialog { Owner = this };
            if (dlg.ShowDialog() != true) return;

            var lightsPath = LightsDataService.Instance.CurrentPath;
            var mapPath = ReadOnlyMapDataService.Instance.CurrentPath;
            var sourcePath = !string.IsNullOrEmpty(lightsPath) ? lightsPath : mapPath;
            var defaultName = string.IsNullOrEmpty(sourcePath)
                ? "lightmap-IMG.png"
                : $"{Path.GetFileNameWithoutExtension(sourcePath)}-IMG.png";

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Light Map as PNG",
                Filter = "PNG image (*.png)|*.png",
                FileName = defaultName,
                AddExtension = true,
                DefaultExt = ".png",
            };
            if (sfd.ShowDialog(this) != true) return;

            await RunExportAsync(async () =>
                await LightImageExporter.ExportAsync(MapView, dlg.Selection, sfd.FileName));
        }

        private async void ExportSelectedLightMaps_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            if (LightsDataService.Instance.HasChanges)
            {
                var resp = MessageBox.Show(this,
                    "The current lights file has unsaved changes. Batch export will load other lights/maps in turn and the current changes would be lost. Continue without saving?",
                    "Unsaved Changes",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
                if (resp != MessageBoxResult.OK) return;
            }

            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Lights Files to Export (companion .iam will load if found)",
                Filter = "Lights Files (*.lgt)|*.lgt|All Files (*.*)|*.*",
                Multiselect = true,
            };
            if (ofd.ShowDialog(this) != true || ofd.FileNames.Length == 0) return;
            var lightPaths = ofd.FileNames;

            var dlg = new ExportLightMapDialog { Owner = this };
            if (dlg.ShowDialog() != true) return;

            var folder = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choose output folder",
            };
            if (folder.ShowDialog(this) != true) return;
            var outDir = folder.FolderName;

            var originalLights = LightsDataService.Instance.CurrentPath;

            await RunExportAsync(async () =>
            {
                int ok = 0, fail = 0;
                foreach (var lightPath in lightPaths)
                {
                    try
                    {
                        await vm.OpenLightsFromPathAsync(lightPath);

                        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                        var outPath = LightImageExporter.BuildOutputFileName(lightPath, outDir);
                        await LightImageExporter.ExportAsync(MapView, dlg.Selection, outPath);
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ExportSelectedLightMaps] {lightPath}: {ex}");
                        fail++;
                    }
                }

                try
                {
                    if (!string.IsNullOrEmpty(originalLights) && File.Exists(originalLights))
                        await vm.OpenLightsFromPathAsync(originalLights);
                    else
                        LightsDataService.Instance.Clear();
                }
                catch { /* ignore restore failures */ }

                MessageBox.Show(this,
                    $"Exported {ok} light map(s) to:\n{outDir}" + (fail > 0 ? $"\n\n{fail} file(s) failed — see Debug output." : ""),
                    "Export Complete",
                    MessageBoxButton.OK, fail > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            });
        }

        private async void ExportCurrentLightStats_Click(object sender, RoutedEventArgs e)
        {
            if (!LightsDataService.Instance.IsLoaded)
            {
                MessageBox.Show(this, "No lights file is loaded.", "Export Stats",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sourcePath = LightsDataService.Instance.CurrentPath;
            var defaultName = string.IsNullOrEmpty(sourcePath)
                ? "lights-Stats.txt"
                : $"{Path.GetFileNameWithoutExtension(sourcePath)}-Stats.txt";

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Lights Stats",
                Filter = "Text file (*.txt)|*.txt",
                FileName = defaultName,
                AddExtension = true,
                DefaultExt = ".txt",
            };
            if (sfd.ShowDialog(this) != true) return;

            await RunExportAsync(async () =>
                await LightStatsExporter.ExportAsync(LightsDataService.Instance, sfd.FileName));
        }

        private async void ExportSelectedLightStats_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            if (LightsDataService.Instance.HasChanges)
            {
                var resp = MessageBox.Show(this,
                    "The current lights file has unsaved changes. Batch export will load other lights files in turn and the current changes would be lost. Continue without saving?",
                    "Unsaved Changes",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
                if (resp != MessageBoxResult.OK) return;
            }

            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Lights Files to Export Stats",
                Filter = "Lights Files (*.lgt)|*.lgt|All Files (*.*)|*.*",
                Multiselect = true,
            };
            if (ofd.ShowDialog(this) != true || ofd.FileNames.Length == 0) return;
            var lightPaths = ofd.FileNames;

            var folder = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choose output folder",
            };
            if (folder.ShowDialog(this) != true) return;
            var outDir = folder.FolderName;

            var originalLights = LightsDataService.Instance.CurrentPath;

            await RunExportAsync(async () =>
            {
                int ok = 0, fail = 0;
                foreach (var lightPath in lightPaths)
                {
                    try
                    {
                        await LightsDataService.Instance.LoadAsync(lightPath);
                        var outPath = LightStatsExporter.BuildOutputFileName(lightPath, outDir);
                        await LightStatsExporter.ExportAsync(LightsDataService.Instance, outPath);
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ExportSelectedLightStats] {lightPath}: {ex}");
                        fail++;
                    }
                }

                try
                {
                    if (!string.IsNullOrEmpty(originalLights) && File.Exists(originalLights))
                        await LightsDataService.Instance.LoadAsync(originalLights);
                    else
                        LightsDataService.Instance.Clear();
                }
                catch { /* ignore restore failures */ }

                MessageBox.Show(this,
                    $"Exported stats for {ok} lights file(s) to:\n{outDir}" + (fail > 0 ? $"\n\n{fail} file(s) failed — see Debug output." : ""),
                    "Export Complete",
                    MessageBoxButton.OK, fail > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            });
        }

        private async Task RunExportAsync(Func<Task> action)
        {
            Cursor previous = Cursor;
            try
            {
                Cursor = Cursors.Wait;
                IsEnabled = false;
                await action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export failed: {ex.Message}", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
                Cursor = previous;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && vm.HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes to the lights file.\n\nSave before closing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }

                if (result == MessageBoxResult.Yes)
                {
                    vm.SaveLightsCommand.Execute(null);
                }
            }

            base.OnClosing(e);
        }
    }
}
