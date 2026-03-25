// /Views/TexturePickerWindow.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosStyleEditor.Models;

namespace UrbanChaosStyleEditor.Views
{
    public partial class TexturePickerWindow : Window
    {
        public int SelectedIndex { get; private set; } = -1;

        public TexturePickerWindow(StyleProject project)
        {
            InitializeComponent();
            BuildGrid(project);
        }

        private void BuildGrid(StyleProject project)
        {
            foreach (var slot in project.Slots)
            {
                var border = new Border
                {
                    Width = 56,
                    Height = 72,
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x32)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Cursor = Cursors.Hand,
                    Tag = slot.Index
                };

                var stack = new StackPanel();

                var imgBorder = new Border
                {
                    Width = 48,
                    Height = 48,
                    Margin = new Thickness(4, 4, 4, 0),
                    Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1B, 0x1E)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                    BorderThickness = new Thickness(1)
                };

                if (slot.Image != null)
                {
                    var img = new Image
                    {
                        Source = slot.Image,
                        Stretch = Stretch.UniformToFill,
                        SnapsToDevicePixels = true
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                    imgBorder.Child = img;
                }

                stack.Children.Add(imgBorder);

                var label = new TextBlock
                {
                    Text = slot.DisplayName,
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                stack.Children.Add(label);

                border.Child = stack;
                border.MouseLeftButtonDown += Slot_Click;

                // Highlight on hover
                border.MouseEnter += (s, e) =>
                {
                    if (s is Border b)
                        b.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                };
                border.MouseLeave += (s, e) =>
                {
                    if (s is Border b)
                        b.BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
                };

                SlotGrid.Items.Add(border);
            }
        }

        private void Slot_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is int index)
            {
                SelectedIndex = index;
                DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}