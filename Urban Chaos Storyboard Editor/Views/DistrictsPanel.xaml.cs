using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UrbanChaosStoryboardEditor.Models;
using UrbanChaosStoryboardEditor.Services;
using UrbanChaosStoryboardEditor.Views.Dialogs;

namespace UrbanChaosStoryboardEditor.Views
{
    public partial class DistrictsPanel : UserControl
    {
        public DistrictsPanel()
        {
            InitializeComponent();
        }

        private void AddDistrict_Click(object sender, RoutedEventArgs e)
        {
            var svc = StyDataService.Instance;
            if (!svc.IsLoaded)
            {
                MessageBox.Show("Please create or open a storyboard file first.", "No File",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newDistrict = new District
            {
                DistrictId = svc.Districts.Count,
                DistrictName = "New District",
                XPos = 100,
                YPos = 100
            };

            svc.Districts.Add(newDistrict);
            svc.MarkDirty();
        }

        private void EditDistrict_Click(object sender, RoutedEventArgs e)
        {
            if (DistrictsGrid.SelectedItem is District district)
            {
                OpenEditDialog(district);
            }
            else
            {
                MessageBox.Show("Please select a district to edit.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteDistrict_Click(object sender, RoutedEventArgs e)
        {
            if (DistrictsGrid.SelectedItem is District district)
            {
                var result = MessageBox.Show($"Delete district '{district.DistrictName}'?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    StyDataService.Instance.Districts.Remove(district);
                    StyDataService.Instance.MarkDirty();
                }
            }
            else
            {
                MessageBox.Show("Please select a district to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SetPosition_Click(object sender, RoutedEventArgs e)
        {
            if (DistrictsGrid.SelectedItem is not District district)
            {
                MessageBox.Show("Please select a district first.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Find the MapCanvas in MainWindow
            var mainWindow = Application.Current.MainWindow as MainWindow;
            var mapCanvas = mainWindow?.FindName("MapCanvas") as Canvas;

            if (mapCanvas != null)
            {
                mapCanvas.Cursor = Cursors.Cross;

                MouseButtonEventHandler? handler = null;
                handler = (s, args) =>
                {
                    var pos = args.GetPosition(mapCanvas);
                    district.XPos = (int)pos.X;
                    district.YPos = (int)pos.Y;
                    StyDataService.Instance.MarkDirty();

                    mapCanvas.Cursor = Cursors.Arrow;
                    mapCanvas.MouseLeftButtonDown -= handler;
                };

                mapCanvas.MouseLeftButtonDown += handler;
            }
        }

        private void DistrictsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DistrictsGrid.SelectedItem is District district)
            {
                OpenEditDialog(district);
            }
        }

        private void OpenEditDialog(District district)
        {
            var dialog = new DistrictEditDialog(district)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                StyDataService.Instance.MarkDirty();
            }
        }
    }
}