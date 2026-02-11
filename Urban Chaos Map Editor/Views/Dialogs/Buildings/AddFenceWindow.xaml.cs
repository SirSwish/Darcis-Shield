// /Views/Dialogs/Buildings/AddFenceWindow.xaml.cs
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;
using UrbanChaosEditor.Shared.Services.Textures;
using UrbanChaosMapEditor.ViewModels;

namespace UrbanChaosMapEditor.Views.Dialogs.Buildings
{
    public partial class AddFenceWindow : Window, IFacetMultiDrawWindow
    {
        private static readonly Regex _digitsOnly = new Regex(@"^[0-9]+$");
        private static readonly Regex _signedDigitsOnly = new Regex(@"^-?[0-9]*$");

        private readonly int _buildingId1;

        public bool WasCancelled { get; private set; } = true;

        // Properties for the facet template
        public FacetType SelectedFacetType { get; private set; } = FacetType.FenceFlat;
        public byte Height { get; private set; } = 4;
        public byte FHeight { get; private set; } = 0;
        public byte BlockHeight { get; private set; } = 16;
        public short Y0 { get; private set; } = 0;
        public short Y1 { get; private set; } = 0;
        public ushort RawStyleId { get; private set; } = 1;  // Raw Style ID from TMA (NOT an index into dstyles[])
        public FacetFlags Flags { get; private set; } = FacetFlags.Unclimbable;

        public AddFenceWindow(int buildingId1)
        {
            InitializeComponent();
            _buildingId1 = buildingId1;

            TxtBuildingInfo.Text = $"Building #{buildingId1}";
            PopulateFenceTypes();

            // Hook up flag checkbox events
            ChkInvisible.Checked += OnFlagChanged;
            ChkInvisible.Unchecked += OnFlagChanged;
            ChkInside.Checked += OnFlagChanged;
            ChkInside.Unchecked += OnFlagChanged;
            ChkDlit.Checked += OnFlagChanged;
            ChkDlit.Unchecked += OnFlagChanged;
            ChkHugFloor.Checked += OnFlagChanged;
            ChkHugFloor.Unchecked += OnFlagChanged;
            ChkElectrified.Checked += OnFlagChanged;
            ChkElectrified.Unchecked += OnFlagChanged;
            ChkTwoSided.Checked += OnFlagChanged;
            ChkTwoSided.Unchecked += OnFlagChanged;
            ChkUnclimbable.Checked += OnFlagChanged;
            ChkUnclimbable.Unchecked += OnFlagChanged;
            ChkOnBuilding.Checked += OnFlagChanged;
            ChkOnBuilding.Unchecked += OnFlagChanged;
            ChkBarbTop.Checked += OnFlagChanged;
            ChkBarbTop.Unchecked += OnFlagChanged;
            ChkSeeThrough.Checked += OnFlagChanged;
            ChkSeeThrough.Unchecked += OnFlagChanged;
            ChkOpen.Checked += OnFlagChanged;
            ChkOpen.Unchecked += OnFlagChanged;
            ChkDeg90.Checked += OnFlagChanged;
            ChkDeg90.Unchecked += OnFlagChanged;
            ChkTwoTextured.Checked += OnFlagChanged;
            ChkTwoTextured.Unchecked += OnFlagChanged;
            ChkFenceCut.Checked += OnFlagChanged;
            ChkFenceCut.Unchecked += OnFlagChanged;

            UpdateFlagsDisplay();

            // Initialize style preview with default value
            UpdateStylePreview();
        }

        private void PopulateFenceTypes()
        {
            if (CmbFenceType == null) return;

            var fenceTypes = Enum.GetValues(typeof(FacetType))
                .Cast<FacetType>()
                .Where(ft => ft.ToString().StartsWith("Fence", StringComparison.Ordinal))
                .ToList();

            CmbFenceType.Items.Clear();
            foreach (var ft in fenceTypes)
            {
                CmbFenceType.Items.Add(new ComboBoxItem
                {
                    Content = ToDisplayName(ft),
                    Tag = ft
                });
            }

            // Prefer FenceFlat if present; otherwise first
            int idx = fenceTypes.IndexOf(FacetType.FenceFlat);
            CmbFenceType.SelectedIndex = idx >= 0 ? idx : (fenceTypes.Count > 0 ? 0 : -1);
        }

