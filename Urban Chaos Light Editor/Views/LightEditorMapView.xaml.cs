using System.Windows.Controls;
using System.Windows.Input;
using UrbanChaosLightEditor.ViewModels;

namespace UrbanChaosLightEditor.Views
{
    public partial class LightEditorMapView : UserControl
    {
        public LightEditorMapView()
        {
            InitializeComponent();

            // Mouse handlers for light placement and movement
            Surface.MouseLeftButtonDown += OnSurfaceMouseDown;
            Surface.MouseMove += OnSurfaceMouseMove;

            // Mouse wheel for zoom
            Scroller.PreviewMouseWheel += OnMouseWheel;
        }

        private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            // Ctrl+Wheel for zoom
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                double oldZoom = vm.Zoom;
                double newZoom = e.Delta > 0
                    ? System.Math.Clamp(oldZoom * 1.15, 0.1, 4.0)
                    : System.Math.Clamp(oldZoom / 1.15, 0.1, 4.0);

                if (newZoom != oldZoom)
                {
                    // Cursor position in viewport space
                    var mouseInViewport = e.GetPosition(Scroller);

                    // Unscaled surface point under the cursor
                    double surfaceX = (Scroller.HorizontalOffset + mouseInViewport.X) / oldZoom;
                    double surfaceY = (Scroller.VerticalOffset + mouseInViewport.Y) / oldZoom;

                    vm.Zoom = newZoom;

                    // After layout recalculates (LayoutTransform), reposition scroll so the
                    // same surface point stays under the cursor
                    Scroller.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Render, () =>
                        {
                            Scroller.ScrollToHorizontalOffset(surfaceX * newZoom - mouseInViewport.X);
                            Scroller.ScrollToVerticalOffset(surfaceY * newZoom - mouseInViewport.Y);
                        });
                }

                e.Handled = true;
            }
            // Without Ctrl: ScrollViewer handles normal scrolling
        }

        private void OnSurfaceMouseDown(object sender, MouseButtonEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            var pos = e.GetPosition(Surface);
            int uiX = (int)pos.X;
            int uiZ = (int)pos.Y;

            // If in add light mode, place a new light
            if (vm.IsAddingLight)
            {
                vm.AddLightAt(uiX, uiZ);
                e.Handled = true;
            }
        }

        private void OnSurfaceMouseMove(object sender, MouseEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            var pos = e.GetPosition(Surface);
            int uiX = (int)pos.X;
            int uiZ = (int)pos.Y;

            // Update cursor position display
            vm.CursorPosition = $"X: {uiX}, Z: {uiZ}";
        }
    }
}