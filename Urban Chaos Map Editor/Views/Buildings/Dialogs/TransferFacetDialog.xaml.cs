// Views/Buildings/Dialogs/TransferFacetDialog.xaml.cs

using System.Windows;
using System.Windows.Controls;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class TransferFacetDialog : Window
    {
        private readonly int _sourceFacetId1;
        private readonly int _sourceBuildingId1;

        /// <summary>The building ID the user selected (1-based). 0 if cancelled.</summary>
        public int SelectedBuildingId { get; private set; }

        public TransferFacetDialog(int sourceFacetId1, int sourceBuildingId1)
        {
            InitializeComponent();

            _sourceFacetId1    = sourceFacetId1;
            _sourceBuildingId1 = sourceBuildingId1;

            TxtFacetInfo.Text = $"Facet #{sourceFacetId1} will be moved from Building #{sourceBuildingId1} to the selected building.";

            PopulateBuildings();
        }

        private void PopulateBuildings()
        {
            var snap = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();
            if (snap.Buildings == null) return;

            for (int i = 0; i < snap.Buildings.Length; i++)
            {
                int id1 = i + 1;
                if (id1 == _sourceBuildingId1) continue;   // exclude current owner

                var b = snap.Buildings[i];
                int facetCount = b.EndFacet - b.StartFacet;
                string label = $"B#{id1}  [{b.Type}]  ({facetCount} facet{(facetCount == 1 ? "" : "s")})";

                BuildingsList.Items.Add(new BuildingItem { Id1 = id1, Label = label });
            }
        }

        private void BuildingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BtnTransfer.IsEnabled = BuildingsList.SelectedItem is BuildingItem;
        }

        private void BuildingsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (BuildingsList.SelectedItem is BuildingItem)
                Confirm();
        }

        private void BtnTransfer_Click(object sender, RoutedEventArgs e) => Confirm();

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Confirm()
        {
            if (BuildingsList.SelectedItem is not BuildingItem item) return;
            SelectedBuildingId = item.Id1;
            DialogResult = true;
            Close();
        }

        private sealed class BuildingItem
        {
            public int    Id1   { get; set; }
            public string Label { get; set; } = "";
            public override string ToString() => Label;
        }
    }
}
