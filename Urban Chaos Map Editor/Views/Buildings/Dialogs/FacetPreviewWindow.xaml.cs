// ============================================================
// /Views/Dialogs/Buildings/FacetPreviewWindow.xaml.cs
// DROP-IN REPLACEMENT
// ============================================================

using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.Services.Styles;
using UrbanChaosEditor.Shared.Services.Textures;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class FacetPreviewWindow : Window
    {
        private const int PanelPx = 64;
        private const string TexturesAsm = "UrbanChaosEditor.Shared";

        private DFacetRec _facet;
        private readonly int _facetIndex1;

        private short[] _dstyles = Array.Empty<short>();
        private BuildingArrays.DStoreyRec[] _storeys = Array.Empty<BuildingArrays.DStoreyRec>();
        private byte[] _paintMem = Array.Empty<byte>();

        private string? _variant;
        private int _worldNumber;

        private ObservableCollection<FlagItem>? _flagItems;

        private TextBox? _txtX0, _txtZ0, _txtX1, _txtZ1;
        private TextBox? _txtHeight, _txtY0, _txtBlockHeight;

        private static readonly Regex _digitsOnly = new Regex(@"^[0-9]+$");
        private static readonly Regex _signedDigitsOnly = new Regex(@"^-?[0-9]+$");

        private static int NormalizeStyleId(int id) => id <= 0 ? 1 : id;

        private static bool IsProceduralDoor(FacetType t) =>
            t == FacetType.Door || t == FacetType.InsideDoor;

        private static bool IsStyledOutsideDoor(FacetType t) =>
            t == FacetType.OutsideDoor;

        private static Brush GetFacetIdentityBrush(FacetType type)
        {
            return type switch
            {
                FacetType.Door or FacetType.InsideDoor or FacetType.OutsideDoor => new SolidColorBrush(Color.FromRgb(0xC7, 0x8C, 0xFF)),
                FacetType.Ladder => new SolidColorBrush(Color.FromRgb(0xFF, 0x9B, 0x3D)),
                FacetType.Fence or FacetType.FenceBrick or FacetType.FenceFlat => new SolidColorBrush(Color.FromRgb(0xFF, 0xD8, 0x4D)),
                _ => new SolidColorBrush(Color.FromRgb(0x5B, 0xD1, 0x5B)),
            };
        }

        public FacetPreviewWindow(DFacetRec facet, int facetId1)
        {
            InitializeComponent();

            _facet = CopyFacetWithHeights(facet, facet.Height, 0, facet.Y0, facet.Y0, facet.BlockHeight);
            _facetIndex1 = facetId1;

            if (!TryResolveVariantAndWorld(out _variant, out _worldNumber))
            {
                _variant = null;
                _worldNumber = 0;
            }

            Loaded += async (_, __) => await BuildUIAsync();
        }

        #region Flag Item Model

        private sealed class FlagItem
        {
            public string Name { get; init; } = "";
            public FacetFlags Bit { get; init; }
            public bool IsSet { get; set; }
        }

        private static IEnumerable<FlagItem> BuildFlagItemsEx(FacetFlags f)
        {
            FlagItem Make(string name, FacetFlags bit) => new() { Name = name, Bit = bit, IsSet = (f & bit) != 0 };

            yield return Make("Invisible", FacetFlags.Invisible);
            yield return Make("Inside", FacetFlags.Inside);
            yield return Make("Dlit", FacetFlags.Dlit);
            yield return Make("HugFloor", FacetFlags.HugFloor);
            yield return Make("Electrified", FacetFlags.Electrified);
            yield return Make("TwoSided", FacetFlags.TwoSided);
            yield return Make("Unclimbable", FacetFlags.Unclimbable);
            yield return Make("OnBuilding", FacetFlags.OnBuilding);
            yield return Make("BarbTop", FacetFlags.BarbTop);
            yield return Make("SeeThrough", FacetFlags.SeeThrough);
            yield return Make("Open", FacetFlags.Open);
            yield return Make("Deg90", FacetFlags.Deg90);
            yield return Make("TwoTextured", FacetFlags.TwoTextured);
            yield return Make("FenceCut", FacetFlags.FenceCut);
        }

        #endregion

        #region Input Validation

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !_digitsOnly.IsMatch(e.Text);
        }

        private void SignedNumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null)
            {
                e.Handled = true;
                return;
            }

            string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            e.Handled = !_signedDigitsOnly.IsMatch(newText);
        }

        #endregion

        #region Coordinate / Height Editors

        private void BuildCoordAndHeightEditors()
        {
            CoordPanel.Children.Clear();

            var coordRow1 = new StackPanel { Orientation = Orientation.Horizontal };

            coordRow1.Children.Add(MakeLabel("X0", toolTip: "X position of starting facet vertex"));
            _txtX0 = MakeBox(width: 60, toolTip: "X position of starting facet vertex");
            _txtX0.PreviewTextInput += NumericOnly_PreviewTextInput;
            _txtX0.LostFocus += Coord_LostFocus;
            coordRow1.Children.Add(_txtX0);

            coordRow1.Children.Add(MakeLabel("Z0", left: 12, toolTip: "Z position of starting facet vertex"));
            _txtZ0 = MakeBox(width: 60, toolTip: "Z position of starting facet vertex");
            _txtZ0.PreviewTextInput += NumericOnly_PreviewTextInput;
            _txtZ0.LostFocus += Coord_LostFocus;
            coordRow1.Children.Add(_txtZ0);

            var coordRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

            coordRow2.Children.Add(MakeLabel("X1", toolTip: "X position of ending facet vertex"));
            _txtX1 = MakeBox(width: 60, toolTip: "X position of ending facet vertex");
            _txtX1.PreviewTextInput += NumericOnly_PreviewTextInput;
            _txtX1.LostFocus += Coord_LostFocus;
            coordRow2.Children.Add(_txtX1);

            coordRow2.Children.Add(MakeLabel("Z1", left: 12, toolTip: "Z position of ending facet vertex"));
            _txtZ1 = MakeBox(width: 60, toolTip: "Z position of ending facet vertex");
            _txtZ1.PreviewTextInput += NumericOnly_PreviewTextInput;
            _txtZ1.LostFocus += Coord_LostFocus;
            coordRow2.Children.Add(_txtZ1);

            CoordPanel.Children.Add(coordRow1);
            CoordPanel.Children.Add(coordRow2);

            HeightPanel.Children.Clear();

            var hRow1 = new StackPanel { Orientation = Orientation.Horizontal };
            hRow1.Children.Add(MakeLabel("Height", toolTip: "Vertical height of the facet (Measured in 1/4 Storeys)"));
            _txtHeight = MakeBox(width: 70, toolTip: "Vertical height of the facet");
            _txtHeight.PreviewTextInput += NumericOnly_PreviewTextInput;
            _txtHeight.LostFocus += Height_LostFocus;
            hRow1.Children.Add(_txtHeight);
            hRow1.Children.Add(new TextBlock { Text = "¼-storey (4 = 1 floor)", Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });

            var hRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            hRow2.Children.Add(MakeLabel("Y0", toolTip: "Altitude offset to the base of the facet"));
            _txtY0 = MakeBox(width: 70, toolTip: "Altitude offset to the base of the facet");
            _txtY0.PreviewTextInput += SignedNumericOnly_PreviewTextInput;
            _txtY0.LostFocus += Height_LostFocus;
            hRow2.Children.Add(_txtY0);
            hRow2.Children.Add(new TextBlock { Text = "256 = 1 floor", Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });

            var hRow3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            hRow3.Children.Add(MakeLabel("Block Height", toolTip: "Height of each facet storey (Measured in lots of 4px)"));
            _txtBlockHeight = MakeBox(width: 70, toolTip: "Height of each facet storey");
            _txtBlockHeight.PreviewTextInput += NumericOnly_PreviewTextInput;
            _txtBlockHeight.LostFocus += Height_LostFocus;
            hRow3.Children.Add(_txtBlockHeight);
            hRow3.Children.Add(new TextBlock { Text = "4px each (16 = 1 floor)", Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });

            HeightPanel.Children.Add(hRow1);
            HeightPanel.Children.Add(hRow2);
            HeightPanel.Children.Add(hRow3);
        }

        private TextBlock MakeLabel(string text, int left = 0, string? toolTip = null)
            => new TextBlock
            {
                Text = text,
                Style = (Style)FindResource("FieldLabel"),
                Margin = new Thickness(left, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = toolTip
            };

        private TextBox MakeBox(double width = 50, string? toolTip = null)
            => new TextBox
            {
                Style = (Style)FindResource("EditableField"),
                Width = width,
                ToolTip = toolTip
            };

        #endregion

        #region Coordinate Editing

        private void Coord_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_txtX0 == null || _txtZ0 == null || _txtX1 == null || _txtZ1 == null)
                return;

            if (!byte.TryParse(_txtX0.Text, out byte x0)) x0 = _facet.X0;
            if (!byte.TryParse(_txtZ0.Text, out byte z0)) z0 = _facet.Z0;
            if (!byte.TryParse(_txtX1.Text, out byte x1)) x1 = _facet.X1;
            if (!byte.TryParse(_txtZ1.Text, out byte z1)) z1 = _facet.Z1;

            x0 = Math.Min(x0, (byte)127);
            z0 = Math.Min(z0, (byte)127);
            x1 = Math.Min(x1, (byte)127);
            z1 = Math.Min(z1, (byte)127);

            if (x0 == _facet.X0 && z0 == _facet.Z0 && x1 == _facet.X1 && z1 == _facet.Z1)
            {
                RefreshCoordDisplay();
                return;
            }

            var acc = new BuildingsAccessor(MapDataService.Instance);
            if (acc.TryUpdateFacetCoords(_facetIndex1, x0, z0, x1, z1))
            {
                _facet = CopyFacetWithCoords(_facet, x0, z0, x1, z1);
                RefreshCoordDisplay();
                DrawPreview(_facet);
            }
            else
            {
                RefreshCoordDisplay();
            }
        }

        private void RefreshCoordDisplay()
        {
            if (_txtX0 == null || _txtZ0 == null || _txtX1 == null || _txtZ1 == null)
                return;

            _txtX0.Text = _facet.X0.ToString();
            _txtZ0.Text = _facet.Z0.ToString();
            _txtX1.Text = _facet.X1.ToString();
            _txtZ1.Text = _facet.Z1.ToString();
        }

        private static DFacetRec CopyFacetWithCoords(DFacetRec f, byte x0, byte z0, byte x1, byte z1)
        {
            return new DFacetRec(f.Type, x0, z0, x1, z1, f.Height, f.FHeight,
                f.StyleIndex, f.Building, f.Storey, f.Flags, f.Y0, f.Y1,
                f.BlockHeight, f.Open, f.Dfcache, f.Shake, f.CutHole, f.Counter0, f.Counter1);
        }

        public void ApplyRedrawCoords(byte x0, byte z0, byte x1, byte z1)
        {
            var acc = new BuildingsAccessor(MapDataService.Instance);
            if (acc.TryUpdateFacetCoords(_facetIndex1, x0, z0, x1, z1))
            {
                _facet = CopyFacetWithCoords(_facet, x0, z0, x1, z1);
                RefreshCoordDisplay();
                DrawPreview(_facet);
            }
        }

        #endregion

        #region Height Editing

        private void Height_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_txtHeight == null || _txtY0 == null || _txtBlockHeight == null)
                return;

            if (!byte.TryParse(_txtHeight.Text, out byte height))
                height = _facet.Height;

            if (!short.TryParse(_txtY0.Text, out short y0))
                y0 = _facet.Y0;

            if (!byte.TryParse(_txtBlockHeight.Text, out byte blockHeight))
                blockHeight = _facet.BlockHeight;

            byte fheight = 0;
            short y1 = y0;

            if (height == _facet.Height &&
                _facet.FHeight == 0 &&
                y0 == _facet.Y0 &&
                y1 == _facet.Y1 &&
                blockHeight == _facet.BlockHeight)
            {
                RefreshHeightDisplay();
                return;
            }

            var acc = new BuildingsAccessor(MapDataService.Instance);
            if (acc.TryUpdateFacetHeights(_facetIndex1, height, fheight, y0, y1, blockHeight))
            {
                _facet = CopyFacetWithHeights(_facet, height, fheight, y0, y1, blockHeight);
                RefreshHeightDisplay();
                DrawPreview(_facet);
            }
            else
            {
                RefreshHeightDisplay();
            }
        }

        private void RefreshHeightDisplay()
        {
            if (_txtHeight == null || _txtY0 == null || _txtBlockHeight == null)
                return;

            if (_facet.FHeight != 0 || _facet.Y1 != _facet.Y0)
            {
                _facet = CopyFacetWithHeights(_facet, _facet.Height, 0, _facet.Y0, _facet.Y0, _facet.BlockHeight);
            }

            _txtHeight.Text = _facet.Height.ToString();
            _txtY0.Text = _facet.Y0.ToString();
            _txtBlockHeight.Text = _facet.BlockHeight.ToString();
        }

        private static DFacetRec CopyFacetWithHeights(DFacetRec f, byte height, byte fheight, short y0, short y1, byte blockHeight)
        {
            return new DFacetRec(f.Type, f.X0, f.Z0, f.X1, f.Z1, height, fheight,
                f.StyleIndex, f.Building, f.Storey, f.Flags, y0, y1,
                blockHeight, f.Open, f.Dfcache, f.Shake, f.CutHole, f.Counter0, f.Counter1);
        }

        #endregion

        #region Redraw / Delete

        private void BtnRedraw_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                mainVm.Map.BeginFacetRedraw(this, _facetIndex1);
                Hide();
                mainVm.StatusMessage = "Click start point (X0,Z0), then end point (X1,Z1). Right-click to cancel.";
            }
        }

        private void BtnApplyDStorey_Click(object sender, RoutedEventArgs e)
        {
            if (!ushort.TryParse(TxtDStorey.Text.Trim(), out ushort newStorey))
            {
                MessageBox.Show("Invalid DStorey value (0-65535).", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var acc = new BuildingsAccessor(MapDataService.Instance);
            if (acc.TryUpdateFacetStorey(_facetIndex1, newStorey))
            {
                _facet = new DFacetRec(
                                    _facet.Type, _facet.X0, _facet.Z0, _facet.X1, _facet.Z1,
                                    _facet.Height, _facet.FHeight, _facet.StyleIndex, _facet.Building, newStorey, _facet.Flags,
                                    _facet.Y0, _facet.Y1, _facet.BlockHeight, _facet.Open,
                                    _facet.Dfcache, _facet.Shake, _facet.CutHole,
                                    _facet.Counter0, _facet.Counter1);

                FacetFlagsText.Text = $"Flags: 0x{((ushort)_facet.Flags):X4}   Building={_facet.Building} Storey={newStorey} StyleIndex={_facet.StyleIndex}";

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    mainVm.StatusMessage = $"Facet #{_facetIndex1} DStorey set to {newStorey}.";

                BuildingsChangeBus.Instance.NotifyChanged();
            }
            else
            {
                MessageBox.Show("Failed to update DStorey.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteFacet_Click(object sender, RoutedEventArgs e)
        {
            if (_facetIndex1 <= 0)
                return;

            var deleter = new FacetDeleter(MapDataService.Instance);
            var result = deleter.TryDeleteFacet(_facetIndex1);

            if (result.IsSuccess)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show($"Failed to delete facet:\n\n{result.ErrorMessage}",
                    "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void OnRedrawCancelled()
        {
            Show();
            Activate();
        }

        public void OnRedrawCompleted()
        {
            Show();
            Activate();
        }

        #endregion

        #region Flag Editing

        private void HookFlagEvents()
        {
            FlagsList.AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent,
                new RoutedEventHandler(OnFlagToggled));
            FlagsList.AddHandler(System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent,
                new RoutedEventHandler(OnFlagToggled));
        }

        private void OnFlagToggled(object? sender, RoutedEventArgs e)
        {
            if (_flagItems == null) return;

            FacetFlags newMask = 0;
            foreach (var it in _flagItems)
                if (it.IsSet) newMask |= it.Bit;

            var ok = new BuildingsAccessor(MapDataService.Instance).TryUpdateFacetFlags(_facetIndex1, newMask);
            if (!ok) return;

            _facet = CopyFacetWithFlags(_facet, newMask);
            FacetFlagsText.Text = $"Resulting Flags: 0x{((ushort)newMask):X4}";

            SummarizeStyleAndRecipe(_facet);
            DrawPreview(_facet);
        }

        private static DFacetRec CopyFacetWithFlags(DFacetRec f, FacetFlags newFlags)
        {
            return new DFacetRec(f.Type, f.X0, f.Z0, f.X1, f.Z1, f.Height, f.FHeight,
                f.StyleIndex, f.Building, f.Storey, newFlags, f.Y0, f.Y1,
                f.BlockHeight, f.Open, f.Dfcache, f.Shake, f.CutHole, f.Counter0, f.Counter1);
        }

        #endregion

        #region UI Building

        private sealed class PaintByteVM
        {
            public int Index { get; init; }
            public string ByteHex { get; init; } = "";
            public int Page { get; init; }
            public string Flag { get; init; } = "";
        }

        private async Task BuildUIAsync()
        {
            BuildCoordAndHeightEditors();

            var arrays = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();

            FacetIdText.Text = $"Facet #{_facetIndex1}";
            FacetTypeText.Text = _facet.Type.ToString();
            FacetTypeText.Foreground = GetFacetIdentityBrush(_facet.Type);

            RefreshCoordDisplay();
            RefreshHeightDisplay();

            FacetFlagsText.Text = $"Resulting Flags: 0x{((ushort)_facet.Flags):X4}";
            TxtDStorey.Text = _facet.Storey.ToString();
            _flagItems = new ObservableCollection<FlagItem>(BuildFlagItemsEx(_facet.Flags));
            FlagsList.ItemsSource = _flagItems;
            HookFlagEvents();

            _dstyles = arrays.Styles ?? Array.Empty<short>();
            _paintMem = arrays.PaintMem ?? Array.Empty<byte>();
            _storeys = arrays.Storeys ?? Array.Empty<BuildingArrays.DStoreyRec>();

            await EnsureStylesLoadedAsync();
            SummarizeStyleAndRecipe(_facet);
            DrawPreview(_facet);
        }

        private static bool HasDualFace(DFacetRec f)
        {
            bool twoTextured = (f.Flags & FacetFlags.TwoTextured) != 0;
            bool twoSided = (f.Flags & FacetFlags.TwoSided) != 0;
            bool hugFloor = (f.Flags & FacetFlags.HugFloor) != 0;
            return !hugFloor && (twoTextured || twoSided);
        }

        private void SummarizeStyleAndRecipe(DFacetRec f)
        {
            bool twoTextured = (f.Flags & FacetFlags.TwoTextured) != 0;
            bool dualFace = HasDualFace(f);

            StyleSectionLabel.Text = dualFace
                ? (twoTextured ? "Style — Face A (Outside)" : "Style — Face A (Front)")
                : "Style";

            SummarizeSingleFaceToNewLayout(
                dstyleIndex: f.StyleIndex + (dualFace ? 1 : 0),
                modeText: StyleModeText,
                baseStyleText: BaseStyleText,
                dstoreyText: AdvancedDStoreyText,
                dstyleText: AdvancedDStyleText,
                paintIndexText: AdvancedPaintMemIndexText,
                paintCountText: AdvancedPaintMemCountText,
                isLadder: f.Type == FacetType.Ladder,
                isDoor: IsProceduralDoor(f.Type));

            if (dualFace)
            {
                int faceBIndex = f.StyleIndex;

                SecondFaceSectionLabel.Text = twoTextured
                    ? "Face B Style (Interior)"
                    : "Face B Style (Back)";

                SummarizeSingleFaceToNewLayout(
                    dstyleIndex: faceBIndex,
                    modeText: SecondFaceStyleModeText,
                    baseStyleText: SecondFaceBaseStyleText,
                    dstoreyText: SecondFaceAdvancedDStoreyText,
                    dstyleText: SecondFaceAdvancedDStyleText,
                    paintIndexText: SecondFaceAdvancedPaintMemIndexText,
                    paintCountText: SecondFaceAdvancedPaintMemCountText,
                    isLadder: false,
                    isDoor: false);

                SecondFacePanel.Visibility = Visibility.Visible;
            }
            else
            {
                SecondFacePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SummarizeSingleFaceToNewLayout(
            int dstyleIndex,
            TextBlock modeText,
            TextBlock baseStyleText,
            TextBlock dstoreyText,
            TextBlock dstyleText,
            TextBlock paintIndexText,
            TextBlock paintCountText,
            bool isLadder,
            bool isDoor)
        {
            if (isLadder)
            {
                modeText.Text = "Mode: Styled";
                baseStyleText.Text = "Base Style: (procedural ladder)";
                dstoreyText.Text = "DStorey: (n/a)";
                dstyleText.Text = $"DStyle: {dstyleIndex}";
                paintIndexText.Text = "PaintMem Index: (n/a)";
                paintCountText.Text = "PaintMem Count: (n/a)";
                return;
            }

            if (isDoor)
            {
                modeText.Text = "Mode: Styled";
                baseStyleText.Text = "Base Style: (procedural door)";
                dstoreyText.Text = "DStorey: (n/a)";
                dstyleText.Text = $"DStyle: {dstyleIndex}";
                paintIndexText.Text = "PaintMem Index: (n/a)";
                paintCountText.Text = "PaintMem Count: (n/a)";
                return;
            }

            if (_dstyles.Length == 0 || dstyleIndex < 0 || dstyleIndex >= _dstyles.Length)
            {
                modeText.Text = "Mode: Styled";
                baseStyleText.Text = $"Base Style: {dstyleIndex}";
                dstoreyText.Text = "DStorey: (unknown)";
                dstyleText.Text = $"DStyle: {dstyleIndex}";
                paintIndexText.Text = "PaintMem Index: (n/a)";
                paintCountText.Text = "PaintMem Count: (n/a)";
                return;
            }

            short val = _dstyles[dstyleIndex];
            if (val >= 0)
            {
                modeText.Text = "Mode: Styled";
                baseStyleText.Text = $"Base Style: {NormalizeStyleId(val)}";
                dstoreyText.Text = "DStorey: (none)";
                dstyleText.Text = $"DStyle: {dstyleIndex}";
                paintIndexText.Text = "PaintMem Index: (n/a)";
                paintCountText.Text = "PaintMem Count: (n/a)";
                return;
            }

            int sid = -val;
            modeText.Text = "Mode: Painted";

            if (sid < 1 || sid >= _storeys.Length)
            {
                baseStyleText.Text = "Base Style: (invalid)";
                dstoreyText.Text = $"DStorey: {sid}";
                dstyleText.Text = $"DStyle: {dstyleIndex}";
                paintIndexText.Text = "PaintMem Index: (invalid)";
                paintCountText.Text = "PaintMem Count: (invalid)";
                return;
            }

            var ds = _storeys[sid];
            baseStyleText.Text = $"Base Style: {NormalizeStyleId(ds.StyleIndex)}";
            dstoreyText.Text = $"DStorey: {sid}";
            dstyleText.Text = $"DStyle: {dstyleIndex}";
            paintIndexText.Text = $"PaintMem Index: {ds.PaintIndex}";
            paintCountText.Text = $"PaintMem Count: {ds.Count}";
        }

        private static bool UsesVerticalUnitsAsPanels(FacetType t) =>
            t == FacetType.Fence || t == FacetType.FenceFlat ||
            t == FacetType.FenceBrick || t == FacetType.Ladder || t == FacetType.Trench;

        private string BuildRawRecipeString(int rawStyleId)
        {
            var tma = StyleDataService.Instance.TmaSnapshot;
            if (tma == null) return "(style.tma not loaded)";

            int idx = StyleDataService.MapRawStyleIdToTmaIndex(rawStyleId <= 0 ? 1 : rawStyleId);
            if (idx < 0 || idx >= tma.TextureStyles.Count) return "(style out of range)";

            var style = tma.TextureStyles[idx];
            var sb = new StringBuilder();
            sb.Append($"Style {idx}: ");
            for (int i = 0; i < style.Entries.Count; i++)
            {
                var e = style.Entries[i];
                if (i > 0) sb.Append(" | ");
                sb.Append($"[{i}] P{e.Page} Tx{e.Tx} Ty{e.Flip}");
            }
            return sb.ToString();
        }

        private static string ToHexLine(byte[] data)
        {
            if (data == null || data.Length == 0) return "(empty)";
            var sb = new StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private byte[] GetPaintBytes(ref BuildingArrays.DStoreyRec ds)
        {
            int count = ds.Count;
            if (count <= 0)
                return Array.Empty<byte>();

            int start = ds.PaintIndex;
            if (_paintMem == null || start < 0 || start + count > _paintMem.Length)
                return Array.Empty<byte>();

            var result = new byte[count];
            Array.Copy(_paintMem, start, result, 0, count);
            return result;
        }

        #endregion

        #region Preview Drawing

        private static bool UsesFenceStretchPreview(FacetType t) =>
            t == FacetType.Fence ||
            t == FacetType.FenceBrick ||
            t == FacetType.FenceFlat ||
            t == FacetType.OutsideDoor;

        private static bool UsesBlockHeightPreview(FacetType t) =>
            t == FacetType.Normal ||
            t == FacetType.Wall ||
            t == FacetType.NormalFoundation ||
            UsesFenceStretchPreview(t);

        private static int NormalizeBlockHeightUnits(byte blockHeight)
        {
            int bh = blockHeight;
            if (bh < 4 || (bh % 4) != 0)
                bh = 16;
            return bh;
        }

        private static int GetFullBlockPreviewSizePx(DFacetRec f)
        {
            int bh = NormalizeBlockHeightUnits(f.BlockHeight);
            return Math.Max(1, (PanelPx * bh) / 16);
        }

        private static int GetFullBlocksDownForPreview(DFacetRec f)
        {
            return Math.Max(0, (int)f.Height / 4);
        }

        private static int GetPartialBlockUnits(DFacetRec f)
        {
            return Math.Max(0, (int)f.Height % 4);
        }

        private static int GetPartialBlockPreviewSizePx(DFacetRec f)
        {
            int partialUnits = GetPartialBlockUnits(f);
            if (partialUnits <= 0)
                return 0;

            int fullBlockPx = GetFullBlockPreviewSizePx(f);
            return Math.Max(1, (fullBlockPx * partialUnits) / 4);
        }

        private static int GetFenceStretchPanelSizePx(DFacetRec f)
        {
            int fullBlockPx = GetFullBlockPreviewSizePx(f);
            int heightUnits = Math.Max(1, (int)f.Height);
            return Math.Max(1, (fullBlockPx * heightUnits) / 4);
        }

        private static int GetBlocksDownForPreview(DFacetRec f)
        {
            if (UsesFenceStretchPreview(f.Type))
                return 1;

            if (UsesBlockHeightPreview(f.Type))
            {
                int fullBlocks = GetFullBlocksDownForPreview(f);
                int partialUnits = GetPartialBlockUnits(f);
                return Math.Max(1, fullBlocks + (partialUnits > 0 ? 1 : 0));
            }

            int totalPixelsY = f.Height * 16 + f.FHeight;
            return Math.Max(1, (totalPixelsY + (PanelPx - 1)) / PanelPx);
        }

        private BitmapSource BuildPreviewBitmapForWallLikeFacet(BitmapSource bmp, DFacetRec f, bool isPartialBlock)
        {
            if (bmp == null)
                return bmp;

            int fullBlockPx = GetFullBlockPreviewSizePx(f);
            int targetPx = isPartialBlock ? GetPartialBlockPreviewSizePx(f) : fullBlockPx;
            if (targetPx <= 0)
                targetPx = fullBlockPx;

            int bh = NormalizeBlockHeightUnits(f.BlockHeight);
            BitmapSource sourceForScaling = bmp;

            if (bh < 16)
            {
                int srcHeight = bmp.PixelHeight;
                int srcWidth = bmp.PixelWidth;
                if (srcHeight > 0 && srcWidth > 0)
                {
                    int cropHeight = Math.Max(1, (srcHeight * bh) / 16);
                    cropHeight = Math.Min(srcHeight, cropHeight);
                    sourceForScaling = new CroppedBitmap(bmp, new Int32Rect(0, 0, srcWidth, cropHeight));
                }
            }

            double scaleX = (double)PanelPx / Math.Max(1, sourceForScaling.PixelWidth);
            double scaleY = (double)targetPx / Math.Max(1, sourceForScaling.PixelHeight);
            var transformed = new TransformedBitmap(sourceForScaling, new ScaleTransform(scaleX, scaleY));
            transformed.Freeze();
            return transformed;
        }

        private BitmapSource BuildPreviewBitmapForFenceStretch(BitmapSource bmp, DFacetRec f)
        {
            if (bmp == null)
                return bmp;

            int targetHeightPx = GetFenceStretchPanelSizePx(f);
            int bh = NormalizeBlockHeightUnits(f.BlockHeight);
            BitmapSource sourceForScaling = bmp;

            if (bh < 16)
            {
                int srcHeight = bmp.PixelHeight;
                int srcWidth = bmp.PixelWidth;
                if (srcHeight > 0 && srcWidth > 0)
                {
                    int cropHeight = Math.Max(1, (srcHeight * bh) / 16);
                    cropHeight = Math.Min(srcHeight, cropHeight);
                    sourceForScaling = new CroppedBitmap(bmp, new Int32Rect(0, 0, srcWidth, cropHeight));
                }
            }

            double scaleX = (double)PanelPx / Math.Max(1, sourceForScaling.PixelWidth);
            double scaleY = (double)targetHeightPx / Math.Max(1, sourceForScaling.PixelHeight);

            var transformed = new TransformedBitmap(sourceForScaling, new ScaleTransform(scaleX, scaleY));
            transformed.Freeze();
            return transformed;
        }
        private static bool HasExplicitSideB(DFacetRec f)
        {
            return (f.Flags & FacetFlags.TwoTextured) != 0 ||
                   (f.Flags & FacetFlags.TwoSided) != 0;
        }

        private void DrawPreview(DFacetRec f)
        {
            bool hasSideB = HasExplicitSideB(f);
            bool twoTextured = (f.Flags & FacetFlags.TwoTextured) != 0;

            // For Inside facets the engine renders the Side A slot (StyleIndex+1) on the interior
            // and Side B slot (StyleIndex+2) on the exterior, so swap the labels.
            bool isInside = hasSideB && (f.Flags & FacetFlags.Inside) != 0;
            FaceALabel.Text = isInside ? "Side B" : "Side A";
            FaceBLabel.Text = isInside ? "Side A" : "Side B";

            if (f.Type == FacetType.Ladder)
            {
                DrawLadderPreview(f);

                ClearSideBPreview();
                if (hasSideB)
                {
                    // Still keep side B area, but ladder has no alternate rendering implemented.
                    // Leave it blank unless a real second-face path is ever needed.
                }

                return;
            }

            if (IsProceduralDoor(f.Type))
            {
                DrawDoorPreview(f);

                ClearSideBPreview();
                if (hasSideB)
                {
                    // Same rule: keep the space, render nothing unless/ until a true side-B procedural variant exists.
                }

                return;
            }

            int dx = Math.Abs(f.X1 - f.X0);
            int dz = Math.Abs(f.Z1 - f.Z0);
            int panelsAcross = Math.Max(dx, dz);
            if (panelsAcross <= 0) panelsAcross = 1;

            bool useFenceStretchPreview = UsesFenceStretchPreview(f.Type);
            bool useBlockHeightPreview = UsesBlockHeightPreview(f.Type);
            int fullBlockPreviewPx = useBlockHeightPreview ? GetFullBlockPreviewSizePx(f) : PanelPx;
            int partialBlockPreviewPx = useBlockHeightPreview ? GetPartialBlockPreviewSizePx(f) : 0;
            int fullBlocksDown = useBlockHeightPreview ? GetFullBlocksDownForPreview(f) : 0;
            int panelsDown = GetBlocksDownForPreview(f);

            int panelWidthPx = PanelPx;
            int width = panelsAcross * panelWidthPx;
            int height = useFenceStretchPreview
                ? GetFenceStretchPanelSizePx(f)
                : useBlockHeightPreview
                    ? (fullBlocksDown * fullBlockPreviewPx) + partialBlockPreviewPx
                    : panelsDown * PanelPx;

            PanelCanvas.Children.Clear();
            GridCanvas.Children.Clear();
            PanelCanvas.Width = width;
            PanelCanvas.Height = height;
            GridCanvas.Width = width;
            GridCanvas.Height = height;

            double yCursor = 0;
            for (int rowFromTop = 0; rowFromTop < panelsDown; rowFromTop++)
            {
                int rowFromBottom = panelsDown - 1 - rowFromTop;
                bool isPartialBlock = useBlockHeightPreview && rowFromBottom == fullBlocksDown && partialBlockPreviewPx > 0;
                int rowHeightPx = useFenceStretchPreview
                    ? GetFenceStretchPanelSizePx(f)
                    : useBlockHeightPreview
                        ? (isPartialBlock ? partialBlockPreviewPx : fullBlockPreviewPx)
                        : PanelPx;

                for (int col = 0; col < panelsAcross; col++)
                {
                    if (TryResolvePanelTile(col, rowFromBottom, panelsAcross, panelsDown,
                                            out byte page, out byte tx, out byte ty, out byte flip))
                    {
                        int tileId = page * 64 + ty * 8 + tx;
                        string tooltipText = useBlockHeightPreview
                            ? $"tex{tileId:D3}hi (block {f.BlockHeight} => {fullBlockPreviewPx}px, row {rowFromBottom}{(isPartialBlock ? ", partial" : "")})"
                            : $"tex{tileId:D3}hi";

                        if (TryLoadTileBitmap(page, tx, ty, flip, out var bmp))
                        {
                            BitmapSource displayBmp = useFenceStretchPreview
                                ? BuildPreviewBitmapForFenceStretch(bmp!, f)
                                : useBlockHeightPreview
                                    ? BuildPreviewBitmapForWallLikeFacet(bmp!, f, isPartialBlock)
                                    : bmp!;

                            var img = new Image
                            {
                                Width = panelWidthPx,
                                Height = rowHeightPx,
                                Source = displayBmp,
                                Stretch = Stretch.Fill,
                                ToolTip = tooltipText
                            };
                            Canvas.SetLeft(img, col * panelWidthPx);
                            Canvas.SetTop(img, yCursor);
                            PanelCanvas.Children.Add(img);
                        }
                        else
                        {
                            AddPlaceholderRect(col, yCursor, tooltipText, panelWidthPx, rowHeightPx);
                        }
                    }
                    else
                    {
                        AddPlaceholderRect(col, yCursor, "(no tile)", panelWidthPx, rowHeightPx);
                    }
                }

                yCursor += rowHeightPx;
            }

            DrawGrid(GridCanvas, width, height, panelWidthPx, useFenceStretchPreview ? height : useBlockHeightPreview ? fullBlockPreviewPx : PanelPx);
            GridCanvas.Children.Add(new Rectangle
            {
                Width = width,
                Height = height,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            });

            if (hasSideB)
            {
                DrawFaceBPreview(
                    f,
                    panelsAcross,
                    panelsDown,
                    panelWidthPx,
                    fullBlockPreviewPx,
                    partialBlockPreviewPx,
                    fullBlocksDown,
                    useBlockHeightPreview,
                    useFenceStretchPreview);
            }
            else
            {
                ClearSideBPreview();
            }
        }

        private void ClearSideBPreview()
        {
            FaceBPanelCanvas.Children.Clear();
            FaceBGridCanvas.Children.Clear();

            FaceBPanelCanvas.Width = 0;
            FaceBPanelCanvas.Height = 0;
            FaceBGridCanvas.Width = 0;
            FaceBGridCanvas.Height = 0;
        }

        private void DrawFaceBPreview(
    DFacetRec f,
    int panelsAcross,
    int panelsDown,
    int panelWidthPx,
    int fullBlockPreviewPx,
    int partialBlockPreviewPx,
    int fullBlocksDown,
    bool useBlockHeightPreview,
    bool useFenceStretchPreview)
        {
            int width = panelsAcross * panelWidthPx;
            int height = useFenceStretchPreview
                ? GetFenceStretchPanelSizePx(f)
                : useBlockHeightPreview
                    ? (fullBlocksDown * fullBlockPreviewPx) + partialBlockPreviewPx
                    : panelsDown * PanelPx;

            FaceBPanelCanvas.Children.Clear();
            FaceBGridCanvas.Children.Clear();
            FaceBPanelCanvas.Width = width;
            FaceBPanelCanvas.Height = height;
            FaceBGridCanvas.Width = width;
            FaceBGridCanvas.Height = height;

            // Dual-face layout: [SideB_header=X+0, SideA_0=X+1, SideB_0=X+2, SideA_1=X+3, ...]
            // Normal:  Side B panel shows StyleIndex+2 (inner face), read forward  (pos = col).
            // Inside:  Side B panel shows StyleIndex+1 (now the interior-visible face), read reversed (pos = N-1-col).
            bool isDualFaceB = HasDualFace(f);
            bool isFaceBInside = isDualFaceB && (f.Flags & FacetFlags.Inside) != 0;
            int faceBStyleStart = isDualFaceB
                ? (isFaceBInside ? f.StyleIndex + 1 : f.StyleIndex + 2)
                : f.StyleIndex;
            bool faceBReadForward = isDualFaceB && !isFaceBInside;

            double yCursor = 0;
            for (int rowFromTop = 0; rowFromTop < panelsDown; rowFromTop++)
            {
                int rowFromBottom = panelsDown - 1 - rowFromTop;
                bool isPartialBlock = useBlockHeightPreview && rowFromBottom == fullBlocksDown && partialBlockPreviewPx > 0;
                int rowHeightPx = useFenceStretchPreview
                    ? GetFenceStretchPanelSizePx(f)
                    : useBlockHeightPreview
                        ? (isPartialBlock ? partialBlockPreviewPx : fullBlockPreviewPx)
                        : PanelPx;

                for (int col = 0; col < panelsAcross; col++)
                {
                    if (TryResolvePanelTileForFace(col, rowFromBottom, panelsAcross, panelsDown, faceBStyleStart, faceBReadForward,
                                                    out byte page, out byte tx, out byte ty, out byte flip))
                    {
                        int tileId = page * 64 + ty * 8 + tx;
                        string tooltipText = useBlockHeightPreview
                            ? $"Side B: tex{tileId:D3}hi (block {f.BlockHeight} => {fullBlockPreviewPx}px, row {rowFromBottom}{(isPartialBlock ? ", partial" : "")})"
                            : $"Side B: tex{tileId:D3}hi";

                        if (TryLoadTileBitmap(page, tx, ty, flip, out var bmp))
                        {
                            BitmapSource displayBmp = useFenceStretchPreview
                                ? BuildPreviewBitmapForFenceStretch(bmp!, f)
                                : useBlockHeightPreview
                                    ? BuildPreviewBitmapForWallLikeFacet(bmp!, f, isPartialBlock)
                                    : bmp!;

                            var img = new Image
                            {
                                Width = panelWidthPx,
                                Height = rowHeightPx,
                                Source = displayBmp,
                                Stretch = Stretch.Fill,
                                ToolTip = tooltipText
                            };
                            Canvas.SetLeft(img, col * panelWidthPx);
                            Canvas.SetTop(img, yCursor);
                            FaceBPanelCanvas.Children.Add(img);
                        }
                        else
                        {
                            AddPlaceholderRectTo(FaceBPanelCanvas, col, yCursor, tooltipText,
                                Color.FromRgb(0x40, 0x30, 0x30), panelWidthPx, rowHeightPx);
                        }
                    }
                    else
                    {
                        AddPlaceholderRectTo(FaceBPanelCanvas, col, yCursor, "(no tile - Side B)",
                            Color.FromRgb(0x30, 0x20, 0x20), panelWidthPx, rowHeightPx);
                    }
                }

                yCursor += rowHeightPx;
            }

            DrawGrid(FaceBGridCanvas, width, height, panelWidthPx, useFenceStretchPreview ? height : useBlockHeightPreview ? fullBlockPreviewPx : PanelPx);
            FaceBGridCanvas.Children.Add(new Rectangle
            {
                Width = width,
                Height = height,
                Stroke = Brushes.Coral,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            });
        }

        private void DrawLadderPreview(DFacetRec f)
        {
            const int RungsPerSegment = 4;
            const int RungThickness = 4;
            const int RailWidth = 4;
            const double WidthScale = 0.67;

            int panelsAcross = 1;
            int totalPixelsY = f.Height * 16 + f.FHeight;
            int panelsDown = Math.Max(1, (totalPixelsY + (PanelPx - 1)) / PanelPx);

            int fullWidth = panelsAcross * PanelPx;
            int scaledWidth = (int)(fullWidth * WidthScale);
            int height = panelsDown * PanelPx;

            PanelCanvas.Children.Clear();
            GridCanvas.Children.Clear();
            PanelCanvas.Width = scaledWidth;
            PanelCanvas.Height = height;
            GridCanvas.Width = scaledWidth;
            GridCanvas.Height = height;

            PanelCanvas.Children.Add(new Rectangle { Width = scaledWidth, Height = height, Fill = Brushes.Black });

            var leftRail = new Rectangle { Width = RailWidth, Height = height, Fill = Brushes.White };
            Canvas.SetLeft(leftRail, 0);
            PanelCanvas.Children.Add(leftRail);

            var rightRail = new Rectangle { Width = RailWidth, Height = height, Fill = Brushes.White };
            Canvas.SetLeft(rightRail, scaledWidth - RailWidth);
            PanelCanvas.Children.Add(rightRail);

            for (int seg = 0; seg < panelsDown; seg++)
            {
                for (int r = 0; r < RungsPerSegment; r++)
                {
                    double rungY = seg * PanelPx + (r + 0.5) * PanelPx / RungsPerSegment - RungThickness / 2.0;
                    var rung = new Rectangle { Width = scaledWidth, Height = RungThickness, Fill = Brushes.White };
                    Canvas.SetTop(rung, rungY);
                    PanelCanvas.Children.Add(rung);
                }
            }

            DrawGrid(GridCanvas, scaledWidth, height, PanelPx, PanelPx);
        }

        private void DrawDoorPreview(DFacetRec f)
        {
            int dx = Math.Abs(f.X1 - f.X0);
            int dz = Math.Abs(f.Z1 - f.Z0);
            int panelsAcross = Math.Max(dx, dz);
            if (panelsAcross <= 0) panelsAcross = 1;

            int totalPixelsY = f.Height * 16 + f.FHeight;
            int panelsDown = Math.Max(1, (totalPixelsY + (PanelPx - 1)) / PanelPx);

            int width = panelsAcross * PanelPx;
            int height = panelsDown * PanelPx;

            PanelCanvas.Children.Clear();
            GridCanvas.Children.Clear();
            PanelCanvas.Width = width;
            PanelCanvas.Height = height;
            GridCanvas.Width = width;
            GridCanvas.Height = height;

            PanelCanvas.Children.Add(new Rectangle
            {
                Width = width,
                Height = height,
                Fill = Brushes.Black,
                Stroke = Brushes.Gray,
                StrokeThickness = 2
            });

            var label = new TextBlock
            {
                Text = f.Type.ToString().ToUpperInvariant(),
                Foreground = Brushes.Gray,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, width / 2.0 - 45);
            Canvas.SetTop(label, height / 2.0 - 10);
            PanelCanvas.Children.Add(label);
        }

        private void AddPlaceholderRect(int col, double top, string tooltip, int panelWidthPx, int panelHeightPx)
        {
            AddPlaceholderRectTo(PanelCanvas, col, top, tooltip, Color.FromRgb(0x30, 0x30, 0x30), panelWidthPx, panelHeightPx);
        }

        private static void AddPlaceholderRectTo(Canvas canvas, int col, double top, string tooltip, Color color, int panelWidthPx, int panelHeightPx)
        {
            var rect = new Rectangle
            {
                Width = panelWidthPx,
                Height = panelHeightPx,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)),
                StrokeThickness = 0.5,
                ToolTip = tooltip
            };
            Canvas.SetLeft(rect, col * panelWidthPx);
            Canvas.SetTop(rect, top);
            canvas.Children.Add(rect);
        }

        private static void DrawGrid(Canvas c, int width, int height, int stepX, int stepY)
        {
            var g = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
            for (int x = stepX; x < width; x += stepX)
                c.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = height, StrokeThickness = 1, Stroke = g });

            for (int y = stepY; y < height; y += stepY)
                c.Children.Add(new Line { X1 = 0, Y1 = y, X2 = width, Y2 = y, StrokeThickness = 1, Stroke = g });
        }

        #endregion

        #region Tile Resolution

        private bool TryResolvePanelTile(
            int col, int rowFromBottom, int panelsAcross, int panelsDown,
            out byte page, out byte tx, out byte ty, out byte flip)
        {
            page = tx = ty = flip = 0;

            if (_dstyles == null || _dstyles.Length == 0) return false;
            if (rowFromBottom < 0 || rowFromBottom >= panelsDown) return false;

            bool dualFace = HasDualFace(_facet);
            int step = dualFace ? 2 : 1;

            // For Inside dual-face facets the Side A panel shows the SideB slot (StyleIndex+2),
            // read forward. For all other facets it shows StyleIndex+1, read reversed.
            bool isInsideSwap = dualFace && (_facet.Flags & FacetFlags.Inside) != 0;
            int slotOffset = dualFace ? (isInsideSwap ? 2 : 1) : 0;
            int styleIndexForRow = _facet.StyleIndex + slotOffset + rowFromBottom * step;
            if (styleIndexForRow < 0 || styleIndexForRow >= _dstyles.Length) return false;

            short dval = _dstyles[styleIndexForRow];

            int count = panelsAcross + 1;
            int pos = isInsideSwap ? col : panelsAcross - 1 - col;

            if (!TryResolveTileIdForCell(dval, pos, count, out int tileId, out byte flipFlag)) return false;
            if (tileId < 0) return false;

            page = (byte)(tileId / 64);
            int idxInPage = tileId % 64;
            tx = (byte)(idxInPage % 8);
            ty = (byte)(idxInPage / 8);
            flip = flipFlag;

            return true;
        }

        private bool TryResolvePanelTileForFace(
            int col, int rowFromBottom, int panelsAcross, int panelsDown,
            int faceStyleStart, bool isSideB,
            out byte page, out byte tx, out byte ty, out byte flip)
        {
            page = tx = ty = flip = 0;

            if (_dstyles == null || _dstyles.Length == 0) return false;
            if (rowFromBottom < 0 || rowFromBottom >= panelsDown) return false;

            int styleIndexForRow = faceStyleStart + rowFromBottom * 2;
            if (styleIndexForRow < 0 || styleIndexForRow >= _dstyles.Length) return false;

            short dval = _dstyles[styleIndexForRow];

            int count = panelsAcross + 1;
            // Side A: engine reads reversed (pos = N-1-col). Side B: engine reads forward (pos = col).
            int pos = isSideB ? col : panelsAcross - 1 - col;

            if (!TryResolveTileIdForCell(dval, pos, count, out int tileId, out byte flipFlag)) return false;
            if (tileId < 0) return false;

            page = (byte)(tileId / 64);
            int idxInPage = tileId % 64;
            tx = (byte)(idxInPage % 8);
            ty = (byte)(idxInPage / 8);
            flip = flipFlag;

            return true;
        }

        private bool TryResolveTileIdForCell(short dstyleValue, int pos, int count, out int tileId, out byte flip)
        {
            tileId = -1;
            flip = 0;

            if (dstyleValue >= 0)
                return ResolveRawTileId(dstyleValue, pos, count, out tileId, out flip);

            int storeyId = -dstyleValue;
            if (_storeys == null || storeyId < 1 || storeyId >= _storeys.Length) return false;

            var ds = _storeys[storeyId];
            return ResolvePaintedTileId(ds, pos, count, out tileId, out flip);
        }

        private bool ResolveRawTileId(int rawStyleId, int pos, int count, out int tileId, out byte flip)
        {
            tileId = -1;
            flip = 0;

            var tma = StyleDataService.Instance.TmaSnapshot;
            if (tma == null) return false;

            int styleId = rawStyleId <= 0 ? 1 : rawStyleId;
            int idx = StyleDataService.MapRawStyleIdToTmaIndex(styleId);
            if (idx < 0 || idx >= tma.TextureStyles.Count) return false;

            var entries = tma.TextureStyles[idx].Entries;
            if (entries == null || entries.Count == 0) return false;

            int pieceIndex = pos == 0 ? 2 : (pos == count - 2 ? 0 : 1);
            if (pieceIndex >= entries.Count) pieceIndex = entries.Count - 1;

            var e = entries[pieceIndex];
            tileId = e.Page * 64 + e.Ty * 8 + e.Tx;
            flip = e.Flip;
            return true;
        }

        private bool ResolvePaintedTileId(BuildingArrays.DStoreyRec ds, int pos, int count, out int tileId, out byte flip)
        {
            tileId = -1;
            flip = 0;
            int baseStyle = ds.StyleIndex;

            if (_paintMem == null || _paintMem.Length == 0 || ds.Count == 0)
                return ResolveRawTileId(baseStyle, pos, count, out tileId, out flip);

            int paintStart = ds.PaintIndex;
            int paintCount = ds.Count;

            if (paintStart < 0 || paintStart + paintCount > _paintMem.Length || pos >= paintCount)
                return ResolveRawTileId(baseStyle, pos, count, out tileId, out flip);

            byte raw = _paintMem[paintStart + pos];
            flip = (byte)(((raw & 0x80) != 0) ? 1 : 0);
            int val = raw & 0x7F;

            if (val == 0)
                return ResolveRawTileId(baseStyle, pos, count, out tileId, out flip);

            tileId = val;
            return true;
        }

        #endregion

        #region Resource Loading

        private bool TryResolveVariantAndWorld(out string? variant, out int world)
        {
            variant = null;
            world = 0;

            try
            {
                var shell = Application.Current.MainWindow?.DataContext;
                if (shell == null) return false;

                var mapProp = shell.GetType().GetProperty("Map");
                var map = mapProp?.GetValue(shell);
                if (map == null) return false;

                var mapType = map.GetType();
                var useBetaProp = mapType.GetProperty("UseBetaTextures");
                var worldProp = mapType.GetProperty("TextureWorld");

                if (useBetaProp?.GetValue(map) is bool useBeta &&
                    worldProp?.GetValue(map) is int w && w > 0)
                {
                    variant = useBeta ? "Beta" : "Release";
                    world = w;
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        private async Task EnsureStylesLoadedAsync()
        {
            if (_worldNumber <= 0) return;
            if (StyleDataService.Instance.IsLoaded) return;

            // For custom worlds (>20), try disk first
            if (_worldNumber > 20)
            {
                var customTmaPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "CustomTextures",
                    $"world{_worldNumber}", "style.tma");

                if (System.IO.File.Exists(customTmaPath))
                {
                    await StyleDataService.Instance.LoadAsync(customTmaPath);
                    return;
                }
            }

            // For shipped worlds, try embedded resource
            if (!string.IsNullOrWhiteSpace(_variant))
            {
                string packUri =
                    $"pack://application:,,,/{TexturesAsm};component/Assets/Textures/{_variant}/world{_worldNumber}/style.tma";
                try
                {
                    var sri = Application.GetResourceStream(new Uri(packUri, UriKind.Absolute));
                    if (sri?.Stream != null)
                        await StyleDataService.Instance.LoadFromResourceStreamAsync(sri.Stream, packUri);
                }
                catch { }
            }
        }

        private bool TryLoadTileBitmap(byte page, byte tx, byte ty, byte flip, out BitmapSource? bmp)
        {
            return TextureResolver.TryResolve(page, tx, ty, flip, _worldNumber, _variant, out bmp);
        }

        #endregion

        #region Paint / Change Style

        private void BtnPaintFacet_Click(object sender, RoutedEventArgs e)
        {
            if (_facet.Type == FacetType.Ladder)
            {
                MessageBox.Show("Ladders cannot be painted - they use procedural textures.",
                    "Cannot Paint", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (IsProceduralDoor(_facet.Type))
            {
                MessageBox.Show("Door and InsideDoor facets cannot be painted - they render procedurally.",
                    "Cannot Paint", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Inside dual-face facets render their interior on the Side A slot (faceOffset=0 = SideB data
            // is exterior). So "Paint Facet" for an Inside facet should paint faceOffset=0 (SideB slot).
            bool isInsideDual = HasDualFace(_facet) && (_facet.Flags & FacetFlags.Inside) != 0;
            int faceOffsetA = isInsideDual ? 0 : 1;
            var painter = new FacetPainterWindow(_facet, _facetIndex1, faceOffset: faceOffsetA) { Owner = this };

            if (painter.ShowDialog() == true)
            {
                var arrays = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();

                if (arrays.Facets != null && _facetIndex1 >= 1 && _facetIndex1 <= arrays.Facets.Length)
                {
                    _dstyles = arrays.Styles ?? Array.Empty<short>();
                    _paintMem = arrays.PaintMem ?? Array.Empty<byte>();
                    _storeys = arrays.Storeys ?? Array.Empty<BuildingArrays.DStoreyRec>();

                    SummarizeStyleAndRecipe(_facet);
                    DrawPreview(_facet);
                }
            }
        }

        private void BtnPaintFaceB_Click(object sender, RoutedEventArgs e)
        {
            if (!HasDualFace(_facet))
            {
                MessageBox.Show("This facet does not have a secondary face.",
                    "Cannot Paint Side B", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_facet.Type == FacetType.Ladder)
            {
                MessageBox.Show("Ladders cannot be painted - they use procedural textures.",
                    "Cannot Paint", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (IsProceduralDoor(_facet.Type))
            {
                MessageBox.Show("Door and InsideDoor facets cannot be painted - they render procedurally.",
                    "Cannot Paint", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Inside dual-face facets: Side B (the visible interior) is the Side A slot (faceOffset=1).
            bool isInsideDualB = (_facet.Flags & FacetFlags.Inside) != 0;
            int faceOffsetB = isInsideDualB ? 1 : 0;
            var painter = new FacetPainterWindow(_facet, _facetIndex1, faceOffset: faceOffsetB) { Owner = this };

            if (painter.ShowDialog() == true)
            {
                var arrays = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();

                if (arrays.Facets != null && _facetIndex1 >= 1 && _facetIndex1 <= arrays.Facets.Length)
                {
                    _facet = arrays.Facets[_facetIndex1 - 1];
                    _dstyles = arrays.Styles ?? Array.Empty<short>();
                    _paintMem = arrays.PaintMem ?? Array.Empty<byte>();
                    _storeys = arrays.Storeys ?? Array.Empty<BuildingArrays.DStoreyRec>();
                }

                SummarizeStyleAndRecipe(_facet);
                DrawPreview(_facet);
            }
        }

        private async void BtnChangeStyle_Click(object sender, RoutedEventArgs e)
        {
            if (_facet.Type == FacetType.Ladder)
            {
                MessageBox.Show("Ladders ignore style textures (procedural).",
                    "Cannot Change Style", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (IsProceduralDoor(_facet.Type))
            {
                MessageBox.Show("Door and InsideDoor facets ignore style textures (procedural).",
                    "Cannot Change Style", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await EnsureStylesLoadedAsync();

            int currentBaseStyle = GetCurrentBaseStyleId(_facet.StyleIndex);
            var picker = new StylePickerWindow(currentBaseStyle) { Owner = this };

            if (picker.ShowDialog() != true || !picker.WasConfirmed || picker.SelectedStyleIndex <= 0)
                return;

            int newStyleId = picker.SelectedStyleIndex;
            var acc = new BuildingsAccessor(MapDataService.Instance);

            int changed = ApplyNewStyleToFace(acc, newStyleId, _facet.StyleIndex);
            if (changed <= 0)
            {
                MessageBox.Show("Failed to apply style (out of bounds or missing building data).",
                    "Change Style Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ReloadAndRefresh(acc);
        }

        private async void BtnChangeSecondFaceStyle_Click(object sender, RoutedEventArgs e)
        {
            if (!HasDualFace(_facet))
            {
                MessageBox.Show("This facet does not have a second face.",
                    "No Second Face", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await EnsureStylesLoadedAsync();

            int faceBIndex = _facet.StyleIndex;
            int currentFaceBStyle = GetCurrentBaseStyleId(faceBIndex);
            var picker = new StylePickerWindow(currentFaceBStyle) { Owner = this };

            if (picker.ShowDialog() != true || !picker.WasConfirmed || picker.SelectedStyleIndex <= 0)
                return;

            int newStyleId = picker.SelectedStyleIndex;
            var acc = new BuildingsAccessor(MapDataService.Instance);

            int changed = ApplyNewStyleToFace(acc, newStyleId, faceBIndex);
            if (changed <= 0)
            {
                MessageBox.Show("Failed to apply style to Face B.",
                    "Change Style Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ReloadAndRefresh(acc);
        }

        private int GetCurrentBaseStyleId(int dstyleIndex)
        {
            if (_dstyles.Length == 0) return 1;
            if (dstyleIndex < 0 || dstyleIndex >= _dstyles.Length) return 1;

            short dval = _dstyles[dstyleIndex];

            if (dval >= 0)
                return NormalizeStyleId(dval);

            int storeyId = -dval;
            if (storeyId >= 0 && storeyId < _storeys.Length)
                return NormalizeStyleId(_storeys[storeyId].StyleIndex);

            return 1;
        }

        private int ApplyNewStyleToFace(BuildingsAccessor acc, int newStyleId, int faceStartIndex)
        {
            if (_dstyles.Length == 0) return 0;

            int totalPixelsY = _facet.Height * 16 + _facet.FHeight;
            int panelsDown = Math.Max(1, (totalPixelsY + (PanelPx - 1)) / PanelPx);

            bool dualFace = HasDualFace(_facet);
            int step = dualFace ? 2 : 1;

            var indices = new HashSet<int>();
            for (int band = 0; band < panelsDown; band++)
            {
                int idx = faceStartIndex + band * step;
                if (idx >= 0) indices.Add(idx);
            }

            int changed = 0;
            foreach (int dstyleIndex in indices.OrderBy(x => x))
            {
                if (dstyleIndex < 0 || dstyleIndex >= _dstyles.Length)
                    continue;

                short cur = _dstyles[dstyleIndex];

                if (cur >= 0)
                {
                    if (acc.TryUpdateDStyleValue(dstyleIndex, (short)newStyleId))
                        changed++;
                }
                else
                {
                    int storeyId = -cur;
                    if (acc.TryUpdateDStoreyBaseStyle(storeyId, (ushort)newStyleId))
                        changed++;
                }
            }

            return changed;
        }

        private void ReloadAndRefresh(BuildingsAccessor acc)
        {
            var arrays = acc.ReadSnapshot();
            _dstyles = arrays.Styles ?? Array.Empty<short>();
            _paintMem = arrays.PaintMem ?? Array.Empty<byte>();
            _storeys = arrays.Storeys ?? Array.Empty<BuildingArrays.DStoreyRec>();

            SummarizeStyleAndRecipe(_facet);
            DrawPreview(_facet);
        }

        private void CenterPreviewPair(Canvas panelCanvas, Canvas gridCanvas, ScrollViewer previewScroll)
        {
            panelCanvas.UpdateLayout();
            gridCanvas.UpdateLayout();
            previewScroll.UpdateLayout();

            double previewWidth = double.IsNaN(panelCanvas.Width) || panelCanvas.Width <= 0
                ? panelCanvas.ActualWidth
                : panelCanvas.Width;

            double previewHeight = double.IsNaN(panelCanvas.Height) || panelCanvas.Height <= 0
                ? panelCanvas.ActualHeight
                : panelCanvas.Height;

            if (double.IsNaN(previewWidth) || double.IsInfinity(previewWidth) || previewWidth < 0)
                previewWidth = 0;

            if (double.IsNaN(previewHeight) || double.IsInfinity(previewHeight) || previewHeight < 0)
                previewHeight = 0;

            double viewportWidth = previewScroll.ViewportWidth;
            double viewportHeight = previewScroll.ViewportHeight;

            if (double.IsNaN(viewportWidth) || double.IsInfinity(viewportWidth) || viewportWidth <= 0)
                viewportWidth = previewScroll.ActualWidth;

            if (double.IsNaN(viewportHeight) || double.IsInfinity(viewportHeight) || viewportHeight <= 0)
                viewportHeight = previewScroll.ActualHeight;

            double offsetX = Math.Max(0, (viewportWidth - previewWidth) / 2.0);
            double offsetY = Math.Max(0, (viewportHeight - previewHeight) / 2.0);

            panelCanvas.Margin = new Thickness(0);
            gridCanvas.Margin = new Thickness(0);

            panelCanvas.HorizontalAlignment = HorizontalAlignment.Left;
            panelCanvas.VerticalAlignment = VerticalAlignment.Top;
            gridCanvas.HorizontalAlignment = HorizontalAlignment.Left;
            gridCanvas.VerticalAlignment = VerticalAlignment.Top;

            panelCanvas.RenderTransform = new TranslateTransform(offsetX, offsetY);
            gridCanvas.RenderTransform = new TranslateTransform(offsetX, offsetY);
        }

        #endregion
    }
}