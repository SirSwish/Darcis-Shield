// /Views/TexturePickerWindow.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UrbanChaosStyleEditor.Models;
using UrbanChaosStyleEditor.Services;

namespace UrbanChaosStyleEditor.Views
{
    public partial class TexturePickerWindow : Window
    {
        /// <summary>
        /// The chosen absolute texture index (0-255 = world, 256+ = shared).
        /// -1 means nothing was picked.
        /// </summary>
        public int SelectedIndex { get; private set; } = -1;

        public TexturePickerWindow(StyleProject project)
        {
            InitializeComponent();
            BuildWorldGrid(project);
            BuildSharedGrid();
        }

        private void BuildWorldGrid(StyleProject project)
        {
            foreach (var slot in project.Slots)
            {
                var border = MakeTileBorder(slot.Index, slot.Image, slot.DisplayName);
                border.MouseLeftButtonDown += Slot_Click;
                WorldSlotGrid.Items.Add(border);
            }
        }

        private void BuildSharedGrid()
        {
            var shared = SharedTextureLoader.Load();

            if (shared.Count == 0)
            {
                SharedSlotGrid.Items.Add(new TextBlock
                {
                    Text = "No shared textures found.\nPlace them in Assets/Textures/Release/shared/",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                    Margin = new Thickness(8),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            // Show in ascending absolute index order
            var sorted = new System.Collections.Generic.SortedDictionary<int, BitmapSource>(
                new System.Collections.Generic.Dictionary<int, BitmapSource>(shared));

            foreach (var kvp in sorted)
            {
                int absIndex = kvp.Key;
                string label = $"tex{absIndex:D3}hi";
                var border = MakeTileBorder(absIndex, kvp.Value, label);
                border.MouseLeftButtonDown += Slot_Click;
                SharedSlotGrid.Items.Add(border);
            }
        }

        private static Border MakeTileBorder(int absoluteIndex, BitmapSource? image, string label)
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
                Tag = absoluteIndex
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

            if (image != null)
            {
                var img = new Image
                {
                    Source = image,
                    Stretch = Stretch.UniformToFill,
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                imgBorder.Child = img;
            }

            stack.Children.Add(imgBorder);
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            });

            border.Child = stack;

            border.MouseEnter += (s, _) =>
            {
                if (s is Border b)
                    b.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
            };
            border.MouseLeave += (s, _) =>
            {
                if (s is Border b)
                    b.BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
            };

            return border;
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
