// /Views/EditorTabs/HeightsTab.xaml.cs
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Views.Heights.Dialogs;

using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Heights
{
    public partial class HeightsTab : UserControl
    {
        private static readonly Regex _digits = new(@"^\d+$");
        private static readonly Regex _signedDigits = new(@"^-?\d+$");

        // Store detected roof shape for "Apply" button
        private RoofBuilder.ClosedShapeResult? _lastDetectedShape;
        private List<int>? _lastDetectedFacetIds;

        // PAP flag checkboxes in bit order for easy iteration
        private CheckBox[]? _papFlagCheckBoxes;

        public HeightsTab()
        {
            InitializeComponent();
            Loaded += HeightsTab_Loaded;
        }

        private void HeightsTab_Loaded(object sender, RoutedEventArgs e)
        {
            _papFlagCheckBoxes = new CheckBox[]
            {
                PapShadow1, PapShadow2, PapShadow3, PapReflective,
                PapHidden,  PapSinkSquare, PapSinkPoint, PapNoUpper,
                PapNoGo,    PapRoofExists, PapZone1, PapZone2,
                PapZone3,   PapZone4, PapFlatRoof, PapWater
            };

            // Wire up Checked/Unchecked to update the hex summary
            foreach (var cb in _papFlagCheckBoxes)
            {
                cb.Checked += PapFlag_Changed;
                cb.Unchecked += PapFlag_Changed;
            }
        }

        /// <summary>
        /// Reads the selected flags from the 16 checkboxes as a ushort bitmask.
        /// </summary>
        public ushort GetSelectedPapFlags()
        {
            if (_papFlagCheckBoxes == null) return 0;

            ushort flags = 0;
            for (int bit = 0; bit < 16; bit++)
            {
                if (_papFlagCheckBoxes[bit].IsChecked == true)
                    flags |= (ushort)(1 << bit);
            }
            return flags;
        }

        /// <summary>
        /// Whether the user wants to SET (OR) or CLEAR (AND NOT) the flags.
        /// </summary>
        public bool IsClearMode => PapModeClear.IsChecked == true;

        private void PapFlag_Changed(object sender, RoutedEventArgs e)
        {
            ushort flags = GetSelectedPapFlags();
            if (TxtPapFlagsSummary != null)
                TxtPapFlagsSummary.Text = $"Selected: 0x{flags:X4}";
        }

        private void PapFlagsSelectArea_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            ushort flags = GetSelectedPapFlags();
            if (flags == 0)
            {
                TxtPapFlagsStatus.Text = "No flags selected � tick at least one flag above.";
                return;
            }

            bool clearMode = IsClearMode;

            // Store the flags and mode on the MapViewModel so the canvas drag handler can use them
            vm.Map.PapFlagsMask = flags;
            vm.Map.PapFlagsClearMode = clearMode;
            vm.Map.SelectedTool = EditorTool.AreaSetPapFlags;
            vm.Map.ShowCellFlags = true;

            string modeStr = clearMode ? "CLEAR" : "SET";
            vm.StatusMessage = $"PAP flags tool: drag on map to {modeStr} flags 0x{flags:X4}.";
            TxtPapFlagsStatus.Text = $"Drag on map to {modeStr} flags 0x{flags:X4}. Right-click to cancel.";

            Debug.WriteLine($"[HeightsTab] AreaSetPapFlags tool selected, flags=0x{flags:X4}, mode={modeStr}");
        }

        private void NewStamp_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new HeightStampCreatorDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                // Auto-select the new stamp and activate the tool
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.Map.SelectedStamp = dialog.Result;
                    vm.Map.SelectedTool = EditorTool.StampHeight;
                    vm.Map.ShowHeights = true;
                    vm.StatusMessage = $"Stamp '{dialog.Result.Name}' created. Click on the map to apply.";
                }
            }
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

            vm.Map.SelectedTool = EditorTool.AreaSetHeight;
            vm.Map.ShowHeights = true;
            vm.StatusMessage = $"Area height tool selected. Drag on the map to set vertices to {vm.Map.AreaSetHeightValue}.";

            Debug.WriteLine("[HeightsTab] AreaSetHeight tool selected (drag rectangle)");
        }

        private void RandomizeArea_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            vm.Map.SelectedTool = EditorTool.RandomizeHeightArea;
            vm.Map.ShowHeights = true;
            vm.StatusMessage = "Randomise area: drag a rectangle on the map to apply fractal terrain to just that region.";

            Debug.WriteLine("[HeightsTab] RandomizeHeightArea tool selected");
        }

        #endregion

    }
}