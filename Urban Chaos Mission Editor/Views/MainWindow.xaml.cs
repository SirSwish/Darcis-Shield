using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Media3D;
using UrbanChaosEditor.Shared.Views.Help;
using UrbanChaosMissionEditor.Help;
using UrbanChaosMissionEditor.Services;
using UrbanChaosMissionEditor.Services.Viewport3D;
using UrbanChaosMissionEditor.ViewModels;
using UrbanChaosMapEditor.Services.Styles;
using UrbanChaosMapEditor.Services.Textures;
using UrbanChaosMapEditor.Views.Viewport3D;
using MapEditorMapDataService = UrbanChaosMapEditor.Services.Core.MapDataService;
using SharedTextureCacheService = UrbanChaosEditor.Shared.Services.Textures.TextureCacheService;

namespace UrbanChaosMissionEditor.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public static readonly RoutedCommand Open3DViewportCommand = new(nameof(Open3DViewportCommand), typeof(MainWindow));

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private Viewport3DWindow? _viewport3DWindow;
    private ModelVisual3D? _eventPointOverlayVisual;
    private ModelVisual3D? _lightOverlayVisual;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        var viewModel = new MainViewModel();
        DataContext = viewModel;

        // Wire up the position callback for AddEventPoint
        viewModel.GetCurrentWorldPosition = () => MapView.GetCurrentWorldPosition();

        // Handle command line arguments
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && System.IO.File.Exists(args[1]))
        {
            viewModel.LoadMission(args[1]);
        }

        // Hook preview mouse events for position selection mode
        PreviewMouseLeftButtonDown += MainWindow_PreviewMouseLeftButtonDown;
        PreviewMouseMove += MainWindow_PreviewMouseMove;

        CommandBindings.Add(new CommandBinding(Open3DViewportCommand, async (_, __) => await Open3DViewportAsync()));
    }

    /// <summary>
    /// Exposes the MapViewControl for position selection from dialogs
    /// </summary>
    public MapViewControl MapViewControl => MapView;

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

    private void MainWindow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (MapView.IsInPositionSelectionMode)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] PreviewMouseLeftButtonDown in position selection mode");

            // Get position relative to the MapView's Surface
            var position = e.GetPosition(MapView);
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Position relative to MapView: ({position.X}, {position.Y})");

            // Check if click is within the MapView bounds
            if (position.X >= 0 && position.Y >= 0 &&
                position.X <= MapView.ActualWidth && position.Y <= MapView.ActualHeight)
            {
                // Forward the click handling to MapView
                MapView.HandlePositionSelectionClick(e);
                e.Handled = true;
            }
        }
    }

    private void MainWindow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (MapView.IsInPositionSelectionMode)
        {
            // Forward mouse move to MapView for coordinate display
            MapView.HandlePositionSelectionMove(e);
        }
    }

    private void CategoryPill_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element &&
            element.DataContext is CategoryFilterViewModel filter)
        {
            filter.Toggle();
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new Dialogs.AboutWindow { Owner = this }.ShowDialog();
    }

    private void FindEventPointUsage_Click(object sender, RoutedEventArgs e)
    {
        var win = new Dialogs.EventPointUsageFinderWindow(LoadMissionFromUsageFinder) { Owner = this };
        win.Show();
    }

    private void LoadMissionFromUsageFinder(string ucmPath)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.LoadMission(ucmPath);
        }
    }

    private void Help_Click(object sender, RoutedEventArgs e)
        => HelpViewerWindow.ShowHelp("Urban Chaos Mission Editor - Help", MissionEditorHelpTopics.All, this);

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
        if (string.IsNullOrWhiteSpace(mapPath) || !System.IO.File.Exists(mapPath))
        {
            MessageBox.Show(this, "Open a map before opening the 3D viewport.", "3D Viewport",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await LoadMapViewportResourcesAsync(mapPath);

        if (DataContext is not MainViewModel vm)
            return;

        _eventPointOverlayVisual = new ModelVisual3D
        {
            Content = EventPoint3DOverlayBuilder.Build(vm.EventPoints)
        };
        _lightOverlayVisual = new ModelVisual3D
        {
        };

        vm.PropertyChanged += On3DMissionVmChanged;
        ReadOnlyLightsDataService.Instance.LightsLoaded += On3DLightsChanged;
        ReadOnlyLightsDataService.Instance.LightsCleared += On3DLightsChanged;

        _viewport3DWindow = new Viewport3DWindow(new[]
        {
            new Viewport3DOverlayLayer("Lights", _lightOverlayVisual, cull => MissionLight3DOverlayBuilder.Build(cull)),
            new Viewport3DOverlayLayer("Event Points", _eventPointOverlayVisual)
        },
        cullDistance: 1536.0,
        cullMargin: 192.0)
        { Owner = this };
        _viewport3DWindow.Closed += (_, __) =>
        {
            vm.PropertyChanged -= On3DMissionVmChanged;
            ReadOnlyLightsDataService.Instance.LightsLoaded -= On3DLightsChanged;
            ReadOnlyLightsDataService.Instance.LightsCleared -= On3DLightsChanged;
            _viewport3DWindow = null;
            _eventPointOverlayVisual = null;
            _lightOverlayVisual = null;
        };
        _viewport3DWindow.Show();
    }

    private void On3DMissionVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_eventPointOverlayVisual == null || DataContext is not MainViewModel vm)
            return;

        if (e.PropertyName == nameof(MainViewModel.EventPoints) ||
            e.PropertyName == nameof(MainViewModel.VisibleEventPoints) ||
            e.PropertyName == nameof(MainViewModel.SelectedEventPoint))
        {
            Dispatcher.Invoke(() =>
                _eventPointOverlayVisual.Content = EventPoint3DOverlayBuilder.Build(vm.EventPoints));
        }
    }

    private void On3DLightsChanged(object? sender, EventArgs e)
    {
        if (_lightOverlayVisual == null)
            return;

        Dispatcher.Invoke(() => _viewport3DWindow?.RebuildCullAwareOverlayLayers());
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
        catch (System.IO.IOException)
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
        var customRoot = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomTextures", $"world{world}");
        if (!System.IO.Directory.Exists(customRoot))
            return null;

        var tmaPath = System.IO.Path.Combine(customRoot, "style.tma");
        if (System.IO.File.Exists(tmaPath))
            return tmaPath;

        return System.IO.Directory.GetFiles(customRoot, "*.tma").FirstOrDefault();
    }

    private void EventPointsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedEventPoint != null)
        {
            // Open editor dialog
            if (vm.EditEventPointCommand.CanExecute(null))
            {
                vm.EditEventPointCommand.Execute(null);
            }
        }
    }
}
