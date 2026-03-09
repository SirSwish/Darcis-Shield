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

            vm.Map.SelectedTool = EditorTool.AreaSetHeight;
            vm.Map.ShowHeights = true;
            vm.StatusMessage = $"Area height tool selected. Drag on the map to set vertices to {vm.Map.AreaSetHeightValue}.";

            Debug.WriteLine("[HeightsTab] AreaSetHeight tool selected (drag rectangle)");
        }

        #endregion

    }
}