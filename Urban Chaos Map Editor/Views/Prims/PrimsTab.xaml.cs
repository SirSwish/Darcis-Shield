using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UrbanChaosMapEditor.Models.Prims;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Prims;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.Views.Prims.Dialogs;

namespace UrbanChaosMapEditor.Views.Prims
{
    public partial class PrimsTab : UserControl
    {
        private bool _suppressSelectionWork;

        public PrimsTab()
        {
            InitializeComponent();
        }

        private List<PrimListItem> GetSelectedPrims()
        {
            return PrimsList.SelectedItems
                .OfType<PrimListItem>()
                .ToList();
        }

        private PrimListItem? GetSingleSelectedPrim()
        {
            var selected = GetSelectedPrims();
            return selected.Count == 1 ? selected[0] : null;
        }

        private void PrimsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionWork) return;
            if (DataContext is not MainWindowViewModel shell) return;

            var selected = GetSelectedPrims();

            // Sync SelectedPrims to the list selection so DeletePrimCommand can see all selected items.
            // Skip if the change was triggered by canvas code setting SelectedPrim (binding update)
            // to avoid clobbering a canvas multi-select.
            if (!shell.Map.SuppressListToCanvasSync)
                shell.Map.SetSelectedPrims(selected);

            if (selected.Count == 1)
            {
                shell.Map.SelectedPrim = selected[0];
            }
            else if (selected.Count > 1)
            {
                // Keep one representative selected for any existing single-selection map logic.
                shell.Map.SelectedPrim = selected[0];
                shell.StatusMessage = $"{selected.Count} prims selected.";
            }
        }

        // Only a real single click in the list may center, and only for a single selected prim.
        private void PrimsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 1) return;
            if (DataContext is not MainWindowViewModel shell) return;

            var sel = GetSingleSelectedPrim();
            if (sel == null) return;

            shell.Map.SelectedPrim = sel;

            if (!IsPrimAlreadyVisible(sel))
                CenterOnPrim(sel, shell);
        }

        // Double-click opens properties only when exactly one prim is selected.
        private void PrimsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;

            var sel = GetSingleSelectedPrim();
            if (sel == null)
            {
                shell.StatusMessage = "Select exactly one prim to open Properties.";
                e.Handled = true;
                return;
            }

            shell.Map.SelectedPrim = sel;
            OpenMergedPrimDialog(shell, sel);
            e.Handled = true;
        }

        private void PrimsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelectedPrims();
                e.Handled = true;
            }
        }

        private void DeletePrim_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedPrims();
        }

        private void DeleteSelectedPrims()
        {
            if (DataContext is not MainWindowViewModel shell) return;

            var selected = GetSelectedPrims();
            if (selected.Count == 0) return;

            var result = MessageBox.Show(
                $"Delete {selected.Count} selected prim{(selected.Count == 1 ? "" : "s")}? ",
                "Delete Prims",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                var indicesToDelete = selected
                    .Select(p => p.Index)
                    .Where(i => i >= 0)
                    .Distinct()
                    .OrderByDescending(i => i)
                    .ToList();

                if (indicesToDelete.Count == 0)
                    return;

                var acc = new PrimsAccessor(MapDataService.Instance);
                var snap = acc.ReadSnapshot();

                var prims = snap.Prims.ToList();
                foreach (int idx in indicesToDelete)
                {
                    if (idx >= 0 && idx < prims.Count)
                        prims.RemoveAt(idx);
                }

                acc.ReplaceAllPrims(prims.ToArray());

                _suppressSelectionWork = true;
                shell.Map.RefreshPrimsList();
                PrimsList.SelectedItems.Clear();
                shell.Map.SelectedPrim = null;
                _suppressSelectionWork = false;

                shell.StatusMessage = $"Deleted {indicesToDelete.Count} prim{(indicesToDelete.Count == 1 ? "" : "s")}.";
            }
            catch (Exception ex)
            {
                shell.StatusMessage = "Failed to delete selected prims.";
                System.Diagnostics.Debug.WriteLine($"[PrimsTab] Multi-delete failed: {ex}");
            }
        }

        private void PrimProperties_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;

            var sel = GetSingleSelectedPrim() ?? shell.Map.SelectedPrim;
            if (sel == null)
            {
                shell.StatusMessage = "Select exactly one prim to edit Properties.";
                return;
            }

            OpenMergedPrimDialog(shell, sel);
        }

        private void PrimHeight_Click(object sender, RoutedEventArgs e)
        {
            // Height is merged into Properties now.
            PrimProperties_Click(sender, e);
        }

        private void PrimPalette_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            var primBtn = (sender as FrameworkElement)?.Tag as PrimButton;
            if (primBtn == null) return;

            shell.Map.PrimNumberToPlace = primBtn.Number;
            shell.Map.IsPlacingPrim = true;
            shell.Map.DragPreviewPrim = null;

            shell.StatusMessage =
                $"Placing {primBtn.Number:D3} - {primBtn.Title}. Move mouse to choose location, click to place. Right-click to cancel.";

            var win = Window.GetWindow(this);
            var mapView = win != null
                ? LogicalTreeHelper.FindLogicalNode(win, "MapViewControl") as IInputElement
                : null;

            mapView?.Focus();
        }

        private void OpenMergedPrimDialog(MainWindowViewModel shell, PrimListItem sel)
        {
            var dlg = new PrimPropertiesDialog(sel.Flags, sel.InsideIndex, sel.Y)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() != true)
                return;

            byte newFlags = dlg.FlagsValue;
            byte newInside = dlg.InsideIndexValue;
            int newY = dlg.ResultHeight;

            var acc = new PrimsAccessor(MapDataService.Instance);
            acc.EditPrim(sel.Index, prim =>
            {
                prim.Flags = newFlags;
                prim.InsideIndex = newInside;
                prim.Y = (short)newY;
                return prim;
            });

            _suppressSelectionWork = true;
            shell.Map.RefreshPrimsList();

            var toSelect = ReselectPrim(shell, sel);
            shell.Map.SelectedPrim = toSelect;

            PrimsList.SelectedItems.Clear();
            if (toSelect != null)
                PrimsList.SelectedItems.Add(toSelect);

            _suppressSelectionWork = false;

            var flagsPretty = PrimFlags.FromByte(newFlags);
            var insideLabel = newInside == 0 ? "Outside" : $"Inside={newInside}";
            shell.StatusMessage = $"Updated {sel.Name} | Height={newY} | Flags: [{flagsPretty}] | {insideLabel}";
        }

        private static PrimListItem? ReselectPrim(MainWindowViewModel shell, PrimListItem original)
        {
            PrimListItem? toSelect = null;

            if (original.Index >= 0 && original.Index < shell.Map.Prims.Count)
            {
                toSelect = shell.Map.Prims[original.Index];

                if (toSelect.MapWhoIndex != original.MapWhoIndex ||
                    toSelect.X != original.X ||
                    toSelect.Z != original.Z ||
                    toSelect.PrimNumber != original.PrimNumber)
                {
                    toSelect = null;
                }
            }

            toSelect ??= shell.Map.Prims.FirstOrDefault(p =>
                p.MapWhoIndex == original.MapWhoIndex &&
                p.X == original.X &&
                p.Z == original.Z &&
                p.PrimNumber == original.PrimNumber);

            return toSelect;
        }

        private void CenterOnPrim(PrimListItem p, MainWindowViewModel shell)
        {
            var win = Window.GetWindow(this);
            if (win == null) return;

            var mapView = LogicalTreeHelper.FindLogicalNode(win, "MapViewControl");
            if (mapView == null) return;

            try
            {
                mapView.GetType()
                       .GetMethod("CenterOnPixel")
                       ?.Invoke(mapView, new object[] { p.PixelX, p.PixelZ });

                shell.StatusMessage = $"Centered on {p.Name} at ({p.X},{p.Z},{p.Y})";
            }
            catch
            {
            }
        }

        private bool IsPrimAlreadyVisible(PrimListItem p)
        {
            var win = Window.GetWindow(this);
            if (win == null) return false;

            var mapView = LogicalTreeHelper.FindLogicalNode(win, "MapViewControl");
            if (mapView == null) return false;

            try
            {
                var result = mapView.GetType()
                    .GetMethod("IsPixelInView")
                    ?.Invoke(mapView, new object[] { p.PixelX, p.PixelZ });
                return result is true;
            }
            catch
            {
                return false;
            }
        }
    }
}