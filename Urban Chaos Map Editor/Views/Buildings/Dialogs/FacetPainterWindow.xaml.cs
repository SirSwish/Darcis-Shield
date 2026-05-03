// /Views/Dialogs/Buildings/FacetPainterWindow.xaml.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Styles;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosEditor.Shared.Services.Textures;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    /// <summary>
    /// Window for painting textures onto facet cells.
    /// </summary>
    public partial class FacetPainterWindow : Window
    {
        private const int PanelPx = TextureFormatConstants.TileWidth;
        private const int PaletteTileSize = EditorUiConstants.PaletteTileSize;
        private const int MaxTextureIndex = 127; // Paint bytes use 7 bits (0-127)

        private readonly DFacetRec _facet;
        private readonly int _facetIndex1;
        private readonly int _worldNumber;
        private readonly string? _variant;
        // 0 = Side B (secondary/inner face), 1 = Side A (primary/outer face, default)
        private readonly int _faceOffset;

        // Grid dimensions
        private int _panelsAcross;
        private int _panelsDown;

        // Current paint data per band (row)
        // Key = band index (0 = bottom), Value = array of paint bytes per column
        private readonly Dictionary<int, byte[]> _paintData = new();

        // Base style for each band (used when paint byte is 0)
        private readonly Dictionary<int, short> _baseStyles = new();

        // Original dstyles data for this facet
        private short[] _dstyles = Array.Empty<short>();
        private BuildingArrays.DStoreyRec[] _storeys = Array.Empty<BuildingArrays.DStoreyRec>();
        private byte[] _paintMem = Array.Empty<byte>();

        // Palette
        private readonly ObservableCollection<PaletteItemVM> _paletteItems = new();
        private int _selectedTextureIndex = -1; // -1 = eraser mode

        // Track if changes were made
        public bool ChangesMade { get; private set; } = false;

        public FacetPainterWindow(DFacetRec facet, int facetIndex1, int faceOffset = 1)
        {
            InitializeComponent();

            _facet = facet;
            _facetIndex1 = facetIndex1;
            _faceOffset = faceOffset;

            // 2SIDED facets are single-channel — only StyleIndex+0 is paintable.
            // (The game uses StyleIndex+1 for the back-face render, but we do not paint it.)
            // 2TEXTURED warehouses are separate opposing DFacetRec entries — not dual-channel.

            if (!TryResolveVariantAndWorld(out _variant, out _worldNumber))
            {
                _variant = "Release";
                _worldNumber = 20;
            }

            Loaded += FacetPainterWindow_Loaded;
        }

        #region Initialization

        private void FacetPainterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Load building arrays
            var arrays = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();
            _dstyles = arrays.Styles ?? Array.Empty<short>();
            _paintMem = arrays.PaintMem ?? Array.Empty<byte>();
            _storeys = arrays.Storeys ?? Array.Empty<BuildingArrays.DStoreyRec>();

            // Calculate grid dimensions
            int dx = Math.Abs(_facet.X1 - _facet.X0);
            int dz = Math.Abs(_facet.Z1 - _facet.Z0);
            _panelsAcross = Math.Max(1, Math.Max(dx, dz));

            int totalPixelsY = _facet.Height * 16 + _facet.FHeight;
            _panelsDown = Math.Max(1, (totalPixelsY + (PanelPx - 1)) / PanelPx);

            FacetDimensionsText.Text = $"{_panelsAcross} columns - {_panelsDown} bands";

            // Initialize paint data from existing facet
            InitializePaintDataFromFacet();

            // Load texture palette
            LoadTexturePalette();

            // Draw the preview
            DrawPreview();
        }

        private void InitializePaintDataFromFacet()
        {
            // 2SIDED facets are single-channel: only StyleIndex+0 is painted.
            // 2TEXTURED: separate opposing facets — each has its own StyleIndex, step by 2 to skip partner.
            bool twoTextured = (_facet.Flags & FacetFlags.TwoTextured) != 0;
            int styleIndexStep = twoTextured ? 2 : 1;
            int styleIndexStart = _facet.StyleIndex;

            // First pass: determine base style from band 0 (or first valid band)
            // This will be used as fallback for any new bands that don't have dstyle entries yet
            short fallbackBaseStyle = 1;
            int firstValidStyleIndex = styleIndexStart;
            if (firstValidStyleIndex >= 0 && firstValidStyleIndex < _dstyles.Length)
            {
                short firstDval = _dstyles[firstValidStyleIndex];
                if (firstDval >= 0)
                {
                    fallbackBaseStyle = firstDval;
                }
                else
                {
                    // Band 0 is painted, get base style from DStorey
                    int storeyId = -firstDval;
                    if (storeyId >= 1 && storeyId < _storeys.Length)
                    {
                        fallbackBaseStyle = (short)_storeys[storeyId].StyleIndex;
                    }
                }
            }

            for (int band = 0; band < _panelsDown; band++)
            {
                // Initialize paint array for this band
                _paintData[band] = new byte[_panelsAcross];

                // Calculate which dstyle index this band uses
                int styleIndexForBand = styleIndexStart + band * styleIndexStep;

                if (styleIndexForBand < 0 || styleIndexForBand >= _dstyles.Length)
                {
                    // New band without dstyle entry - use fallback base style
                    _baseStyles[band] = fallbackBaseStyle;
                    Debug.WriteLine($"[FacetPainter]   Band {band}: no dstyle entry (index {styleIndexForBand}), using fallback base style {fallbackBaseStyle}");
                    continue;
                }

                short dval = _dstyles[styleIndexForBand];

                // Sanity check: if this is a "new" band (beyond the original facet's allocation),
                // the dval might belong to a different facet. We detect this by checking:
                // 1. If dval is a raw style (>= 0), check if it's reasonably close to the fallback
                // 2. If dval is wildly different, it's probably another facet's data
                // We use a heuristic: style IDs for the same building are usually similar
                // A difference of more than ~50 suggests we're reading wrong data
                bool isLikelyWrongData = false;
                if (dval >= 0 && fallbackBaseStyle >= 0)
                {
                    int styleDiff = Math.Abs(dval - fallbackBaseStyle);
                    // If the style differs significantly and this isn't band 0, suspect wrong data
                    if (band > 0 && styleDiff > 50)
                    {
                        isLikelyWrongData = true;
                        Debug.WriteLine($"[FacetPainter]   Band {band}: dval={dval} differs significantly from fallback={fallbackBaseStyle}, likely reading another facet's data");
                    }
                }

                if (isLikelyWrongData)
                {
                    // Use fallback instead of the suspicious value
                    _baseStyles[band] = fallbackBaseStyle;
                }
                else if (dval >= 0)
                {
                    // Raw style - no painting
                    _baseStyles[band] = dval;
                }
                else
                {
                    // Painted - load from DStorey
                    int storeyId = -dval;
                    if (storeyId >= 1 && storeyId < _storeys.Length)
                    {
                        var ds = _storeys[storeyId];  // storeys array includes slot 0, use storeyId directly
                        _baseStyles[band] = (short)ds.StyleIndex;

                        // Load paint bytes.
                        // 2SIDED or Inside: stored forward (pos = col).
                        // All others: stored reversed (pos = N-1-col).
                        bool twoSided = (_facet.Flags & FacetFlags.TwoSided) != 0;
                        bool isInside = (_facet.Flags & FacetFlags.Inside) != 0;
                        bool readForward = twoSided || isInside;
                        int paintStart = ds.PaintIndex;
                        int paintCount = ds.Count;

                        Debug.WriteLine($"[FacetPainter]   Band {band}: paintStart={paintStart}, paintCount={paintCount}, _panelsAcross={_panelsAcross}");

                        for (int col = 0; col < _panelsAcross; col++)
                        {
                            int pos = readForward ? col : _panelsAcross - 1 - col;

                            if (pos < paintCount)
                            {
                                int paintIndex = paintStart + pos;
                                if (paintIndex >= 0 && paintIndex < _paintMem.Length)
                                {
                                    _paintData[band][col] = _paintMem[paintIndex];
                                    Debug.WriteLine($"[FacetPainter]     col={col}, pos={pos}, paintIndex={paintIndex}, value=0x{_paintMem[paintIndex]:X2}");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[FacetPainter]     col={col}, pos={pos} >= paintCount={paintCount}, using base style");
                            }
                        }
                    }
                    else
                    {
                        // Invalid storey ID - use fallback base style
                        _baseStyles[band] = fallbackBaseStyle;
                        Debug.WriteLine($"[FacetPainter]   Band {band}: invalid storeyId {storeyId}, using fallback base style {fallbackBaseStyle}");
                    }
                }
            }

            Debug.WriteLine($"[FacetPainter] Initialized {_panelsDown} bands - {_panelsAcross} columns");
            for (int band = 0; band < _panelsDown; band++)
            {
                Debug.WriteLine($"  Band {band}: BaseStyle={_baseStyles[band]}, PaintBytes=[{string.Join(",", _paintData[band].Select(b => b.ToString("X2")))}]");
            }
        }

        private void LoadTexturePalette()
        {
            _paletteItems.Clear();

            for (int i = 0; i <= MaxTextureIndex; i++)
            {
                if (!TryLoadPaletteTexture(i, out var bmp))
                    continue;

                var item = new PaletteItemVM
                {
                    Index = i,
                    TooltipText = $"tex{i:D3}hi",
                    Thumbnail = bmp
                };

                _paletteItems.Add(item);
            }

            TexturePalette.ItemsSource = _paletteItems;
        }

        private bool TryLoadPaletteTexture(int textureIndex, out BitmapSource? bmp)
        {
            return TextureResolver.TryResolvePalette(textureIndex, _worldNumber, _variant, out bmp);
        }


        #endregion

        #region Palette Selection

        private void PaletteItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is int index)
            {
                SelectTexture(index);
            }
        }

        private void BtnSelectEraser_Click(object sender, RoutedEventArgs e)
        {
            SelectTexture(-1); // -1 = eraser
        }

        private void SelectTexture(int index)
        {
            _selectedTextureIndex = index;

            if (index < 0)
            {
                // Eraser mode
                SelectedTextureLabel.Text = "Eraser (Use Base Style)";
                SelectedTexturePreview.Child = null;
            }
            else
            {
                SelectedTextureLabel.Text = $"tex{index:D3}hi";

                if (TryLoadPaletteTexture(index, out var bmp))
                {
                    var img = new Image
                    {
                        Width = 60,
                        Height = 60,
                        Source = bmp
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                    SelectedTexturePreview.Child = img;
                }
                else
                {
                    SelectedTexturePreview.Child = null;
                }
            }

            // Update palette visual selection
            foreach (var item in _paletteItems)
            {
                item.IsSelected = (item.Index == index);
            }
        }

        #endregion

        #region Canvas Painting

        private void PanelCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(PanelCanvas);
            int col = (int)(pos.X / PanelPx);
            int rowFromTop = (int)(pos.Y / PanelPx);
            int band = _panelsDown - 1 - rowFromTop; // Convert to band (0 = bottom)

            if (col < 0 || col >= _panelsAcross || band < 0 || band >= _panelsDown)
                return;

            // Paint the cell
            PaintCell(band, col, _selectedTextureIndex, ChkFlipHorizontal.IsChecked == true);
        }

        private void PanelCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(PanelCanvas);
            int col = (int)(pos.X / PanelPx);
            int rowFromTop = (int)(pos.Y / PanelPx);
            int band = _panelsDown - 1 - rowFromTop;

            if (col < 0 || col >= _panelsAcross || band < 0 || band >= _panelsDown)
                return;

            // Clear the cell (use base style)
            PaintCell(band, col, -1, false);
        }

        private void PaintCell(int band, int col, int textureIndex, bool flip)
        {
            if (!_paintData.ContainsKey(band))
                _paintData[band] = new byte[_panelsAcross];

            byte paintByte;
            if (textureIndex < 0)
            {
                // Eraser - use 0 to indicate "use base style"
                paintByte = 0;
            }
            else
            {
                // Texture index (0-127) with optional flip bit
                paintByte = (byte)(textureIndex & 0x7F);
                if (flip)
                    paintByte |= 0x80;
            }

            _paintData[band][col] = paintByte;
            ChangesMade = true;

            Debug.WriteLine($"[FacetPainter] Painted band={band} col={col} byte=0x{paintByte:X2}");

            // Redraw the preview
            DrawPreview();
        }

        #endregion

        #region Preview Drawing

        private void DrawPreview()
        {
            int width = _panelsAcross * PanelPx;
            int height = _panelsDown * PanelPx;

            PanelCanvas.Children.Clear();
            GridCanvas.Children.Clear();
            PanelCanvas.Width = width;
            PanelCanvas.Height = height;
            GridCanvas.Width = width;
            GridCanvas.Height = height;

            for (int rowFromTop = 0; rowFromTop < _panelsDown; rowFromTop++)
            {
                int band = _panelsDown - 1 - rowFromTop;

                for (int col = 0; col < _panelsAcross; col++)
                {
                    byte paintByte = _paintData.ContainsKey(band) ? _paintData[band][col] : (byte)0;
                    bool isPainted = (paintByte & 0x7F) != 0;
                    bool flipX = (paintByte & 0x80) != 0;

                    BitmapSource? bmp = null;
                    string tooltip;

                    if (isPainted)
                    {
                        // Use painted texture
                        int texIndex = paintByte & 0x7F;
                        tooltip = $"tex{texIndex:D3}hi" + (flipX ? " (flipped)" : "");

                        if (TryLoadPaletteTexture(texIndex, out bmp) && flipX && bmp != null)
                        {
                            // Apply flip
                            var tb = new TransformedBitmap(bmp, new ScaleTransform(-1, 1));
                            tb.Freeze();
                            bmp = tb;
                        }
                    }
                    else
                    {
                        // Use base style
                        short baseStyle = _baseStyles.ContainsKey(band) ? _baseStyles[band] : (short)1;
                        tooltip = $"Base style {baseStyle}";

                        // Try to load base style texture
                        if (TryResolveBaseStyleTexture(baseStyle, col, out bmp, out int tileId))
                        {
                            tooltip = $"tex{tileId:D3}hi (base)";
                        }
                    }

                    // Draw the cell
                    if (bmp != null)
                    {
                        var img = new Image
                        {
                            Width = PanelPx,
                            Height = PanelPx,
                            Source = bmp,
                            ToolTip = tooltip
                        };
                        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                        Canvas.SetLeft(img, col * PanelPx);
                        Canvas.SetTop(img, rowFromTop * PanelPx);
                        PanelCanvas.Children.Add(img);
                    }
                    else
                    {
                        // Placeholder
                        var rect = new Rectangle
                        {
                            Width = PanelPx - 2,
                            Height = PanelPx - 2,
                            Fill = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)),
                            ToolTip = tooltip
                        };
                        Canvas.SetLeft(rect, col * PanelPx + 1);
                        Canvas.SetTop(rect, rowFromTop * PanelPx + 1);
                        PanelCanvas.Children.Add(rect);
                    }

                    // Draw painted indicator (green border) if this cell is painted
                    if (isPainted)
                    {
                        var indicator = new Rectangle
                        {
                            Width = PanelPx - 4,
                            Height = PanelPx - 4,
                            Stroke = Brushes.LimeGreen,
                            StrokeThickness = 2,
                            Fill = Brushes.Transparent
                        };
                        Canvas.SetLeft(indicator, col * PanelPx + 2);
                        Canvas.SetTop(indicator, rowFromTop * PanelPx + 2);
                        GridCanvas.Children.Add(indicator);
                    }
                }
            }

            // Draw grid lines
            DrawGrid(GridCanvas, width, height, PanelPx, PanelPx);

            // For the secondary channel of a 2SIDED facet, the topmost band never renders in-game.
            // Draw a warning overlay so the user knows.
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
        }

        private bool TryResolveBaseStyleTexture(short baseStyle, int col, out BitmapSource? bmp, out int tileId)
        {
            bmp = null;
            tileId = 0;

            var tma = StyleDataService.Instance.TmaSnapshot;
            if (tma == null) return false;

            int styleId = baseStyle <= 0 ? 1 : baseStyle;
            int idx = StyleDataService.MapRawStyleIdToTmaIndex(styleId);
            if (idx < 0 || idx >= tma.TextureStyles.Count) return false;

            var entries = tma.TextureStyles[idx].Entries;
            if (entries == null || entries.Count == 0) return false;

            // Pick piece based on column position (simplified)
            int pieceIndex = col == 0 ? 0 : (col == _panelsAcross - 1 ? 1 : 2);
            if (pieceIndex >= entries.Count) pieceIndex = entries.Count - 1;

            var entry = entries[pieceIndex];
            tileId = entry.Page * 64 + entry.Ty * 8 + entry.Tx;

            return TryLoadTileBitmap(entry.Page, entry.Tx, entry.Ty, entry.Flip, out bmp);
        }

        private bool TryLoadTileBitmap(byte page, byte tx, byte ty, byte flip, out BitmapSource? bmp)
        {
            return TextureResolver.TryResolve(page, tx, ty, flip, _worldNumber, _variant, out bmp);
        }

        private static void DrawGrid(Canvas canvas, int width, int height, int cellW, int cellH)
        {
            var gridBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));

            // Vertical lines
            for (int x = cellW; x < width; x += cellW)
            {
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = gridBrush,
                    StrokeThickness = 1
                };
                canvas.Children.Add(line);
            }

            // Horizontal lines
            for (int y = cellH; y < height; y += cellH)
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 1
                };
                canvas.Children.Add(line);
            }
        }

        #endregion

        #region Save Logic

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SavePaintData();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save paint data:\n\n{ex.Message}",
                    "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SavePaintData()
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
                throw new InvalidOperationException("No map loaded.");

            var acc = new BuildingsAccessor(svc);
            var snap = acc.ReadSnapshot();

            // Determine which bands need DStorey (have any non-zero paint bytes)
            var bandsNeedingStorey = new List<int>();
            for (int band = 0; band < _panelsDown; band++)
            {
                if (_paintData.ContainsKey(band) && _paintData[band].Any(b => (b & 0x7F) != 0))
                {
                    bandsNeedingStorey.Add(band);
                }
            }

            Debug.WriteLine($"[FacetPainter] Saving {bandsNeedingStorey.Count} painted band(s) (0 = clearing)...");

            // Always call ApplyPaint — even when all bands are cleared it must restore
            // any existing DStorey references in dstyles[] back to base style values.
            var painter = new FacetPainter(svc);
            var result = painter.ApplyPaint(_facetIndex1, _panelsAcross, _panelsDown, _paintData, _baseStyles, _faceOffset);

            if (!result.IsSuccess)
            {
                throw new Exception(result.ErrorMessage);
            }

            Debug.WriteLine($"[FacetPainter] Save complete. Allocated {result.StoreysAllocated} DStoreys, {result.PaintBytesAllocated} paint bytes.");
        }

        #endregion

        #region Button Handlers

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            // Clear all paint data
            foreach (var band in _paintData.Keys.ToList())
            {
                Array.Clear(_paintData[band], 0, _paintData[band].Length);
            }
            ChangesMade = true;
            DrawPreview();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region Helpers

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

        #endregion

        #region ViewModels

        private sealed class PaletteItemVM : INotifyPropertyChanged
        {
            public int Index { get; init; }
            public string TooltipText { get; init; } = "";
            public BitmapSource? Thumbnail { get; set; }

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected != value)
                    {
                        _isSelected = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        #endregion
    }
}
