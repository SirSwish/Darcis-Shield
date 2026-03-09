// /Views/Dialogs/Buildings/AddWalkableWindow.xaml.cs
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UrbanChaosMapEditor.Views.Roofs.Dialogs
{
    /// <summary>
    /// Dialog for adding a walkable region to a building.
    /// The tile bounds are set by the caller (from click-drag on map).
    /// User specifies the height.
    /// </summary>
    public partial class AddWalkableWindow : Window
    {
        private static readonly Regex _signedDigitsOnly = new(@"^-?[0-9]*$");

        public bool WasCancelled { get; private set; } = true;

        // Input: Set by caller before showing dialog
        public int BuildingId1 { get; set; }
        public string BuildingName { get; set; } = "";
        public int TileX1 { get; set; }
        public int TileZ1 { get; set; }
        public int TileX2 { get; set; }
        public int TileZ2 { get; set; }

        // Output: Set when user clicks OK
        public int WorldY { get; private set; }
        public byte StoreyY { get; private set; }

        public AddWalkableWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Calculate normalized bounds
            int minX = Math.Min(TileX1, TileX2);
            int maxX = Math.Max(TileX1, TileX2);
            int minZ = Math.Min(TileZ1, TileZ2);
            int maxZ = Math.Max(TileZ1, TileZ2);

            int width = maxX - minX + 1;
            int depth = maxZ - minZ + 1;

            TxtBuilding.Text = $"#{BuildingId1}: {BuildingName}";
            TxtBounds.Text = $"({minX}, {minZ}) ? ({maxX}, {maxZ})";
            TxtSize.Text = $"{width} × {depth} tiles";

            // Default height - 1 storey = 256
            TxtWorldY.Text = "256";
            TxtWorldY.Focus();
            TxtWorldY.SelectAll();
        }

        private void SignedNumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            string newText = textBox?.Text.Insert(textBox.SelectionStart, e.Text) ?? e.Text;
            e.Handled = !_signedDigitsOnly.IsMatch(newText);
        }

        private void TxtWorldY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtInfo == null) return;

            if (int.TryParse(TxtWorldY.Text, out int worldY))
            {
                int storey = worldY / 256;
                int offset = ((worldY % 256) + 256) % 256;
                int walkableY = worldY >> 5;
                int storeyY = worldY >> 6;

                TxtInfo.Text = $"Storey {storey}, Offset {offset}\n" +
                              $"Walkable.Y = {walkableY} (worldY >> 5)\n" +
                              $"Walkable.StoreyY = {storeyY} (worldY >> 6)";
            }
            else
            {
                TxtInfo.Text = "(enter a valid number)";
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtWorldY.Text, out int worldY))
            {
                MessageBox.Show("Please enter a valid integer for World Y.",
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WorldY = worldY;
            StoreyY = (byte)Math.Clamp(worldY >> 6, 0, 255);
            WasCancelled = false;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            WasCancelled = true;
            DialogResult = false;
            Close();
        }

        #region Programmatic XAML

        private TextBlock TxtBuilding = null!;
        private TextBlock TxtBounds = null!;
        private TextBlock TxtSize = null!;
        private TextBox TxtWorldY = null!;
        private TextBlock TxtInfo = null!;

        private void InitializeComponent()
        {
            Title = "Add Walkable Region";
            Width = 380;
            Height = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(12) };

            // Row definitions
            for (int i = 0; i < 8; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;

            // Building
            AddLabel(grid, "Building:", row, 0);
            TxtBuilding = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 5),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(TxtBuilding, row);
            Grid.SetColumn(TxtBuilding, 1);
            grid.Children.Add(TxtBuilding);
            row++;

            // Bounds
            AddLabel(grid, "Tile Bounds:", row, 0);
            TxtBounds = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 5)
            };
            Grid.SetRow(TxtBounds, row);
            Grid.SetColumn(TxtBounds, 1);
            grid.Children.Add(TxtBounds);
            row++;

            // Size
            AddLabel(grid, "Size:", row, 0);
            TxtSize = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 5)
            };
            Grid.SetRow(TxtSize, row);
            Grid.SetColumn(TxtSize, 1);
            grid.Children.Add(TxtSize);
            row++;

            // Separator
            var sep = new Separator { Margin = new Thickness(0, 10, 0, 10) };
            Grid.SetRow(sep, row);
            Grid.SetColumnSpan(sep, 2);
            grid.Children.Add(sep);
            row++;

            // World Y
            AddLabel(grid, "World Y:", row, 0);
            TxtWorldY = new TextBox
            {
                Margin = new Thickness(5, 5, 0, 5),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            TxtWorldY.PreviewTextInput += SignedNumericOnly_PreviewTextInput;
            TxtWorldY.TextChanged += TxtWorldY_TextChanged;
            Grid.SetRow(TxtWorldY, row);
            Grid.SetColumn(TxtWorldY, 1);
            grid.Children.Add(TxtWorldY);
            row++;

            // Info
            AddLabel(grid, "Info:", row, 0);
            TxtInfo = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, 5, 0, 5),
                Foreground = System.Windows.Media.Brushes.Gray,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(TxtInfo, row);
            Grid.SetColumn(TxtInfo, 1);
            grid.Children.Add(TxtInfo);
            row++;

            // Help text
            var helpText = new TextBlock
            {
                Text = "The walkable region defines the roof surface that characters " +
                       "can walk on and grab ledges from. Set the height to match " +
                       "the top of the building's walls.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = System.Windows.Media.Brushes.DimGray,
                FontSize = 11,
                Margin = new Thickness(0, 10, 0, 10)
            };
            Grid.SetRow(helpText, row);
            Grid.SetColumnSpan(helpText, 2);
            grid.Children.Add(helpText);
            row++;

            // Spacer
            row++;

            // Buttons
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(btnPanel, row);
            Grid.SetColumnSpan(btnPanel, 2);

            var btnOk = new Button
            {
                Content = "Add Walkable",
                Width = 100,
                Height = 28,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            btnOk.Click += BtnOk_Click;
            btnPanel.Children.Add(btnOk);

            var btnCancel = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 28,
                IsCancel = true
            };
            btnCancel.Click += BtnCancel_Click;
            btnPanel.Children.Add(btnCancel);

            grid.Children.Add(btnPanel);

            Content = grid;
        }

        private static void AddLabel(Grid grid, string text, int row, int col)
        {
            var lbl = new Label
            {
                Content = text,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(lbl, row);
            Grid.SetColumn(lbl, col);
            grid.Children.Add(lbl);
        }

        #endregion
    }
}