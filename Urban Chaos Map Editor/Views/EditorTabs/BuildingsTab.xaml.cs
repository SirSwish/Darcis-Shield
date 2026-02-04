// Views/EditorTabs/BuildingsTab.xaml.cs
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;
using UrbanChaosMapEditor.ViewModels;
using UrbanChaosMapEditor.Views.Dialogs.Buildings;

namespace UrbanChaosMapEditor.Views.EditorTabs
{
    public partial class BuildingsTab : UserControl
    {
        private bool _isWaitingForWalkableDrawing;

        public BuildingsTab()
        {
            InitializeComponent();
            Loaded += (_, __) => (DataContext as BuildingsTabViewModel)?.Refresh();
        }

        #region Buildings List Handlers

        private void BuildingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            if (sender is ListBox lb && lb.SelectedItem is BuildingsTabViewModel.BuildingVM building)
            {
                vm.HandleBuildingTreeSelection(building);

                // Auto-select first facet type if available
                if (vm.SelectedBuildingFacetGroups?.Count > 0)
                {
                    vm.SelectedFacetTypeGroup = vm.SelectedBuildingFacetGroups[0];
                }
            }

            // Clear walkable selection on building change
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
            {
                shell.Map.SelectedWalkableId1 = 0;
            }
        }

        private void BuildingsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            if (BuildingsList.SelectedItem is BuildingsTabViewModel.BuildingVM building)
            {
                OpenBuildingPreview(building, vm);
                e.Handled = true;
            }
        }

        private void BuildingsList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Deselect building on right-click
            if (DataContext is BuildingsTabViewModel vm)
            {
                vm.SelectedBuilding = null;
                vm.SelectedBuildingId = 0;
                vm.SelectedFacetTypeGroup = null;
                vm.SelectedFacet = null;
            }

            // Clear the ListBox selection
            if (sender is ListBox lb)
            {
                lb.SelectedItem = null;
            }

            e.Handled = true;
        }

        #endregion

        #region Facet Types List Handlers

        private void FacetTypesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            if (sender is ListBox lb && lb.SelectedItem is BuildingsTabViewModel.FacetTypeGroupVM typeGroup)
            {
                // Auto-select first facet in the group if available
                if (typeGroup.Facets?.Count > 0)
                {
                    vm.SelectedFacet = typeGroup.Facets[0];
                }
            }
        }

        #endregion

        #region Facets List Handlers

        private void FacetsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            if (sender is ListBox lb && lb.SelectedItem is BuildingsTabViewModel.FacetVM facet)
            {
                vm.HandleTreeSelection(facet);
            }
        }

        private void FacetsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FacetsList.SelectedItem is BuildingsTabViewModel.FacetVM fvm)
            {
                // Use the cable editor for cable facets
                if (fvm.Type == FacetType.Cable)
                    OpenCableEditor(fvm);
                else
                    OpenFacetPreview(fvm);

                e.Handled = true;
            }
        }

        #endregion

        #region Cable List Handlers

        private void CableList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            if (sender is ListView lv && lv.SelectedItem is BuildingsTabViewModel.FacetVM fvm)
            {
                // Set SelectedFacet so the delete button becomes visible
                vm.SelectedFacet = fvm;
                vm.HandleTreeSelection(fvm);
            }
            else if (sender is ListView lv2 && lv2.SelectedItem == null)
            {
                // Clear selection when nothing is selected
                vm.SelectedFacet = null;
            }
        }

        private void CableList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListView lv)
                return;

            if (lv.SelectedItem is BuildingsTabViewModel.FacetVM fvm)
            {
                // Always use the cable editor for cables
                if (fvm.Type == FacetType.Cable)
                    OpenCableEditor(fvm);
                else
                    OpenFacetPreview(fvm);

                e.Handled = true;
            }
        }

        #endregion

        #region Walkables and RoofFace4 Handlers

        private void WalkablesList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            if (sender is not ListView lv)
                return;

            vm.HandleWalkableSelection(lv.SelectedItem);

            // Bridge to the actual map VM the overlay is listening to
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
            {
                shell.Map.SelectedWalkableId1 =
                    (lv.SelectedItem as BuildingsTabViewModel.WalkableVM)?.WalkableId1 ?? 0;
            }
        }

        private void WalkablesList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            if (WalkablesList.SelectedItem is not BuildingsTabViewModel.WalkableVM wrow)
                return;

            // Get raw arrays
            if (!MapDataService.Instance.TryGetWalkables(out var walkables, out var roofFaces4))
                return;

            int id1 = wrow.WalkableId1;
            if (id1 < 1 || id1 >= walkables.Length)
                return;

            var dlg = new WalkablePreviewWindow(id1, walkables[id1], roofFaces4)
            {
                Owner = Application.Current.MainWindow
            };
            dlg.Show();

            e.Handled = true;
        }

        private void RoofFaces4List_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            if (RoofFaces4List.SelectedItem is not BuildingsTabViewModel.RoofFace4VM rrow)
                return;

            if (!MapDataService.Instance.TryGetWalkables(out _, out var roofFaces4))
                return;

            int idx = rrow.FaceId;
            if (idx < 0 || idx >= roofFaces4.Length)
                return;

            var dlg = new RoofFace4PreviewWindow(idx, roofFaces4[idx])
            {
                Owner = Application.Current.MainWindow
            };
            dlg.Show();

            e.Handled = true;
        }

        #endregion

        #region Preview Window Helpers

        private void OpenFacetPreview(BuildingsTabViewModel.FacetVM fvm)
        {
            var snap = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();
            int idx0 = fvm.FacetId1 - 1;
            if (idx0 < 0 || idx0 >= snap.Facets.Length) return;

            var df = snap.Facets[idx0];

            var dlg = new FacetPreviewWindow(df, fvm.FacetId1)
            {
                Owner = Application.Current.MainWindow
            };
            dlg.Show();
        }

        private void OpenBuildingPreview(BuildingsTabViewModel.BuildingVM bvm, BuildingsTabViewModel vm)
        {
            // Determine the 0-based index of this building in the VM
            int idx0 = vm.Buildings.IndexOf(bvm);
            if (idx0 < 0)
                return;

            int buildingId1 = idx0 + 1;

            var snap = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();
            if (buildingId1 < 1 || buildingId1 > snap.Buildings.Length)
                return;

            DBuildingRec building = snap.Buildings[buildingId1 - 1];

            var dlg = new BuildingPreviewWindow(building, buildingId1)
            {
                Owner = Application.Current.MainWindow
            };
            dlg.Show();
        }

        /// <summary>
        /// Opens the full Cable Editor window for editing cable parameters.
        /// </summary>
        private void OpenCableEditor(BuildingsTabViewModel.FacetVM fvm)
        {
            DFacetRec df = fvm.Raw;
            int facetId = fvm.FacetId1;

            var dlg = new CableFacetEditorWindow(df, facetId)
            {
                Owner = Application.Current.MainWindow
            };

            // Refresh the buildings tab if changes were saved
            if (dlg.ShowDialog() == true)
            {
                if (DataContext is BuildingsTabViewModel vm)
                {
                    vm.Refresh();
                }
            }
        }

        /// <summary>
        /// Opens the older read-only Cable Preview window.
        /// Kept for backwards compatibility but OpenCableEditor is preferred.
        /// </summary>
        private void OpenCablePreview(BuildingsTabViewModel.FacetVM fvm)
        {
            DFacetRec df = fvm.Raw;
            int facetId = fvm.FacetId1;

            var dlg = new CableFacetPreviewWindow(df, facetId)
            {
                Owner = Application.Current.MainWindow
            };
            dlg.Show();
        }

        #endregion

        #region Action Button Handlers

        private void BtnAddBuilding_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddBuildingDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.WasConfirmed)
            {
                var adder = new BuildingAdder(MapDataService.Instance);
                int newBuildingId = adder.TryAddBuilding(dialog.SelectedBuildingType);

                if (newBuildingId > 0)
                {
                    if (DataContext is BuildingsTabViewModel vm)
                    {
                        vm.Refresh();
                        vm.SelectedBuildingId = newBuildingId;
                    }

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    {
                        mainVm.StatusMessage = $"Added Building #{newBuildingId} ({dialog.SelectedBuildingType}). " +
                                              "Use 'Add Facets' to draw walls and fences.";
                    }

                    MessageBox.Show($"Building #{newBuildingId} created successfully.\n\n" +
                                   $"Type: {dialog.SelectedBuildingType}\n\n" +
                                   "The building is empty. Use 'Add Facets' to draw walls and fences.",
                        "Building Added", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to add building. See debug output for details.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnAddWall_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            int buildingId = vm.SelectedBuildingId;
            if (buildingId <= 0)
            {
                MessageBox.Show("Please select a building first.",
                    "Add Facets", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var window = new AddWallWindow(buildingId)
            {
                Owner = Window.GetWindow(this)
            };

            window.ShowDialog();

            if (!window.WasCancelled)
            {
                vm.Refresh();
            }
        }

        private void BtnAddFence_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            if (vm.SelectedBuilding == null)
                return;

            int buildingId = vm.SelectedBuildingId;

            var wnd = new AddFenceWindow(buildingId)
            {
                Owner = Application.Current.MainWindow
            };

            wnd.ShowDialog();
        }

        private void BtnAddLadder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            int? buildingId = vm.SelectedBuildingId;
            if (buildingId == null || buildingId <= 0)
            {
                MessageBox.Show("Please select a building first.", "Add Ladder",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var addLadderWindow = new AddLadderWindow(buildingId.Value)
            {
                Owner = Window.GetWindow(this)
            };

            addLadderWindow.Show();
        }

        private void BtnAddDoor_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            int? buildingId = vm.SelectedBuildingId;
            if (buildingId == null || buildingId <= 0)
            {
                MessageBox.Show("Please select a building first.", "Add Door",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var addDoorWindow = new AddDoorWindow(buildingId.Value)
            {
                Owner = Window.GetWindow(this)
            };

            addDoorWindow.Show();
        }

        private void BtnAddWalkable_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            int buildingId = vm.SelectedBuildingId;
            if (buildingId <= 0)
            {
                MessageBox.Show("Please select a building first.",
                    "Add Walkable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get MapViewModel from MainWindow
            if (Application.Current.MainWindow?.DataContext is not MainWindowViewModel mainVm)
            {
                MessageBox.Show("Cannot access main window.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Enter walkable drawing mode
            mainVm.Map.IsDrawingWalkable = true;
            _isWaitingForWalkableDrawing = true;

            mainVm.StatusMessage = "Click and drag on the map to define the walkable region, then release to set height...";

            // Find MapView and subscribe to completion event
            var mapView = FindMapViewInVisualTree();
            if (mapView != null)
            {
                mapView.WalkableDrawingCompleted += OnWalkableDrawingCompleted;
            }
        }

        private void OnWalkableDrawingCompleted(object? sender, EventArgs e)
        {
            if (!_isWaitingForWalkableDrawing)
                return;

            _isWaitingForWalkableDrawing = false;

            // Unsubscribe from event
            if (sender is MapView mapView)
            {
                mapView.WalkableDrawingCompleted -= OnWalkableDrawingCompleted;
            }

            if (DataContext is not BuildingsTabViewModel vm)
                return;

            if (Application.Current.MainWindow?.DataContext is not MainWindowViewModel mainVm)
                return;

            var mapVm = mainVm.Map;

            // Get selected building
            int buildingId = vm.SelectedBuildingId;
            string buildingName = vm.SelectedBuilding?.DisplayName ?? $"Building #{buildingId}";

            // Get selection rectangle
            var rect = mapVm.GetWalkableSelectionRect();
            if (!rect.HasValue)
            {
                mapVm.ClearWalkableSelection();
                return;
            }

            // Show dialog for height input
            var dialog = new AddWalkableWindow
            {
                Owner = Window.GetWindow(this),
                BuildingId1 = buildingId,
                BuildingName = buildingName,
                TileX1 = rect.Value.MinX,
                TileZ1 = rect.Value.MinY,
                TileX2 = rect.Value.MaxX,
                TileZ2 = rect.Value.MaxY
            };

            if (dialog.ShowDialog() == true)
            {
                // Convert UI tile coordinates to game tile coordinates (flip both axes)
                int gameX1 = (MapConstants.TilesPerSide - 1) - rect.Value.MaxX;
                int gameZ1 = (MapConstants.TilesPerSide - 1) - rect.Value.MaxY;
                int gameX2 = (MapConstants.TilesPerSide - 1) - rect.Value.MinX;
                int gameZ2 = (MapConstants.TilesPerSide - 1) - rect.Value.MinY;

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
                    mainVm.StatusMessage = $"Added walkable #{result.WalkableId1} to {buildingName}";

                    // Refresh UI
                    vm.Refresh();
                    BuildingsChangeBus.Instance.NotifyChanged();
                }
                else
                {
                    MessageBox.Show($"Failed to add walkable:\n\n{result.Error}",
                        "Add Walkable Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // Clear selection and exit drawing mode
            mapVm.ClearWalkableSelection();
        }

        private void BtnDeleteWalkable_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            // Get selected walkable from the WalkablesList
            if (WalkablesList.SelectedItem is not BuildingsTabViewModel.WalkableVM walkable)
            {
                MessageBox.Show("Please select a walkable from the list first.",
                    "Delete Walkable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int walkableId = walkable.WalkableId1;
            int buildingId = vm.SelectedBuildingId;

            var confirm = MessageBox.Show(
                $"Delete walkable #{walkableId} from Building #{buildingId}?\n\n" +
                "This will also delete any associated RoofFace4 entries.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

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

                // Refresh UI
                vm.Refresh();
                BuildingsChangeBus.Instance.NotifyChanged();
            }
            else
            {
                MessageBox.Show($"Failed to delete walkable:\n\n{result.Error}",
                    "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Find the MapView control in the visual tree.
        /// </summary>
        private MapView? FindMapViewInVisualTree()
        {
            // Walk up to find MainWindow, then search down for MapView
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null) return null;

            return FindVisualChild<MapView>(mainWindow);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void BtnDeleteFacet_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            var selectedFacet = vm.SelectedFacet;
            if (selectedFacet == null)
            {
                MessageBox.Show("No facet selected.", "Delete Facet",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int facetId = selectedFacet.FacetId1;
            int buildingId = selectedFacet.BuildingId;
            bool isCable = selectedFacet.Type == FacetType.Cable;

            // Confirm deletion
            string itemType = isCable ? "cable" : "facet";
            var confirmResult = MessageBox.Show(
                $"Delete {itemType} #{facetId}?",
                $"Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            var deleter = new FacetDeleter(MapDataService.Instance);
            var result = deleter.TryDeleteFacet(facetId);

            if (result.IsSuccess)
            {
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                {
                    if (isCable)
                        mainVm.StatusMessage = $"Deleted cable #{facetId}.";
                    else
                        mainVm.StatusMessage = $"Deleted facet #{facetId} from building #{buildingId}.";
                }

                vm.SelectedFacet = null;
                vm.Refresh();

                // Only try to select next facet if it wasn't a cable
                if (!isCable && buildingId > 0)
                {
                    SelectNextFacetInBuilding(vm, buildingId);
                }
            }
            else
            {
                MessageBox.Show($"Failed to delete {itemType}:\n\n{result.ErrorMessage}",
                    "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAddCable_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is BuildingsTabViewModel vm)
            {
                // Auto-select Cables pill so the cable list becomes visible
                vm.ShowCablesList = true;
            }

            var addCableWindow = new AddCableWindow
            {
                Owner = Window.GetWindow(this)
            };

            // Use Show() instead of ShowDialog() to allow interaction with the main window
            // This is necessary for the "Draw on Map" feature to work
            addCableWindow.Closed += (s, args) =>
            {
                // Refresh after the window is closed (whether cancelled or not)
                if (!addCableWindow.WasCancelled && DataContext is BuildingsTabViewModel vmRefresh)
                {
                    vmRefresh.Refresh();
                }
            };

            addCableWindow.Show();
        }

        private void BtnDeleteBuilding_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            int buildingId = vm.SelectedBuildingId;
            if (buildingId <= 0)
            {
                MessageBox.Show("No building selected.", "Delete Building",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var acc = new BuildingsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();

            int facetCount = 0;
            if (snap.Facets != null)
            {
                foreach (var f in snap.Facets)
                {
                    if (f.Building == buildingId)
                        facetCount++;
                }
            }

            int walkableCount = 0;
            if (snap.Walkables != null)
            {
                for (int i = 1; i < snap.Walkables.Length; i++)
                {
                    if (snap.Walkables[i].Building == buildingId)
                        walkableCount++;
                }
            }

            string message = $"Delete Building #{buildingId}?\n\n" +
                            $"This will remove:\n" +
                            $"  • {facetCount} facet(s)\n" +
                            $"  • {walkableCount} walkable(s)\n" +
                            $"  • Associated roof faces\n\n" +
                            $"This action cannot be undone.";

            var confirmResult = MessageBox.Show(message, "Confirm Delete Building",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            var deleter = new BuildingDeleter(MapDataService.Instance);
            var deleteResult = deleter.TryDeleteBuilding(buildingId);

            if (deleteResult.IsSuccess)
            {
                vm.SelectedBuildingId = 0;
                vm.SelectedBuilding = null;
                vm.SelectedFacet = null;
                vm.SelectedFacetTypeGroup = null;

                vm.Refresh();

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                {
                    mainVm.StatusMessage = $"Deleted building #{buildingId}: " +
                                          $"{deleteResult.FacetsDeleted} facets, " +
                                          $"{deleteResult.WalkablesDeleted} walkables, " +
                                          $"{deleteResult.RoofFacesDeleted} roof faces removed.";
                }

                MessageBox.Show($"Building #{buildingId} deleted successfully.\n\n" +
                               $"Removed {deleteResult.FacetsDeleted} facet(s), " +
                               $"{deleteResult.WalkablesDeleted} walkable(s), and " +
                               $"{deleteResult.RoofFacesDeleted} roof face(s).",
                    "Building Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Failed to delete building:\n\n{deleteResult.ErrorMessage}",
                    "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Helpers

        private void SelectNextFacetInBuilding(BuildingsTabViewModel vm, int buildingId)
        {
            var building = vm.Buildings.FirstOrDefault(b => b.Id == buildingId);
            if (building == null)
            {
                vm.SelectedFacet = null;
                return;
            }

            // Find the first facet in this building via the facet groups
            var firstGroup = vm.SelectedBuildingFacetGroups?.FirstOrDefault();
            var firstFacet = firstGroup?.Facets?.FirstOrDefault();

            if (firstFacet != null)
            {
                vm.SelectedFacetTypeGroup = firstGroup;
                vm.SelectedFacet = firstFacet;
                vm.SelectedBuildingId = buildingId;
            }
            else
            {
                vm.SelectedFacet = null;
                vm.SelectedFacetTypeGroup = null;
                vm.SelectedBuildingId = buildingId;
            }
        }

        private static T? FindAncestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T wanted)
                    return wanted;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        #endregion
    }
}