// /Views/StyleDetailWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosStyleEditor.Models;
using UrbanChaosStyleEditor.Services;

namespace UrbanChaosStyleEditor.Views
{
    public partial class StyleDetailWindow : Window
    {
        private readonly StyleEntry _style;
        private readonly StyleProject _project;
        private readonly PieceRow[] _rows = new PieceRow[5];

        public bool Saved { get; private set; }

        public StyleDetailWindow(StyleEntry style, StyleProject project)
        {
            InitializeComponent();
            _style = style;
            _project = project;

            TxtStyleName.Text = style.Name ?? "";

            for (int i = 0; i < 5; i++)
            {
                var piece = i < style.Pieces.Count ? style.Pieces[i] : new StylePiece();
                var row = CreatePieceRow(i, piece);
                _rows[i] = row;
                PieceRows.Children.Add(row.Container);
            }
        }

        private PieceRow CreatePieceRow(int index, StylePiece piece)
        {
            var row = new PieceRow { Index = index };

            var container = new Border
            {
                Background = index % 2 == 0 ? new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x32)) : Brushes.Transparent,
                Padding = new Thickness(4),
                Margin = new Thickness(0, 1, 0, 1),
                CornerRadius = new CornerRadius(3)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Slot label
            var slotLabel = new TextBlock
            {
                Text = $"[{index}]",
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xB8, 0x4B)),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(slotLabel, 0);
            grid.Children.Add(slotLabel);

            // Preview
            var previewBorder = new Border
            {
                Width = 48,
                Height = 48,
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1B, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Click to pick a texture from the slots"
            };
            var previewImage = new Image
            {
                Stretch = Stretch.UniformToFill,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(previewImage, BitmapScalingMode.NearestNeighbor);
            previewBorder.Child = previewImage;
            previewBorder.Tag = index;
            previewBorder.MouseLeftButtonDown += Preview_Click;
            Grid.SetColumn(previewBorder, 1);
            grid.Children.Add(previewBorder);
            row.PreviewImage = previewImage;

            // Texture index (read-only, computed)
            var texIndexLabel = new TextBlock
            {
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas")
            };
            Grid.SetColumn(texIndexLabel, 2);
            grid.Children.Add(texIndexLabel);
            row.TexIndexLabel = texIndexLabel;

            // Page
            row.TxtPage = CreateNumericBox(piece.Page.ToString());
            Grid.SetColumn(row.TxtPage, 3);
            grid.Children.Add(row.TxtPage);
            row.TxtPage.TextChanged += (_, __) => OnPieceFieldChanged(row);

            // Tx
            row.TxtTx = CreateNumericBox(piece.Tx.ToString());
            Grid.SetColumn(row.TxtTx, 4);
            grid.Children.Add(row.TxtTx);
            row.TxtTx.TextChanged += (_, __) => OnPieceFieldChanged(row);

            // Ty
            row.TxtTy = CreateNumericBox(piece.Ty.ToString());
            Grid.SetColumn(row.TxtTy, 5);
            grid.Children.Add(row.TxtTy);
            row.TxtTy.TextChanged += (_, __) => OnPieceFieldChanged(row);

            // Flags panel
            var flagsPanel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

            row.ChkFlipX = new CheckBox { Content = "FlipX", Foreground = Brushes.White, FontSize = 10, Margin = new Thickness(0, 0, 6, 0) };
            row.ChkFlipX.IsChecked = (piece.Flip & 0x01) != 0;

            row.ChkFlipY = new CheckBox { Content = "FlipY", Foreground = Brushes.White, FontSize = 10, Margin = new Thickness(0, 0, 6, 0) };
            row.ChkFlipY.IsChecked = (piece.Flip & 0x02) != 0;

            row.ChkGouraud = new CheckBox { Content = "Gour", Foreground = Brushes.White, FontSize = 10, Margin = new Thickness(0, 0, 6, 0) };
            row.ChkTextured = new CheckBox { Content = "Tex", Foreground = Brushes.White, FontSize = 10, Margin = new Thickness(0, 0, 6, 0) };
            row.ChkMasked = new CheckBox { Content = "Mask", Foreground = Brushes.White, FontSize = 10, Margin = new Thickness(0, 0, 6, 0) };
            row.ChkTransparent = new CheckBox { Content = "Trans", Foreground = Brushes.White, FontSize = 10, Margin = new Thickness(0, 0, 6, 0) };
            row.ChkTiled = new CheckBox { Content = "Tile", Foreground = Brushes.White, FontSize = 10, Margin = new Thickness(0, 0, 6, 0) };

            // Load flags from style if available
            byte flags = 0;
            if (index < _style.Pieces.Count && _style.Pieces[index] != null)
            {
                // Flags are stored separately in the TMA
                // For now default to Textured
                flags = 0x02;
            }

            // Check if style has explicit flags from a loaded TMA
            if (_style is StyleEntry se && se.Pieces.Count > index)
            {
                // The StylePiece doesn't store flags directly - they come from the StyleEntry
                // We'll handle this when we have flags on the entry
            }

            row.ChkTextured.IsChecked = true; // Default

            flagsPanel.Children.Add(row.ChkFlipX);
            flagsPanel.Children.Add(row.ChkFlipY);
            flagsPanel.Children.Add(row.ChkGouraud);
            flagsPanel.Children.Add(row.ChkTextured);
            flagsPanel.Children.Add(row.ChkMasked);
            flagsPanel.Children.Add(row.ChkTransparent);
            flagsPanel.Children.Add(row.ChkTiled);

            Grid.SetColumn(flagsPanel, 6);
            grid.Children.Add(flagsPanel);

            container.Child = grid;
            row.Container = container;

            // Set initial preview
            UpdateRowPreview(row);

            return row;
        }

