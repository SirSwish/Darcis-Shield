// /Views/Dialogs/Buildings/StylePickerWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosMapEditor.Models.Styles;
using UrbanChaosMapEditor.Services.DataServices;
using UrbanChaosEditor.Shared.Services.Textures;

namespace UrbanChaosMapEditor.Views.Dialogs.Buildings
{
    public partial class StylePickerWindow : Window
    {
        private List<StyleItemViewModel> _allStyles = new();
        private StyleItemViewModel? _selectedItem;

        /// <summary>
        /// The selected style index (raw, 0-based). -1 if cancelled.
        /// </summary>
        public int SelectedStyleIndex { get; private set; } = -1;

        /// <summary>
        /// Whether the user confirmed selection (clicked Select) vs cancelled.
        /// </summary>
        public bool WasConfirmed { get; private set; }

        public StylePickerWindow(int currentStyleIndex = 1)
        {
            InitializeComponent();
            LoadStyles();

            // Pre-select the current style if valid
            if (currentStyleIndex >= 0 && currentStyleIndex < _allStyles.Count)
            {
                SelectItem(_allStyles.FirstOrDefault(s => s.RawIndex == currentStyleIndex));
            }
        }

        private void LoadStyles()
        {
            var svc = StyleDataService.Instance;
            var tma = svc.TmaSnapshot;

            if (tma == null || tma.TextureStyles == null)
            {
                TxtStyleCount.Text = "No TMA loaded";
                return;
            }

            _allStyles.Clear();

            // Skip index 0 (dummy) - start from 1
            for (int i = 1; i < tma.TextureStyles.Count; i++)
            {
                var style = tma.TextureStyles[i];
                var vm = new StyleItemViewModel
                {
                    RawIndex = i,
                    Name = style.Name ?? "",
                    DisplayLabel = string.IsNullOrWhiteSpace(style.Name)
                        ? $"Style #{i}"
                        : $"#{i}: {style.Name}",
                    Entries = style.Entries ?? new List<TextureEntry>()
                };

                // Build page info string
                if (vm.Entries.Count > 0)
                {
                    var pages = vm.Entries.Select(e => $"p{e.Page}").Distinct();
                    vm.PageInfo = string.Join(", ", pages);
                }

                // Load thumbnails
                LoadThumbnails(vm);

                _allStyles.Add(vm);
            }

            ApplyFilter();
        }

        private void LoadThumbnails(StyleItemViewModel vm)
        {
            var cache = TextureCacheService.Instance;
            var thumbs = new BitmapSource?[5];

            for (int slot = 0; slot < Math.Min(5, vm.Entries.Count); slot++)
            {
                var entry = vm.Entries[slot];

                // Convert page/tx/ty to texture index
                // index = page * 64 + (ty * 8 + tx)
                int indexInPage = entry.Ty * 8 + entry.Tx;
                int totalIndex = entry.Page * 64 + indexInPage;

                // Determine folder based on page
                string relKey;
                if (entry.Page <= 3)
                {
                    // World textures - we need the current world number
                    // For now, try to get from TexturesAccessor or use a default
                    int world = GetCurrentWorld();
                    relKey = $"world{world}_{totalIndex:000}";
                }
                else if (entry.Page <= 7)
                {
                    relKey = $"shared_{totalIndex:000}";
                }
                else
                {
                    // Page 8+ = insides or prims
                    relKey = $"shared_prims_{totalIndex:000}";
                }

                if (cache.TryGetRelative(relKey, out var bmp) && bmp != null)
                {
                    thumbs[slot] = bmp;
                }
            }

            vm.Thumb0 = thumbs[0];
            vm.Thumb1 = thumbs[1];
            vm.Thumb2 = thumbs[2];
            vm.Thumb3 = thumbs[3];
            vm.Thumb4 = thumbs[4];
        }

        private int GetCurrentWorld()
        {
            // Try to get from MapDataService via TexturesAccessor
            try
            {
                if (MapDataService.Instance.IsLoaded)
                {
                    var acc = new UrbanChaosMapEditor.Services.TexturesAccessor(MapDataService.Instance);
                    return acc.ReadTextureWorld();
                }
            }
            catch { }

            return 20; // Default fallback
        }

        private void ApplyFilter()
        {
            string filter = TxtFilter.Text?.Trim().ToLowerInvariant() ?? "";

            IEnumerable<StyleItemViewModel> filtered = _allStyles;

            if (!string.IsNullOrEmpty(filter))
            {
                filtered = _allStyles.Where(s =>
                    s.DisplayLabel.ToLowerInvariant().Contains(filter) ||
                    s.Name.ToLowerInvariant().Contains(filter) ||
                    s.RawIndex.ToString().Contains(filter));
            }

            var list = filtered.ToList();
            StylesList.ItemsSource = list;
            TxtStyleCount.Text = $"{list.Count} of {_allStyles.Count} styles";
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void StyleItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is StyleItemViewModel item)
            {
                SelectItem(item);

                // Double-click to confirm
                if (e.ClickCount == 2)
                {
                    ConfirmSelection();
                }
            }
        }

        private void SelectItem(StyleItemViewModel? item)
        {
            _selectedItem = item;

            if (item != null)
            {
                SelectionPreview.Visibility = Visibility.Visible;
                BtnSelect.IsEnabled = true;

                TxtSelectedName.Text = item.DisplayLabel;
                TxtSelectedInfo.Text = $"Raw Index: {item.RawIndex}  |  Entries: {item.Entries.Count}  |  {item.PageInfo}";

                PreviewThumb0.Source = item.Thumb0;
                PreviewThumb1.Source = item.Thumb1;
                PreviewThumb2.Source = item.Thumb2;
                PreviewThumb3.Source = item.Thumb3;
                PreviewThumb4.Source = item.Thumb4;
            }
            else
            {
                SelectionPreview.Visibility = Visibility.Collapsed;
                BtnSelect.IsEnabled = false;
            }
        }

        private void ConfirmSelection()
        {
            if (_selectedItem == null) return;

            SelectedStyleIndex = _selectedItem.RawIndex;
            WasConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            WasConfirmed = false;
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// View model for a single style item in the picker list.
    /// </summary>
    public sealed class StyleItemViewModel
    {
        public int RawIndex { get; set; }
        public string Name { get; set; } = "";
        public string DisplayLabel { get; set; } = "";
        public string PageInfo { get; set; } = "";
        public List<TextureEntry> Entries { get; set; } = new();

        // Thumbnail images for each entry slot (up to 5)
        public BitmapSource? Thumb0 { get; set; }
        public BitmapSource? Thumb1 { get; set; }
        public BitmapSource? Thumb2 { get; set; }
        public BitmapSource? Thumb3 { get; set; }
        public BitmapSource? Thumb4 { get; set; }
    }
}