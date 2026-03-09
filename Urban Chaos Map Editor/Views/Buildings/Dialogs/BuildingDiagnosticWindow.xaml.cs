using Microsoft.Win32;
using System.IO;
using System.Windows;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class BuildingDiagnosticsWindow : Window
    {
        public BuildingDiagnosticsWindow()
        {
            InitializeComponent();
            TxtOutput.Text = "Click a button above to generate a diagnostic report.\n\n" +
                            "• Full Map Report - Shows all buildings, facets, storeys, and walkables\n" +
                            "• Compare Buildings - Side-by-side comparison of two buildings\n" +
                            "• Single Building - Detailed view of one building (uses Building A field)";
        }

        private void BtnFullReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = MapDataService.Instance;
                if (!svc.IsLoaded)
                {
                    TxtOutput.Text = "ERROR: No map loaded.";
                    return;
                }

                TxtOutput.Text = "Generating full report...";
                var report = WalkableDiagnostics.GenerateFullReport(svc);
                TxtOutput.Text = report;
            }
            catch (Exception ex)
            {
                TxtOutput.Text = $"ERROR: {ex.Message}\n\n{ex.StackTrace}";
            }
        }

        private void BtnCompare_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = MapDataService.Instance;
                if (!svc.IsLoaded)
                {
                    TxtOutput.Text = "ERROR: No map loaded.";
                    return;
                }

                if (!int.TryParse(TxtBuildingA.Text.Trim(), out int buildingA) || buildingA < 1)
                {
                    TxtOutput.Text = "ERROR: Invalid Building A ID.";
                    return;
                }

                if (!int.TryParse(TxtBuildingB.Text.Trim(), out int buildingB) || buildingB < 1)
                {
                    TxtOutput.Text = "ERROR: Invalid Building B ID.";
                    return;
                }

                TxtOutput.Text = "Generating comparison report...";
                var report = WalkableDiagnostics.CompareBuildingsReport(svc, buildingA, buildingB);
                TxtOutput.Text = report;
            }
            catch (Exception ex)
            {
                TxtOutput.Text = $"ERROR: {ex.Message}\n\n{ex.StackTrace}";
            }
        }

        private void BtnSingleBuilding_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = MapDataService.Instance;
                if (!svc.IsLoaded)
                {
                    TxtOutput.Text = "ERROR: No map loaded.";
                    return;
                }

                if (!int.TryParse(TxtBuildingA.Text.Trim(), out int buildingId) || buildingId < 1)
                {
                    TxtOutput.Text = "ERROR: Invalid Building A ID.";
                    return;
                }

                TxtOutput.Text = "Generating building summary...";
                var report = WalkableDiagnostics.BuildingSummary(svc, buildingId);
                TxtOutput.Text = report;
            }
            catch (Exception ex)
            {
                TxtOutput.Text = $"ERROR: {ex.Message}\n\n{ex.StackTrace}";
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtOutput.Text);
                MessageBox.Show("Report copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveToFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    DefaultExt = ".txt",
                    FileName = $"building_diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, TxtOutput.Text);
                    MessageBox.Show($"Report saved to:\n{dlg.FileName}", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}