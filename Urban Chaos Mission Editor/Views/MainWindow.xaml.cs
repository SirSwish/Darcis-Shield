using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
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
    private GridLength _lastLeftPanelWidth = new(280);
    private GridLength _lastRightPanelWidth = new(420);
    private bool _isLeftPanelCollapsed;
    private bool _isRightPanelCollapsed;

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
        PreviewKeyDown += MainWindow_PreviewKeyDown;

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

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && MapView.IsInPositionSelectionMode)
        {
            MapView.ExitPositionSelectionMode();
            e.Handled = true;
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

    private void FindTriggerUsage_Click(object sender, RoutedEventArgs e)
    {
        var win = new Dialogs.TriggerUsageFinderWindow(LoadMissionFromUsageFinder) { Owner = this };
        win.Show();
    }

    private async void ExportAllEventPoints_Click(object sender, RoutedEventArgs e)
    {
        string initialDirectory = GetInitialDebugDirectory();
        var folderDialog = new OpenFolderDialog
        {
            Title = "Select UCM workspace directory",
            InitialDirectory = initialDirectory
        };

        if (folderDialog.ShowDialog(this) != true)
            return;

        EditorSettingsService.Instance.LastDebugSearchDirectory = folderDialog.FolderName;

        var saveDialog = new SaveFileDialog
        {
            Title = "Export all Event Points to JSON",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"event-points-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            InitialDirectory = folderDialog.FolderName,
            AddExtension = true,
            DefaultExt = ".json",
            OverwritePrompt = true
        };

        if (saveDialog.ShowDialog(this) != true)
            return;

        try
        {
            IsEnabled = false;
            var exporter = new EventPointJsonExportService();
            var summary = await Task.Run(() => exporter.ExportDirectory(folderDialog.FolderName, saveDialog.FileName));

            MessageBox.Show(this,
                $"Exported {summary.EventPointCount:N0} Event Points from {summary.ExportedMissionCount:N0} of {summary.UcmFileCount:N0} UCM files.\n\nOutput:\n{summary.OutputPath}" +
                (summary.FailedFileCount > 0 ? $"\n\n{summary.FailedFileCount:N0} file(s) could not be read; see the failures section in the JSON." : string.Empty),
                "Event Point Export Complete",
                MessageBoxButton.OK,
                summary.FailedFileCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Event Point export failed:\n\n{ex.Message}",
                "Event Point Export Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private string GetInitialDebugDirectory()
    {
        if (DataContext is MainViewModel vm &&
            !string.IsNullOrWhiteSpace(vm.CurrentFilePath))
        {
            var currentMissionDirectory = System.IO.Path.GetDirectoryName(vm.CurrentFilePath);
            if (!string.IsNullOrWhiteSpace(currentMissionDirectory) &&
                System.IO.Directory.Exists(currentMissionDirectory))
            {
                return currentMissionDirectory;
            }
        }

        var lastDebugDirectory = EditorSettingsService.Instance.LastDebugSearchDirectory;
        if (!string.IsNullOrWhiteSpace(lastDebugDirectory) &&
            System.IO.Directory.Exists(lastDebugDirectory))
        {
            return lastDebugDirectory;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
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

    private void ToggleLeftPanel_Click(object sender, RoutedEventArgs e)
    {
        _isLeftPanelCollapsed = !_isLeftPanelCollapsed;

        if (_isLeftPanelCollapsed)
        {
            _lastLeftPanelWidth = LeftPanelColumn.Width.Value > 0 ? LeftPanelColumn.Width : _lastLeftPanelWidth;
            LeftSidePanel.Visibility = Visibility.Collapsed;
            LeftSideSplitter.Visibility = Visibility.Collapsed;
            LeftPanelColumn.MinWidth = 0;
            LeftPanelColumn.Width = new GridLength(0);
            LeftPanelToggleButton.Content = "▶";
            LeftPanelToggleButton.ToolTip = "Expand left panel";
        }
        else
        {
            LeftPanelColumn.MinWidth = 200;
            LeftPanelColumn.Width = _lastLeftPanelWidth.Value > 0 ? _lastLeftPanelWidth : new GridLength(280);
            LeftSidePanel.Visibility = Visibility.Visible;
            LeftSideSplitter.Visibility = Visibility.Visible;
            LeftPanelToggleButton.Content = "◀";
            LeftPanelToggleButton.ToolTip = "Collapse left panel";
        }
    }

    private void ToggleRightPanel_Click(object sender, RoutedEventArgs e)
    {
        _isRightPanelCollapsed = !_isRightPanelCollapsed;

        if (_isRightPanelCollapsed)
        {
            _lastRightPanelWidth = RightPanelColumn.Width.Value > 0 ? RightPanelColumn.Width : _lastRightPanelWidth;
            RightSidePanel.Visibility = Visibility.Collapsed;
            RightSideSplitter.Visibility = Visibility.Collapsed;
            RightPanelColumn.MinWidth = 0;
            RightPanelColumn.Width = new GridLength(0);
            RightPanelToggleButton.Content = "◀";
            RightPanelToggleButton.ToolTip = "Expand right panel";
        }
        else
        {
            RightPanelColumn.MinWidth = 340;
            RightPanelColumn.Width = _lastRightPanelWidth.Value > 0 ? _lastRightPanelWidth : new GridLength(420);
            RightSidePanel.Visibility = Visibility.Visible;
            RightSideSplitter.Visibility = Visibility.Visible;
            RightPanelToggleButton.Content = "▶";
            RightPanelToggleButton.ToolTip = "Collapse right panel";
        }
    }

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

        _eventPointOverlayVisual = new ModelVisual3D();
        _lightOverlayVisual = new ModelVisual3D
        {
        };

        vm.PropertyChanged += On3DMissionVmChanged;
        ReadOnlyLightsDataService.Instance.LightsLoaded += On3DLightsChanged;
        ReadOnlyLightsDataService.Instance.LightsCleared += On3DLightsChanged;

        _viewport3DWindow = new Viewport3DWindow(new[]
        {
            new Viewport3DOverlayLayer("Lights", _lightOverlayVisual, cull => MissionLight3DOverlayBuilder.Build(cull)),
            new Viewport3DOverlayLayer("Event Points", _eventPointOverlayVisual, cull => EventPoint3DOverlayBuilder.Build(vm.EventPoints, cull))
        },
        cullDistance: 1024.0,
        cullMargin: 128.0)
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
            Dispatcher.Invoke(() => _viewport3DWindow?.RebuildCullAwareOverlayLayers());
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

}
