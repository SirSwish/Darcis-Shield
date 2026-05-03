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

public partial class EventPointUsageFinderWindow : Window
{
    private readonly Action<string> _loadMission;
    private readonly UcmFileService _ucmFileService = new();
    private readonly ObservableCollection<EventPointUsageResult> _results = new();
    private CancellationTokenSource? _searchCancellation;

    public EventPointUsageFinderWindow(Action<string> loadMission)
    {
        InitializeComponent();
        _loadMission = loadMission;

        ResultsGrid.ItemsSource = _results;
        DirectoryTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        PopulateFilterValues();
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
        }
    }

    private void FilterModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilterValueCombo is not null)
        {
            PopulateFilterValues();
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

        if (FilterValueCombo.SelectedItem is not FilterItem selectedFilter)
        {
            MessageBox.Show(this, "Please select a search value.", "No search value",
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
                () => FindMatches(directory, selectedFilter.Value, _searchCancellation.Token),
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
        if (ResultsGrid.SelectedItem is EventPointUsageResult result)
        {
            _loadMission(result.FullPath);
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PopulateFilterValues()
    {
        if (FilterValueCombo is null)
        {
            return;
        }

        var searchByCategory = FilterModeCombo.SelectedIndex == 1;
        FilterValueCombo.ItemsSource = searchByCategory
            ? Enum.GetValues<WaypointCategory>()
                .Select(category => new FilterItem(FormatEnumName(category), category))
                .ToList()
            : Enum.GetValues<WaypointType>()
                .Where(type => type != WaypointType.None)
                .Select(type => new FilterItem(EditorStrings.GetWaypointTypeName(type), type))
                .ToList();

        FilterValueCombo.SelectedIndex = 0;
    }

    private List<EventPointUsageResult> FindMatches(string directory, object filterValue, CancellationToken cancellationToken)
    {
        var matches = new List<EventPointUsageResult>();

        foreach (var filePath in Directory.EnumerateFiles(directory, "*.ucm", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var mission = _ucmFileService.ReadMission(filePath);
                var matchedEps = mission.EventPoints
                    .Where(ep => ep.Used && MatchesFilter(ep, filterValue))
                    .Select(ep => ep.Index)
                    .OrderBy(index => index)
                    .ToList();

                if (matchedEps.Count > 0)
                {
                    matches.Add(new EventPointUsageResult
                    {
                        FileName = Path.GetFileName(filePath),
                        FullPath = filePath,
                        MissionName = string.IsNullOrWhiteSpace(mission.MissionName)
                            ? "(unnamed)"
                            : mission.MissionName,
                        MatchCount = matchedEps.Count,
                        EpIndices = string.Join(", ", matchedEps)
                    });
                }
            }
            catch
            {
                // Skip files that are not readable UCM missions.
            }
        }

        return matches
            .OrderBy(result => result.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesFilter(EventPoint eventPoint, object filterValue)
    {
        return filterValue switch
        {
            WaypointType waypointType => eventPoint.WaypointType == waypointType,
            WaypointCategory category => eventPoint.Category == category,
            _ => false
        };
    }

    private static string FormatEnumName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var text = value.ToString();
        return string.Concat(text.Select((ch, index) =>
            index > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString()));
    }

    private sealed record FilterItem(string DisplayName, object Value);

    private sealed class EventPointUsageResult
    {
        public string FileName { get; init; } = string.Empty;
        public int MatchCount { get; init; }
        public string EpIndices { get; init; } = string.Empty;
        public string MissionName { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
    }
}
