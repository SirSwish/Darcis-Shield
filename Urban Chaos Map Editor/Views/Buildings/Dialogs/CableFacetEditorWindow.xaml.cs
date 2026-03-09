// /Views/Dialogs/Buildings/CableFacetEditorWindow.xaml.cs
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.ViewModels.Buildings;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.Services.Buildings;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class CableFacetEditorWindow : Window
    {
        private static readonly Regex _digitsOnly = new Regex(@"^[0-9]+$");
        private static readonly Regex _signedDigitsOnly = new Regex(@"^-?[0-9]*$");

        // DFacet byte offsets (same as CableAdder)
        private const int OFF_TYPE = 0;
        private const int OFF_HEIGHT = 1;
        private const int OFF_X0 = 2;
        private const int OFF_X1 = 3;
        private const int OFF_Y0 = 4;
        private const int OFF_Y1 = 6;
        private const int OFF_Z0 = 8;
        private const int OFF_Z1 = 9;
        private const int OFF_FLAGS = 10;
        private const int OFF_STYLE = 12;
        private const int OFF_BUILDING = 14;
        private const int OFF_STOREY = 16;
        private const int OFF_FHEIGHT = 18;
        private const int DFACET_SIZE = 26;

        private readonly int _facetId1;
        private readonly DFacetRec _originalFacet;
        private bool _isLoading = true;
        private bool _hasChanges = false;

        public CableFacetEditorWindow(DFacetRec facet, int facetId1)
        {
            InitializeComponent();

            _facetId1 = facetId1;
            _originalFacet = facet;

            TxtHeader.Text = $"Cable Editor - Facet #{facetId1}";
            TxtFacetId.Text = $"Facet ID: {facetId1} (1-based index)";

            LoadFacetData(facet);

            _isLoading = false;
            UpdatePreview();
        }

        private void LoadFacetData(DFacetRec f)
        {
            // Coordinates
            TxtX0.Text = f.X0.ToString();
            TxtZ0.Text = f.Z0.ToString();
            TxtY0.Text = f.Y0.ToString();
            TxtX1.Text = f.X1.ToString();
            TxtZ1.Text = f.Z1.ToString();
            TxtY1.Text = f.Y1.ToString();

            // Cable parameters
            TxtSegments.Text = f.Height.ToString();
            TxtStepAngle1.Text = f.CableStep1Signed.ToString();
            TxtStepAngle2.Text = f.CableStep2Signed.ToString();
            TxtFHeight.Text = f.FHeight.ToString();
            TxtFlags.Text = f.Flags.ToString();

            // Raw hex display
            UpdateRawHexDisplay(f);
        }

        private void UpdateRawHexDisplay(DFacetRec f)
        {
            string hex = $"Type: {(byte)f.Type:X2}  Height: {f.Height:X2}  " +
                         $"X0: {f.X0:X2}  X1: {f.X1:X2}  Z0: {f.Z0:X2}  Z1: {f.Z1:X2}  " +
                         $"Y0: {f.Y0:X4}  Y1: {f.Y1:X4}  " +
                         $"Flags: {(ushort)f.Flags:X4}  Style: {f.StyleIndex:X4}  Building: {f.Building:X4}  " +
                         $"Storey: {f.Storey:X4}  FHeight: {f.FHeight:X2}";
            TxtRawHex.Text = hex;
        }

        #region Event Handlers matching XAML

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;
            _hasChanges = true;
            BtnSave.IsEnabled = true;
            UpdatePreview();
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            string newText = textBox?.Text.Insert(textBox.SelectionStart, e.Text) ?? e.Text;
            e.Handled = !_digitsOnly.IsMatch(newText) || (int.TryParse(newText, out int val) && val > 255);
        }

        private void SignedNumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            string newText = textBox?.Text.Insert(textBox.SelectionStart, e.Text) ?? e.Text;

            // Allow minus sign at start, digits elsewhere
            if (e.Text == "-" && textBox?.SelectionStart == 0 && !textBox.Text.Contains("-"))
            {
                e.Handled = false;
                return;
            }

            e.Handled = !_signedDigitsOnly.IsMatch(newText);
            if (!e.Handled && newText != "-" && newText != "")
            {
                if (int.TryParse(newText, out int val))
                    e.Handled = val < -32768 || val > 32767;
            }
        }

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void BtnAutoSegments_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseCurrentValues(out byte x0, out byte z0, out short y0,
                                        out byte x1, out byte z1, out short y1,
                                        out _, out _, out _, out _))
                return;

            double dx = (x1 - x0) * 256.0;
            double dz = (z1 - z0) * 256.0;
            double dy = y1 - y0;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            int segments = CableAdder.CalculateSegmentCount(length);
            TxtSegments.Text = segments.ToString();
        }

        private void BtnAutoAngles_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseCurrentValues(out byte x0, out byte z0, out short y0,
                                        out byte x1, out byte z1, out short y1,
                                        out byte segments, out _, out _, out _))
                return;

            double dx = (x1 - x0) * 256.0;
            double dz = (z1 - z0) * 256.0;
            double dy = y1 - y0;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            var (step1, step2) = CableAdder.CalculateStepAngles(length, segments);
            TxtStepAngle1.Text = step1.ToString();
            TxtStepAngle2.Text = step2.ToString();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (SaveChanges())
            {
                RefreshBuildingsTab();
                DialogResult = true;
                Close();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete cable #{_facetId1}?\n\nThis will zero out the facet data.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            // Zero out the facet by setting type to 0
            var svc = MapDataService.Instance;
            var accessor = new BuildingsAccessor(svc);

            if (accessor.TryGetFacetOffset(_facetId1, out int facetOffset))
            {
                svc.Edit(bytes =>
                {
                    // Zero the entire facet
                    for (int i = 0; i < DFACET_SIZE; i++)
                        bytes[facetOffset + i] = 0;
                });

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    mainVm.StatusMessage = $"Cable #{_facetId1} deleted.";

                RefreshBuildingsTab();
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Failed to locate facet data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Parsing and Validation

        private bool TryParseCurrentValues(out byte x0, out byte z0, out short y0,
                                           out byte x1, out byte z1, out short y1,
                                           out byte segments, out short stepAngle1, out short stepAngle2, out byte fHeight)
        {
            x0 = z0 = x1 = z1 = segments = fHeight = 0;
            y0 = y1 = stepAngle1 = stepAngle2 = 0;

            if (!byte.TryParse(TxtX0.Text, out x0)) return false;
            if (!byte.TryParse(TxtZ0.Text, out z0)) return false;
            if (!short.TryParse(TxtY0.Text, out y0)) return false;
            if (!byte.TryParse(TxtX1.Text, out x1)) return false;
            if (!byte.TryParse(TxtZ1.Text, out z1)) return false;
            if (!short.TryParse(TxtY1.Text, out y1)) return false;
            if (!byte.TryParse(TxtSegments.Text, out segments)) return false;
            if (!short.TryParse(TxtStepAngle1.Text, out stepAngle1)) return false;
            if (!short.TryParse(TxtStepAngle2.Text, out stepAngle2)) return false;
            if (!byte.TryParse(TxtFHeight.Text, out fHeight)) return false;

            return true;
        }

        #endregion

        #region Preview

        private void UpdatePreview()
        {
            if (PreviewCanvas == null) return;

            PreviewCanvas.Children.Clear();

            if (!TryParseCurrentValues(out byte x0, out byte z0, out short y0,
                                        out byte x1, out byte z1, out short y1,
                                        out byte segments, out short stepAngle1, out short stepAngle2, out _))
            {
                return;
            }

            // Calculate world-space coordinates
            double worldX0 = (128 - x0) * 256.0;
            double worldZ0 = (128 - z0) * 256.0;
            double worldX1 = (128 - x1) * 256.0;
            double worldZ1 = (128 - z1) * 256.0;

            // Calculate length for display
            double dx = worldX1 - worldX0;
            double dz = worldZ1 - worldZ0;
            double dy = y1 - y0;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // Update info displays
            int tilesDx = Math.Abs(x1 - x0);
            int tilesDz = Math.Abs(z1 - z0);
            TxtCalculatedLength.Text = $"{Math.Max(tilesDx, tilesDz)} tiles ({length:F0} units)";
            TxtHeightDelta.Text = $"{y1 - y0}";

            // Scale to fit canvas
            double canvasWidth = PreviewCanvas.ActualWidth > 0 ? PreviewCanvas.ActualWidth : 300;
            double canvasHeight = PreviewCanvas.ActualHeight > 0 ? PreviewCanvas.ActualHeight : 150;

            double margin = 30;
            double drawWidth = canvasWidth - margin * 2;
            double drawHeight = canvasHeight - margin * 2;

            // Draw catenary approximation (side view)
            DrawCatenaryPreview(margin, margin, drawWidth, drawHeight,
                                y0, y1, segments, stepAngle1, stepAngle2, length);
        }

        private void DrawCatenaryPreview(double left, double top, double width, double height,
                                         short y0, short y1, int segments, short step1, short step2, double length)
        {
            if (segments < 2) segments = 2;

            // Draw reference line (straight)
            var refLine = new Line
            {
                X1 = left,
                Y1 = top + height / 2 - (y0 / 20.0),
                X2 = left + width,
                Y2 = top + height / 2 - (y1 / 20.0),
                Stroke = Brushes.Gray,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            PreviewCanvas.Children.Add(refLine);

            // Calculate catenary points
            // The step angles control the "sag" - we simulate this
            double sagAmount = Math.Abs(step1 + step2) / 20.0;
            sagAmount = Math.Max(sagAmount, 5); // minimum visible sag

            var points = new Point[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                double x = left + t * width;

                // Linear interpolation of Y
                double baseY = y0 + t * (y1 - y0);

                // Add catenary sag (parabolic approximation)
                double sag = sagAmount * 4 * t * (1 - t); // maximum at middle

                double y = top + height / 2 - (baseY / 20.0) + sag;
                points[i] = new Point(x, y);
            }

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

            // Draw endpoint markers
            var startMarker = new Ellipse { Width = 10, Height = 10, Fill = Brushes.LimeGreen };
            Canvas.SetLeft(startMarker, points[0].X - 5);
            Canvas.SetTop(startMarker, points[0].Y - 5);
            PreviewCanvas.Children.Add(startMarker);

            var endMarker = new Ellipse { Width = 10, Height = 10, Fill = Brushes.Orange };
            Canvas.SetLeft(endMarker, points[segments].X - 5);
            Canvas.SetTop(endMarker, points[segments].Y - 5);
            PreviewCanvas.Children.Add(endMarker);

            // Draw labels
            var startLabel = new TextBlock { Text = $"({_originalFacet.X0},{_originalFacet.Z0})", FontSize = 10, Foreground = Brushes.White };
            Canvas.SetLeft(startLabel, points[0].X - 15);
            Canvas.SetTop(startLabel, points[0].Y + 8);
            PreviewCanvas.Children.Add(startLabel);

            var endLabel = new TextBlock { Text = $"({_originalFacet.X1},{_originalFacet.Z1})", FontSize = 10, Foreground = Brushes.White };
            Canvas.SetLeft(endLabel, points[segments].X - 15);
            Canvas.SetTop(endLabel, points[segments].Y + 8);
            PreviewCanvas.Children.Add(endLabel);
        }

        #endregion

        #region Save

        private bool SaveChanges()
        {
            if (!TryParseCurrentValues(out byte x0, out byte z0, out short y0,
                                        out byte x1, out byte z1, out short y1,
                                        out byte segments, out short stepAngle1, out short stepAngle2, out byte fHeight))
            {
                MessageBox.Show("Please enter valid values for all fields.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (segments < 2)
            {
                MessageBox.Show("Segments must be at least 2.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Write directly to map data using raw bytes
            var svc = MapDataService.Instance;
            var accessor = new BuildingsAccessor(svc);

            if (!accessor.TryGetFacetOffset(_facetId1, out int facetOffset))
            {
                MessageBox.Show("Failed to locate facet in map data.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            svc.Edit(bytes =>
            {
                // Write the facet data
                bytes[facetOffset + OFF_TYPE] = (byte)FacetType.Cable; // 9
                bytes[facetOffset + OFF_HEIGHT] = segments;
                bytes[facetOffset + OFF_X0] = x0;
                bytes[facetOffset + OFF_X1] = x1;

                // Y0 and Y1 are signed 16-bit little-endian
                bytes[facetOffset + OFF_Y0] = (byte)(y0 & 0xFF);
                bytes[facetOffset + OFF_Y0 + 1] = (byte)((y0 >> 8) & 0xFF);
                bytes[facetOffset + OFF_Y1] = (byte)(y1 & 0xFF);
                bytes[facetOffset + OFF_Y1 + 1] = (byte)((y1 >> 8) & 0xFF);

                bytes[facetOffset + OFF_Z0] = z0;
                bytes[facetOffset + OFF_Z1] = z1;

                // Flags - keep as unclimbable for cables
                ushort flags = (ushort)FacetFlags.Unclimbable;
                bytes[facetOffset + OFF_FLAGS] = (byte)(flags & 0xFF);
                bytes[facetOffset + OFF_FLAGS + 1] = (byte)((flags >> 8) & 0xFF);

                // StyleIndex = step_angle1 (as unsigned representation of signed value)
                ushort step1U = unchecked((ushort)stepAngle1);
                bytes[facetOffset + OFF_STYLE] = (byte)(step1U & 0xFF);
                bytes[facetOffset + OFF_STYLE + 1] = (byte)((step1U >> 8) & 0xFF);

                // Building = step_angle2 (as unsigned representation of signed value)
                ushort step2U = unchecked((ushort)stepAngle2);
                bytes[facetOffset + OFF_BUILDING] = (byte)(step2U & 0xFF);
                bytes[facetOffset + OFF_BUILDING + 1] = (byte)((step2U >> 8) & 0xFF);

                // Storey = 0 for cables
                bytes[facetOffset + OFF_STOREY] = 0;
                bytes[facetOffset + OFF_STOREY + 1] = 0;

                // FHeight = mode/texture style
                bytes[facetOffset + OFF_FHEIGHT] = fHeight;

                Debug.WriteLine($"[CableFacetEditor] Saved cable #{_facetId1} at offset 0x{facetOffset:X}");
            });

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                mainVm.StatusMessage = $"Cable #{_facetId1} saved.";
            }

            return true;
        }

        #endregion

        #region Helpers

        private void RefreshBuildingsTab()
        {
            // Find and refresh the BuildingsTabViewModel
            if (Application.Current.MainWindow is Views.Core.MainWindow mainWindow)
            {
                // Try to find the BuildingsTab and refresh it
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

            // Also notify the map that bytes changed
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
    }
}