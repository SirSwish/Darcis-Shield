// /Views/Dialogs/Buildings/FacetPreviewWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Models.Styles;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;
using UrbanChaosMapEditor.ViewModels;
using StyleTextureEntry = UrbanChaosMapEditor.Models.Styles.TextureEntry;

namespace UrbanChaosMapEditor.Views.Dialogs.Buildings
{
    public partial class FacetPreviewWindow : Window
    {
        private const int PanelPx = 64;
        private const string TexturesAsm = "UrbanChaosEditor.Shared";

        private DFacetRec _facet;
        private readonly int _facetIndex1;

        // Snapshot tables for style resolution
        private short[] _dstyles = Array.Empty<short>();
        private BuildingArrays.DStoreyRec[] _storeys = Array.Empty<BuildingArrays.DStoreyRec>();
        private byte[] _paintMem = Array.Empty<byte>();

        // Texture location hints
        private readonly string? _variant;
        private readonly int _worldNumber;

        private ObservableCollection<FlagItem>? _flagItems;

        // Regex for input validation
        private static readonly Regex _digitsOnly = new Regex(@"^[0-9]+$");
        private static readonly Regex _signedDigitsOnly = new Regex(@"^-?[0-9]+$");

        private static int NormalizeStyleId(int id) => id <= 0 ? 1 : id;

        public FacetPreviewWindow(DFacetRec facet, int facetId1)
        {
            InitializeComponent();
            _facet = facet;
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
            string newText = textBox?.Text.Insert(textBox.SelectionStart, e.Text) ?? e.Text;
            e.Handled = !_signedDigitsOnly.IsMatch(newText);
        }

        #endregion

        #region Coordinate Editing

        private void Coord_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox) return;

