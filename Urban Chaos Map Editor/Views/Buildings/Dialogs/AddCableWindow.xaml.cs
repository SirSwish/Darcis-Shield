// /Views/Dialogs/Buildings/AddCableWindow.xaml.cs
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.ViewModels.Buildings;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.Views.Core;


namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class AddCableWindow : Window
    {
        private static readonly Regex _digitsOnly = new Regex(@"^[0-9]+$");
        private static readonly Regex _signedDigitsOnly = new Regex(@"^-?[0-9]*$");

        private bool _isUpdating = true; // Start true to prevent events during initialization
        private bool _useAutoAngles = true;
        private bool _isInitialized = false;

        public bool WasCancelled { get; private set; } = true;

        // Parsed values
        private byte _x0, _z0, _x1, _z1;
        private short _y0, _y1;
        private byte _segments;
        private short _stepAngle1, _stepAngle2;
        private readonly int _buildingId1;

        public AddCableWindow(int buildingId1)
        {
            InitializeComponent();
            _buildingId1 = buildingId1;
            Loaded += (_, __) =>
            {
                _isInitialized = true;
                _isUpdating = false;
                RecalculateAndPreview();
            };
        }

        #region Input Validation

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !_digitsOnly.IsMatch(e.Text);
        }

        private void SignedNumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;

            // Allow minus sign at start
            if (e.Text == "-" && textBox?.SelectionStart == 0 && !textBox.Text.Contains("-"))
            {
                e.Handled = false;
                return;
            }

            e.Handled = !_digitsOnly.IsMatch(e.Text);
        }

        private void Coordinate_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || !_isInitialized) return;
            RecalculateAndPreview();
        }

        private void Parameter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || !_isInitialized) return;

            // If user manually edits segments or angles, they're no longer "auto"
            if (sender == TxtStepAngle1 || sender == TxtStepAngle2)
            {
                _useAutoAngles = false;
            }

            RecalculateAndPreview();
        }

        private void ChkAdvanced_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkAdvanced.IsChecked == true)
            {
                PanelAdvanced.Visibility = Visibility.Visible;
            }
            else
            {
                PanelAdvanced.Visibility = Visibility.Collapsed;
                _useAutoAngles = true;
                RecalculateAndPreview();
            }
        }

        #endregion

        #region Actions

        private void BtnDrawOnMap_Click(object sender, RoutedEventArgs e)
        {
            // Get the MapViewModel from the main window
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                // Hide this window while user draws on map (minimizing affects owner window)
                Hide();

                // Start cable placement mode on the map
                mainVm.Map.BeginCablePlacement(this);

                mainVm.StatusMessage = "Cable placement: Click start point on map. Right-click or Escape to cancel.";
            }
            else
            {
                MessageBox.Show(
                    "Could not access the map. Please try again.",
                    "Draw on Map",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (!ParseAndValidate())
            {
                MessageBox.Show("Please enter valid values for all fields.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_buildingId1 <= 0)
            {
                MessageBox.Show("No building selected. Cables must belong to a building.",
                    "No Building", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            byte fHeight = 0; // working cables use 0 or 4, not 2

            var (success, newFacetId, error) = CableAdder.TryAddCable(
                _buildingId1,
                _x0, _z0, _y0,
                _x1, _z1, _y1,
                _segments,
                _useAutoAngles ? null : (short?)_stepAngle1,
                _useAutoAngles ? null : (short?)_stepAngle2,
                fHeight);

            if (success)
            {
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                {
                    mainVm.StatusMessage = $"Cable #{newFacetId} added to Building #{_buildingId1}: ({_x0},{_z0}) → ({_x1},{_z1}), {_segments} segments.";
                }

                RefreshBuildingsTab();
                WasCancelled = false;
                Close();
            }
            else
            {
                MessageBox.Show($"Failed to add cable:\n\n{error ?? "Unknown error"}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            WasCancelled = true;
            Close();
        }

        #endregion

        #region Calculations

        private void RecalculateAndPreview()
        {
            // Guard against calls before window is fully loaded
            if (!_isInitialized) return;
            if (!ParseCoordinates()) return;

            // Calculate length and segments
            int dx = Math.Abs(_x1 - _x0);
            int dz = Math.Abs(_z1 - _z0);
            int dy = Math.Abs(_y1 - _y0);

            // Approximate length in world units
            double worldDx = dx * 256.0;
            double worldDz = dz * 256.0;
            double worldDy = dy;
            double length = Math.Sqrt(worldDx * worldDx + worldDy * worldDy + worldDz * worldDz);

            // Update length display
            TxtCalculatedLength.Text = $"~{Math.Max(dx, dz)} tiles ({length:F0} units)";

            // Auto-calculate segments
            _isUpdating = true;
            int autoSegments = CableAdder.CalculateSegmentCount(length);
            TxtCalculatedSegments.Text = $"Suggested: {autoSegments}";

            // Only update segments box if it's empty or user hasn't manually edited it
            if (string.IsNullOrWhiteSpace(TxtSegments.Text))
            {
                TxtSegments.Text = autoSegments.ToString();
            }

            // Parse current segments
            if (!byte.TryParse(TxtSegments.Text, out _segments) || _segments < 2)
            {
                _segments = (byte)autoSegments;
            }

            // Auto-calculate angles if in auto mode
            if (_useAutoAngles)
            {
                var (step1, step2) = CableAdder.CalculateStepAngles(length, _segments);
                _stepAngle1 = step1;
                _stepAngle2 = step2;
                TxtStepAngle1.Text = step1.ToString();
                TxtStepAngle2.Text = step2.ToString();
            }
            else
            {
                short.TryParse(TxtStepAngle1.Text, out _stepAngle1);
                short.TryParse(TxtStepAngle2.Text, out _stepAngle2);
            }

            _isUpdating = false;

            // Update preview
            DrawPreview();

            // Enable/disable create button
            BtnCreate.IsEnabled = ParseAndValidate();
        }

        private bool ParseCoordinates()
        {
            // Guard against calls before controls are initialized
            if (!_isInitialized) return false;
            if (TxtX0 == null || TxtZ0 == null || TxtY0 == null ||
                TxtX1 == null || TxtZ1 == null || TxtY1 == null)
                return false;

            bool valid = true;
            valid &= byte.TryParse(TxtX0.Text, out _x0) && _x0 <= 127;
            valid &= byte.TryParse(TxtZ0.Text, out _z0) && _z0 <= 127;
            valid &= short.TryParse(TxtY0.Text, out _y0);
            valid &= byte.TryParse(TxtX1.Text, out _x1) && _x1 <= 127;
            valid &= byte.TryParse(TxtZ1.Text, out _z1) && _z1 <= 127;
            valid &= short.TryParse(TxtY1.Text, out _y1);
            return valid;
        }

        private bool ParseAndValidate()
        {
            if (!ParseCoordinates()) return false;
            if (!byte.TryParse(TxtSegments.Text, out _segments)) return false;
            if (_segments < 2) return false;

            // Check that start and end are different
            if (_x0 == _x1 && _z0 == _z1)
            {
                return false; // Zero-length cable
            }

            return true;
        }

        #endregion

        #region Preview Drawing

        private void DrawPreview()
        {
            PreviewCanvas.Children.Clear();

            if (!ParseCoordinates()) return;

            double canvasWidth = PreviewCanvas.ActualWidth > 0 ? PreviewCanvas.ActualWidth : 300;
            double canvasHeight = PreviewCanvas.ActualHeight > 0 ? PreviewCanvas.ActualHeight : 150;
            double margin = 30;

            // Draw a simple side-view representation
            double drawWidth = canvasWidth - margin * 2;
            double drawHeight = canvasHeight - margin * 2;

            int segments = _segments > 0 ? _segments : 8;

            // Calculate sag amount based on step angles
            double sagAmount = Math.Abs(_stepAngle1 + _stepAngle2) / 20.0;
            sagAmount = Math.Max(sagAmount, 5);

            // Generate points
            var points = new Point[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                double x = margin + t * drawWidth;

                // Linear Y interpolation
                double baseY = _y0 + t * (_y1 - _y0);

                // Add sag
                double sag = sagAmount * 4 * t * (1 - t);

                double y = margin + drawHeight / 2 - (baseY / 20.0) + sag;
                points[i] = new Point(x, y);
            }

            // Draw straight reference line
            var refLine = new Line
            {
                X1 = points[0].X,
                Y1 = margin + drawHeight / 2 - (_y0 / 20.0),
                X2 = points[segments].X,
                Y2 = margin + drawHeight / 2 - (_y1 / 20.0),
                Stroke = Brushes.Gray,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            PreviewCanvas.Children.Add(refLine);

            // Draw cable segments
            for (int i = 0; i < segments; i++)
            {
                var segment = new Line
                {
                    X1 = points[i].X,
                    Y1 = points[i].Y,
                    X2 = points[i + 1].X,
                    Y2 = points[i + 1].Y,
                    Stroke = Brushes.Red,
                    StrokeThickness = 3,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                PreviewCanvas.Children.Add(segment);
            }

            // Draw endpoints
            var startMarker = new Ellipse { Width = 12, Height = 12, Fill = Brushes.LimeGreen };
            Canvas.SetLeft(startMarker, points[0].X - 6);
            Canvas.SetTop(startMarker, points[0].Y - 6);
            PreviewCanvas.Children.Add(startMarker);

            var endMarker = new Ellipse { Width = 12, Height = 12, Fill = Brushes.Orange };
            Canvas.SetLeft(endMarker, points[segments].X - 6);
            Canvas.SetTop(endMarker, points[segments].Y - 6);
            PreviewCanvas.Children.Add(endMarker);

            // Labels
            var startLabel = new TextBlock
            {
                Text = $"Start ({_x0},{_z0})",
                Foreground = Brushes.LimeGreen,
                FontSize = 10
            };
            Canvas.SetLeft(startLabel, points[0].X - 20);
            Canvas.SetTop(startLabel, points[0].Y + 10);
            PreviewCanvas.Children.Add(startLabel);

            var endLabel = new TextBlock
            {
                Text = $"End ({_x1},{_z1})",
                Foreground = Brushes.Orange,
                FontSize = 10
            };
            Canvas.SetLeft(endLabel, points[segments].X - 20);
            Canvas.SetTop(endLabel, points[segments].Y + 10);
            PreviewCanvas.Children.Add(endLabel);
        }

        #endregion

        #region Helpers

        private void RefreshBuildingsTab()
        {
            // Find and refresh the BuildingsTabViewModel
            if (Application.Current.MainWindow is Views.Core.MainWindow mainWindow)
            {
                var tabControl = FindChild<TabControl>(mainWindow);
                if (tabControl != null)
                {
                    foreach (var item in tabControl.Items)
                    {
                        if (item is TabItem tabItem && tabItem.Content is FrameworkElement fe)
                        {
                            if (fe.DataContext is BuildingsTabViewModel btvm)
                            {
                                btvm.RefreshCommand?.Execute(null);
                                break;
                            }
                        }
                    }
                }
            }

            // Mark map as dirty
            MapDataService.Instance.MarkDirty();
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result) return result;

                var found = FindChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        #endregion

        #region Map placement callbacks

        /// <summary>
        /// Called by MapViewModel when the user has finished selecting the cable endpoints on the map.
        /// Coordinates are in tile space (0–127).
        /// </summary>
        public void OnPlacementCompleted(byte x0, byte z0, byte x1, byte z1)
        {
            // Restore the window (it was hidden during placement)
            Show();
            Activate();

            _isUpdating = true;
            TxtX0.Text = x0.ToString();
            TxtZ0.Text = z0.ToString();
            TxtX1.Text = x1.ToString();
            TxtZ1.Text = z1.ToString();
            _isUpdating = false;

            RecalculateAndPreview();
        }

        /// <summary>
        /// Called by MapViewModel when cable placement is cancelled from the map.
        /// </summary>
        public void OnPlacementCancelled()
        {
            // Restore the window (it was hidden during placement)
            Show();
            Activate();
            // Nothing else to do – user keeps their existing values.
        }

        #endregion
    }
}