        private static string ToDisplayName(FacetType ft)
        {
            // e.g. FenceFlat -> "Fence Flat"
            string s = ft.ToString();
            if (s.Length <= 1) return s;
            var chars = new System.Collections.Generic.List<char>(s.Length + 4);
            chars.Add(s[0]);
            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsUpper(c) && !char.IsUpper(s[i - 1])) chars.Add(' ');
                chars.Add(c);
            }
            return new string(chars.ToArray());
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !_digitsOnly.IsMatch(e.Text);
        }

        private void SignedNumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            string newText = textBox?.Text.Insert(textBox.SelectionStart, e.Text) ?? e.Text;
            // Allow empty, "-", or valid signed number
            e.Handled = !string.IsNullOrEmpty(newText) &&
                        newText != "-" &&
                        !_signedDigitsOnly.IsMatch(newText);
        }

        private void OnFlagChanged(object sender, RoutedEventArgs e)
        {
            UpdateFlagsDisplay();
        }

        private void UpdateFlagsDisplay()
        {
            // Build flags from checkboxes
            FacetFlags flags = 0;

            if (ChkInvisible?.IsChecked == true) flags |= FacetFlags.Invisible;
            if (ChkInside?.IsChecked == true) flags |= FacetFlags.Inside;
            if (ChkDlit?.IsChecked == true) flags |= FacetFlags.Dlit;
            if (ChkHugFloor?.IsChecked == true) flags |= FacetFlags.HugFloor;
            if (ChkElectrified?.IsChecked == true) flags |= FacetFlags.Electrified;
            if (ChkTwoSided?.IsChecked == true) flags |= FacetFlags.TwoSided;
            if (ChkUnclimbable?.IsChecked == true) flags |= FacetFlags.Unclimbable;
            if (ChkOnBuilding?.IsChecked == true) flags |= FacetFlags.OnBuilding;
            if (ChkBarbTop?.IsChecked == true) flags |= FacetFlags.BarbTop;
            if (ChkSeeThrough?.IsChecked == true) flags |= FacetFlags.SeeThrough;
            if (ChkOpen?.IsChecked == true) flags |= FacetFlags.Open;
            if (ChkDeg90?.IsChecked == true) flags |= FacetFlags.Deg90;
            if (ChkTwoTextured?.IsChecked == true) flags |= FacetFlags.TwoTextured;
            if (ChkFenceCut?.IsChecked == true) flags |= FacetFlags.FenceCut;

            Flags = flags;

            if (TxtFlagsHex != null)
                TxtFlagsHex.Text = $"0x{(ushort)flags:X4}";
        }

        private void TxtStyleIndex_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateStylePreview();
        }

        private void BtnPickStyle_Click(object sender, RoutedEventArgs e)
        {
            // Get current raw style id
            int currentStyle = 1;
            if (ushort.TryParse(TxtStyleIndex.Text, out ushort parsed))
                currentStyle = parsed;

            // Open the style picker
            var picker = new StylePickerWindow(currentStyle)
            {
                Owner = this
            };

            if (picker.ShowDialog() == true && picker.WasConfirmed)
            {
                // Update the style textbox with the selected Raw Style ID
                TxtStyleIndex.Text = picker.SelectedStyleIndex.ToString();
                // Preview will update via TextChanged event
            }
        }

        private void UpdateStylePreview()
        {
            // Guard: controls not yet initialized during InitializeComponent
            if (StyleThumb0 == null || TxtStyleName == null || TxtStyleInfo == null)
                return;

            // Clear existing thumbnails
            StyleThumb0.Source = null;
            StyleThumb1.Source = null;
            StyleThumb2.Source = null;
            StyleThumb3.Source = null;
            StyleThumb4.Source = null;
            TxtStyleName.Text = "";
            TxtStyleInfo.Text = "Select a style to see preview";

            if (!ushort.TryParse(TxtStyleIndex.Text, out ushort rawStyleId) || rawStyleId == 0)
                return;

            var svc = StyleDataService.Instance;
            var tma = svc.TmaSnapshot;

            if (tma == null || tma.TextureStyles == null)
            {
                TxtStyleInfo.Text = "No TMA loaded";
                return;
            }

            // Map to TMA index (0 and 1 both map to row 1)
            int tmaIndex = StyleDataService.MapRawStyleIdToTmaIndex(rawStyleId);

            if (tmaIndex < 0 || tmaIndex >= tma.TextureStyles.Count)
            {
                TxtStyleInfo.Text = $"Style #{rawStyleId} not found";
                return;
            }

            var style = tma.TextureStyles[tmaIndex];

            // Update style name label
            string styleName = string.IsNullOrWhiteSpace(style.Name)
                ? $"Style #{rawStyleId}"
                : style.Name;
            TxtStyleName.Text = styleName;

            // Build info string
            var entries = style.Entries;
            if (entries == null || entries.Count == 0)
            {
                TxtStyleInfo.Text = $"{styleName} (no entries)";
                return;
            }

            // Load thumbnails for each entry
            var thumbImages = new Image[] { StyleThumb0, StyleThumb1, StyleThumb2, StyleThumb3, StyleThumb4 };
            var cache = TextureCacheService.Instance;
            int world = GetCurrentWorld();

            string pageInfo = "";
            for (int slot = 0; slot < Math.Min(5, entries.Count); slot++)
            {
                var entry = entries[slot];
                var bmp = GetTextureForEntry(entry, world, cache);

                if (bmp != null)
                    thumbImages[slot].Source = bmp;

                if (slot == 0)
                    pageInfo = $"Page {entry.Page}";
            }

            TxtStyleInfo.Text = $"{styleName}  |  {entries.Count} entries  |  {pageInfo}";
        }

        private BitmapSource? GetTextureForEntry(Models.Styles.TextureEntry entry, int world, TextureCacheService cache)
        {
            // Convert page/tx/ty to texture index
            // index = page * 64 + (ty * 8 + tx)
            int indexInPage = entry.Ty * 8 + entry.Tx;
            int totalIndex = entry.Page * 64 + indexInPage;

            // Determine folder based on page
            string relKey;
            if (entry.Page <= 3)
            {
                // World textures
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
                return bmp;

            return null;
        }

        private int GetCurrentWorld()
        {
            try
            {
                if (MapDataService.Instance.IsLoaded)
                {
                    var acc = new TexturesAccessor(MapDataService.Instance);
                    return acc.ReadTextureWorld();
                }
            }
            catch { }

            return 20; // Default fallback
        }

        private bool ParseAndValidate()
        {
            // Selected fence type
            if (CmbFenceType.SelectedItem is ComboBoxItem cbi && cbi.Tag is FacetType ft)
                SelectedFacetType = ft;
            else
                SelectedFacetType = FacetType.Fence;

            // Parse height fields
            if (!byte.TryParse(TxtHeight.Text, out byte height)) height = 4;
            Height = height;

            if (!byte.TryParse(TxtFHeight.Text, out byte fheight)) fheight = 0;
            FHeight = fheight;

            if (!byte.TryParse(TxtBlockHeight.Text, out byte blockHeight)) blockHeight = 16;
            BlockHeight = blockHeight;

            if (!short.TryParse(TxtY0.Text, out short y0)) y0 = 0;
            Y0 = y0;

            if (!short.TryParse(TxtY1.Text, out short y1)) y1 = 0;
            Y1 = y1;

            // Parse Raw Style ID (this is the TMA style, NOT an index into dstyles[])
            if (!ushort.TryParse(TxtStyleIndex.Text, out ushort rawStyleId)) rawStyleId = 1;
            RawStyleId = rawStyleId;

            // Flags already updated via checkboxes
            return true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            WasCancelled = true;
            Close();
        }

        private void BtnDraw_Click(object sender, RoutedEventArgs e)
        {
            if (!ParseAndValidate())
                return;

            // Start multi-draw mode
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                // Create the facet template
                // NOTE: We pass RawStyleId here - BuildingAdder will allocate dstyles[] entries
                var template = new FacetTemplate
                {
                    Type = SelectedFacetType,
                    Height = Height,
                    FHeight = FHeight,
                    BlockHeight = BlockHeight,
                    Y0 = Y0,
                    Y1 = Y1,
                    RawStyleId = RawStyleId,  // Raw Style ID from TMA, NOT dstyles index
                    Flags = Flags,
                    BuildingId1 = _buildingId1,
                    Storey = 0
                };

                // Begin drawing mode
                mainVm.Map.BeginFacetMultiDraw(this, template);

                // Hide this window
                Hide();

                mainVm.StatusMessage = $"Drawing facets for Building #{_buildingId1}. Click start then end point. Right-click to finish.";
            }

            WasCancelled = false;
        }

        /// <summary>
        /// Called when drawing is cancelled (right-click with no facets drawn).
        /// </summary>
        public void OnDrawCancelled()
        {
            WasCancelled = true;
            Close();
        }

        /// <summary>
        /// Called when drawing is completed (right-click after drawing facets).
        /// </summary>
        public void OnDrawCompleted(int facetsAdded)
        {
            WasCancelled = false;
            Close();
        }
    }
}
