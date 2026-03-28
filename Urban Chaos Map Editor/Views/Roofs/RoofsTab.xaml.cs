// Views/Roofs/RoofsTab.xaml.cs
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Models.Roofs;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.ViewModels.Roofs;
using UrbanChaosMapEditor.Views.Core;
using UrbanChaosMapEditor.Views.Roofs.Dialogs;

namespace UrbanChaosMapEditor.Views.Roofs
{
    public partial class RoofsTab : UserControl
    {
        private static readonly Regex _signedDigits = new(@"^-?[0-9]*$");
        private bool _isWaitingForWalkableDrawing;

        public RoofsTab()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                var vm = DataContext as RoofsTabViewModel;
                if (vm == null) return;

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    vm.MapViewModel = mainVm.Map;

                if (MapDataService.Instance.IsLoaded)
                    vm.Refresh();
            };
        }

        // ====================================================================
        // Refresh
        // ====================================================================

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as RoofsTabViewModel)?.Refresh();
        }

        // ====================================================================
        // Walkable Selection
        // ====================================================================

        private void WalkablesList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not RoofsTabViewModel vm) return;
            if (sender is not ListView lv) return;

            vm.HandleWalkableSelection(lv.SelectedItem);

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
            {
                shell.Map.SelectedWalkableId1 =
                    (lv.SelectedItem as WalkableVM)?.WalkableId1 ?? 0;
            }
        }

        private void WalkablesList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not RoofsTabViewModel vm) return;
            if (WalkablesList.SelectedItem is not WalkableVM wrow) return;

            if (!MapDataService.Instance.TryGetWalkables(out var walkables, out var roofFaces4))
                return;

            int id1 = wrow.WalkableId1;
            if (id1 < 1 || id1 >= walkables.Length) return;

            var dlg = new WalkablePreviewWindow(id1, walkables[id1], roofFaces4)
            {
                Owner = Application.Current.MainWindow
            };
            dlg.Show();
            e.Handled = true;
        }

        // ====================================================================
        // RoofFace4 Selection
        // ====================================================================

        private void RoofFaces4List_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not RoofsTabViewModel vm) return;
            if (RoofFaces4List.SelectedItem is not RoofFace4VM rrow) return;

            if (!MapDataService.Instance.TryGetWalkables(out _, out var roofFaces4))
                return;

            int idx = rrow.FaceId;
            if (idx < 0 || idx >= roofFaces4.Length) return;

            // Pass the walkable ID for "Apply to All" support
            int walkableId1 = vm.SelectedWalkable?.WalkableId1 ?? 0;

            var dlg = new RoofFace4PreviewWindow(idx, roofFaces4[idx], walkableId1)
            {
                Owner = Application.Current.MainWindow
            };
            dlg.Show();
            e.Handled = true;
        }

        // ====================================================================
        // Add Walkable
        // ====================================================================

        private void BtnAddWalkable_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not RoofsTabViewModel vm) return;

            int buildingId = vm.SelectedBuildingId;
            if (buildingId <= 0)
            {
                MessageBox.Show("Enter a building ID in the filter box first.",
                    "Add Walkable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Application.Current.MainWindow?.DataContext is not MainWindowViewModel mainVm)
            {
                MessageBox.Show("Cannot access main window.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            mainVm.Map.IsDrawingWalkable = true;
            _isWaitingForWalkableDrawing = true;

            mainVm.StatusMessage = "Click and drag on the map to define the walkable region, then release to set height...";

            var mapView = FindMapViewInVisualTree();
            if (mapView != null)
                mapView.WalkableDrawingCompleted += OnWalkableDrawingCompleted;
        }

        private void OnWalkableDrawingCompleted(object? sender, EventArgs e)
        {
            if (!_isWaitingForWalkableDrawing) return;
            _isWaitingForWalkableDrawing = false;

            if (sender is MapView mapView)
                mapView.WalkableDrawingCompleted -= OnWalkableDrawingCompleted;

            if (DataContext is not RoofsTabViewModel vm) return;
            if (Application.Current.MainWindow?.DataContext is not MainWindowViewModel mainVm) return;

            var mapVm = mainVm.Map;
            int buildingId = vm.SelectedBuildingId;

            var rect = mapVm.GetWalkableSelectionRect();
            if (!rect.HasValue)
            {
                mapVm.ClearWalkableSelection();
                return;
            }

            var dialog = new AddWalkableWindow
            {
                Owner = Window.GetWindow(this),
                BuildingId1 = buildingId,
                BuildingName = $"Building #{buildingId}",
                TileX1 = rect.Value.MinX,
                TileZ1 = rect.Value.MinY,
                TileX2 = rect.Value.MaxX,
                TileZ2 = rect.Value.MaxY
            };

            if (dialog.ShowDialog() == true)
            {
                int gameX1 = (MapConstants.TilesPerSide - 1) - rect.Value.MaxX;
                int gameZ1 = (MapConstants.TilesPerSide - 1) - rect.Value.MaxY;
                int gameX2 = (MapConstants.TilesPerSide - 1) - rect.Value.MinX + 1;
                int gameZ2 = (MapConstants.TilesPerSide - 1) - rect.Value.MinY + 1;

                var template = new WalkableTemplate
                {
                    BuildingId1 = buildingId,
                    X1 = (byte)Math.Min(gameX1, gameX2),
                    Z1 = (byte)Math.Min(gameZ1, gameZ2),
                    X2 = (byte)Math.Max(gameX1, gameX2),
                    Z2 = (byte)Math.Max(gameZ1, gameZ2),
                    WorldY = dialog.WorldY,
                    StoreyY = dialog.StoreyY
                };

                var adder = new WalkableAdder(MapDataService.Instance);
                var result = adder.TryAddWalkable(template);

                if (result.Success)
                {
                    mainVm.StatusMessage = $"Added walkable #{result.WalkableId1} to Building #{buildingId}";
                    vm.Refresh();
                    RoofsChangeBus.Instance.NotifyChanged();
                    BuildingsChangeBus.Instance.NotifyChanged();
                }
                else
                {
                    MessageBox.Show($"Failed to add walkable:\n\n{result.Error}",
                        "Add Walkable Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            mapVm.ClearWalkableSelection();
        }

        // ====================================================================
        // Delete Walkable
        // ====================================================================

        private void BtnDeleteWalkable_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not RoofsTabViewModel vm) return;

            if (WalkablesList.SelectedItem is not WalkableVM walkable)
            {
                MessageBox.Show("Please select a walkable from the list first.",
                    "Delete Walkable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int walkableId = walkable.WalkableId1;

            var confirm = MessageBox.Show(
                $"Delete walkable #{walkableId} from Building #{walkable.BuildingId}?\n\n" +
                "This will also delete any associated RoofFace4 entries.",
                "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            var deleter = new WalkableDeleter(MapDataService.Instance);
            var result = deleter.DeleteWalkable(walkableId);

            if (result.Success)
            {
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                {
                    string msg = $"Deleted walkable #{walkableId}";
                    if (result.RoofFacesDeleted > 0)
                        msg += $" and {result.RoofFacesDeleted} RoofFace4 entries";
                    mainVm.StatusMessage = msg;
                }

                vm.Refresh();
                RoofsChangeBus.Instance.NotifyChanged();
                BuildingsChangeBus.Instance.NotifyChanged();
            }
            else
            {
                MessageBox.Show($"Failed to delete walkable:\n\n{result.Error}",
                    "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ====================================================================
        // Add RoofFace4
        // ====================================================================

        private void BtnAddRoofFace4_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not RoofsTabViewModel vm) return;

            if (WalkablesList.SelectedItem is not WalkableVM selectedWalkable)
            {
                MessageBox.Show("Please select a walkable region first.\n\n" +
                               "RoofFace4 entries define roof geometry within a walkable region.",
                    "Add Roof Face", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int walkableId1 = selectedWalkable.WalkableId1;
            int buildingId1 = selectedWalkable.BuildingId;

            if (!MapDataService.Instance.TryGetWalkables(out var walkables, out _))
            {
                MessageBox.Show("Cannot access walkable data.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (walkableId1 < 1 || walkableId1 >= walkables.Length)
            {
                MessageBox.Show("Invalid walkable selection.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var walkable = walkables[walkableId1];

            var dialog = new AddRoofFace4Dialog(walkableId1, buildingId1, walkable)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.WasConfirmed)
            {
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    mainVm.StatusMessage = $"Added {dialog.AddedCount} roof face(s) to walkable #{walkableId1}";

                vm.Refresh();
                RoofsChangeBus.Instance.NotifyChanged();
            }
        }

        // ====================================================================
        // Delete RoofFace4
        // ====================================================================

        private void BtnDeleteRoofFace4_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not RoofsTabViewModel vm) return;

            if (RoofFaces4List.SelectedItem is not RoofFace4VM selectedRf4)
            {
                MessageBox.Show("Please select a roof face to delete.",
                    "Delete Roof Face", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int roofFace4Id = selectedRf4.Index;

            var confirmResult = MessageBox.Show(
                $"Delete roof face #{roofFace4Id}?\n\n" +
                $"Position: RX={selectedRf4.RX}, RZ={selectedRf4.RZ}\n" +
                $"Altitude: Y={selectedRf4.Y}",
                "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            var adder = new RoofFace4Adder(MapDataService.Instance);
            var result = adder.TryDeleteRoofFace4(roofFace4Id);

            if (result.IsSuccess)
            {
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    mainVm.StatusMessage = $"Deleted roof face #{roofFace4Id}";

                vm.Refresh();
                RoofsChangeBus.Instance.NotifyChanged();
            }
            else
            {
                MessageBox.Show($"Failed to delete roof face:\n\n{result.ErrorMessage}",
                    "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ====================================================================
        // Altitude Tool Handlers
        // ====================================================================

        private void Altitude_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var newText = textBox?.Text.Insert(textBox.SelectionStart, e.Text) ?? e.Text;
            e.Handled = !_signedDigits.IsMatch(newText);
        }

        private void SetAltitude_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow?.DataContext is not MainWindowViewModel mainVm)
                return;

            if (!int.TryParse(AltitudeInput.Text, out int altitude))
                altitude = 0;

            mainVm.Map.TargetAltitude = altitude;
            mainVm.Map.SelectedTool = EditorTool.SetAltitude;

            Debug.WriteLine($"[RoofsTab] Set Altitude tool selected, TargetAltitude={mainVm.Map.TargetAltitude}");
        }

        private void SampleAltitude_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow?.DataContext is not MainWindowViewModel mainVm)
                return;

            mainVm.Map.SelectedTool = EditorTool.SampleAltitude;
            Debug.WriteLine("[RoofsTab] Sample Altitude tool selected");
        }

        private void AltitudeInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb || RawAltitudeDisplay == null)
                return;

            if (int.TryParse(tb.Text, out int altitude))
            {
                RawAltitudeDisplay.Text = (altitude >> 3).ToString();

                // If Set Altitude tool is already active, update TargetAltitude immediately
                // so the next map click uses the new value without re-clicking "Set Alt".
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm &&
                    mainVm.Map.SelectedTool == EditorTool.SetAltitude)
                {
                    mainVm.Map.TargetAltitude = altitude;
                }
            }
            else
            {
                RawAltitudeDisplay.Text = "0";
            }
        }

        private void ResetAltitude_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                mainVm.Map.SelectedTool = EditorTool.ResetAltitude;
                Debug.WriteLine("[RoofsTab] Reset Altitude tool selected");
            }
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private MapView? FindMapViewInVisualTree()
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null) return null;
            return FindVisualChild<MapView>(mainWindow);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found) return found;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}