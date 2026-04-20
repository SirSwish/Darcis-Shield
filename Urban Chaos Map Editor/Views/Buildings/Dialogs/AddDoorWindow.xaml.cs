using System.Windows;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class AddDoorWindow : Window, IFacetMultiDrawWindow
    {
        private readonly int _buildingId1;

        public bool WasCancelled { get; private set; } = true;

        public AddDoorWindow(int buildingId1)
        {
            InitializeComponent();
            _buildingId1 = buildingId1;
            TxtBuildingInfo.Text = $"Building #{buildingId1}";
        }

        private void BtnDrawOnMap_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow?.DataContext is not MainWindowViewModel mainVm)
            {
                MessageBox.Show("Cannot access main window.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Parse Y0 from input (Quarter Storeys → raw)
            short y0 = 0;
            if (short.TryParse(TxtY0.Text, out var parsedQS))
                y0 = (short)(parsedQS * 64);

            // Door uses fixed values:
            // - Height: 4 (coarse)
            // - BlockHeight: 16
            // - Flags: TwoSided + Unclimbable
            var template = new FacetTemplate
            {
                Type = FacetType.Door,
                Height = 4,
                FHeight = 0,
                BlockHeight = 16,
                Y0 = y0,
                Y1 = y0,
                RawStyleId = 0,
                Flags = FacetFlags.TwoSided | FacetFlags.Unclimbable,
                BuildingId1 = _buildingId1,
                Storey = 0
            };

            mainVm.Map.BeginFacetMultiDraw(this, template);
            Hide();

            mainVm.StatusMessage = "Click start point, then end point for the door. Right-click to finish.";
        }

        public void OnDrawCancelled()
        {
            Show();
            Activate();
            WasCancelled = true;
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel m)
                m.StatusMessage = "Door drawing cancelled.";
        }

        public void OnDrawCompleted(int facetsAdded)
        {
            WasCancelled = false;

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel m)
                m.StatusMessage = $"Door added ({facetsAdded} facet).";

            BuildingsChangeBus.Instance.NotifyChanged();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            WasCancelled = true;
            Close();
        }
    }
}