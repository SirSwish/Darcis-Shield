using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class FacetSelectionDialog : Window
    {
        public sealed class CandidateVm
        {
            public int FacetId1 { get; init; }
            public int BuildingId1 { get; init; }
            public int StoreyId { get; init; }
            public string TypeName { get; init; } = "";
            public string Coords { get; init; } = "";

            public string Display =>
                $"Facet #{FacetId1} | Building #{BuildingId1} | Storey {StoreyId} | {TypeName} | {Coords}";
        }

        public ObservableCollection<CandidateVm> Candidates { get; } = new();

        public CandidateVm? SelectedCandidate { get; private set; }

        // NEW
        public bool OpenEditor { get; private set; }

        public FacetSelectionDialog(IEnumerable<CandidateVm> candidates)
        {
            InitializeComponent();

            foreach (var c in candidates)
                Candidates.Add(c);

            DataContext = this;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (CandidatesList.SelectedItem is CandidateVm vm)
            {
                SelectedCandidate = vm;
                OpenEditor = false;
                DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // NEW
        private void CandidatesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (CandidatesList.SelectedItem is CandidateVm vm)
            {
                SelectedCandidate = vm;
                OpenEditor = true;
                DialogResult = true;
            }
        }
    }
}