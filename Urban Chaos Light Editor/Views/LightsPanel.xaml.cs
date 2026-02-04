using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UrbanChaosLightEditor.ViewModels;
using UrbanChaosLightEditor.Views.Dialogs;

namespace UrbanChaosLightEditor.Views
{
    public partial class LightsPanel : UserControl
    {
        public LightsPanel()
        {
            InitializeComponent();
        }

        private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

        private void LightsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (e.Key == Key.C) { CopyLight_Click(sender, new RoutedEventArgs()); e.Handled = true; return; }
                if (e.Key == Key.V) { PasteLight_Click(sender, new RoutedEventArgs()); e.Handled = true; return; }
            }
            if (e.Key == Key.Delete) { DeleteLight_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        }

        private void LightsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.HasSelectedLight == true)
                EditLight_Click(sender, new RoutedEventArgs());
        }

        private void AddLight_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null || !vm.IsLightsLoaded) return;  // Changed from IsMapLoaded

            // Show dialog to set light properties before placement
            var dlg = new AddEditLightDialog(
                initialHeight: 0,
                initialRange: 128,
                initialRed: 0,
                initialGreen: 0,
                initialBlue: 0)
            {
                Owner = Window.GetWindow(this),
                Title = "Add Light"
            };

            if (dlg.ShowDialog() == true)
            {
                // Store the placement parameters
                vm.PlacementRange = (byte)dlg.ResultRange;
                vm.PlacementRed = (sbyte)dlg.ResultRed;
                vm.PlacementGreen = (sbyte)dlg.ResultGreen;
                vm.PlacementBlue = (sbyte)dlg.ResultBlue;
                vm.PlacementY = dlg.ResultHeight;

                // Enter placement mode
                vm.IsAddingLight = true;
            }
        }

        private void EditLight_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null || vm.SelectedLightIndex < 0) return;

            var light = vm.SelectedLight;
            if (light == null) return;

            var dlg = new AddEditLightDialog(
                initialHeight: light.Y,
                initialRange: light.Range,
                initialRed: light.Red,
                initialGreen: light.Green,
                initialBlue: light.Blue)
            {
                Owner = Window.GetWindow(this),
                Title = "Edit Light"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var acc = new Services.LightsAccessor(Services.LightsDataService.Instance);
                    var entry = acc.ReadEntry(vm.SelectedLightIndex);

                    entry.Range = (byte)dlg.ResultRange;
                    entry.Red = (sbyte)dlg.ResultRed;
                    entry.Green = (sbyte)dlg.ResultGreen;
                    entry.Blue = (sbyte)dlg.ResultBlue;
                    entry.Y = dlg.ResultHeight;

                    acc.WriteEntry(vm.SelectedLightIndex, entry);
                    vm.StatusMessage = $"Updated light #{vm.SelectedLightIndex}.";
                }
                catch (Exception ex)
                {
                    vm.StatusMessage = $"Failed to edit light: {ex.Message}";
                }
            }
        }

        private void DeleteLight_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            vm.DeleteLightCommand.Execute(null);
        }

        private void CopyLight_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            vm.CopyLightCommand.Execute(null);
        }

        private void PasteLight_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            vm.PasteLightCommand.Execute(null);
        }
    }
}