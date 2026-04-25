// Views/Roofs/RoofsTab.xaml.cs
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using HeightSettings = UrbanChaosMapEditor.Models.Core.HeightDisplaySettings;
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

        // True only for a brief window when the user has physically interacted
        // with the corresponding list (mouse click or keyboard arrow/home/end/page).
        // Jump-to-viewport is gated on these so programmatic assignments from
        // Refresh()/auto-select/building-filter changes do NOT move the viewport.
        private bool _userInitiatedWalkableSelection;
        private bool _userInitiatedRoofFaceSelection;

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

            HeightSettings.DisplayModeChanged += (_, __) => Dispatcher.Invoke(RefreshAltitudeMode);
        }

        private void RefreshAltitudeMode()
        {
            bool rawMode = HeightSettings.ShowRawHeights;
            AltitudeLabel.Text = rawMode ? "Altitude (world):" : "Altitude (QS):";
        }

        // ====================================================================
        // Refresh
        // ====================================================================

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as RoofsTabViewModel)?.ClearFilter();
        }

        // ====================================================================
        // Walkable Selection
        // ====================================================================

        private void WalkablesList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not RoofsTabViewModel vm) return;
            if (sender is not ListView lv) return;

            // Capture and immediately clear the flag so cascading programmatic
            // assignments in the same pump iteration don't piggy-back on the jump.
            bool userInitiated = _userInitiatedWalkableSelection;
            _userInitiatedWalkableSelection = false;

            vm.HandleWalkableSelection(lv.SelectedItem);

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel shell)
            {
                shell.Map.SelectedWalkableId1 =
                    (lv.SelectedItem as WalkableVM)?.WalkableId1 ?? 0;
            }

            // Jump ONLY when the user physically picked this walkable from the
            // list. Refresh/auto-select/building-filter assignments must not jump.
            if (userInitiated && lv.SelectedItem is WalkableVM w)
                JumpToWalkable(w);
        }

        private void RoofFaces4List_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView lv) return;

            bool userInitiated = _userInitiatedRoofFaceSelection;
            _userInitiatedRoofFaceSelection = false;

            if (userInitiated && lv.SelectedItem is RoofFace4VM rf)
                JumpToRoofFace(rf);
        }

        /// <summary>
        /// Sets the user-initiated flag for mouse clicks on the Walkables list.
        /// A Background-priority reset prevents the flag leaking into a later
        /// programmatic event if this click didn't cause a selection change.
        /// </summary>
        private void WalkablesList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _userInitiatedWalkableSelection = true;
            Dispatcher.BeginInvoke(
                new Action(() => _userInitiatedWalkableSelection = false),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void RoofFaces4List_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _userInitiatedRoofFaceSelection = true;
            Dispatcher.BeginInvoke(
                new Action(() => _userInitiatedRoofFaceSelection = false),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Centers the map viewport on a walkable's midpoint if it is off-screen.
        /// Matches the coordinate convention used by WalkablesLayer.ToMapRect:
        /// pixel = (128 - tile) * 64.
        /// </summary>
        private static void JumpToWalkable(WalkableVM w)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;

            var mapView = mw.MapViewControl;
            if (mapView.Scroller == null) return;

            // Don't jump while placing a prim — would fight the cursor.
            if (mapView.DataContext is MapViewModel mapVm && mapVm.IsPlacingPrim) return;

            // Rect corners in canvas pixels.
            double px0 = (128 - w.X1) * 64.0;
            double pz0 = (128 - w.Z1) * 64.0;
            double px1 = (128 - w.X2) * 64.0;
            double pz1 = (128 - w.Z2) * 64.0;

            if (IsRectVisible(mapView, px0, pz0, px1, pz1)) return;

            int midPx = (int)((px0 + px1) / 2.0);
            int midPz = (int)((pz0 + pz1) / 2.0);
            mapView.CenterOnPixel(midPx, midPz);
        }

        /// <summary>
        /// Centers the map viewport on a RoofFace4 tile if it is off-screen.
        /// Matches the coordinate convention used by RoofsLayer: the tile's
        /// upper-left pixel is (128 - TileX - 1) * 64, (128 - TileZ - 1) * 64,
        /// and the tile is 64x64 px, so centre is +32,+32 from the upper-left.
        /// </summary>
        private static void JumpToRoofFace(RoofFace4VM rf)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;

            var mapView = mw.MapViewControl;
            if (mapView.Scroller == null) return;

            if (mapView.DataContext is MapViewModel mapVm && mapVm.IsPlacingPrim) return;

            int tileX = rf.TileX;
            int tileZ = rf.TileZ;
            if (tileX < 0 || tileX > 127 || tileZ < 0 || tileZ > 127) return;

            double left = (128 - tileX - 1) * 64.0;
            double top  = (128 - tileZ - 1) * 64.0;
            double right  = left + 64.0;
            double bottom = top  + 64.0;

            if (IsRectVisible(mapView, left, top, right, bottom)) return;

            int midPx = (int)(left + 32);
            int midPz = (int)(top  + 32);
            mapView.CenterOnPixel(midPx, midPz);
        }

        /// <summary>
        /// True when the canvas rectangle [px0..px1] x [pz0..pz1] is currently
        /// inside the visible viewport (any overlap counts as visible).
        /// Mirrors the logic in BuildingsTab.JumpToFacet so behaviour is consistent.
        /// </summary>
        private static bool IsRectVisible(MapView mapView, double px0, double pz0, double px1, double pz1)
        {
            var scroller = mapView.Scroller;
            double zoom = (mapView.DataContext as MapViewModel)?.Zoom ?? 1.0;
            if (zoom <= 0 || scroller.ViewportWidth <= 0 || scroller.ViewportHeight <= 0)
                return false;

            const double margin = 256.0; // MapSurface has Margin="256"
            double viewLeft   = scroller.HorizontalOffset / zoom - margin;
            double viewTop    = scroller.VerticalOffset   / zoom - margin;
            double viewRight  = viewLeft + scroller.ViewportWidth  / zoom;
            double viewBottom = viewTop  + scroller.ViewportHeight / zoom;

            double rectLeft   = Math.Min(px0, px1);
            double rectRight  = Math.Max(px0, px1);
            double rectTop    = Math.Min(pz0, pz1);
            double rectBottom = Math.Max(pz0, pz1);

            return rectRight  >= viewLeft  && rectLeft   <= viewRight &&
                   rectBottom >= viewTop   && rectTop    <= viewBottom;
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
                MessageBox.Show("Select a building first — walkables must belong to a specific building.",
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

        private void WalkablesList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                BtnDeleteWalkable_Click(sender, e);
                e.Handled = true;
                return;
            }

            // Keyboard navigation that will change selection — mark as user-initiated
            // so the jump-to-viewport fires, with a Background reset so the flag
            // can't leak into a subsequent programmatic SelectionChanged.
            if (e.Key == Key.Up || e.Key == Key.Down ||
                e.Key == Key.PageUp || e.Key == Key.PageDown ||
                e.Key == Key.Home || e.Key == Key.End)
            {
                _userInitiatedWalkableSelection = true;
                Dispatcher.BeginInvoke(
                    new Action(() => _userInitiatedWalkableSelection = false),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void RoofFaces4List_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                BtnDeleteRoofFace4_Click(sender, e);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up || e.Key == Key.Down ||
                e.Key == Key.PageUp || e.Key == Key.PageDown ||
                e.Key == Key.Home || e.Key == Key.End)
            {
                _userInitiatedRoofFaceSelection = true;
                Dispatcher.BeginInvoke(
                    new Action(() => _userInitiatedRoofFaceSelection = false),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }

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
        // PAP Cell Flags
        // ====================================================================

        /// <summary>
        /// Returns (setMask, clearMask) from the tri-state PAP flag checkboxes.
        /// Checked = bit goes into setMask, Unchecked = bit goes into clearMask,
        /// Indeterminate = bit is absent from both (leave unchanged).
        /// </summary>
        private (ushort setMask, ushort clearMask) GetPapFlagMasks()
        {
            CheckBox[] boxes = { PapShadow1, PapShadow2, PapShadow3, PapReflective,
                                 PapHidden, PapSinkSquare, PapSinkPoint, PapNoUpper,
                                 PapNoGo, PapRoofExists, PapZone1, PapZone2,
                                 PapZone3, PapZone4, PapFlatRoof, PapWater };
            ushort setMask = 0, clearMask = 0;
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].IsChecked == true)  setMask   |= (ushort)(1 << i);
                else if (boxes[i].IsChecked == false) clearMask |= (ushort)(1 << i);
                // null (Indeterminate) = leave unchanged — contributes to neither mask
            }
            return (setMask, clearMask);
        }

        private void PapFlag_Changed(object sender, RoutedEventArgs e)
        {
            // PapWater is the last checkbox declared — if it's null we're still inside InitializeComponent
            if (TxtPapFlagsSummary == null || PapWater == null) return;
            var (setMask, clearMask) = GetPapFlagMasks();
            if (setMask == 0 && clearMask == 0)
                TxtPapFlagsSummary.Text = "(no change)";
            else if (clearMask == 0)
                TxtPapFlagsSummary.Text = $"+0x{setMask:X4}";
            else if (setMask == 0)
                TxtPapFlagsSummary.Text = $"-0x{clearMask:X4}";
            else
                TxtPapFlagsSummary.Text = $"+0x{setMask:X4} -0x{clearMask:X4}";
        }

        private void PapFlagsDragArea_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow?.DataContext is not MainWindowViewModel mainVm) return;

            var (setMask, clearMask) = GetPapFlagMasks();
            mainVm.Map.PapFlagsSetMask = setMask;
            mainVm.Map.PapFlagsClearMask = clearMask;
            mainVm.Map.SelectedTool = EditorTool.AreaSetPapFlags;

            if (setMask == 0 && clearMask == 0)
                mainVm.StatusMessage = "PAP flags: all Indeterminate — drag will not change any flags.";
            else if (clearMask == 0)
                mainVm.StatusMessage = $"PAP flags: drag on map to set 0x{setMask:X4}.";
            else if (setMask == 0)
                mainVm.StatusMessage = $"PAP flags: drag on map to clear 0x{clearMask:X4}.";
            else
                mainVm.StatusMessage = $"PAP flags: drag on map to set 0x{setMask:X4}, clear 0x{clearMask:X4}.";
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

            if (!int.TryParse(AltitudeInput.Text, out int inputVal))
                inputVal = 0;

            mainVm.Map.TargetAltitude = inputVal;
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
            if (sender is not TextBox tb)
                return;

            if (int.TryParse(tb.Text, out int inputVal))
            {
                // If Set Altitude tool is already active, update TargetAltitude immediately
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm &&
                    mainVm.Map.SelectedTool == EditorTool.SetAltitude)
                {
                    mainVm.Map.TargetAltitude = inputVal;
                }
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