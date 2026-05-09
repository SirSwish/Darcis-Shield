using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.Models;
using UrbanChaosMissionEditor.Services;

namespace UrbanChaosMissionEditor.Views.Dialogs;

public partial class TriggerUsageFinderWindow : Window
{
    private readonly Action<string> _loadMission;
    private readonly UcmFileService _ucmFileService = new();
    private readonly ObservableCollection<TriggerUsageResult> _results = new();
    private CancellationTokenSource? _searchCancellation;

    public TriggerUsageFinderWindow(Action<string> loadMission)
    {
        InitializeComponent();
        _loadMission = loadMission;

        ResultsGrid.ItemsSource = _results;

        var lastDir = EditorSettingsService.Instance.LastDebugSearchDirectory;
        DirectoryTextBox.Text = !string.IsNullOrWhiteSpace(lastDir) && Directory.Exists(lastDir)
            ? lastDir
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        PopulateTriggerTypes();
    }

    private void PopulateTriggerTypes()
    {
        TriggerTypeCombo.ItemsSource = Enum.GetValues<TriggerType>()
            .Where(type => type != TriggerType.None)
            .Select(type => new TriggerItem(EditorStrings.GetTriggerTypeName(type), type))
            .ToList();

        TriggerTypeCombo.SelectedIndex = 0;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select UCM search directory",
            InitialDirectory = Directory.Exists(DirectoryTextBox.Text)
                ? DirectoryTextBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog(this) == true)
        {
            DirectoryTextBox.Text = dialog.FolderName;
            EditorSettingsService.Instance.LastDebugSearchDirectory = dialog.FolderName;
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        var directory = DirectoryTextBox.Text.Trim();
        if (!Directory.Exists(directory))
        {
            MessageBox.Show(this, "Please select a valid directory.", "Directory not found",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        EditorSettingsService.Instance.LastDebugSearchDirectory = directory;

        if (TriggerTypeCombo.SelectedItem is not TriggerItem selectedTrigger)
        {
            MessageBox.Show(this, "Please select a trigger type.", "No trigger selected",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _searchCancellation?.Cancel();
        _searchCancellation = new CancellationTokenSource();

        _results.Clear();
        SearchButton.IsEnabled = false;
        CancelSearchButton.IsEnabled = true;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = true;
        StatusText.Text = "Searching...";
        SummaryText.Text = string.Empty;

        try
        {
            var results = await Task.Run(
                () => FindMatches(directory, selectedTrigger.Value, _searchCancellation.Token),
                _searchCancellation.Token);

            foreach (var result in results)
            {
                _results.Add(result);
            }

            SummaryText.Text = $"{_results.Count} mission(s) found.";
            StatusText.Text = "Search complete.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Search cancelled.";
        }
        finally
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Visibility = Visibility.Collapsed;
            CancelSearchButton.IsEnabled = false;
            SearchButton.IsEnabled = true;
        }
    }

    private void CancelSearch_Click(object sender, RoutedEventArgs e)
    {
        _searchCancellation?.Cancel();
    }

    private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem is TriggerUsageResult result)
        {
            _loadMission(result.FullPath);
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private List<TriggerUsageResult> FindMatches(string directory, TriggerType triggerType, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(directory, "*.ucm", SearchOption.AllDirectories).ToList();
        var matches = new System.Collections.Concurrent.ConcurrentBag<TriggerUsageResult>();

        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
        };

        Parallel.ForEach(files, options, filePath =>
        {
            try
            {
                var (missionName, eps) = _ucmFileService.ReadEventPointsForScan(filePath);
                var matchedEps = eps
                    .Where(ep => ep.Used && ep.TriggeredBy == triggerType)
                    .Select(ep => ep.Index)
                    .OrderBy(index => index)
                    .ToList();

                if (matchedEps.Count > 0)
                {
                    matches.Add(new TriggerUsageResult
                    {
                        FileName = Path.GetFileName(filePath),
                        FullPath = filePath,
                        MissionName = string.IsNullOrWhiteSpace(missionName) ? "(unnamed)" : missionName,
                        MatchCount = matchedEps.Count,
                        EpIndices = string.Join(", ", matchedEps)
                    });
                }
            }
            catch
            {
                // Skip files that are not readable UCM missions.
            }
        });

        return matches
            .OrderBy(result => result.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record TriggerItem(string DisplayName, TriggerType Value);

    private sealed class TriggerUsageResult
    {
        public string FileName { get; init; } = string.Empty;
        public int MatchCount { get; init; }
        public string EpIndices { get; init; } = string.Empty;
        public string MissionName { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
    }
}