            if (!byte.TryParse(TxtX0.Text, out byte x0)) x0 = _facet.X0;
            if (!byte.TryParse(TxtZ0.Text, out byte z0)) z0 = _facet.Z0;
            if (!byte.TryParse(TxtX1.Text, out byte x1)) x1 = _facet.X1;
            if (!byte.TryParse(TxtZ1.Text, out byte z1)) z1 = _facet.Z1;

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
            TxtX0.Text = _facet.X0.ToString();
            TxtZ0.Text = _facet.Z0.ToString();
            TxtX1.Text = _facet.X1.ToString();
            TxtZ1.Text = _facet.Z1.ToString();
            FacetCoordsText.Text = $"Current: ({_facet.X0},{_facet.Z0}) → ({_facet.X1},{_facet.Z1})";
        }

        private static DFacetRec CopyFacetWithCoords(DFacetRec f, byte x0, byte z0, byte x1, byte z1)
        {
            return new DFacetRec(f.Type, x0, z0, x1, z1, f.Height, f.FHeight,
                f.StyleIndex, f.Building, f.Storey, f.Flags, f.Y0, f.Y1,
                f.BlockHeight, f.Open, f.Dfcache, f.Shake, f.CutHole, f.Counter0, f.Counter1);
        }

        /// <summary>
        /// Called from MapView when the user finishes drawing (two clicks).
        /// Updates the facet coordinates and refreshes the UI.
        /// </summary>
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
            if (sender is not TextBox) return;

            if (!byte.TryParse(TxtHeight.Text, out byte height)) height = _facet.Height;
            if (!byte.TryParse(TxtFHeight.Text, out byte fheight)) fheight = _facet.FHeight;
            if (!short.TryParse(TxtY0.Text, out short y0)) y0 = _facet.Y0;
            if (!short.TryParse(TxtY1.Text, out short y1)) y1 = _facet.Y1;
            if (!byte.TryParse(TxtBlockHeight.Text, out byte blockHeight)) blockHeight = _facet.BlockHeight;

            if (height == _facet.Height && fheight == _facet.FHeight &&
                y0 == _facet.Y0 && y1 == _facet.Y1 && blockHeight == _facet.BlockHeight)
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
            TxtHeight.Text = _facet.Height.ToString();
            TxtFHeight.Text = _facet.FHeight.ToString();
            TxtY0.Text = _facet.Y0.ToString();
            TxtY1.Text = _facet.Y1.ToString();
            TxtBlockHeight.Text = _facet.BlockHeight.ToString();

            int approxStoreys = UsesVerticalUnitsAsPanels(_facet.Type)
                ? Math.Max(1, (int)_facet.Height)
                : Math.Max(1, ((int)_facet.Height + 3) / 4);

            FacetHeightText.Text = UsesVerticalUnitsAsPanels(_facet.Type)
                ? $"~{approxStoreys} stacked fence panels"
                : $"~{approxStoreys} storeys";
        }

        private static DFacetRec CopyFacetWithHeights(DFacetRec f, byte height, byte fheight, short y0, short y1, byte blockHeight)
        {
            return new DFacetRec(f.Type, f.X0, f.Z0, f.X1, f.Z1, height, fheight,
                f.StyleIndex, f.Building, f.Storey, f.Flags, y0, y1,
                blockHeight, f.Open, f.Dfcache, f.Shake, f.CutHole, f.Counter0, f.Counter1);
        }

        #endregion

        #region Redraw on Map

        private void BtnRedraw_Click(object sender, RoutedEventArgs e)
        {
            // Start the redraw operation - hide this window and enter drawing mode
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                // Store reference to this window for callback
                mainVm.Map.BeginFacetRedraw(this, _facetIndex1);

                // Hide this window
                Hide();

                mainVm.StatusMessage = "Click start point (X0,Z0), then end point (X1,Z1). Right-click to cancel.";
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
                // Close this window since the facet no longer exists
                DialogResult = true;
                Close();

                // The BuildingsTabViewModel will handle selecting the next facet
                // via the BuildingsChangeBus notification
            }
            else
            {
                MessageBox.Show($"Failed to delete facet:\n\n{result.ErrorMessage}",
                    "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Called when redraw is cancelled (right-click or escape).
        /// Restores the window without changes.
        /// </summary>
        public void OnRedrawCancelled()
        {
            Show();
            Activate();
        }

        /// <summary>
        /// Called when redraw is completed (two clicks made).
        /// Shows the window with updated coordinates.
        /// </summary>
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
            FacetFlagsText.Text = $"Flags: 0x{((ushort)newMask):X4}   Building={_facet.Building} Storey={_facet.Storey} StyleIndex={_facet.StyleIndex}";
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
            var arrays = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();

            FacetIdText.Text = $"Facet #{_facetIndex1}";
            FacetTypeText.Text = _facet.Type.ToString();

            RefreshCoordDisplay();
            RefreshHeightDisplay();

            FacetFlagsText.Text = $"Flags: 0x{((ushort)_facet.Flags):X4}   Building={_facet.Building} Storey={_facet.Storey} StyleIndex={_facet.StyleIndex}";
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

        private void SummarizeStyleAndRecipe(DFacetRec f)
        {
            // Ladders don't use style.tma textures - they're procedurally rendered
            if (f.Type == FacetType.Ladder)
            {
                StyleModeText.Text = "Mode: LADDER (procedural)";
                RawStyleText.Text = $"StyleIndex={f.StyleIndex} (ignored for ladders)";
                RecipeText.Text = "Ladders use a procedurally generated texture with white rungs and rails.";
                RawStylePanel.Visibility = Visibility.Visible;
                PaintedPanel.Visibility = Visibility.Collapsed;
                return;
            }

            // Doors don't use style.tma textures - they render as black rectangles
            if (f.Type == FacetType.Door || f.Type == FacetType.InsideDoor || f.Type == FacetType.OutsideDoor)
            {
                string doorTypeName = f.Type switch
                {
                    FacetType.Door => "DOOR",
                    FacetType.InsideDoor => "INSIDE DOOR",
                    FacetType.OutsideDoor => "OUTSIDE DOOR",
                    _ => "DOOR"
                };
                StyleModeText.Text = $"Mode: {doorTypeName} (procedural)";
                RawStyleText.Text = $"StyleIndex={f.StyleIndex} (ignored for doors)";
                RecipeText.Text = "Doors render as black rectangles. Style is ignored by the engine.";
                RawStylePanel.Visibility = Visibility.Visible;
                PaintedPanel.Visibility = Visibility.Collapsed;
                return;
            }

            if (_dstyles.Length == 0 || f.StyleIndex >= _dstyles.Length)
            {
                StyleModeText.Text = "Mode: (unknown - missing dstyles)";
                RawStyleText.Text = $"StyleIndex={f.StyleIndex}";
                RecipeText.Text = "";
                RawStylePanel.Visibility = Visibility.Visible;
                PaintedPanel.Visibility = Visibility.Collapsed;
                return;
            }

            short val = _dstyles[f.StyleIndex];
            if (val >= 0)
            {
                StyleModeText.Text = "Mode: RAW style";
                RawStyleText.Text = $"Raw style id (style.tma): {val}";
                RecipeText.Text = BuildRawRecipeString(val);
                RawStylePanel.Visibility = Visibility.Visible;
                PaintedPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                int sid = -val;
                StyleModeText.Text = $"Mode: PAINTED (DStorey {sid})";

                if (sid < 1 || sid > _storeys.Length)
                {
                    PaintedSummaryText.Text = $"Invalid DStorey id: {sid}";
                    PaintBytesHexText.Text = "(none)";
                    PaintBytesGrid.ItemsSource = null;
                    RecipeText.Text = "";
                    RawStylePanel.Visibility = Visibility.Collapsed;
                    PaintedPanel.Visibility = Visibility.Visible;
                    return;
                }

                var ds = _storeys[sid - 1];
                PaintedSummaryText.Text = $"Base style: {ds.StyleIndex}   PaintMem: Index={ds.PaintIndex} Count={ds.Count}";
                RecipeText.Text = BuildRawRecipeString(ds.StyleIndex);

                var bytes = GetPaintBytes(ref ds);
                PaintBytesHexText.Text = ToHexLine(bytes);

                var rows = new ObservableCollection<PaintByteVM>();
                for (int i = 0; i < bytes.Length; i++)
                {
                    byte b = bytes[i];
                    rows.Add(new PaintByteVM
                    {
                        Index = i,
                        ByteHex = b.ToString("X2"),
                        Page = b & 0x7F,
                        Flag = (b & 0x80) != 0 ? "1" : "0"
                    });
                }
                PaintBytesGrid.ItemsSource = rows;

                RawStylePanel.Visibility = Visibility.Collapsed;
                PaintedPanel.Visibility = Visibility.Visible;
            }
        }

        private static bool UsesVerticalUnitsAsPanels(FacetType t) =>
            t == FacetType.Fence || t == FacetType.FenceFlat ||
            t == FacetType.FenceBrick || t == FacetType.Ladder || t == FacetType.Trench;

        private string BuildRawRecipeString(int rawStyleId)
        {
            var tma = StyleDataService.Instance.TmaSnapshot;
            if (tma == null) return "(style.tma not loaded)";

            int idx = StyleDataService.MapRawStyleIdToTmaIndex(rawStyleId);
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
            // Count is SBYTE - negative values have special meaning, treat as 0
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

        private void DrawPreview(DFacetRec f)
        {
            // Check if this is a ladder - render specially
            if (f.Type == FacetType.Ladder)
            {
                DrawLadderPreview(f);
                return;
            }

            // Check if this is a door type - render as black rectangle
            if (f.Type == FacetType.Door || f.Type == FacetType.InsideDoor || f.Type == FacetType.OutsideDoor)
            {
                DrawDoorPreview(f);
                return;
            }

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

            for (int rowFromTop = 0; rowFromTop < panelsDown; rowFromTop++)
            {
                int rowFromBottom = panelsDown - 1 - rowFromTop;

                for (int col = 0; col < panelsAcross; col++)
                {
                    if (TryResolvePanelTile(col, rowFromBottom, panelsAcross, panelsDown,
                                            out byte page, out byte tx, out byte ty, out byte flip))
                    {
                        int tileId = page * 64 + ty * 8 + tx;
                        string tooltipText = $"tex{tileId:D3}hi";

                        if (TryLoadTileBitmap(page, tx, ty, flip, out var bmp))
                        {
                            var img = new Image
                            {
                                Width = PanelPx,
                                Height = PanelPx,
                                Source = bmp,
                                ToolTip = tooltipText
                            };
                            Canvas.SetLeft(img, col * PanelPx);
                            Canvas.SetTop(img, rowFromTop * PanelPx);
                            PanelCanvas.Children.Add(img);
                        }
                        else
                        {
                            AddPlaceholderRect(col, rowFromTop, tooltipText);
                        }
                    }
                    else
                    {
                        AddPlaceholderRect(col, rowFromTop, "(no tile)");
                    }
                }
            }

            DrawGrid(GridCanvas, width, height, PanelPx, PanelPx);
            var outline = new Rectangle
            {
                Width = width,
                Height = height,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            };
            GridCanvas.Children.Add(outline);
        }

        /// <summary>
        /// Draws a ladder preview with white rungs and side rails.
        /// Ladders are always 1 cell wide but can be multiple storeys tall.
        /// Applies the ~67% width scaling that the game uses.
        /// </summary>
        private void DrawLadderPreview(DFacetRec f)
        {
            // Ladder constants
            const int RungsPerSegment = 4;      // 4 rungs per 64px segment
            const int RungThickness = 4;        // 4px thick rungs
            const int RailWidth = 4;            // 4px wide side rails
            const double WidthScale = 0.67;     // ~67% width scaling the game applies

            // Ladders are always 1 cell wide
            int panelsAcross = 1;

            // Calculate height in storeys/segments
            int totalPixelsY = f.Height * 16 + f.FHeight;
            int panelsDown = Math.Max(1, (totalPixelsY + (PanelPx - 1)) / PanelPx);

            // Full dimensions before scaling
            int fullWidth = panelsAcross * PanelPx;
            int height = panelsDown * PanelPx;

            // Apply width scaling (ladder is narrower than a full cell)
            int scaledWidth = (int)(fullWidth * WidthScale);
            int widthOffset = (fullWidth - scaledWidth) / 2; // Center the ladder

            // Canvas setup
            PanelCanvas.Children.Clear();
            GridCanvas.Children.Clear();
            PanelCanvas.Width = fullWidth;
            PanelCanvas.Height = height;
            GridCanvas.Width = fullWidth;
            GridCanvas.Height = height;

            // Background (dark)
            var background = new Rectangle
            {
                Width = fullWidth,
                Height = height,
                Fill = new SolidColorBrush(Color.FromRgb(0x30, 0x33, 0x38))
            };
            PanelCanvas.Children.Add(background);

            // Ladder brush (white)
            var ladderBrush = Brushes.White;

            // Draw left rail
            var leftRail = new Rectangle
            {
                Width = RailWidth,
                Height = height,
                Fill = ladderBrush
            };
            Canvas.SetLeft(leftRail, widthOffset);
            Canvas.SetTop(leftRail, 0);
            PanelCanvas.Children.Add(leftRail);

            // Draw right rail
            var rightRail = new Rectangle
            {
                Width = RailWidth,
                Height = height,
                Fill = ladderBrush
            };
            Canvas.SetLeft(rightRail, widthOffset + scaledWidth - RailWidth);
            Canvas.SetTop(rightRail, 0);
            PanelCanvas.Children.Add(rightRail);

            // Draw rungs - 4 per segment, evenly spaced
            int totalRungs = panelsDown * RungsPerSegment;
            double rungSpacing = (double)height / totalRungs;
            int rungWidth = scaledWidth - (2 * RailWidth); // Width between rails

            for (int i = 0; i < totalRungs; i++)
            {
                // Position rungs from bottom to top, offset by half spacing to center them
                double rungY = height - (i + 0.5) * rungSpacing - (RungThickness / 2.0);

                var rung = new Rectangle
                {
                    Width = rungWidth,
                    Height = RungThickness,
                    Fill = ladderBrush
                };
                Canvas.SetLeft(rung, widthOffset + RailWidth);
                Canvas.SetTop(rung, rungY);
                PanelCanvas.Children.Add(rung);
            }

            // Draw grid lines for each storey segment
            DrawGrid(GridCanvas, fullWidth, height, PanelPx, PanelPx);

            // Draw outline
            var outline = new Rectangle
            {
                Width = fullWidth,
                Height = height,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            };
            GridCanvas.Children.Add(outline);

            // Add info label
            var infoText = new TextBlock
            {
                Text = $"LADDER\n{panelsDown} storey(s)\n{totalRungs} rungs",
                Foreground = Brushes.Yellow,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x00, 0x00)),
                Padding = new Thickness(4, 2, 4, 2)
            };
            Canvas.SetLeft(infoText, 2);
            Canvas.SetTop(infoText, 2);
            GridCanvas.Children.Add(infoText);
        }

        /// <summary>
        /// Draws a door preview as a black 64x64 rectangle.
        /// Doors are always 1 cell wide and 1 cell high.
        /// OutsideDoors can be wider but we still show based on actual facet dimensions.
        /// </summary>
        private void DrawDoorPreview(DFacetRec f)
        {
            // Calculate dimensions based on facet coords
            int dx = Math.Abs(f.X1 - f.X0);
            int dz = Math.Abs(f.Z1 - f.Z0);
            int panelsAcross = Math.Max(dx, dz);
            if (panelsAcross <= 0) panelsAcross = 1;

            // Doors are typically 1 cell high, but respect actual height
            int totalPixelsY = f.Height * 16 + f.FHeight;
            int panelsDown = Math.Max(1, (totalPixelsY + (PanelPx - 1)) / PanelPx);

            int width = panelsAcross * PanelPx;
            int height = panelsDown * PanelPx;

            // Canvas setup
            PanelCanvas.Children.Clear();
            GridCanvas.Children.Clear();
            PanelCanvas.Width = width;
            PanelCanvas.Height = height;
            GridCanvas.Width = width;
            GridCanvas.Height = height;

            // Draw black rectangle for the door
            var doorRect = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = Brushes.Black
            };
            PanelCanvas.Children.Add(doorRect);

            // Draw a simple door frame/outline in dark gray
            var frameRect = new Rectangle
            {
                Width = width - 8,
                Height = height - 4,
                Stroke = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)),
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(frameRect, 4);
            Canvas.SetTop(frameRect, 2);
            PanelCanvas.Children.Add(frameRect);

            // Draw a door handle (small circle on the right side)
            var handle = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60))
            };
            Canvas.SetLeft(handle, width - 16);
            Canvas.SetTop(handle, height / 2 - 3);
            PanelCanvas.Children.Add(handle);

            // Draw grid lines
            DrawGrid(GridCanvas, width, height, PanelPx, PanelPx);

            // Draw outline
            var outline = new Rectangle
            {
                Width = width,
                Height = height,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            };
            GridCanvas.Children.Add(outline);

            // Determine door type name
            string doorTypeName = f.Type switch
            {
                FacetType.Door => "DOOR",
                FacetType.InsideDoor => "INSIDE DOOR",
                FacetType.OutsideDoor => "OUTSIDE DOOR",
                _ => "DOOR"
            };

            // Add info label
            var infoText = new TextBlock
            {
                Text = $"{doorTypeName}\n{panelsAcross}×{panelsDown} cell(s)",
                Foreground = Brushes.Cyan,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x00, 0x00)),
                Padding = new Thickness(4, 2, 4, 2)
            };
            Canvas.SetLeft(infoText, 2);
            Canvas.SetTop(infoText, 2);
            GridCanvas.Children.Add(infoText);
        }

        private void AddPlaceholderRect(int col, int rowFromTop, string tooltip)
        {
            var rect = new Rectangle
            {
                Width = PanelPx,
                Height = PanelPx,
                Fill = new SolidColorBrush(Color.FromRgb(0x55, 0x58, 0x5E)),
                ToolTip = tooltip
            };
            Canvas.SetLeft(rect, col * PanelPx);
            Canvas.SetTop(rect, rowFromTop * PanelPx);
            PanelCanvas.Children.Add(rect);
        }

        private static void DrawGrid(Canvas c, int width, int height, int stepX, int stepY)
        {
            var g = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
            for (int x = stepX; x < width; x += stepX)
            {
                var l = new Line { X1 = x, Y1 = 0, X2 = x, Y2 = height, StrokeThickness = 1, Stroke = g };
                c.Children.Add(l);
            }
            for (int y = stepY; y < height; y += stepY)
            {
                var l = new Line { X1 = 0, Y1 = y, X2 = width, Y2 = y, StrokeThickness = 1, Stroke = g };
                c.Children.Add(l);
            }
        }

        #endregion


        #region Tile Resolution (abbreviated - keep your existing implementation)

        private bool TryResolvePanelTile(int col, int rowFromBottom, int panelsAcross, int panelsDown,
                                         out byte page, out byte tx, out byte ty, out byte flip)
        {
            page = tx = ty = flip = 0;

            if (_dstyles == null || _dstyles.Length == 0) return false;
            if (rowFromBottom < 0 || rowFromBottom >= panelsDown) return false;

            bool twoTextured = (_facet.Flags & FacetFlags.TwoTextured) != 0;
            bool twoSided = (_facet.Flags & FacetFlags.TwoSided) != 0;
            bool hugFloor = (_facet.Flags & FacetFlags.HugFloor) != 0;

            int styleIndexStep = (!hugFloor && (twoTextured || twoSided)) ? 2 : 1;
            int styleIndexStart = _facet.StyleIndex;
            if (twoTextured) styleIndexStart--;

            // For multi-band walls, each band has its own dstyle entry
            // Band 0 = bottom, Band 1 = middle, etc.
            // styleRow should directly map to rowFromBottom
            int styleRow = rowFromBottom;

            int styleIndexForRow = styleIndexStart + styleRow * styleIndexStep;
            if (styleIndexForRow < 0 || styleIndexForRow >= _dstyles.Length) return false;

            short dval = _dstyles[styleIndexForRow];
            int count = panelsAcross + 1;
            int pos = panelsAcross - 1 - col;

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

            if (storeyId >= _storeys.Length) return false;  // Bounds check
            var ds = _storeys[storeyId];  // storeys array includes slot 0
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

            int pieceIndex = pos == 0 ? 0 : (pos == count - 2 ? 1 : 2);
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
            if (string.IsNullOrWhiteSpace(_variant) || _worldNumber <= 0) return;
            if (StyleDataService.Instance.IsLoaded) return;

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

        private bool TryLoadTileBitmap(byte page, byte tx, byte ty, byte flip, out BitmapSource? bmp)
        {
            bmp = null;
            if (string.IsNullOrWhiteSpace(_variant) || _worldNumber <= 0) return false;

            var candidates = new List<string>(3);
            if (page <= 3) candidates.Add($"world{_worldNumber}");
            else if (page <= 7) candidates.Add("shared");
            else if (page == 8) candidates.Add($"world{_worldNumber}/insides");
            else candidates.Add($"world{_worldNumber}");

            int totalIndex = (tx == 255 && ty == 255) ? page : page * 64 + ty * 8 + tx;

            foreach (var subfolder in candidates)
            {
                string packUri =
                    $"pack://application:,,,/{TexturesAsm};component/Assets/Textures/{_variant}/{subfolder}/tex{totalIndex:D3}hi.png";
                try
                {
                    var sri = Application.GetResourceStream(new Uri(packUri));
                    if (sri?.Stream == null) continue;

                    var baseBmp = new BitmapImage();
                    baseBmp.BeginInit();
                    baseBmp.CacheOption = BitmapCacheOption.OnLoad;
                    baseBmp.StreamSource = sri.Stream;
                    baseBmp.EndInit();
                    baseBmp.Freeze();

                    bool flipX = (flip & 0x01) != 0;
                    bool flipY = (flip & 0x02) != 0;

                    if (flipX || flipY)
                    {
                        var tb = new TransformedBitmap(baseBmp, new ScaleTransform(flipX ? -1 : 1, flipY ? -1 : 1));
                        tb.Freeze();
                        bmp = tb;
                    }
                    else
                    {
                        bmp = baseBmp;
                    }
                    return true;
                }
                catch { }
            }
            return false;
        }

        #endregion

        #region Paint Facet

        private void BtnPaintFacet_Click(object sender, RoutedEventArgs e)
        {
            // Don't allow painting ladders or doors - they don't use style textures
            if (_facet.Type == FacetType.Ladder)
            {
                MessageBox.Show("Ladders cannot be painted - they use procedural textures.",
                    "Cannot Paint", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_facet.Type == FacetType.Door || _facet.Type == FacetType.InsideDoor || _facet.Type == FacetType.OutsideDoor)
            {
                MessageBox.Show("Doors cannot be painted - they render as solid black.",
                    "Cannot Paint", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Open the painter window
            var painter = new FacetPainterWindow(_facet, _facetIndex1)
            {
                Owner = this
            };

            if (painter.ShowDialog() == true)
            {
                // Reload the facet data and refresh the preview
                var arrays = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();

                // Re-read the facet in case it was modified
                if (arrays.Facets != null && _facetIndex1 >= 1 && _facetIndex1 <= arrays.Facets.Length)
                {
                    // Note: We keep the same _facet reference for coordinates etc,
                    // but the style/paint data is in the arrays
                    _dstyles = arrays.Styles ?? Array.Empty<short>();
                    _paintMem = arrays.PaintMem ?? Array.Empty<byte>();
                    _storeys = arrays.Storeys ?? Array.Empty<BuildingArrays.DStoreyRec>();

                    // Refresh UI
                    SummarizeStyleAndRecipe(_facet);
                    DrawPreview(_facet);
                }
            }
        }

        private async void BtnChangeStyle_Click(object sender, RoutedEventArgs e)
        {
            // Same restrictions as painting (style ignored)
            if (_facet.Type == FacetType.Ladder)
            {
                MessageBox.Show("Ladders ignore style textures (procedural).",
                    "Cannot Change Style", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_facet.Type == FacetType.Door || _facet.Type == FacetType.InsideDoor || _facet.Type == FacetType.OutsideDoor)
            {
                MessageBox.Show("Doors ignore style textures (procedural black).",
                    "Cannot Change Style", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await EnsureStylesLoadedAsync();

            int currentBaseStyle = GetCurrentBaseStyleId();
            var picker = new StylePickerWindow(currentBaseStyle) { Owner = this };

            if (picker.ShowDialog() != true || !picker.WasConfirmed || picker.SelectedStyleIndex <= 0)
                return;

            int newStyleId = picker.SelectedStyleIndex;

            var acc = new BuildingsAccessor(MapDataService.Instance);

            int changed = ApplyNewStyleToFacet(acc, newStyleId);
            if (changed <= 0)
            {
                MessageBox.Show("Failed to apply style (out of bounds or missing building data).",
                    "Change Style Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Reload snapshot and refresh preview UI
            var arrays = acc.ReadSnapshot();
            _dstyles = arrays.Styles ?? Array.Empty<short>();
            _paintMem = arrays.PaintMem ?? Array.Empty<byte>();
            _storeys = arrays.Storeys ?? Array.Empty<BuildingArrays.DStoreyRec>();

            SummarizeStyleAndRecipe(_facet);
            DrawPreview(_facet);

            // Optional: notify rest of app if you have a bus
            // BuildingsChangeBus.Instance.NotifyFacetChanged(_facetIndex1);
            // BuildingsChangeBus.Instance.NotifyChanged();
        }

        private int GetCurrentBaseStyleId()
        {
            if (_dstyles.Length == 0) return 1;
            if (_facet.StyleIndex < 0 || _facet.StyleIndex >= _dstyles.Length) return 1;

            short dval = _dstyles[_facet.StyleIndex];

            // RAW: dval is raw style id
            if (dval >= 0)
                return NormalizeStyleId(dval);

            // PAINTED: dval is -storeyId. Base style lives in storeys[storeyId].StyleIndex (common in your format).
            int storeyId = -dval;
            if (_storeys.Length == 0) return 1;

            // Your project has mixed indexing in places; this makes it robust:
            if (storeyId >= 0 && storeyId < _storeys.Length)
                return NormalizeStyleId(_storeys[storeyId].StyleIndex);

            if (storeyId - 1 >= 0 && storeyId - 1 < _storeys.Length)
                return NormalizeStyleId(_storeys[storeyId - 1].StyleIndex);

            return 1;
        }

        private int ApplyNewStyleToFacet(BuildingsAccessor acc, int newStyleId)
        {
            if (_dstyles.Length == 0) return 0;

            // We update ALL dstyle slots used by this facet’s vertical bands
            int totalPixelsY = _facet.Height * 16 + _facet.FHeight;
            int panelsDown = Math.Max(1, (totalPixelsY + (PanelPx - 1)) / PanelPx);

            bool twoTextured = (_facet.Flags & FacetFlags.TwoTextured) != 0;
            bool twoSided = (_facet.Flags & FacetFlags.TwoSided) != 0;
            bool hugFloor = (_facet.Flags & FacetFlags.HugFloor) != 0;

            int step = (!hugFloor && (twoTextured || twoSided)) ? 2 : 1;
            int start = _facet.StyleIndex;
            if (twoTextured) start--;

            int changed = 0;

            // Distinct indices (avoid duplicates)
            var indices = new HashSet<int>();
            for (int rowFromBottom = 0; rowFromBottom < panelsDown; rowFromBottom++)
            {
                int idx = start + rowFromBottom * step;
                if (idx >= 0) indices.Add(idx);
            }

            foreach (int dstyleIndex in indices.OrderBy(x => x))
            {
                if (dstyleIndex < 0 || dstyleIndex >= _dstyles.Length)
                    continue;

                short cur = _dstyles[dstyleIndex];

                if (cur >= 0)
                {
                    // RAW: write new raw style into dstyles slot
                    if (acc.TryUpdateDStyleValue(dstyleIndex, (short)newStyleId))
                        changed++;
                }
                else
                {
                    // PAINTED: update base style in the referenced DStorey (keep paint overrides)
                    int storeyId = -cur;
                    if (acc.TryUpdateDStoreyBaseStyle(storeyId, (ushort)newStyleId))
                        changed++;
                }
            }

            return changed;
        }

        #endregion
    }

}