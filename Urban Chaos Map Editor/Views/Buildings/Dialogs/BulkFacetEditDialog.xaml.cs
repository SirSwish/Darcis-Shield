// Views/Buildings/Dialogs/BulkFacetEditDialog.xaml.cs

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class BulkFacetEditDialog : Window
    {
        private readonly IReadOnlyList<int> _facetIds;

        public bool WasApplied { get; private set; }

        public BulkFacetEditDialog(IReadOnlyList<int> facetIds, string polygonLabel)
        {
            InitializeComponent();

            _facetIds = facetIds;

            TxtTitle.Text = $"Bulk Edit: {polygonLabel}";

            string idList = facetIds.Count <= 12
                ? string.Join("  ", facetIds.Select(id => $"#{id}"))
                : string.Join("  ", facetIds.Take(12).Select(id => $"#{id}")) + $"  +{facetIds.Count - 12} more";

            TxtSubtitle.Text = $"Applies selected values to all {facetIds.Count} facets in this polygon group.\n{idList}";

            AnalyseAndPopulate();
        }

        // ── Initialisation ────────────────────────────────────────────────────

        private void AnalyseAndPopulate()
        {
            if (_facetIds.Count == 0) return;

            var snap = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();

            // ── Collect values from all facets ──────────────────────────────

            byte?  firstHeight      = null, firstBlockHeight = null;
            short? firstY0          = null, firstStyle       = null;

            bool heightUniform = true, blockHeightUniform = true;
            bool y0Uniform     = true, styleUniform        = true;

            // Per-flag: track first value and whether all are the same
            var flagFirst    = new Dictionary<FacetFlags, bool>();
            var flagUniform  = new Dictionary<FacetFlags, bool>();
            FacetFlags[] allFlags =
            {
                FacetFlags.Invisible, FacetFlags.Inside,     FacetFlags.Dlit,
                FacetFlags.HugFloor,  FacetFlags.Electrified, FacetFlags.TwoSided,
                FacetFlags.Unclimbable, FacetFlags.OnBuilding, FacetFlags.BarbTop,
                FacetFlags.SeeThrough, FacetFlags.Open,       FacetFlags.Deg90,
                FacetFlags.TwoTextured, FacetFlags.FenceCut
            };
            foreach (var flag in allFlags)
                flagUniform[flag] = true;

            foreach (int id1 in _facetIds)
            {
                int idx0 = id1 - 1;
                if (idx0 < 0 || idx0 >= snap.Facets.Length) continue;

                DFacetRec f = snap.Facets[idx0];

                // Numeric fields
                if (firstHeight == null)      firstHeight      = f.Height;
                else if (f.Height      != firstHeight)      heightUniform      = false;

                if (firstBlockHeight == null) firstBlockHeight = f.BlockHeight;
                else if (f.BlockHeight != firstBlockHeight) blockHeightUniform = false;

                if (firstY0 == null)          firstY0          = f.Y0;
                else if (f.Y0          != firstY0)          y0Uniform          = false;

                // Resolve base style (same logic as before)
                short bStyle = 1;
                if (snap.Styles != null && f.StyleIndex < snap.Styles.Length)
                {
                    short dval = snap.Styles[f.StyleIndex];
                    bStyle = dval > 0 ? dval : (short)1;
                }
                if (firstStyle == null)  firstStyle = bStyle;
                else if (bStyle != firstStyle) styleUniform = false;

                // Flags
                foreach (var flag in allFlags)
                {
                    bool on = (f.Flags & flag) != 0;
                    if (!flagFirst.ContainsKey(flag)) flagFirst[flag] = on;
                    else if (on != flagFirst[flag])   flagUniform[flag] = false;
                }
            }

            // ── Populate fields from first facet ────────────────────────────

            short rawY0 = firstY0 ?? 0;
            TxtY0.Text = (HeightDisplaySettings.ShowRawHeights ? rawY0 : rawY0 / 64).ToString();
            TxtHeight.Text      = (firstHeight      ?? 0).ToString();
            byte rawBH = firstBlockHeight ?? 0;
            TxtBlockHeight.Text = (HeightDisplaySettings.ShowRawHeights ? rawBH : rawBH / 4).ToString();
            TxtStyle.Text       = (firstStyle       ?? 1).ToString();

            // ── Uniformity symbols for fields ───────────────────────────────

            SetFieldSym(SymY0,          y0Uniform);
            SetFieldSym(SymHeight,      heightUniform);
            SetFieldSym(SymBlockHeight, blockHeightUniform);
            SetFieldSym(SymStyle,       styleUniform);

            // ── Seed flags: uniform → checked/unchecked, mixed → indeterminate ──

            void SeedFlag(CheckBox cb, TextBlock sym, FacetFlags flag)
            {
                bool uniform = flagUniform[flag];
                cb.IsChecked = uniform ? flagFirst.GetValueOrDefault(flag) : (bool?)null;
                SetFlagSym(sym, uniform);
            }

            SeedFlag(FlagInvisible,   SymFlagInvisible,   FacetFlags.Invisible);
            SeedFlag(FlagInside,      SymFlagInside,      FacetFlags.Inside);
            SeedFlag(FlagDlit,        SymFlagDlit,        FacetFlags.Dlit);
            SeedFlag(FlagHugFloor,    SymFlagHugFloor,    FacetFlags.HugFloor);
            SeedFlag(FlagElectrified, SymFlagElectrified, FacetFlags.Electrified);
            SeedFlag(FlagTwoSided,    SymFlagTwoSided,    FacetFlags.TwoSided);
            SeedFlag(FlagUnclimbable, SymFlagUnclimbable, FacetFlags.Unclimbable);
            SeedFlag(FlagOnBuilding,  SymFlagOnBuilding,  FacetFlags.OnBuilding);
            SeedFlag(FlagBarbTop,     SymFlagBarbTop,     FacetFlags.BarbTop);
            SeedFlag(FlagSeeThrough,  SymFlagSeeThrough,  FacetFlags.SeeThrough);
            SeedFlag(FlagOpen,        SymFlagOpen,        FacetFlags.Open);
            SeedFlag(FlagDeg90,       SymFlagDeg90,       FacetFlags.Deg90);
            SeedFlag(FlagTwoTextured, SymFlagTwoTextured, FacetFlags.TwoTextured);
            SeedFlag(FlagFenceCut,    SymFlagFenceCut,    FacetFlags.FenceCut);
        }

        private static readonly Brush _greenBrush  = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        private static readonly Brush _orangeBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));

        private static void SetFieldSym(TextBlock sym, bool uniform)
        {
            sym.Text       = uniform ? "=" : "≠";
            sym.Foreground = uniform ? _greenBrush : _orangeBrush;
        }

        private static void SetFlagSym(TextBlock sym, bool uniform)
        {
            sym.Text       = uniform ? "=" : "≠";
            sym.Foreground = uniform ? _greenBrush : _orangeBrush;
        }

        // ── Apply ─────────────────────────────────────────────────────────────

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            // Parse and validate only the enabled fields
            byte  height = 0, blockHeight = 0;
            short y0 = 0, baseStyle = 0;

            if (ChkApplyY0.IsChecked == true)
            {
                if (!short.TryParse(TxtY0.Text.Trim(), out short y0Input))
                {
                    MessageBox.Show("Y Offset must be a signed integer.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                y0 = HeightDisplaySettings.ShowRawHeights ? y0Input : (short)(y0Input * 64);
            }
            if (ChkApplyHeight.IsChecked == true)
            {
                if (!byte.TryParse(TxtHeight.Text.Trim(), out height))
                {
                    MessageBox.Show("Height must be a value 0–255.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            if (ChkApplyBlockHeight.IsChecked == true)
            {
                if (!byte.TryParse(TxtBlockHeight.Text.Trim(), out byte blockHeightInput))
                {
                    MessageBox.Show("Block Height must be a positive number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                blockHeight = HeightDisplaySettings.ShowRawHeights ? blockHeightInput : (byte)(blockHeightInput * 4);
            }
            if (ChkApplyStyle.IsChecked == true)
            {
                if (!short.TryParse(TxtStyle.Text.Trim(), out baseStyle) || baseStyle < 1)
                {
                    MessageBox.Show("Base Style must be a positive style number (1 or greater).", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var snap = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();

            // Build flag mask + values.
            // Indeterminate (null) = leave that flag untouched per-facet.
            FacetFlags flagMask   = 0;
            FacetFlags flagValues = 0;

            void AccumFlag(CheckBox cb, FacetFlags flag)
            {
                if (cb.IsChecked == null) return;   // indeterminate → don't change
                flagMask |= flag;
                if (cb.IsChecked == true) flagValues |= flag;
            }

            AccumFlag(FlagInvisible,   FacetFlags.Invisible);
            AccumFlag(FlagInside,      FacetFlags.Inside);
            AccumFlag(FlagDlit,        FacetFlags.Dlit);
            AccumFlag(FlagHugFloor,    FacetFlags.HugFloor);
            AccumFlag(FlagElectrified, FacetFlags.Electrified);
            AccumFlag(FlagTwoSided,    FacetFlags.TwoSided);
            AccumFlag(FlagUnclimbable, FacetFlags.Unclimbable);
            AccumFlag(FlagOnBuilding,  FacetFlags.OnBuilding);
            AccumFlag(FlagBarbTop,     FacetFlags.BarbTop);
            AccumFlag(FlagSeeThrough,  FacetFlags.SeeThrough);
            AccumFlag(FlagOpen,        FacetFlags.Open);
            AccumFlag(FlagDeg90,       FacetFlags.Deg90);
            AccumFlag(FlagTwoTextured, FacetFlags.TwoTextured);
            AccumFlag(FlagFenceCut,    FacetFlags.FenceCut);

            // When TwoSided is being explicitly set, auto-determine HugFloor:
            //   Closed polygon → HugFloor OFF (walls enclose a roof area)
            //   Open / no polygon → HugFloor ON (fence/grounded wall)
            // This overrides any manual HugFloor checkbox selection for 2SIDED toggling.
            if (FlagTwoSided.IsChecked == true)
            {
                int buildingId1 = snap.Facets[_facetIds[0] - 1].Building;
                bool isClosed = buildingId1 > 0 && RoofEnclosureService.IsClosedPolygon(buildingId1);
                flagMask |= FacetFlags.HugFloor;
                if (isClosed)
                    flagValues &= ~FacetFlags.HugFloor;  // closed → HugFloor OFF
                else
                    flagValues |= FacetFlags.HugFloor;   // open   → HugFloor ON
            }

            bool applyHeights    = ChkApplyHeight.IsChecked      == true
                                || ChkApplyBlockHeight.IsChecked == true
                                || ChkApplyY0.IsChecked          == true;
            bool applyStyle      = ChkApplyStyle.IsChecked     == true;
            bool applyFlags      = flagMask != 0;

            if (!applyHeights && !applyStyle && !applyFlags)
            {
                MessageBox.Show("No properties are selected to apply.", "Nothing to do",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int successCount = 0;
            int failCount    = 0;

            foreach (int facetId1 in _facetIds)
            {
                bool ok = true;
                var acc = new BuildingsAccessor(MapDataService.Instance);

                int idx0 = facetId1 - 1;
                if (idx0 < 0 || idx0 >= snap.Facets.Length) { failCount++; continue; }

                DFacetRec existing = snap.Facets[idx0];

                if (applyHeights)
                {
                    // Use current value for any unchecked height field
                    byte  h  = ChkApplyHeight.IsChecked      == true ? height      : existing.Height;
                    short y  = ChkApplyY0.IsChecked          == true ? y0          : existing.Y0;
                    byte  bh = ChkApplyBlockHeight.IsChecked == true ? blockHeight : existing.BlockHeight;
                    ok &= acc.TryUpdateFacetHeights(facetId1, h, existing.FHeight, y, existing.Y1, bh);
                }

                if (applyStyle)
                    ok &= acc.TryUpdateFacetBaseStyle(facetId1, baseStyle);

                if (applyFlags)
                {
                    // Merge: keep existing bits that aren't in the mask
                    FacetFlags merged = (existing.Flags & ~flagMask) | (flagValues & flagMask);
                    ok &= acc.TryUpdateFacetFlags(facetId1, merged);
                }

                if (ok) successCount++; else failCount++;
            }

            if (failCount > 0)
            {
                MessageBox.Show(
                    $"Applied to {successCount} facet(s). {failCount} update(s) failed — check debug output.",
                    "Partial Success", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Re-run roof enclosure when height fields changed and Auto-Detect Roofs is on
            if (successCount > 0 && applyHeights)
            {
                int buildingId1 = snap.Facets[_facetIds[0] - 1].Building;

                if (buildingId1 > 0)
                {
                    var mapVm = (Application.Current.MainWindow?.DataContext as MainWindowViewModel)?.Map;

                    if (mapVm?.AutoDetectRoofs ?? true)
                    {
                        RoofEnclosureService.CheckAndApplyRoofEnclosure(
                            buildingId1,
                            applyRoofTextures: mapVm?.AutoPaintRoofTextures ?? true,
                            createWalkables:   mapVm?.AutoCreateWalkables   ?? true,
                            forceRecalculate:  true);
                    }
                }
            }

            WasApplied = successCount > 0;
            DialogResult = WasApplied;
            Close();
        }

        // ── Style Picker ──────────────────────────────────────────────────────

        private void BtnPickStyle_Click(object sender, RoutedEventArgs e)
        {
            short.TryParse(TxtStyle.Text.Trim(), out short current);
            var picker = new StylePickerWindow(current > 0 ? current : 1) { Owner = this };

            if (picker.ShowDialog() == true && picker.WasConfirmed && picker.SelectedStyleIndex > 0)
                TxtStyle.Text = picker.SelectedStyleIndex.ToString();
        }

        // ── Cancel ────────────────────────────────────────────────────────────

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