        private TextBox CreateNumericBox(string text)
        {
            return new TextBox
            {
                Text = text,
                Width = 36,
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };
        }

        private void OnPieceFieldChanged(PieceRow row)
        {
            UpdateRowPreview(row);
        }

        private void UpdateRowPreview(PieceRow row)
        {
            byte page = ParseByte(row.TxtPage);
            byte tx = ParseByte(row.TxtTx);
            byte ty = ParseByte(row.TxtTy);

            int texIndex = page * 64 + ty * 8 + tx;
            row.TexIndexLabel.Text = texIndex.ToString("D3");

            if (texIndex >= 256)
            {
                // Shared texture (pages 4-7)
                row.PreviewImage.Source = SharedTextureLoader.TryGet(texIndex);
            }
            else if (texIndex >= 0 && texIndex < _project.Slots.Count)
            {
                row.PreviewImage.Source = _project.Slots[texIndex].Image;
            }
            else
            {
                row.PreviewImage.Source = null;
            }
        }

        private void Preview_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not int rowIndex) return;

            var picker = new TexturePickerWindow(_project) { Owner = this };
            if (picker.ShowDialog() == true && picker.SelectedIndex >= 0)
            {
                int texIndex = picker.SelectedIndex;
                byte page = (byte)(texIndex / 64);
                byte remainder = (byte)(texIndex % 64);
                byte ty = (byte)(remainder / 8);
                byte tx = (byte)(remainder % 8);

                var row = _rows[rowIndex];
                row.TxtPage.Text = page.ToString();
                row.TxtTx.Text = tx.ToString();
                row.TxtTy.Text = ty.ToString();
                UpdateRowPreview(row);
            }
        }

        private static byte ParseByte(TextBox box)
        {
            if (byte.TryParse(box.Text.Trim(), out byte val))
                return val;
            return 0;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _style.Name = TxtStyleName.Text.Trim();
            _style.Pieces.Clear();

            for (int i = 0; i < 5; i++)
            {
                var row = _rows[i];
                byte flip = 0;
                if (row.ChkFlipX.IsChecked == true) flip |= 0x01;
                if (row.ChkFlipY.IsChecked == true) flip |= 0x02;

                _style.Pieces.Add(new StylePiece
                {
                    Page = ParseByte(row.TxtPage),
                    Tx = ParseByte(row.TxtTx),
                    Ty = ParseByte(row.TxtTy),
                    Flip = flip
                });
            }

            Saved = true;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private class PieceRow
        {
            public int Index;
            public Border Container = null!;
            public Image PreviewImage = null!;
            public TextBlock TexIndexLabel = null!;
            public TextBox TxtPage = null!;
            public TextBox TxtTx = null!;
            public TextBox TxtTy = null!;
            public CheckBox ChkFlipX = null!;
            public CheckBox ChkFlipY = null!;
            public CheckBox ChkGouraud = null!;
            public CheckBox ChkTextured = null!;
            public CheckBox ChkMasked = null!;
            public CheckBox ChkTransparent = null!;
            public CheckBox ChkTiled = null!;
        }
    }
}