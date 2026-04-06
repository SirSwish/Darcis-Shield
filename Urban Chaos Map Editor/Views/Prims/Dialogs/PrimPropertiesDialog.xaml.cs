using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UrbanChaosEditor.Shared.Services.Textures;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Heights;
using UrbanChaosMapEditor.Services.Roofs;

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

        // Prim position for Snap to Floor (MapWho cell + sub-cell coords)
        private readonly int _mapWhoIndex;
        private readonly byte _primX;
        private readonly byte _primZ;

        public PrimPropertiesDialog(byte flags, byte insideIndex, int initialHeight,
            int mapWhoIndex = -1, byte primX = 0, byte primZ = 0)
        {
            InitializeComponent();
            _mapWhoIndex = mapWhoIndex;
            _primX = primX;
            _primZ = primZ;
            BtnSnapToFloor.IsEnabled = mapWhoIndex >= 0 && MapDataService.Instance.IsLoaded;

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

        private void SnapToFloor_Click(object sender, RoutedEventArgs e)
        {
            if (_mapWhoIndex < 0 || !MapDataService.Instance.IsLoaded) return;

            // Convert MapWho cell + sub-cell coords to UI tile coordinates.
            // MapWhoIndex: column-major 0..1023 in game space (gameCol = idx/32, gameRow = idx%32).
            // UI flips both axes: uiCol = 31 - gameCol, uiRow = 31 - gameRow.
            // Within cell: game X/Z origin is bottom-right, UI is top-left → cellPixelX = 255 - X.
            // Each tile = 64 px, so tileX = uiCol * 4 + cellPixelX / 64.
            int gameCol = _mapWhoIndex / 32;
            int gameRow = _mapWhoIndex % 32;
            int uiCol   = 31 - gameCol;
            int uiRow   = 31 - gameRow;
            int tileX   = uiCol * 4 + (255 - _primX) / 64;
            int tileZ   = uiRow * 4 + (255 - _primZ) / 64;

            // Clamp to valid tile range [0, 127]
            tileX = Math.Clamp(tileX, 0, 127);
            tileZ = Math.Clamp(tileZ, 0, 127);

            var altAcc = new AltitudeAccessor(MapDataService.Instance);
            var hgtAcc = new HeightsAccessor(MapDataService.Instance);

            // Floor altitude: stored divided by 8 (PAP_ALT_SHIFT), so scale back up to compare
            // with vertex heights which are stored at face value.
            int altEffective = altAcc.ReadAltRaw(tileX, tileZ) * (1 << AltitudeAccessor.PAP_ALT_SHIFT);

            // Four surrounding vertex heights (stored at face value, 1 unit = 8 Y pixels)
            int tx1 = Math.Clamp(tileX + 1, 0, 127);
            int tz1 = Math.Clamp(tileZ + 1, 0, 127);
            int v00 = hgtAcc.ReadHeight(tileX, tileZ);
            int v10 = hgtAcc.ReadHeight(tx1,   tileZ);
            int v01 = hgtAcc.ReadHeight(tileX, tz1);
            int v11 = hgtAcc.ReadHeight(tx1,   tz1);

            // Exclude zeros so altitude=0 (unset) doesn't win over negative terrain.
            // Among remaining values, Max gives the one closest to 0 for negative terrain.
            int[] candidates = new[] { altEffective, v00, v10, v01, v11 };
            int[] nonZero = candidates.Where(v => v != 0).ToArray();
            int floorHeight = nonZero.Length > 0 ? nonZero.Max() : 0;

            // 1 unit of height = 8 pixels of Y
            FromHeight(floorHeight * 8);
            UpdateUi();
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