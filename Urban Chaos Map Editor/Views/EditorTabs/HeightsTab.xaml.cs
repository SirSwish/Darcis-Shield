// /Views/EditorTabs/HeightsTab.xaml.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;
using UrbanChaosMapEditor.ViewModels;

namespace UrbanChaosMapEditor.Views.EditorTabs
{
    public partial class HeightsTab : UserControl
    {
        private static readonly Regex _digits = new(@"^\d+$");
        private static readonly Regex _signedDigits = new(@"^-?\d+$");

        // Store detected roof shape for "Apply" button
        private RoofBuilder.ClosedShapeResult? _lastDetectedShape;
        private List<int>? _lastDetectedFacetIds;

        public HeightsTab()
        {
            InitializeComponent();
        }

        #region Terrain Height Input Validation

        private void Units_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !_digits.IsMatch(e.Text);
        }

        private void Units_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var text = e.DataObject.GetData(DataFormats.Text) as string ?? "";
                if (!_digits.IsMatch(text)) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        #endregion

        #region Area Height Tool (Drag Rectangle)

        private void AreaHeight_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var newText = textBox?.Text.Insert(textBox.SelectionStart, e.Text) ?? e.Text;
            e.Handled = !_signedDigits.IsMatch(newText);
        }

        private void AreaHeight_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var text = e.DataObject.GetData(DataFormats.Text) as string ?? "";
                if (!_signedDigits.IsMatch(text)) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        private void DragArea_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            // Force the tool; the actual drag logic lives in HeightsLayer
            vm.Map.SelectedTool = EditorTool.AreaSetHeight;

            Debug.WriteLine("[HeightsTab] AreaSetHeight tool selected (drag rectangle)");
        }

        #endregion

        #region Altitude Input Validation

        private void Altitude_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow digits and minus sign for signed values
            var textBox = sender as TextBox;
            var newText = textBox?.Text.Insert(textBox.SelectionStart, e.Text) ?? e.Text;
            e.Handled = !_signedDigits.IsMatch(newText);
        }

        private void Altitude_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var text = e.DataObject.GetData(DataFormats.Text) as string ?? "";
                if (!_signedDigits.IsMatch(text)) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        #endregion

        #region Altitude Tool Handlers

        private void SetAltitude_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.Map.SelectedTool = EditorTool.SetAltitude;
                Debug.WriteLine("[HeightsTab] Set Altitude tool selected");
            }
        }

        private void SampleAltitude_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.Map.SelectedTool = EditorTool.SampleAltitude;
                Debug.WriteLine("[HeightsTab] Sample Altitude tool selected");
            }
        }

        private void ResetAltitude_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.Map.SelectedTool = EditorTool.ResetAltitude;
                Debug.WriteLine("[HeightsTab] Reset Altitude tool selected");
            }
        }

        #endregion

        #region Roof Building Handlers

        private void BuildRoof_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            // Get selected facets from the ViewModel
            var selectedFacetIds = vm.Map.GetSelectedFacetIds();

            if (selectedFacetIds == null || !selectedFacetIds.Any())
            {
                ShowAnalysisResult("No facets selected. Select facets in the Buildings tab first, or use Detect to find closed shapes near a point.");
                return;
            }

            try
            {
                // Create the services using singleton MapDataService
                var mapData = MapDataService.Instance;
                if (!mapData.IsLoaded) return;

                var buildings = new BuildingsAccessor(mapData);
                var altitude = new AltitudeAccessor(mapData);
                var roofBuilder = new RoofBuilder(mapData, buildings, altitude);

                // First analyze to show what will be done
                var result = roofBuilder.AnalyzeClosedShape(selectedFacetIds);

                if (!result.IsClosedShape)
                {
                    ShowAnalysisResult($"Cannot build roof: {result.ErrorMessage ?? "Selected facets do not form a closed shape."}\n\nTry selecting all facets that form a complete rectangle or polygon.");
                    return;
                }

                // Build the roof
                int modified = roofBuilder.BuildRoof(selectedFacetIds);

                ShowAnalysisResult($"Roof built successfully!\n\n" +
                    $"Modified {modified} cells\n" +
                    $"Bounds: ({result.MinX},{result.MinZ}) to ({result.MaxX},{result.MaxZ})\n" +
                    $"Altitude set to: {result.TopAltitude}");

                // Refresh the map display
                vm.Map.RefreshAltitudeLayer();
            }
            catch (Exception ex)
            {
                ShowAnalysisResult($"Error building roof: {ex.Message}");
                Debug.WriteLine($"[HeightsTab] BuildRoof error: {ex}");
            }
        }

        private void DetectRoof_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.Map.SelectedTool = EditorTool.DetectRoof;
                ShowAnalysisResult("Click in the map to detect closed shapes formed by nearby facets.");
                Debug.WriteLine("[HeightsTab] Detect Roof tool selected");
            }
        }

        /// <summary>
        /// Called by the map view when a point is clicked in DetectRoof mode.
        /// </summary>
        public void OnDetectRoofClick(int blockX, int blockZ)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            try
            {
                var mapData = MapDataService.Instance;
                if (!mapData.IsLoaded) return;

                var buildings = new BuildingsAccessor(mapData);
                var altitude = new AltitudeAccessor(mapData);
                var roofBuilder = new RoofBuilder(mapData, buildings, altitude);

                var result = roofBuilder.FindClosedShapeNearPoint(blockX, blockZ, searchRadius: 10);

                _lastDetectedShape = result;
                _lastDetectedFacetIds = result.Edges.Select(e => e.FacetId).Distinct().ToList();

                if (result.IsClosedShape)
                {
                    ShowAnalysisResult(
                        $"Closed shape detected!\n\n" +
                        $"Facets: {string.Join(", ", _lastDetectedFacetIds.Take(10))}{(_lastDetectedFacetIds.Count > 10 ? "..." : "")}\n" +
                        $"Bounds: ({result.MinX},{result.MinZ}) to ({result.MaxX},{result.MaxZ})\n" +
                        $"Interior cells: {result.InteriorCells.Count}\n" +
                        $"Top altitude: {result.TopAltitude}",
                        showApplyButton: true);
                }
                else
                {
                    ShowAnalysisResult($"No closed shape found at ({blockX},{blockZ}).\n\n{result.ErrorMessage ?? "Try clicking closer to a building."}");
                }
            }
            catch (Exception ex)
            {
                ShowAnalysisResult($"Error detecting shape: {ex.Message}");
                Debug.WriteLine($"[HeightsTab] DetectRoof error: {ex}");
            }
        }

        private void ApplyDetectedRoof_Click(object sender, RoutedEventArgs e)
        {
            if (_lastDetectedShape == null || _lastDetectedFacetIds == null || !_lastDetectedShape.IsClosedShape)
            {
                ShowAnalysisResult("No valid detected shape to apply.");
                return;
            }

            if (DataContext is not MainWindowViewModel vm) return;

            try
            {
                var mapData = MapDataService.Instance;
                if (!mapData.IsLoaded) return;

                var buildings = new BuildingsAccessor(mapData);
                var altitude = new AltitudeAccessor(mapData);
                var roofBuilder = new RoofBuilder(mapData, buildings, altitude);

                int modified = roofBuilder.BuildRoof(_lastDetectedFacetIds);

                ShowAnalysisResult($"Roof applied!\n\nModified {modified} cells to altitude {_lastDetectedShape.TopAltitude}.");

                // Clear the detection
                _lastDetectedShape = null;
                _lastDetectedFacetIds = null;

                // Refresh the map display
                vm.Map.RefreshAltitudeLayer();
            }
            catch (Exception ex)
            {
                ShowAnalysisResult($"Error applying roof: {ex.Message}");
                Debug.WriteLine($"[HeightsTab] ApplyDetectedRoof error: {ex}");
            }
        }

        #endregion

        #region UI Helpers

        private void ShowAnalysisResult(string text, bool showApplyButton = false)
        {
            AnalysisResultBorder.Visibility = Visibility.Visible;
            AnalysisResultText.Text = text;
            ApplyDetectedRoofButton.Visibility = showApplyButton ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HideAnalysisResult()
        {
            AnalysisResultBorder.Visibility = Visibility.Collapsed;
            _lastDetectedShape = null;
            _lastDetectedFacetIds = null;
        }

        #endregion
    }
}