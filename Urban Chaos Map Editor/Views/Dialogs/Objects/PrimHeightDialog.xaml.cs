using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosEditor.Shared.Services.Textures;

namespace UrbanChaosMapEditor.Views
{
    public partial class PrimHeightDialog : Window
    {
        // In/out
        public int InitialHeight { get; }
        public int ResultHeight { get; private set; }

        // Internal state (storey & offset in [0..255])
        private int _storey; // floor division by 256 (can be negative)
        private int _offset; // 0..255
        private bool _dragging;
        private double _lastY;

        public PrimHeightDialog(int initialHeight)
        {
            InitializeComponent();

            InitialHeight = initialHeight;
            FromHeight(InitialHeight);

            // Prepare tiled background from shared_386 (64x64), repeated vertically 4x
            if (TextureCacheService.Instance.TryGetRelative("shared_386", out var img) && img != null)
            {
                var brush = new ImageBrush(img)
                {
                    Stretch = Stretch.Fill,
                    TileMode = TileMode.Tile,
                    ViewportUnits = BrushMappingMode.Absolute,
                    Viewport = new Rect(0, 0, 64, 64)
                };
                Wall.Background = brush;
            }
            else
            {
                Wall.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }

            UpdateUi();
        }

        // Convert height to storey + positive offset
        private void FromHeight(int h)
        {
            // floor div/mod for negatives
            _storey = FloorDiv(h, 256);
            _offset = FloorMod(h, 256);
        }

        private static int FloorDiv(int a, int b)
        {
            int q = a / b;
            int r = a % b;
            if (r != 0 && ((r > 0) != (b > 0))) q--;
            return q;
        }

        private static int FloorMod(int a, int b)
        {
            int r = a % b;
            if (r < 0) r += Math.Abs(b);
            return r;
        }

        private void UpdateUi()
        {
            // dot position: offset measured up from bottom; Canvas Y grows down
            double yFromTop = 256 - (_offset + Dot.Height / 2.0);
            double xCenter = (Wall.Width - Dot.Width) / 2.0;

            Canvas.SetLeft(Dot, xCenter);
            Canvas.SetTop(Dot, yFromTop);

            int total = _storey * 256 + _offset;
            LblStorey.Text = _storey.ToString();
            LblOffset.Text = _offset.ToString();
            LblTotal.Text = total.ToString();
        }

        private void Wall_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragging = true;
            _lastY = e.GetPosition(Wall).Y;
            Wall.CaptureMouse();
        }

        private void Wall_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            var y = e.GetPosition(Wall).Y;
            double dy = y - _lastY; // + down, - up
            _lastY = y;

            // Moving up increases offset; moving down decreases it
            int delta = (int)Math.Round(-dy);
            if (delta != 0)
            {
                _offset += delta;
                while (_offset >= 256) { _offset -= 256; _storey++; }
                while (_offset < 0) { _offset += 256; _storey--; }
                UpdateUi();
            }
        }

        private void Wall_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            if (Wall.IsMouseCaptured) Wall.ReleaseMouseCapture();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultHeight = _storey * 256 + _offset;
            DialogResult = true;
        }
    }
}
