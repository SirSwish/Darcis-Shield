using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using UrbanChaosMissionEditor.ViewModels;

namespace UrbanChaosMissionEditor.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

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