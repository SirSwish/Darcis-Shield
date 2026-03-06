// /Views/AddStoreyDialog.xaml.cs and EditStoreyDialog.xaml.cs
// Updated to use correct DStorey field names: Style, PaintIndex, Count, Padding
// (was incorrectly using Height, Flags, Building which don't exist in the C struct)

using System.Windows;
using System.Windows.Controls;
using UrbanChaosMapEditor.Models;

namespace UrbanChaosMapEditor.Views
{
    /// <summary>
    /// Dialog for adding a new DStorey entry.
    /// DStorey fields: Style (U16), PaintIndex (U16), Count (S8), Padding (U8)
    /// </summary>
    public class AddStoreyDialog : Window
    {
        private TextBox _txtStyle;
        private TextBox _txtPaintIndex;
        private TextBox _txtCount;

        public ushort Style { get; private set; }
        public ushort PaintIndex { get; private set; }
        public sbyte Count { get; private set; }

        public AddStoreyDialog(int buildingId)
        {
            Title = $"Add DStorey for Building #{buildingId}";
            Width = 300;
            base.Height = 220;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.DarkGray;

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Style
            var lblStyle = new TextBlock { Text = "Style:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblStyle, 0);
            grid.Children.Add(lblStyle);

            _txtStyle = new TextBox { Text = "0", Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtStyle, 0);
            Grid.SetColumn(_txtStyle, 1);
            grid.Children.Add(_txtStyle);

            // PaintIndex
            var lblPaintIndex = new TextBlock { Text = "Paint Idx:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblPaintIndex, 1);
            grid.Children.Add(lblPaintIndex);

            _txtPaintIndex = new TextBox { Text = "0", Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtPaintIndex, 1);
            Grid.SetColumn(_txtPaintIndex, 1);
            grid.Children.Add(_txtPaintIndex);

            // Count
            var lblCount = new TextBlock { Text = "Count:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblCount, 2);
            grid.Children.Add(lblCount);

            _txtCount = new TextBox { Text = "0", Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtCount, 2);
            Grid.SetColumn(_txtCount, 1);
            grid.Children.Add(_txtCount);

            // Info
            var infoText = new TextBlock
            {
                Text = "Style = base TMA texture id (fallback)\n" +
                       "Paint Idx = offset into paint_mem[]\n" +
                       "Count = number of paint bytes",
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.DarkGray,
                Margin = new Thickness(0, 10, 0, 10)
            };
            Grid.SetRow(infoText, 3);
            Grid.SetColumnSpan(infoText, 2);
            grid.Children.Add(infoText);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(buttonPanel, 4);
            Grid.SetColumnSpan(buttonPanel, 2);

            var btnOk = new Button { Content = "OK", Width = 60, Margin = new Thickness(0, 0, 5, 0) };
            btnOk.Click += (s, e) =>
            {
                if (ushort.TryParse(_txtStyle.Text, out ushort style) &&
                    ushort.TryParse(_txtPaintIndex.Text, out ushort paintIndex) &&
                    sbyte.TryParse(_txtCount.Text, out sbyte count))
                {
                    Style = style;
                    PaintIndex = paintIndex;
                    Count = count;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Invalid values.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            buttonPanel.Children.Add(btnOk);

            var btnCancel = new Button { Content = "Cancel", Width = 60, IsCancel = true };
            buttonPanel.Children.Add(btnCancel);

            grid.Children.Add(buttonPanel);
            Content = grid;
        }
    }

    /// <summary>
    /// Dialog for editing an existing DStorey entry.
    /// DStorey fields: Style (U16), PaintIndex (U16), Count (S8), Padding (U8)
    /// </summary>
    public class EditStoreyDialog : Window
    {
        private TextBox _txtStyle;
        private TextBox _txtPaintIndex;
        private TextBox _txtCount;
        private TextBox _txtPadding;

        public ushort Style { get; private set; }
        public ushort PaintIndex { get; private set; }
        public sbyte Count { get; private set; }
        public byte Padding { get; private set; }

        public EditStoreyDialog(DStoreyViewModel storey)
        {
            Title = $"Edit DStorey #{storey.Index}";
            Width = 300;
            base.Height = 280;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.DarkGray;

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Style
            var lblStyle = new TextBlock { Text = "Style:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblStyle, 0);
            grid.Children.Add(lblStyle);

            _txtStyle = new TextBox { Text = storey.Style.ToString(), Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtStyle, 0);
            Grid.SetColumn(_txtStyle, 1);
            grid.Children.Add(_txtStyle);

            // PaintIndex
            var lblPaintIndex = new TextBlock { Text = "Paint Idx:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblPaintIndex, 1);
            grid.Children.Add(lblPaintIndex);

            _txtPaintIndex = new TextBox { Text = storey.PaintIndex.ToString(), Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtPaintIndex, 1);
            Grid.SetColumn(_txtPaintIndex, 1);
            grid.Children.Add(_txtPaintIndex);

            // Count
            var lblCount = new TextBlock { Text = "Count:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblCount, 2);
            grid.Children.Add(lblCount);

            _txtCount = new TextBox { Text = storey.Count.ToString(), Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtCount, 2);
            Grid.SetColumn(_txtCount, 1);
            grid.Children.Add(_txtCount);

            // Padding
            var lblPadding = new TextBlock { Text = "Padding:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblPadding, 3);
            grid.Children.Add(lblPadding);

            _txtPadding = new TextBox { Text = storey.Padding.ToString(), Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtPadding, 3);
            Grid.SetColumn(_txtPadding, 1);
            grid.Children.Add(_txtPadding);

            // Info
            var infoText = new TextBlock
            {
                Text = $"Referenced by {storey.ReferencedByDStyles.Count} dstyles entries\n" +
                       $"Used by {storey.UsedByFacets.Count} facets\n" +
                       (storey.PaintBytes.Length > 0
                           ? $"Paint: [{storey.PaintHex}]"
                           : "No paint data"),
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.DarkGray,
                Margin = new Thickness(0, 10, 0, 10)
            };
            Grid.SetRow(infoText, 4);
            Grid.SetColumnSpan(infoText, 2);
            grid.Children.Add(infoText);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(buttonPanel, 5);
            Grid.SetColumnSpan(buttonPanel, 2);

            var btnOk = new Button { Content = "OK", Width = 60, Margin = new Thickness(0, 0, 5, 0) };
            btnOk.Click += (s, e) =>
            {
                if (ushort.TryParse(_txtStyle.Text, out ushort style) &&
                    ushort.TryParse(_txtPaintIndex.Text, out ushort paintIndex) &&
                    sbyte.TryParse(_txtCount.Text, out sbyte count) &&
                    byte.TryParse(_txtPadding.Text, out byte padding))
                {
                    Style = style;
                    PaintIndex = paintIndex;
                    Count = count;
                    Padding = padding;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Invalid values.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            buttonPanel.Children.Add(btnOk);

            var btnCancel = new Button { Content = "Cancel", Width = 60, IsCancel = true };
            buttonPanel.Children.Add(btnCancel);

            grid.Children.Add(buttonPanel);
            Content = grid;
        }
    }
}