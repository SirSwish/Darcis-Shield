using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Models.Prims;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Prims;
using UrbanChaosMapEditor.Services.Textures;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.Views.Core.Dialogs;
using UrbanChaosMapEditor.Views.Help;
using UrbanChaosMapEditor.Views.Prims.Dialogs;

namespace UrbanChaosMapEditor.Views.Core
{
    public partial class MainWindow : Window
    {
        private bool _heightHotkeyLatched;
        private PrimListItem? _copiedPrim;
        private const double MinExpandedEditorWidth = 300;
        private const double CollapsedRailWidth = 28;
        private double _lastDrawerWidth = MinExpandedEditorWidth;

        public MainWindow()
        {
            InitializeComponent();

            // Apply dark title bar as early as possible
            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;

            AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(MainWindow_PreviewKeyDown), handledEventsToo: true);
            AddHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler(MainWindow_PreviewKeyUp), handledEventsToo: true);
        }

        private static readonly Regex _digits = new(@"^\d+$");

        #region Dark Title Bar (Windows 10/11)

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int SetPreferredAppMode(int preferredAppMode);

        [DllImport("uxtheme.dll", EntryPoint = "#136", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern void FlushMenuThemes();

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        /// <summary>
        /// Called when window handle is available - best time to set DWM attributes
        /// </summary>
        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            ApplyDarkTitleBar();
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                int darkMode = 1;

                // Try the Windows 11 / 10 20H1+ attribute first
                int result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                // Fallback for older Windows 10 builds
                if (result != 0)
                {
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkMode, sizeof(int));
                }

                // On Windows 11, we can also set the caption color directly
                // 0x001E1E1E = RGB(30, 30, 30) in COLORREF format (BGR)
                int captionColor = 0x001E1E1E;
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

                // Set title text color to white
                // 0x00FFFFFF = RGB(255, 255, 255) in COLORREF format
                int textColor = 0x00FFFFFF;
                DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
            }
            catch
            {
                // Silently fail on older Windows versions that don't support these attributes
            }
        }

        #endregion

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ensure dark title bar is applied (backup call)
            ApplyDarkTitleBar();

            var vm = DataContext as MainWindowViewModel;
            System.Diagnostics.Debug.WriteLine($"[Recent] VM? {(vm != null)}  Count={(vm?.RecentFiles?.Count ?? -1)}");
        }

        private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only act on top-level tab changes (TabControls inside tabs also fire this event)
            if (e.OriginalSource != EditorTabControl) return;
            if (DataContext is MainWindowViewModel shell)
                shell.Map.SelectedTool = EditorTool.None;
        }

        private void EditorExpander_Expanded(object sender, RoutedEventArgs e)
        {
            EditorRailCol.Width = new GridLength(CollapsedRailWidth);
            EditorDrawerCol.MinWidth = MinExpandedEditorWidth;
            var target = Math.Max(_lastDrawerWidth, MinExpandedEditorWidth);
            EditorDrawerCol.Width = new GridLength(target);
        }

        private void EditorExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            var w = EditorDrawerCol.ActualWidth;
            if (w > 0) _lastDrawerWidth = w;

            EditorDrawerCol.MinWidth = 0;
            EditorDrawerCol.Width = new GridLength(0);
            EditorRailCol.Width = new GridLength(CollapsedRailWidth);
        }

        private void TextureThumb_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            if ((sender as FrameworkElement)?.Tag is not TextureThumb thumb) return;

            var map = shell.Map;
            map.SelectedTool = EditorTool.PaintTexture;
            map.SelectedTextureGroup = thumb.Group;
            map.SelectedTextureNumber = thumb.Number;

            shell.StatusMessage = $"Texture paint: {thumb.RelativeKey} (rot {map.SelectedRotationIndex}) - click a tile to apply";
        }

        private void RotateTexture_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            shell.Map.SelectedRotationIndex = (shell.Map.SelectedRotationIndex + 1) % 4;
            shell.StatusMessage = $"Rotation: {shell.Map.SelectedRotationIndex}";
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            HelpViewerWindow.ShowHelp(owner: this);
        }

        private void GoToCell_Click(object sender, RoutedEventArgs e)
        {
            int curTx = 0, curTy = 0;
            if (DataContext is MainWindowViewModel vm)
            {
                curTx = System.Math.Clamp(vm.Map.CursorX / 64, 0, 127);
                curTy = System.Math.Clamp(vm.Map.CursorZ / 64, 0, 127);
            }

            var dlg = new GoToCellDialog(curTx, curTy) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                MapViewControl.GoToTileCenter(dlg.Tx, dlg.Ty);
                if (DataContext is MainWindowViewModel vm2)
                    vm2.StatusMessage = $"Jumped to cell [{dlg.Tx},{dlg.Ty}]";
            }
        }

        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            var map = shell.Map;
            map.SelectedRotationIndex = (map.SelectedRotationIndex + 3) % 4;
            shell.StatusMessage = $"Rotation: {map.SelectedRotationIndex}  (0?180-, 1?90-, 2?0-, 3?270-)";
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            var map = shell.Map;
            map.SelectedRotationIndex = (map.SelectedRotationIndex + 1) % 4;
            shell.StatusMessage = $"Rotation: {map.SelectedRotationIndex}  (0?180-, 1?90-, 2?0-, 3?270-)";
        }

        private void PrimsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            if ((sender as ListView)?.SelectedItem is not PrimListItem p) return;

            shell.Map.SelectedPrim = p;

            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (shell.ShowPrimPropertiesCommand.CanExecute(null))
                    shell.ShowPrimPropertiesCommand.Execute(null);
                e.Handled = true;
                return;
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                PrimHeight_Click(sender!, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            MapViewControl.CenterOnPixel(p.PixelX, p.PixelZ);
            shell.StatusMessage = $"Centered on {p.Name} at ({p.X},{p.Z},{p.Y})";
        }

        private void PrimsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelectedPrim();
                e.Handled = true;
            }
            if ((e.Key == Key.LeftShift || e.Key == Key.RightShift))
            {
                if (Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;

                if (DataContext is MainWindowViewModel vm && vm.Map.SelectedPrim is { } sel)
                {
                    OpenPrimHeightDialog(sel);
                    e.Handled = true;
                }
            }
        }

        private void DeletePrim_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedPrim();
        }

        private void DeleteSelectedPrim()
        {
            if (DataContext is not MainWindowViewModel shell) return;
            var sel = shell.Map.SelectedPrim;
            if (sel == null) return;

            try
            {
                var acc = new PrimsAccessor(MapDataService.Instance);
                acc.DeletePrim(sel.Index);

                shell.Map.RefreshPrimsList();
                shell.Map.SelectedPrim = null;

                shell.StatusMessage = $"Deleted \"{sel.Name}\" (index {sel.Index}).";
            }
            catch (Exception ex)
            {
                shell.StatusMessage = "Error: failed to delete prim.";
                MessageBox.Show($"Failed to delete prim.\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrimProperties_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            var sel = shell.Map.SelectedPrim;
            if (sel == null)
            {
                System.Diagnostics.Debug.WriteLine("[PrimProps] No selection.");
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[PrimProps] Open for index={sel.Index}, flags=0x{sel.Flags:X2}, inside={sel.InsideIndex}, y={sel.Y}");

            var dlg = new PrimPropertiesDialog(sel.Flags, sel.InsideIndex, sel.Y)
            {
                Owner = this
            };

            if (dlg.ShowDialog() != true)
            {
                System.Diagnostics.Debug.WriteLine("[PrimProps] Cancelled.");
                return;
            }

            byte newFlags = dlg.FlagsValue;
            byte newInside = dlg.InsideIndexValue;
            int newY = dlg.ResultHeight;

            System.Diagnostics.Debug.WriteLine(
                $"[PrimProps] OK -> flags=0x{newFlags:X2}, inside={newInside}, y={newY}");

            var acc = new PrimsAccessor(MapDataService.Instance);
            acc.EditPrim(sel.Index, prim =>
            {
                prim.Flags = newFlags;
                prim.InsideIndex = newInside;
                prim.Y = (short)newY;
                return prim;
            });

            shell.Map.RefreshPrimsList();

            PrimListItem? toSelect = null;
            if (sel.Index >= 0 && sel.Index < shell.Map.Prims.Count)
            {
                toSelect = shell.Map.Prims[sel.Index];
                if (toSelect.MapWhoIndex != sel.MapWhoIndex ||
                    toSelect.X != sel.X ||
                    toSelect.Z != sel.Z ||
                    toSelect.PrimNumber != sel.PrimNumber)
                {
                    toSelect = null;
                }
            }

            toSelect ??= shell.Map.Prims.FirstOrDefault(p =>
                p.MapWhoIndex == sel.MapWhoIndex &&
                p.X == sel.X &&
                p.Z == sel.Z &&
                p.PrimNumber == sel.PrimNumber);

            shell.Map.SelectedPrim = toSelect;

            var flagsPretty = PrimFlags.FromByte(newFlags);
            var insideLabel = newInside == 0 ? "Outside" : $"Inside={newInside}";
            shell.StatusMessage = $"Updated {sel.Name} | Height={newY} | Flags: [{flagsPretty}] | {insideLabel}";

            System.Diagnostics.Debug.WriteLine($"[PrimProps] Applied. Reselected: {(toSelect != null ? "yes" : "no")}");
        }

        private void PrimPaletteButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            if ((sender as FrameworkElement)?.Tag is not PrimButton pb) return;

            shell.Map.BeginPlacePrim(pb.Number);
            shell.StatusMessage =
                $"Add Prim: {pb.Title} ({pb.Number:000}). Click on the map to place. Right-click/Esc to cancel.";

            MapViewControl.Focus();
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
        }

        private void CopyPrim_Click(object? sender, RoutedEventArgs e)
        {
            CopySelectedPrim();
        }

        private void PastePrim_Click(object? sender, RoutedEventArgs e)
        {
            PastePrimAtCursor();
        }

        private void CopySelectedPrim()
        {
            var shell = DataContext as MainWindowViewModel;
            if (shell == null || shell.Map?.SelectedPrim is null)
            {
                if (shell != null) shell.StatusMessage = "No object selected to copy.";
                return;
            }

            var sel = shell.Map.SelectedPrim;

            _copiedPrim = new PrimListItem
            {
                Index = sel.Index,
                MapWhoIndex = sel.MapWhoIndex,
                MapWhoRow = sel.MapWhoRow,
                MapWhoCol = sel.MapWhoCol,
                PrimNumber = sel.PrimNumber,
                Name = sel.Name,
                X = sel.X,
                Z = sel.Z,
                Y = sel.Y,
                Yaw = sel.Yaw,
                Flags = sel.Flags,
                InsideIndex = sel.InsideIndex,
                PixelX = sel.PixelX,
                PixelZ = sel.PixelZ
            };

            shell.StatusMessage = $"Copied \"{sel.Name}\" (#{sel.PrimNumber:000}).";
        }

        private void PastePrimAtCursor()
        {
            if (_copiedPrim == null) { if (DataContext is MainWindowViewModel s1) s1.StatusMessage = "Clipboard is empty."; return; }
            if (DataContext is not MainWindowViewModel shell) return;
            var map = shell.Map;

            int uiX = MapConstants.MapPixels - map.CursorX;
            int uiY = MapConstants.MapPixels - map.CursorZ;

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                uiX = (int)(Math.Round(uiX / 64.0) * 64.0);
                uiY = (int)(Math.Round(uiY / 64.0) * 64.0);

                uiX = Math.Clamp(uiX, 0, MapConstants.MapPixels - 1);
                uiY = Math.Clamp(uiY, 0, MapConstants.MapPixels - 1);
            }

            ObjectSpace.UiPixelsToGamePrim(uiX, uiY, out int mapWhoIndex, out byte gameX, out byte gameZ);

            var clip = _copiedPrim;
            var newEntry = new PrimsAccessor.PrimEntry
            {
                PrimNumber = (byte)clip.PrimNumber,
                MapWhoIndex = mapWhoIndex,
                X = gameX,
                Z = gameZ,
                Y = clip.Y,
                Yaw = clip.Yaw,
                Flags = clip.Flags,
                InsideIndex = clip.InsideIndex
            };

            try
            {
                var acc = new PrimsAccessor(MapDataService.Instance);
                acc.AddPrim(newEntry);

                map.RefreshPrimsList();

                var inserted = map.Prims.LastOrDefault(p =>
                    p.MapWhoIndex == mapWhoIndex &&
                    p.X == gameX && p.Z == gameZ &&
                    p.PrimNumber == clip.PrimNumber);

                map.SelectedPrim = inserted ?? map.SelectedPrim;

                shell.StatusMessage = $"Pasted \"{clip.Name}\" (#{clip.PrimNumber:000}) at cell {mapWhoIndex} (X:{gameX}, Z:{gameZ}).";
            }
            catch (System.Exception ex)
            {
                shell.StatusMessage = "Error: failed to paste object.";
                MessageBox.Show($"Failed to paste object.\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox)
                return;

            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (e.Key == Key.Z)
                {
                    if (UndoService.Instance.TryUndo(out int remaining))
                    {
                        TexturesChangeBus.Instance.NotifyChanged();
                        if (DataContext is MainWindowViewModel undoShell)
                            undoShell.StatusMessage = remaining > 0
                                ? $"Undo applied. {remaining} step(s) remaining."
                                : "Undo applied. No more history.";
                    }
                    else
                    {
                        if (DataContext is MainWindowViewModel undoShell)
                            undoShell.StatusMessage = "Nothing to undo.";
                    }
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.C)
                {
                    if (DataContext is not MainWindowViewModel shell || shell.Map == null)
                    {
                        e.Handled = true;
                        return;
                    }

                    var map = shell.Map;

                    // Texture area copy takes priority when SelectTextureArea is active
                    if (map.SelectedTool == UrbanChaosMapEditor.Models.Core.EditorTool.SelectTextureArea
                        && map.TextureAreaCommitted)
                    {
                        map.CopyTextureCellsCommand.Execute(null);
                        shell.StatusMessage = $"Copied {map.TextureClipboard?.Width}\u00d7{map.TextureClipboard?.Height} cells. Ctrl+V to paste.";
                        e.Handled = true;
                        return;
                    }

                    if (map.SelectedPrim != null)
                    {
                        CopySelectedPrim();
                    }
                    else
                    {
                        shell.StatusMessage = "Nothing selected to copy.";
                    }

                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.V)
                {
                    if (DataContext is not MainWindowViewModel shell || shell.Map == null)
                    {
                        e.Handled = true;
                        return;
                    }

                    var map = shell.Map;

                    // Texture clipboard paste takes priority when we have cells
                    if (map.TextureClipboard != null)
                    {
                        map.SelectedTool = UrbanChaosMapEditor.Models.Core.EditorTool.PasteTexture;
                        shell.StatusMessage = "Click on the map to paste cells. Right-click to cancel.";
                        e.Handled = true;
                        return;
                    }

                    if (_copiedPrim != null)
                    {
                        PastePrimAtCursor();
                    }
                    else
                    {
                        shell.StatusMessage = "Clipboard is empty.";
                    }

                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
            {
                if (DataContext is not MainWindowViewModel shell || shell.Map == null)
                    return;

                var map = shell.Map;
                var sel = map.SelectedPrim;
                if (sel == null)
                    return;

                if (map.IsPlacingPrim)
                    return;

                int dxUi = 0, dzUi = 0;
                switch (e.Key)
                {
                    case Key.Left: dxUi = -1; break;
                    case Key.Right: dxUi = 1; break;
                    case Key.Up: dzUi = -1; break;
                    case Key.Down: dzUi = 1; break;
                }

                int newUiX = Math.Clamp(sel.PixelX + dxUi, 0, MapConstants.MapPixels - 1);
                int newUiZ = Math.Clamp(sel.PixelZ + dzUi, 0, MapConstants.MapPixels - 1);

                ObjectSpace.UiPixelsToGamePrim(newUiX, newUiZ, out int mapWhoIndex, out byte gameX, out byte gameZ);

                try
                {
                    var acc = new PrimsAccessor(MapDataService.Instance);
                    acc.MovePrim(sel.Index, mapWhoIndex, gameX, gameZ);

                    map.RefreshPrimsList();

                    PrimListItem? toSelect = null;
                    if (sel.Index >= 0 && sel.Index < map.Prims.Count)
                        toSelect = map.Prims[sel.Index];

                    map.SelectedPrim = toSelect;

                    shell.StatusMessage =
                        $"Moved {sel.Name} to cell {mapWhoIndex} (X={gameX}, Z={gameZ}).";
                }
                catch (Exception ex)
                {
                    shell.StatusMessage = "Error: failed to move prim with arrow keys.";
                    System.Diagnostics.Debug.WriteLine(ex);
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete)
            {
                if (DataContext is MainWindowViewModel vm2 && vm2.DeletePrimCommand.CanExecute(null))
                {
                    vm2.DeletePrimCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }
        }

        private void MainWindow_PreviewKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
                _heightHotkeyLatched = false;
        }

        private void PrimHeight_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel shell) return;
            var sel = shell.Map.SelectedPrim;
            if (sel == null) return;

            var dlg = new PrimPropertiesDialog(sel.Flags, sel.InsideIndex, sel.Y)
            {
                Owner = this
            };

            if (dlg.ShowDialog() != true) return;

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

            shell.Map.RefreshPrimsList();

            PrimListItem? toSelect = null;
            if (sel.Index >= 0 && sel.Index < shell.Map.Prims.Count)
            {
                toSelect = shell.Map.Prims[sel.Index];
                if (toSelect.PrimNumber != sel.PrimNumber ||
                    toSelect.MapWhoIndex != sel.MapWhoIndex ||
                    toSelect.X != sel.X ||
                    toSelect.Z != sel.Z)
                {
                    toSelect = null;
                }
            }

            toSelect ??= shell.Map.Prims.FirstOrDefault(p =>
                p.MapWhoIndex == sel.MapWhoIndex &&
                p.X == sel.X &&
                p.Z == sel.Z &&
                p.PrimNumber == sel.PrimNumber);

            shell.Map.SelectedPrim = toSelect;

            int offset = ((newY % 256) + 256) % 256;
            int storey = (int)Math.Floor(newY / 256.0);

            var flagsPretty = PrimFlags.FromByte(newFlags);
            var insideLabel = newInside == 0 ? "Outside" : $"Inside={newInside}";
            shell.StatusMessage =
                $"Updated {sel.Name} | Height={newY} px (storey={storey}, offset={offset}) | Flags: [{flagsPretty}] | {insideLabel}";
        }

        private void OpenPrimHeightDialog(PrimListItem sel)
        {
            var dlg = new PrimPropertiesDialog(sel.Flags, sel.InsideIndex, sel.Y)
            {
                Owner = this
            };

            if (dlg.ShowDialog() != true) return;

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

            if (DataContext is MainWindowViewModel vm)
            {
                vm.Map.RefreshPrimsList();

                PrimListItem? toSelect = null;
                if (sel.Index >= 0 && sel.Index < vm.Map.Prims.Count)
                {
                    toSelect = vm.Map.Prims[sel.Index];
                    if (toSelect.PrimNumber != sel.PrimNumber ||
                        toSelect.MapWhoIndex != sel.MapWhoIndex ||
                        toSelect.X != sel.X ||
                        toSelect.Z != sel.Z)
                    {
                        toSelect = null;
                    }
                }

                toSelect ??= vm.Map.Prims.FirstOrDefault(p =>
                    p.MapWhoIndex == sel.MapWhoIndex &&
                    p.X == sel.X &&
                    p.Z == sel.Z &&
                    p.PrimNumber == sel.PrimNumber);

                vm.Map.SelectedPrim = toSelect;

                var flagsPretty = PrimFlags.FromByte(newFlags);
                var insideLabel = newInside == 0 ? "Outside" : $"Inside={newInside}";
                vm.StatusMessage = $"Updated {sel.Name} | Height={newY} | Flags: [{flagsPretty}] | {insideLabel}";
            }
        }

        private void OpenRecent_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            var mi = sender as MenuItem;
            var path = (mi?.Tag as string) ?? (mi?.DataContext as string);
            if (string.IsNullOrWhiteSpace(path)) return;

            var ext = System.IO.Path.GetExtension(path);
            vm.OpenMapFromPath(path);

            RecentFilesService.Instance.Add(path);
        }

        private void ClearRecent_Click(object sender, RoutedEventArgs e)
        {
            RecentFilesService.Instance.Clear();

            if (DataContext is MainWindowViewModel vm)
                vm.GetType()
                  .GetMethod("SyncRecentFiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                  ?.Invoke(vm, null);
        }

        private void Recent_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            var m = (MenuItem)sender;
            m.Items.Clear();

            foreach (var path in vm.RecentFiles)
            {
                var mi = new MenuItem
                {
                    Header = path,
                    Tag = path,
                    ToolTip = path
                };
                mi.Click += OpenRecent_Click;
                m.Items.Add(mi);
            }

            m.Items.Add(new Separator());

            var clear = new MenuItem { Header = "_Clear Recent" };
            clear.Click += ClearRecent_Click;
            m.Items.Add(clear);
        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            var asm = Assembly.GetExecutingAssembly();

            string version =
                asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString()
                ?? "Unknown";

            MessageBox.Show(
                "Urban Chaos Map Editor\n\n" +
                $"Version: ({version})\n" +
                "Urban Chaos Map Editor is a part of the Darcis Shield Editor Project",
                "About",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}