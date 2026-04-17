// /Views/MainWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using UrbanChaosStyleEditor.Models;
using UrbanChaosStyleEditor.ViewModels;

namespace UrbanChaosStyleEditor.Views
{
    public partial class MainWindow : Window
    {
        private StyleEditorViewModel Vm => (StyleEditorViewModel)DataContext;

        private Point _dragStartPoint;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new StyleEditorViewModel();
        }

        private void NewTextureSet_Click(object sender, RoutedEventArgs e) => Vm.NewTextureSet();
        private void Export_Click(object sender, RoutedEventArgs e) => Vm.ExportProject();
        private void Refresh_Click(object sender, RoutedEventArgs e) => Vm.ScanCustomTextures();
        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void ImportTexture_Click(object sender, RoutedEventArgs e) => Vm.ImportTexture();
        private void ImportBatch_Click(object sender, RoutedEventArgs e) => Vm.ImportBatch();
        private void ClearSlot_Click(object sender, RoutedEventArgs e) => Vm.ClearSlot();

        private void EditTile_Click(object sender, RoutedEventArgs e)
        {
            if (Vm.SelectedSlot == null)
            {
                Vm.StatusMessage = "Select a texture slot first.";
                return;
            }

            var editor = new TileEditorWindow(Vm.SelectedSlot.Image)
            {
                Owner = this,
                Title = $"Tile Editor - {Vm.SelectedSlot.DisplayName}"
            };

            if (editor.ShowDialog() == true && editor.Saved)
            {
                var frozen = new WriteableBitmap(editor.ResultBitmap);
                frozen.Freeze();
                Vm.SelectedSlot.Image = frozen;
                Vm.StatusMessage = $"Saved edits to {Vm.SelectedSlot.DisplayName}";
            }
        }

        private void ImportSky_Click(object sender, RoutedEventArgs e) => Vm.ImportSky();
        private void ClearSky_Click(object sender, RoutedEventArgs e) => Vm.ClearSky();

        private void LoadTma_Click(object sender, RoutedEventArgs e) => Vm.LoadTmaFromFile();

        private void CmbSoundGroup_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (Vm.SelectedSlot == null || CmbSoundGroup.SelectedItem is not ComboBoxItem item) return;
            if (item.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                Vm.SelectedSlot.SoundGroupIndex = idx;
                Vm.SelectedSlot.SoundGroup = idx >= 0 ? item.Content?.ToString() : null;
            }
        }

        private void StylesList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Vm.SelectedStyle == null) return;

            var detail = new StyleDetailWindow(Vm.SelectedStyle, Vm.Project)
            {
                Owner = this,
                Title = $"Edit Style - {Vm.SelectedStyle.DisplayName}"
            };

            if (detail.ShowDialog() == true && detail.Saved)
            {
                Vm.StatusMessage = $"Updated {Vm.SelectedStyle.DisplayName}";
                // Force refresh of the list display
                var idx = StylesList.SelectedIndex;
                StylesList.Items.Refresh();
                StylesList.SelectedIndex = idx;
            }
        }

        #region Drag-and-Drop Slot Reordering

        private void Slot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not TextureSlot slot)
                return;

            Vm.SelectedSlot = slot;
            _dragStartPoint = e.GetPosition(null);
            SyncSoundGroupCombo(slot);
        }

        private void SyncSoundGroupCombo(TextureSlot slot)
        {
            if (CmbSoundGroup == null) return;

            int targetIndex = slot.SoundGroupIndex;
            // ComboBox item 0 = "(none)" with Tag="-1", items 1-10 = groups with Tag="0"-"9"
            if (targetIndex < 0)
            {
                CmbSoundGroup.SelectedIndex = 0;
            }
            else if (targetIndex + 1 < CmbSoundGroup.Items.Count)
            {
                CmbSoundGroup.SelectedIndex = targetIndex + 1;
            }
            else
            {
                CmbSoundGroup.SelectedIndex = 0;
            }
        }

        private void Slot_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;
            if (sender is not FrameworkElement fe || fe.Tag is not TextureSlot slot)
                return;
            if (!slot.IsOccupied)
                return;

            var pos = e.GetPosition(null);
            var diff = _dragStartPoint - pos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var data = new DataObject("TextureSlotIndex", slot.Index);
                DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
            }
        }

        private void Slot_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TextureSlotIndex"))
                e.Effects = DragDropEffects.Move;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }

        private void Slot_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("TextureSlotIndex"))
                return;
            if (sender is not FrameworkElement fe || fe.Tag is not TextureSlot targetSlot)
                return;

            int fromIndex = (int)e.Data.GetData("TextureSlotIndex");
            int toIndex = targetSlot.Index;

            Vm.MoveSlot(fromIndex, toIndex);
            e.Handled = true;
        }

        #endregion
    }
}