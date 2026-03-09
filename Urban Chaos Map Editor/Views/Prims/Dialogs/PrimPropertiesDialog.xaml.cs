using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosEditor.Shared.Services.Textures;

namespace UrbanChaosMapEditor.Views.Prims.Dialogs
{
    public partial class PrimPropertiesDialog : Window
    {
        // Flag bindings
        public bool OnFloor { get; set; }
        public bool Searchable { get; set; }
        public bool NotOnPsx { get; set; }
        public bool Damaged { get; set; }
        public bool Warehouse { get; set; }
        public bool HiddenItem { get; set; }
        public bool Reserved1 { get; set; }
        public bool Reserved2 { get; set; }

        public bool IsInside { get; set; }

        public byte FlagsValue { get; private set; }
        public byte InsideIndexValue { get; private set; }
        public int ResultHeight { get; private set; }

        private const double VisualHeightPx = 64.0;   // one tile only
        private bool _dragging;

        // Height state
        private int _storey;
        private int _offset; // 0..255

        public PrimPropertiesDialog(byte flags, byte insideIndex, int initialHeight)
        {
            InitializeComponent();

            OnFloor = (flags & (1 << 0)) != 0;
            Searchable = (flags & (1 << 1)) != 0;
            NotOnPsx = (flags & (1 << 2)) != 0;
            Damaged = (flags & (1 << 3)) != 0;
            Warehouse = (flags & (1 << 4)) != 0;
            HiddenItem = (flags & (1 << 5)) != 0;
            Reserved1 = (flags & (1 << 6)) != 0;
            Reserved2 = (flags & (1 << 7)) != 0;

            IsInside = insideIndex != 0;

            FromHeight(initialHeight);

            if (TextureCacheService.Instance.TryGetRelative("shared_386", out var img) && img != null)
            {
                Wall.Background = new ImageBrush(img)
                {
                    Stretch = Stretch.Fill,
                    TileMode = TileMode.None
                };
            }
            else
            {
                Wall.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }

            DataContext = this;
            UpdateUi();
        }

        private void FromHeight(int h)
        {
            _storey = FloorDiv(h, 256);
            _offset = FloorMod(h, 256);
            ResultHeight = (_storey * 256) + _offset;
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
            ResultHeight = (_storey * 256) + _offset;

            // 0..255 mapped into 64px visual
            double normalized = _offset / 255.0;
            double yCenter = (1.0 - normalized) * VisualHeightPx;

            double top = yCenter - (Dot.Height / 2.0);
            if (top < 0) top = 0;
            if (top > Wall.Height - Dot.Height) top = Wall.Height - Dot.Height;

            Canvas.SetLeft(Dot, (Wall.Width - Dot.Width) / 2.0);
            Canvas.SetTop(Dot, top);

            LblStorey.Text = _storey.ToString(CultureInfo.InvariantCulture);
            LblOffset.Text = _offset.ToString(CultureInfo.InvariantCulture);
            LblTotal.Text = ResultHeight.ToString(CultureInfo.InvariantCulture);

            if (!HeightTextBox.IsKeyboardFocusWithin)
                HeightTextBox.Text = ResultHeight.ToString(CultureInfo.InvariantCulture);
        }

        private void SetOffsetFromMouseY(double y)
        {
            // Clamp to wall
            if (y < 0) y = 0;
            if (y > Wall.Height) y = Wall.Height;

            // Top = high offset, bottom = low offset
            double normalized = 1.0 - (y / Wall.Height);
            int offset = (int)Math.Round(normalized * 255.0);

            if (offset < 0) offset = 0;
            if (offset > 255) offset = 255;

            _offset = offset;
            UpdateUi();
        }

        private void ApplyManualHeight()
        {
            if (int.TryParse(HeightTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                FromHeight(parsed);
                UpdateUi();
            }
            else
            {
                HeightTextBox.Text = ResultHeight.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void Wall_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragging = true;
            Wall.CaptureMouse();
            SetOffsetFromMouseY(e.GetPosition(Wall).Y);
        }

        private void Wall_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            SetOffsetFromMouseY(e.GetPosition(Wall).Y);
        }

        private void Wall_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;

            if (Wall.IsMouseCaptured)
                Wall.ReleaseMouseCapture();
        }

        private void Wall_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            _storey += e.Delta > 0 ? 1 : -1;
            UpdateUi();
            e.Handled = true;
        }

        private void MinusStorey_Click(object sender, RoutedEventArgs e)
        {
            _storey--;
            UpdateUi();
        }

        private void PlusStorey_Click(object sender, RoutedEventArgs e)
        {
            _storey++;
            UpdateUi();
        }

        private void HeightTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyManualHeight();
                e.Handled = true;
            }
        }

        private void HeightTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyManualHeight();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ApplyManualHeight();

            byte flags = 0;
            if (OnFloor) flags |= 1 << 0;
            if (Searchable) flags |= 1 << 1;
            if (NotOnPsx) flags |= 1 << 2;
            if (Damaged) flags |= 1 << 3;
            if (Warehouse) flags |= 1 << 4;
            if (HiddenItem) flags |= 1 << 5;
            if (Reserved1) flags |= 1 << 6;
            if (Reserved2) flags |= 1 << 7;

            FlagsValue = flags;
            InsideIndexValue = (byte)(IsInside ? 1 : 0);
            ResultHeight = (_storey * 256) + _offset;

            DialogResult = true;
        }
    }
}