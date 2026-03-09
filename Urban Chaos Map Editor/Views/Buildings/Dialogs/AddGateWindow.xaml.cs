using System.Windows;
using System.Windows.Controls;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class AddGateWindow : Window, IFacetMultiDrawWindow
    {
        private readonly int _buildingId1;

        public bool WasCancelled { get; private set; } = true;

        public AddGateWindow(int buildingId1)
        {
            InitializeComponent();
            _buildingId1 = buildingId1;
            TxtBuildingInfo.Text = $"Fence Building #{buildingId1}";
        }

        private void BtnDrawOnMap_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow?.DataContext is not MainWindowViewModel mainVm)
            {
                MessageBox.Show("Cannot access main window.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var template = new FacetTemplate
            {
                Type = FacetType.OutsideDoor,
                Height = GetSelectedHeight(),
                FHeight = 0,
                BlockHeight = 16,
                Y0 = 0,
                Y1 = 0,
                RawStyleId = ushort.TryParse(TxtStyleId.Text, out var sid) ? sid : (ushort)22, // default gate style
                Flags = GetFlags(),
                BuildingId1 = _buildingId1,
                Storey = 0
            };

            mainVm.Map.BeginFacetMultiDraw(this, template);
            Hide();

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel m)
                m.StatusMessage = "Click start point, then end point for the gate. Right-click to finish.";
        }

        private byte GetSelectedHeight()
        {
            if (CmbGateType.SelectedItem is ComboBoxItem item && item.Tag is string tag && byte.TryParse(tag, out var h))
                return h;
            return 2; // standard gate
        }

        private FacetFlags GetFlags()
        {
            FacetFlags f = FacetFlags.TwoSided | FacetFlags.OnBuilding;
            if (ChkElectrified.IsChecked == true) f |= FacetFlags.Electrified;
            if (ChkBarbedTop.IsChecked == true) f |= FacetFlags.BarbTop;
            return f;
        }

        // === IFacetMultiDrawWindow implementation ===
        public void OnDrawCancelled()
        {
            Show();
            Activate();
            WasCancelled = true;
            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel m)
                m.StatusMessage = "Gate drawing cancelled.";
        }

        public void OnDrawCompleted(int facetsAdded)
        {
            Show();
            Activate();
            WasCancelled = false;

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel m)
                m.StatusMessage = $"Gate added successfully ({facetsAdded} facet(s)).";

            BuildingsChangeBus.Instance.NotifyChanged();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            WasCancelled = true;
            Close();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            WasCancelled = false;
            Close();
        }
    }
}