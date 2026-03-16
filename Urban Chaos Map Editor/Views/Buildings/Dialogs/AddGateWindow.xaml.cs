using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using UrbanChaosEditor.Shared.Services.Textures;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Textures;
using UrbanChaosMapEditor.Services.Styles;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class AddGateWindow : Window, IFacetMultiDrawWindow
    {
        private static readonly Regex _digitsOnly = new Regex(@"^[0-9]+$");
        private static readonly Regex _signedDigitsOnly = new Regex(@"^-?[0-9]*$");

        private readonly int _buildingId1;

        public bool WasCancelled { get; private set; } = true;

        public byte Height { get; private set; } = 4;
        public byte FHeight { get; private set; } = 0;
        public byte BlockHeight { get; private set; } = 16;
        public short Y0 { get; private set; } = 0;
        public short Y1 { get; private set; } = 0;
        public ushort RawStyleId { get; private set; } = 22;
        public FacetFlags Flags { get; private set; } = FacetFlags.Unclimbable | FacetFlags.Deg90;

        public AddGateWindow(int buildingId1)
        {
            InitializeComponent();
            _buildingId1 = buildingId1;

            TxtBuildingInfo.Text = $"Building #{buildingId1}";

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
            UpdateStylePreview();
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !_digitsOnly.IsMatch(e.Text);
        }

        private void SignedNumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            string newText = textBox?.Text.Insert(textBox.SelectionStart, e.Text) ?? e.Text;

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
            int currentStyle = 22;
            if (ushort.TryParse(TxtStyleIndex.Text, out ushort parsed))
                currentStyle = parsed;

            var picker = new StylePickerWindow(currentStyle)
            {
                Owner = this
            };

            if (picker.ShowDialog() == true && picker.WasConfirmed)
            {
                TxtStyleIndex.Text = picker.SelectedStyleIndex.ToString();
            }
        }

        private void UpdateStylePreview()
        {
            if (StyleThumb0 == null || TxtStyleName == null || TxtStyleInfo == null)
                return;

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

            int tmaIndex = StyleDataService.MapRawStyleIdToTmaIndex(rawStyleId);

            if (tmaIndex < 0 || tmaIndex >= tma.TextureStyles.Count)
            {
                TxtStyleInfo.Text = $"Style #{rawStyleId} not found";
                return;
            }

            var style = tma.TextureStyles[tmaIndex];

            string styleName = string.IsNullOrWhiteSpace(style.Name)
                ? $"Style #{rawStyleId}"
                : style.Name;
            TxtStyleName.Text = styleName;

            var entries = style.Entries;
            if (entries == null || entries.Count == 0)
            {
                TxtStyleInfo.Text = $"{styleName} (no entries)";
                return;
            }

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
            int indexInPage = entry.Ty * 8 + entry.Tx;
            int totalIndex = entry.Page * 64 + indexInPage;

            string relKey;
            if (entry.Page <= 3)
            {
                relKey = $"world{world}_{totalIndex:000}";
            }
            else if (entry.Page <= 7)
            {
                relKey = $"shared_{totalIndex:000}";
            }
            else
            {
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
            catch
            {
            }

            return 20;
        }

        private bool ParseAndValidate()
        {
            if (!byte.TryParse(TxtHeight.Text, out byte height))
                height = 4;
            Height = height;

            FHeight = 0;

            if (!byte.TryParse(TxtBlockHeight.Text, out byte blockHeight))
                blockHeight = 16;
            BlockHeight = blockHeight;

            if (!short.TryParse(TxtY0.Text, out short y0))
                y0 = 0;
            Y0 = y0;
            Y1 = y0;

            if (!ushort.TryParse(TxtStyleIndex.Text, out ushort rawStyleId))
                rawStyleId = 22;
            RawStyleId = rawStyleId;

            return true;
        }

        private FacetTemplate BuildTemplate()
        {
            return new FacetTemplate
            {
                Type = FacetType.OutsideDoor,
                Height = Height,
                FHeight = FHeight,
                BlockHeight = BlockHeight,
                Y0 = Y0,
                Y1 = Y1,
                RawStyleId = RawStyleId,
                Flags = Flags,
                BuildingId1 = _buildingId1,
                Storey = 0
            };
        }

        private void BtnDraw_Click(object sender, RoutedEventArgs e)
        {
            if (!ParseAndValidate())
                return;

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                var template = BuildTemplate();

                mainVm.Map.BeginFacetMultiDraw(this, template);

                Hide();

                mainVm.StatusMessage =
                    $"Drawing gate for Building #{_buildingId1}. Click start then end point. Right-click to finish.";
            }

            WasCancelled = false;
        }

        public void OnDrawCancelled()
        {
            WasCancelled = true;
            Close();
        }

        public void OnDrawCompleted(int facetsAdded)
        {
            WasCancelled = false;

            BuildingsChangeBus.Instance.NotifyChanged();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            WasCancelled = true;
            Close();
        }
    }
}