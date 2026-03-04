// /Views/AddStoreyDialog.xaml.cs and EditStoreyDialog.xaml.cs
// Combined in one file for simplicity - split if needed

using System.Windows;
using System.Windows.Controls;
using UrbanChaosMapEditor.Models;

namespace UrbanChaosMapEditor.Views
{
    /// <summary>
    /// Dialog for adding a new DStorey entry
    /// </summary>
    public class AddStoreyDialog : Window
    {
        private TextBox _txtStyle;
        private TextBox _txtHeight;
        private TextBox _txtFlags;

        public ushort Style { get; private set; }
        public byte StoreyHeight { get; private set; }
        public byte Flags { get; private set; }

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

            // Height
            var lblHeight = new TextBlock { Text = "Height:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblHeight, 1);
            grid.Children.Add(lblHeight);

            _txtHeight = new TextBox { Text = "24", Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtHeight, 1);
            Grid.SetColumn(_txtHeight, 1);
            grid.Children.Add(_txtHeight);

            // Flags
            var lblFlags = new TextBlock { Text = "Flags:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblFlags, 2);
            grid.Children.Add(lblFlags);

            _txtFlags = new TextBox { Text = "5", Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtFlags, 2);
            Grid.SetColumn(_txtFlags, 1);
            grid.Children.Add(_txtFlags);

            // Help text
            var helpText = new TextBlock
            {
                Text = "Height = WorldY / 32\nFlags: 0x05 is common for interiors",
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.DarkGray,
                Margin = new Thickness(0, 10, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(helpText, 3);
            Grid.SetColumnSpan(helpText, 2);
            grid.Children.Add(helpText);

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
                    byte.TryParse(_txtHeight.Text, out byte height) &&
                    byte.TryParse(_txtFlags.Text, out byte flags))
                {
                    Style = style;
                    StoreyHeight = height;
                    Flags = flags;
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
    /// Dialog for editing an existing DStorey entry
    /// </summary>
    public class EditStoreyDialog : Window
    {
        private TextBox _txtStyle;
        private TextBox _txtHeight;
        private TextBox _txtFlags;
        private TextBox _txtBuilding;

        public ushort Style { get; private set; }
        public byte StoreyHeight { get; private set; }
        public byte Flags { get; private set; }
        public ushort Building { get; private set; }

        public EditStoreyDialog(DStoreyViewModel storey)
        {
            Title = $"Edit DStorey #{storey.Index}";
            Width = 300;
            base.Height = 260;
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

            // Height
            var lblHeight = new TextBlock { Text = "Height:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblHeight, 1);
            grid.Children.Add(lblHeight);

            _txtHeight = new TextBox { Text = storey.Height.ToString(), Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtHeight, 1);
            Grid.SetColumn(_txtHeight, 1);
            grid.Children.Add(_txtHeight);

            // Flags
            var lblFlags = new TextBlock { Text = "Flags:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblFlags, 2);
            grid.Children.Add(lblFlags);

            _txtFlags = new TextBox { Text = storey.Flags.ToString(), Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtFlags, 2);
            Grid.SetColumn(_txtFlags, 1);
            grid.Children.Add(_txtFlags);

            // Building
            var lblBuilding = new TextBlock { Text = "Building:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblBuilding, 3);
            grid.Children.Add(lblBuilding);

            _txtBuilding = new TextBox { Text = storey.Building.ToString(), Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtBuilding, 3);
            Grid.SetColumn(_txtBuilding, 1);
            grid.Children.Add(_txtBuilding);

            // Info
            var infoText = new TextBlock
            {
                Text = $"Referenced by {storey.ReferencedByDStyles.Count} dstyles entries\nUsed by {storey.UsedByFacets.Count} facets",
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
                    byte.TryParse(_txtHeight.Text, out byte height) &&
                    byte.TryParse(_txtFlags.Text, out byte flags) &&
                    ushort.TryParse(_txtBuilding.Text, out ushort building))
                {
                    Style = style;
                    StoreyHeight = height;
                    Flags = flags;
                    Building = building;
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