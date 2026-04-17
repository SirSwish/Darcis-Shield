// Views/EditorTabs/BuildingsTab.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Models.Roofs;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.ViewModels.Buildings;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.ViewModels.Roofs;
using UrbanChaosMapEditor.Views.Buildings.Dialogs;
using UrbanChaosMapEditor.Views.Roofs.Dialogs;
using UrbanChaosMapEditor.Views.Core;

namespace UrbanChaosMapEditor.Views.Buildings
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
                if (vm.SelectedFacet == null)
                {
                    vm.HandleBuildingTreeSelection(building);

                    if (vm.SelectedBuildingFacetGroups?.Count > 0 && vm.SelectedFacetTypeGroup == null)
                        vm.SelectedFacetTypeGroup = vm.SelectedBuildingFacetGroups[0];
                }
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
            if (DataContext is BuildingsTabViewModel vm)
            {
                vm.SelectedBuilding = null;
                vm.SelectedBuildingId = 0;
                vm.SelectedFacetTypeGroup = null;
                vm.SelectedFacet = null;
            }

            if (sender is ListBox lb)
                lb.SelectedItem = null;

            // Also clear any walkable highlight owned by the Roofs tab selection.
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                shell.Map.SelectedWalkableId1 = 0;

            e.Handled = true;
        }

        #endregion

        #region Facet Types List Handlers

        private void FacetTypesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            //if (sender is ListBox lb && lb.SelectedItem is BuildingsTabViewModel.FacetTypeGroupVM typeGroup)
            //{
            //    // Auto-select first facet in the group if available
            //    if (typeGroup.Facets?.Count > 0)
            //    {
            //        vm.SelectedFacet = typeGroup.Facets[0];
            //    }
            //}
        }

        #endregion

        #region Polygon Groups List Handlers

        private void PolygonsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm) return;
            var group = vm.SelectedPolygonGroup;
            if (group == null || group.FacetIds.Count == 0) return;

            // Scroll the first member facet into view in the facets list
            var firstFacet = vm.SelectedBuildingFacets.FirstOrDefault(f => f.FacetId1 == group.FacetIds[0]);
            if (firstFacet != null)
                FacetsList?.ScrollIntoView(firstFacet);
        }

        private void PolygonsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm) return;
            var group = vm.SelectedPolygonGroup;
            if (group == null || group.FacetIds.Count == 0) return;

            OpenBulkFacetEdit(group, vm);
            e.Handled = true;
        }

        private void OpenBulkFacetEdit(BuildingsTabViewModel.PolygonGroupVM group, BuildingsTabViewModel vm)
        {
            var dlg = new BulkFacetEditDialog(group.FacetIds, group.Label)
            {
                Owner = Application.Current.MainWindow
            };

            if (dlg.ShowDialog() == true)
                vm.Refresh();
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

                // Jump the viewport to the facet only when the user selected it from
                // this list — not when the selection was driven by a map click.
                if (!vm.IsSelectingFromMap)
                    JumpToFacet(facet);
            }

            // Buildings tab should never own walkable highlighting.
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                shell.Map.SelectedWalkableId1 = 0;
        }

        private static void JumpToFacet(BuildingsTabViewModel.FacetVM fvm)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;

            var mapView = mw.MapViewControl;
            var scroller = mapView.Scroller;
            if (scroller == null) return;

            // Canvas-pixel coords for both endpoints.
            // Coordinate convention (matches FacetHandlesLayer): pixel = (128 - coord) * 64
            var f    = fvm.Raw;
            double px0 = (128 - f.X0) * 64.0;
            double pz0 = (128 - f.Z0) * 64.0;
            double px1 = (128 - f.X1) * 64.0;
            double pz1 = (128 - f.Z1) * 64.0;

            // Check whether any part of the facet is already in the viewport.
            // CenterOnPixel uses scrollOffset = canvasPixel * zoom, so
            // visible canvas range = [scrollOffset / zoom, (scrollOffset + viewportSize) / zoom].
            double zoom = (mapView.DataContext as MapViewModel)?.Zoom ?? 1.0;
            if (zoom > 0 && scroller.ViewportWidth > 0 && scroller.ViewportHeight > 0)
            {
                double viewLeft   = scroller.HorizontalOffset / zoom;
                double viewTop    = scroller.VerticalOffset   / zoom;
                double viewRight  = viewLeft + scroller.ViewportWidth  / zoom;
                double viewBottom = viewTop  + scroller.ViewportHeight / zoom;

                double facetLeft   = Math.Min(px0, px1);
                double facetRight  = Math.Max(px0, px1);
                double facetTop    = Math.Min(pz0, pz1);
                double facetBottom = Math.Max(pz0, pz1);

                // Any overlap between the facet bounding box and the viewport means it's visible.
                bool visible = facetRight  >= viewLeft  && facetLeft   <= viewRight &&
                               facetBottom >= viewTop   && facetTop    <= viewBottom;
                if (visible) return;
            }

            // Facet is off-screen — center the viewport on its midpoint.
            int midPx = (256 - f.X0 - f.X1) * 32;
            int midPz = (256 - f.Z0 - f.Z1) * 32;
            mapView.CenterOnPixel(midPx, midPz);
        }

        private void FacetsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FacetsList.SelectedItem is BuildingsTabViewModel.FacetVM fvm)
            {
                // Save current type group before the preview opens
                var vm = DataContext as BuildingsTabViewModel;
                var savedGroup = vm?.SelectedFacetTypeGroup;

                if (fvm.Type == FacetType.Cable)
                    OpenCableEditor(fvm);
                else
                    OpenFacetPreview(fvm);

                // Ensure the type group always matches the double-clicked facet's type
                if (vm != null)
                {
                    var correctGroup = vm.SelectedBuildingFacetGroups?
                        .FirstOrDefault(g => g.Type == fvm.Type);
                    if (correctGroup != null && vm.SelectedFacetTypeGroup != correctGroup)
                        vm.SelectedFacetTypeGroup = correctGroup;
                }

                e.Handled = true;
            }
        }

        private void FacetsList_TransferFacet_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm) return;

            // Resolve the right-clicked facet — prefer the context-menu's placement target
            // so the user doesn't have to left-click first.
            BuildingsTabViewModel.FacetVM? fvm = null;

            if (sender is MenuItem mi)
            {
                // Walk up: MenuItem → ContextMenu → ListBoxItem
                var cm = mi.Parent as ContextMenu;
                if (cm?.PlacementTarget is FrameworkElement target)
                {
                    fvm = (target.DataContext
                           ?? (target as ListBoxItem)?.Content
                           ?? (ItemsControl.ContainerFromElement(FacetsList, target) is ListBoxItem li
                               ? li.DataContext
                               : null))
                          as BuildingsTabViewModel.FacetVM;
                }
            }

            // Fall back to whatever is selected
            fvm ??= vm.SelectedFacet as BuildingsTabViewModel.FacetVM;

            if (fvm == null)
            {
                MessageBox.Show("Right-click a facet in the list to transfer it.", "Transfer Facet",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int sourceBuildingId = fvm.Raw.Building;

            var dlg = new TransferFacetDialog(fvm.FacetId1, sourceBuildingId)
            {
                Owner = Application.Current.MainWindow
            };

            if (dlg.ShowDialog() != true || dlg.SelectedBuildingId <= 0) return;

            int targetBuildingId = dlg.SelectedBuildingId;

            // ── Capture everything before deletion (IDs shift afterwards) ──────
            var snap = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();
            var f    = fvm.Raw;

            short dval = (snap.Styles != null && f.StyleIndex < snap.Styles.Length)
                ? snap.Styles[f.StyleIndex]
                : (short)1;
            ushort rawStyleId = dval > 0 ? (ushort)dval : (ushort)1;

            var template = new FacetTemplate
            {
                Type        = f.Type,
                Height      = f.Height,
                FHeight     = f.FHeight,
                BlockHeight = f.BlockHeight,
                Y0          = f.Y0,
                Y1          = f.Y1,
                RawStyleId  = rawStyleId,
                Flags       = f.Flags,
                BuildingId1 = targetBuildingId,
                Storey      = f.Storey,
            };

            var coords = new List<(byte x0, byte z0, byte x1, byte z1)>
            {
                (f.X0, f.Z0, f.X1, f.Z1)
            };

            int facetId1 = fvm.FacetId1;

            // ── Delete from source ────────────────────────────────────────────
            var deleter    = new FacetDeleter(MapDataService.Instance);
            var deleteResult = deleter.TryDeleteFacet(facetId1);

            if (!deleteResult.IsSuccess)
            {
                MessageBox.Show($"Failed to remove facet #{facetId1}: {deleteResult.ErrorMessage}",
                    "Transfer Facet", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // ── Recreate in target building ───────────────────────────────────
            var adder     = new BuildingAdder(MapDataService.Instance);
            var addResult = adder.TryAddFacets(targetBuildingId, coords, template);

            if (!addResult.IsSuccess)
            {
                MessageBox.Show($"Facet #{facetId1} was deleted but could not be recreated in Building #{targetBuildingId}: {addResult.ErrorMessage}\n\nUndo (Ctrl+Z) is recommended.",
                    "Transfer Facet — Partial Failure", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    mainVm.StatusMessage = $"Transferred facet #{facetId1} from Building #{sourceBuildingId} to Building #{targetBuildingId}.";
            }

            vm.Refresh();
        }

        #endregion


        #region Gate Handler

        private void BtnAddGate_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            int? buildingId = vm.SelectedBuildingId;
            if (buildingId == null || buildingId <= 0)
            {
                MessageBox.Show("Please select a fence building first.", "Add Gate",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var addGateWindow = new AddGateWindow(buildingId.Value)
            {
                Owner = Window.GetWindow(this)
            };

            addGateWindow.Closed += (s, args) =>
            {
                if (!addGateWindow.WasCancelled && DataContext is BuildingsTabViewModel vmRefresh)
                {
                    vmRefresh.Refresh();
                }
            };

            addGateWindow.Show();
        }

        private void BtnCloneFacet_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm) return;
            if (vm.SelectedFacet is not BuildingsTabViewModel.FacetVM fvm) return;
            if (vm.SelectedBuildingId <= 0) return;

            if (Application.Current.MainWindow?.DataContext is not MainWindowViewModel mainVm)
                return;

            // Read current dstyles table to resolve RawStyleId from the StyleIndex pointer
            var snap = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();
            var f = fvm.Raw;

            short dval = (snap.Styles != null && f.StyleIndex < snap.Styles.Length)
                ? snap.Styles[f.StyleIndex]
                : (short)1;
            ushort rawStyleId = dval > 0 ? (ushort)dval : (ushort)1;

            var template = new FacetTemplate
            {
                Type        = f.Type,
                Height      = f.Height,
                FHeight     = f.FHeight,
                BlockHeight = f.BlockHeight,
                Y0          = f.Y0,
                Y1          = f.Y1,
                RawStyleId  = rawStyleId,
                Flags       = f.Flags,
                BuildingId1 = vm.SelectedBuildingId,
                Storey      = f.Storey,
            };

            var handler = new CloneFacetDrawHandler(() =>
            {
                if (DataContext is BuildingsTabViewModel v) v.Refresh();
            });

            mainVm.Map.BeginFacetMultiDraw(handler, template);
            mainVm.StatusMessage =
                $"Cloning facet #{fvm.FacetId1} for Building #{vm.SelectedBuildingId}. " +
                "Click start then end point. Right-click to finish.";
        }

        /// <summary>
        /// Minimal IFacetMultiDrawWindow implementation for Clone Selected Facet.
        /// No dialog — just refreshes the VM when drawing ends.
        /// </summary>
        private sealed class CloneFacetDrawHandler : IFacetMultiDrawWindow
        {
            private readonly Action _onFinished;
            public CloneFacetDrawHandler(Action onFinished) => _onFinished = onFinished;
            public void OnDrawCancelled()          => _onFinished();
            public void OnDrawCompleted(int count) => _onFinished();
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

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            vm.SelectedBuildingId = newBuildingId;          // selects it via VM
                            vm.Refresh(forceSelectBuildingId: newBuildingId);
                            BuildingsList.ScrollIntoView(vm.SelectedBuilding);
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    {
                        mainVm.StatusMessage = $"Added Building #{newBuildingId} ({dialog.SelectedBuildingType}). " +
                                              "Use 'Add Facets' to draw walls and fences.";
                    }

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

            // Capture the index of the facet within its group before deletion
            int prevFacetIndexInGroup = -1;
            if (!isCable && vm.SelectedFacetTypeGroup != null)
            {
                var facets = vm.SelectedFacetTypeGroup.Facets;
                for (int i = 0; i < facets.Count; i++)
                {
                    if (facets[i].FacetId1 == facetId)
                    {
                        prevFacetIndexInGroup = i;
                        break;
                    }
                }
            }

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

                vm.Refresh();

                // Only try to select next facet if it wasn't a cable
                if (!isCable && buildingId > 0)
                {
                    SelectNextFacetInBuilding(vm, buildingId, prevFacetIndexInGroup);
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
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            int buildingId = vm.SelectedBuildingId;
            if (buildingId <= 0)
            {
                MessageBox.Show("Please select a building first. Cables must belong to a building.",
                    "Add Cable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var addCableWindow = new AddCableWindow(buildingId)
            {
                Owner = Window.GetWindow(this)
            };

            addCableWindow.Closed += (s, args) =>
            {
                if (!addCableWindow.WasCancelled && DataContext is BuildingsTabViewModel vmRefresh)
                {
                    vmRefresh.Refresh();
                }
            };

            addCableWindow.Show();
        }

        public bool SelectFacetFromMap(int facetId1, int buildingId1, bool openEditor)
        {
            System.Diagnostics.Debug.WriteLine($"[BuildingsTab] SelectFacetFromMap facet#{facetId1} bld#{buildingId1}, DataContext={DataContext?.GetType().Name ?? "null"}");

            if (DataContext is not BuildingsTabViewModel vm)
            {
                System.Diagnostics.Debug.WriteLine("[BuildingsTab] FAIL: DataContext is not BuildingsTabViewModel");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[BuildingsTab] vm.Buildings.Count={vm.Buildings.Count}");

            if (!vm.TrySelectFacetFromMap(facetId1, buildingId1))
            {
                System.Diagnostics.Debug.WriteLine($"[BuildingsTab] FAIL: TrySelectFacetFromMap returned false (building found={vm.Buildings.Any(b => b.Id == buildingId1)})");
                return false;
            }

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
                shell.Map.SelectedWalkableId1 = 0;

            var selectedFacet = vm.SelectedFacet;

            if (vm.SelectedBuilding != null)
                BuildingsList?.ScrollIntoView(vm.SelectedBuilding);
            if (vm.SelectedFacet != null)
                FacetsList?.ScrollIntoView(vm.SelectedFacet);

            if (openEditor)
            {
                if (selectedFacet == null)
                    return false;

                if (selectedFacet.Type == FacetType.Cable)
                    OpenCableEditor(selectedFacet);
                else
                    OpenFacetPreview(selectedFacet);
            }

            Focus();
            return true;
        }

        private void BuildingsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                BtnDeleteBuilding_Click(sender, e);
                e.Handled = true;
                return;
            }

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (ctrl && e.Key == Key.X)
            {
                BtnMoveBuilding_Click(sender, e);
                e.Handled = true;
            }
        }

        private void BtnMoveBuilding_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BuildingsTabViewModel vm)
                return;

            int buildingId = vm.SelectedBuildingId;
            if (buildingId <= 0)
            {
                MessageBox.Show("Select a building first.", "Move Building",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (Application.Current.MainWindow?.DataContext is not MainWindowViewModel mainVm)
                return;

            var mover = new Services.Buildings.BuildingMover(MapDataService.Instance);
            var snap = mover.Capture(buildingId);

            if (snap == null)
            {
                MessageBox.Show($"Building #{buildingId} has no facets — cannot move.",
                    "Move Building", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Activate the MoveBuilding tool on the map
            mainVm.Map.BuildingMoveClipboard = snap;
            mainVm.Map.SelectedTool = Models.Core.EditorTool.MoveBuilding;

            // Prime the ghost position to the anchor (center of building)
            mainVm.Map.MoveGhostUiX = snap.AnchorUiX;
            mainVm.Map.MoveGhostUiZ = snap.AnchorUiZ;

            mainVm.StatusMessage = $"Building #{buildingId} captured. Click on the map to place. Right-click or Escape to cancel.";
        }

        private void FacetsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                BtnDeleteFacet_Click(sender, e);
                e.Handled = true;
            }
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
                            $"  - {facetCount} facet(s)\n" +
                            $"  - {walkableCount} walkable(s)\n" +
                            $"  - Associated roof faces\n\n" +
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

        private void SelectNextFacetInBuilding(BuildingsTabViewModel vm, int buildingId, int deletedFacetIndex = -1)
        {
            var building = vm.Buildings.FirstOrDefault(b => b.Id == buildingId);
            if (building == null)
            {
                vm.SelectedFacet = null;
                return;
            }

            // Refresh() already restored SelectedFacetTypeGroup to the previously selected group.
            var group = vm.SelectedFacetTypeGroup ?? vm.SelectedBuildingFacetGroups?.FirstOrDefault();
            var facets = group?.Facets;

            if (facets != null && facets.Count > 0)
            {
                // Select the facet just before the deleted one; clamp to 0 if it was the first.
                int targetIndex = deletedFacetIndex > 0 ? deletedFacetIndex - 1 : 0;
                targetIndex = Math.Min(targetIndex, facets.Count - 1);

                vm.SelectedFacetTypeGroup = group;
                vm.SelectedFacet = facets[targetIndex];
                vm.SelectedBuildingId = buildingId;
            }
            else
            {
                vm.SelectedFacet = null;
                vm.SelectedFacetTypeGroup = null;
                vm.SelectedBuildingId = buildingId;
            }
        }

        /// <summary>
        /// Centers the map on a facet's midpoint only if that point is not already in the viewport.
        /// Not called when a facet is selected via map click (it's already visible).
        /// </summary>

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