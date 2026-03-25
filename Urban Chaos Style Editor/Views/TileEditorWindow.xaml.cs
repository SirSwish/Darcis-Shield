// /Views/TileEditorWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UrbanChaosStyleEditor.Views
{
    public partial class TileEditorWindow : Window
    {
        private const int TileWidth = 64;
        private const int TileHeight = 64;
        private const int Bpp = 4; // BGRA
        private const int Stride = TileWidth * Bpp;

        private readonly byte[] _pixels = new byte[TileWidth * TileHeight * Bpp];
        private WriteableBitmap _bitmap;
        private int _zoom = 8;
        private bool _showGrid = true;
        private bool _isPainting;
        private Color _currentColor = Colors.White;

        // Alpha rect selection
        private bool _isDraggingRect;
        private Point _rectStart;
        private Rectangle? _selectionRect;

        public WriteableBitmap ResultBitmap => _bitmap;
        public bool Saved { get; private set; }

        private static readonly Color[] DefaultPalette = new[]
        {
            Colors.White, Colors.Black, Colors.Red, Colors.Green, Colors.Blue,
            Colors.Yellow, Colors.Cyan, Colors.Magenta, Colors.Orange, Colors.Purple,
            Color.FromRgb(128, 128, 128), Color.FromRgb(64, 64, 64),
            Color.FromRgb(192, 192, 192), Color.FromRgb(128, 0, 0),
            Color.FromRgb(0, 128, 0), Color.FromRgb(0, 0, 128),
            Color.FromRgb(128, 128, 0), Color.FromRgb(0, 128, 128),
            Color.FromRgb(128, 0, 128), Color.FromRgb(210, 180, 140),
            Color.FromRgb(139, 90, 43), Color.FromRgb(85, 107, 47),
            Color.FromRgb(107, 142, 35), Color.FromRgb(160, 82, 45)
        };

        public TileEditorWindow(BitmapSource? existingImage = null)
        {
            InitializeComponent();

            _bitmap = new WriteableBitmap(TileWidth, TileHeight, 96, 96, PixelFormats.Bgra32, null);

            if (existingImage != null)
            {
                var converted = new FormatConvertedBitmap(existingImage, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                converted.CopyPixels(_pixels, Stride, 0);
            }
            else
            {
                // Default: all white, fully opaque
                for (int i = 0; i < _pixels.Length; i += 4)
                {
                    _pixels[i + 0] = 255; // B
                    _pixels[i + 1] = 255; // G
                    _pixels[i + 2] = 255; // R
                    _pixels[i + 3] = 255; // A
                }
            }

            FlushPixelsToBitmap();
            BuildPalette();
            UpdateColorPreview();
            RedrawCanvas();
            UpdatePreview();
        }

        private void FlushPixelsToBitmap()
        {
            _bitmap.WritePixels(new Int32Rect(0, 0, TileWidth, TileHeight), _pixels, Stride, 0);
        }

        private void BuildPalette()
        {
            foreach (var color in DefaultPalette)
            {
                var rect = new Border
                {
                    Width = 18,
                    Height = 18,
                    Margin = new Thickness(1),
                    Background = new SolidColorBrush(color),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Tag = color
                };
                rect.MouseLeftButtonDown += PaletteColor_Click;
                PalettePanel.Children.Add(rect);
            }
        }

        private void PaletteColor_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is Color c)
            {
                _currentColor = c;
                SliderR.Value = c.R;
                SliderG.Value = c.G;
                SliderB.Value = c.B;
                UpdateColorPreview();
            }
        }

        private void ColorSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SliderR == null || SliderG == null || SliderB == null) return;
            _currentColor = Color.FromRgb((byte)SliderR.Value, (byte)SliderG.Value, (byte)SliderB.Value);
            if (LblR != null) LblR.Text = ((int)SliderR.Value).ToString();
            if (LblG != null) LblG.Text = ((int)SliderG.Value).ToString();
            if (LblB != null) LblB.Text = ((int)SliderB.Value).ToString();
            UpdateColorPreview();
        }

        private void AlphaSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LblA != null) LblA.Text = ((int)SliderA.Value).ToString();
        }

        private void UpdateColorPreview()
        {
            if (ColorPreviewBorder != null)
                ColorPreviewBorder.Background = new SolidColorBrush(_currentColor);
        }

        private void ColorPreview_Click(object sender, MouseButtonEventArgs e) { }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _zoom = (int)e.NewValue;
            if (ZoomLabel != null) ZoomLabel.Text = $"{_zoom}x";
            RedrawCanvas();
        }

        private void ChkGrid_Changed(object sender, RoutedEventArgs e)
        {
            _showGrid = ChkGrid.IsChecked == true;
            RedrawCanvas();
        }

        #region Canvas Drawing

        private void RedrawCanvas()
        {
            if (PixelCanvas == null || _bitmap == null) return;

            int canvasW = TileWidth * _zoom;
            int canvasH = TileHeight * _zoom;
            PixelCanvas.Width = canvasW;
            PixelCanvas.Height = canvasH;
            PixelCanvas.Children.Clear();

            // Checkerboard background for transparent pixels
            if (ChkCheckerboard != null && ChkCheckerboard.IsChecked == true)
            {
                DrawCheckerboard(canvasW, canvasH);
            }

            // Draw the bitmap scaled
            var img = new Image
            {
                Source = _bitmap,
                Width = canvasW,
                Height = canvasH,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
            Canvas.SetLeft(img, 0);
            Canvas.SetTop(img, 0);
            PixelCanvas.Children.Add(img);

            // Draw grid
            if (_showGrid && _zoom >= 4)
            {
                var gridBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                for (int x = 0; x <= TileWidth; x++)
                {
                    PixelCanvas.Children.Add(new Line
                    {
                        X1 = x * _zoom,
                        Y1 = 0,
                        X2 = x * _zoom,
                        Y2 = canvasH,
                        Stroke = gridBrush,
                        StrokeThickness = 0.5
                    });
                }
                for (int y = 0; y <= TileHeight; y++)
                {
                    PixelCanvas.Children.Add(new Line
                    {
                        X1 = 0,
                        Y1 = y * _zoom,
                        X2 = canvasW,
                        Y2 = y * _zoom,
                        Stroke = gridBrush,
                        StrokeThickness = 0.5
                    });
                }
            }
        }

        private void DrawCheckerboard(int canvasW, int canvasH)
        {
            int checkSize = Math.Max(4, _zoom / 2);
            var light = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
            var dark = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

            for (int cy = 0; cy < canvasH; cy += checkSize)
            {
                for (int cx = 0; cx < canvasW; cx += checkSize)
                {
                    bool isLight = ((cx / checkSize) + (cy / checkSize)) % 2 == 0;
                    var rect = new Rectangle
                    {
                        Width = checkSize,
                        Height = checkSize,
                        Fill = isLight ? light : dark
                    };
                    Canvas.SetLeft(rect, cx);
                    Canvas.SetTop(rect, cy);
                    PixelCanvas.Children.Add(rect);
                }
            }
        }

        #endregion

        #region Mouse Input

        private (int px, int py) CanvasToPixel(Point canvasPos)
        {
            int px = Math.Clamp((int)(canvasPos.X / _zoom), 0, TileWidth - 1);
            int py = Math.Clamp((int)(canvasPos.Y / _zoom), 0, TileHeight - 1);
            return (px, py);
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(PixelCanvas);
            var (px, py) = CanvasToPixel(pos);

            if (ToolAlphaRect.IsChecked == true)
            {
                _isDraggingRect = true;
                _rectStart = pos;
                PixelCanvas.CaptureMouse();

                _selectionRect = new Rectangle
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xD7, 0x00))
                };
                Canvas.SetLeft(_selectionRect, pos.X);
                Canvas.SetTop(_selectionRect, pos.Y);
                PixelCanvas.Children.Add(_selectionRect);
                return;
            }

            if (ToolPicker.IsChecked == true)
            {
                PickColor(px, py);
                return;
            }

            if (ToolFill.IsChecked == true)
            {
                FloodFill(px, py, _currentColor);
                FlushPixelsToBitmap();
                RedrawCanvas();
                UpdatePreview();
                return;
            }

            _isPainting = true;
            PixelCanvas.CaptureMouse();
            ApplyTool(px, py);
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var (px, py) = CanvasToPixel(e.GetPosition(PixelCanvas));
            PickColor(px, py);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingRect && _selectionRect != null)
            {
                var pos = e.GetPosition(PixelCanvas);
                double x = Math.Min(pos.X, _rectStart.X);
                double y = Math.Min(pos.Y, _rectStart.Y);
                double w = Math.Abs(pos.X - _rectStart.X);
                double h = Math.Abs(pos.Y - _rectStart.Y);

                Canvas.SetLeft(_selectionRect, x);
                Canvas.SetTop(_selectionRect, y);
                _selectionRect.Width = w;
                _selectionRect.Height = h;
                return;
            }

            if (!_isPainting) return;
            var (px, py) = CanvasToPixel(e.GetPosition(PixelCanvas));
            ApplyTool(px, py);
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingRect)
            {
                _isDraggingRect = false;
                PixelCanvas.ReleaseMouseCapture();

                var endPos = e.GetPosition(PixelCanvas);
                ApplyAlphaToRect(_rectStart, endPos);

                if (_selectionRect != null)
                {
                    PixelCanvas.Children.Remove(_selectionRect);
                    _selectionRect = null;
                }
                return;
            }

            _isPainting = false;
            PixelCanvas.ReleaseMouseCapture();
        }

        #endregion

        #region Paint Tools

        private void ApplyTool(int px, int py)
        {
            if (ToolPencil.IsChecked == true)
                SetPixel(px, py, _currentColor);
            else if (ToolEraser.IsChecked == true)
                SetPixel(px, py, Colors.Transparent);

            FlushPixelsToBitmap();
            RedrawCanvas();
            UpdatePreview();
        }

        private void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || x >= TileWidth || y < 0 || y >= TileHeight) return;
            int offset = (y * TileWidth + x) * Bpp;
            _pixels[offset + 0] = color.B;
            _pixels[offset + 1] = color.G;
            _pixels[offset + 2] = color.R;
            _pixels[offset + 3] = color.A;
        }

        private Color GetPixel(int x, int y)
        {
            if (x < 0 || x >= TileWidth || y < 0 || y >= TileHeight)
                return Colors.Transparent;
            int offset = (y * TileWidth + x) * Bpp;
            return Color.FromArgb(_pixels[offset + 3], _pixels[offset + 2], _pixels[offset + 1], _pixels[offset + 0]);
        }

        private void PickColor(int px, int py)
        {
            var c = GetPixel(px, py);
            _currentColor = Color.FromRgb(c.R, c.G, c.B);
            SliderR.Value = c.R;
            SliderG.Value = c.G;
            SliderB.Value = c.B;
            UpdateColorPreview();

            // Also show the alpha value
            if (SliderA != null) SliderA.Value = c.A;
        }

        private void FloodFill(int startX, int startY, Color fillColor)
        {
            var targetColor = GetPixel(startX, startY);
            var fill = Color.FromArgb(255, fillColor.R, fillColor.G, fillColor.B);
            if (targetColor == fill) return;

            var queue = new Queue<(int x, int y)>();
            var visited = new bool[TileWidth, TileHeight];
            queue.Enqueue((startX, startY));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (x < 0 || x >= TileWidth || y < 0 || y >= TileHeight) continue;
                if (visited[x, y]) continue;
                visited[x, y] = true;

                if (GetPixel(x, y) != targetColor) continue;

                SetPixel(x, y, fill);

                queue.Enqueue((x + 1, y));
                queue.Enqueue((x - 1, y));
                queue.Enqueue((x, y + 1));
                queue.Enqueue((x, y - 1));
            }
        }

        #endregion

        #region Alpha Rectangle Tool

        private void ApplyAlphaToRect(Point start, Point end)
        {
            var (px0, py0) = CanvasToPixel(start);
            var (px1, py1) = CanvasToPixel(end);

            int minX = Math.Min(px0, px1);
            int maxX = Math.Max(px0, px1);
            int minY = Math.Min(py0, py1);
            int maxY = Math.Max(py0, py1);

            byte alpha = (byte)SliderA.Value;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int offset = (y * TileWidth + x) * Bpp;
                    // Keep RGB, set alpha only
                    _pixels[offset + 3] = alpha;
                }
            }

            FlushPixelsToBitmap();
            RedrawCanvas();
            UpdatePreview();
        }

        private void SetAllOpaque_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 3; i < _pixels.Length; i += 4)
                _pixels[i] = 255;

            FlushPixelsToBitmap();
            RedrawCanvas();
            UpdatePreview();
        }

        private void SetAllTransparent_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 3; i < _pixels.Length; i += 4)
                _pixels[i] = 0;

            FlushPixelsToBitmap();
            RedrawCanvas();
            UpdatePreview();
        }

        #endregion

        private void UpdatePreview()
        {
            if (PreviewImage != null)
                PreviewImage.Source = _bitmap;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Saved = true;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}