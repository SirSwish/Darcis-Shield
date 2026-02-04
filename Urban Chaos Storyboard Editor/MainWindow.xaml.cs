using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using UrbanChaosStoryboardEditor.Models;
using UrbanChaosStoryboardEditor.Services;
using UrbanChaosStoryboardEditor.ViewModels;
using UrbanChaosStoryboardEditor.Views.Dialogs;

namespace UrbanChaosStoryboardEditor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel();

            var svc = StyDataService.Instance;

            // Update counts and file path when data changes
            svc.FileLoaded += (_, __) => UpdateUI();
            svc.FileCleared += (_, __) => UpdateUI();
            svc.Districts.CollectionChanged += OnDistrictsChanged;
            svc.Missions.CollectionChanged += (_, __) => UpdateCounts();

            UpdateUI();
        }

        private void OnDistrictsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateDistrictMarkers();
            UpdateCounts();
        }

        private void UpdateUI()
        {
            var svc = StyDataService.Instance;

            // Show/hide placeholder and map background
            PlaceholderText.Visibility = svc.IsLoaded ? Visibility.Collapsed : Visibility.Visible;
            MapBackgroundImage.Visibility = svc.IsLoaded ? Visibility.Visible : Visibility.Collapsed;

            FilePathText.Text = svc.CurrentPath ?? "(none)";

            UpdateDistrictMarkers();
            UpdateCounts();
        }

        private void UpdateDistrictMarkers()
        {
            var svc = StyDataService.Instance;

            // Filter out districts at 0,0 (placeholder/unused districts)
            var visibleDistricts = svc.Districts
                .Where(d => d.XPos != 0 || d.YPos != 0)
                .ToList();

            DistrictMarkers.ItemsSource = visibleDistricts;
        }

        private void UpdateCounts()
        {
            var svc = StyDataService.Instance;
            DistrictCountText.Text = svc.Districts.Count.ToString();
            MissionCountText.Text = svc.Missions.Count.ToString();
        }

        private void Pin_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Left-click opens the context menu
            if (sender is Ellipse pin && pin.ContextMenu != null)
            {
                pin.ContextMenu.PlacementTarget = pin;
                pin.ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void PinContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu) return;

            // Get the district from the Tag (set via binding)
            District? district = null;

            // The ContextMenu's Tag binding doesn't work well, get it from PlacementTarget
            if (menu.PlacementTarget is Ellipse pin && pin.Tag is District d)
            {
                district = d;
            }

            if (district == null) return;

            // Clear existing items
            menu.Items.Clear();

            // Add header
            var header = new MenuItem
            {
                Header = $"📍 {(string.IsNullOrEmpty(district.DistrictName) ? $"District {district.DistrictId}" : district.DistrictName)}",
                IsEnabled = false,
                FontWeight = FontWeights.Bold
            };
            menu.Items.Add(header);
            menu.Items.Add(new Separator());

            // Find all missions for this district
            var svc = StyDataService.Instance;
            var missions = svc.Missions
                .Where(m => m.District == district.DistrictId)
                .OrderBy(m => m.ObjectId)
                .ToList();

            if (missions.Count == 0)
            {
                menu.Items.Add(new MenuItem
                {
                    Header = "(No missions in this district)",
                    IsEnabled = false,
                    FontStyle = FontStyles.Italic
                });
            }
            else
            {
                foreach (var mission in missions)
                {
                    var menuItem = new MenuItem
                    {
                        Header = $"🎯 [{mission.ObjectId}] {mission.MissionName}",
                        Tag = mission
                    };
                    menuItem.Click += MissionMenuItem_Click;
                    menu.Items.Add(menuItem);
                }
            }

            // Add separator and edit district option
            menu.Items.Add(new Separator());
            var editDistrictItem = new MenuItem
            {
                Header = "✏️ Edit District...",
                Tag = district
            };
            editDistrictItem.Click += EditDistrictMenuItem_Click;
            menu.Items.Add(editDistrictItem);
        }

        private void MissionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MissionEntry mission)
            {
                var dialog = new MissionEditDialog(mission)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    StyDataService.Instance.MarkDirty();
                }
            }
        }

        private void EditDistrictMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is District district)
            {
                var dialog = new DistrictEditDialog(district)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    StyDataService.Instance.MarkDirty();
                    UpdateDistrictMarkers();
                }
            }
        }
    }
}