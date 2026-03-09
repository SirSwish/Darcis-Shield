// /Views/Dialogs/Buildings/AddBuildingDialog.xaml.cs
using System.Windows;
using System.Windows.Controls;
using UrbanChaosMapEditor.Models.Buildings;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class AddBuildingDialog : Window
    {
        public BuildingType SelectedBuildingType { get; private set; } = BuildingType.House;
        public bool WasConfirmed { get; private set; }

        public AddBuildingDialog()
        {
            InitializeComponent();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            WasConfirmed = false;
            DialogResult = false;
            Close();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (CmbBuildingType.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
            {
                if (byte.TryParse(tagStr, out byte typeVal))
                {
                    SelectedBuildingType = (BuildingType)typeVal;
                }
            }

            WasConfirmed = true;
            DialogResult = true;
            Close();
        }
    }
}