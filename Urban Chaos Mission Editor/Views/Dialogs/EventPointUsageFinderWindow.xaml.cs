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
        ItemTypeFilterCombo.ItemsSource = BuildItemTypeFilterItems();
        ItemTypeFilterCombo.SelectedIndex = 0;
        VehicleTypeFilterCombo.ItemsSource = BuildVehicleTypeFilterItems();
        VehicleTypeFilterCombo.SelectedIndex = 0;
        VehicleBehaviorFilterCombo.ItemsSource = BuildVehicleBehaviorFilterItems();
        VehicleBehaviorFilterCombo.SelectedIndex = 0;
        VehicleKeyFilterCombo.ItemsSource = BuildVehicleKeyFilterItems();
        VehicleKeyFilterCombo.SelectedIndex = 0;
        CameraTypeFilterCombo.ItemsSource = BuildCameraTypeFilterItems();
        CameraTypeFilterCombo.SelectedIndex = 0;
        CameraMovementFilterCombo.ItemsSource = BuildCameraMovementFilterItems();
        CameraMovementFilterCombo.SelectedIndex = 0;
        TargetTypeFilterCombo.ItemsSource = BuildTargetTypeFilterItems();
        TargetTypeFilterCombo.SelectedIndex = 0;
        ActivatePrimTypeFilterCombo.ItemsSource = BuildActivatePrimTypeFilterItems();
        ActivatePrimTypeFilterCombo.SelectedIndex = 0;

        var lastDir = EditorSettingsService.Instance.LastDebugSearchDirectory;
        DirectoryTextBox.Text = !string.IsNullOrWhiteSpace(lastDir) && Directory.Exists(lastDir)
            ? lastDir
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

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
            EditorSettingsService.Instance.LastDebugSearchDirectory = dialog.FolderName;
        }
    }

    private void FilterModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilterValueCombo is not null)
        {
            PopulateFilterValues();
        }
    }

    private void FilterValueCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSubFilterVisibility();
    }

    private void UpdateSubFilterVisibility()
    {
        if (HiddenInPrimOnlyCheckBox is null) return;

        bool isCreateItem = FilterValueCombo.SelectedItem is FilterItem { Value: WaypointType itemType }
                            && itemType == WaypointType.CreateItem;
        bool isCreateVehicle = FilterValueCombo.SelectedItem is FilterItem { Value: WaypointType vehicleType }
                               && vehicleType == WaypointType.CreateVehicle;
        bool isCreateCamera = FilterValueCombo.SelectedItem is FilterItem { Value: WaypointType cameraType }
                              && cameraType == WaypointType.CreateCamera;
        bool isCreateTarget = FilterValueCombo.SelectedItem is FilterItem { Value: WaypointType targetType }
                              && targetType == WaypointType.CreateTarget;
        bool isActivatePrim = FilterValueCombo.SelectedItem is FilterItem { Value: WaypointType activatePrimType }
                              && activatePrimType == WaypointType.ActivatePrim;

        if (ItemFilterPanel is not null)
        {
            ItemFilterPanel.Visibility = isCreateItem ? Visibility.Visible : Visibility.Collapsed;
            if (!isCreateItem)
            {
                if (ItemTypeFilterCombo is not null)
                    ItemTypeFilterCombo.SelectedIndex = 0;
                HiddenInPrimOnlyCheckBox.IsChecked = false;
            }
        }

        if (VehicleFilterPanel is not null)
        {
            VehicleFilterPanel.Visibility = isCreateVehicle ? Visibility.Visible : Visibility.Collapsed;
            if (!isCreateVehicle)
            {
                if (VehicleTypeFilterCombo is not null)
                    VehicleTypeFilterCombo.SelectedIndex = 0;
                if (VehicleBehaviorFilterCombo is not null)
                    VehicleBehaviorFilterCombo.SelectedIndex = 0;
                if (VehicleKeyFilterCombo is not null)
                    VehicleKeyFilterCombo.SelectedIndex = 0;
            }
        }

        if (CameraFilterPanel is not null)
        {
            CameraFilterPanel.Visibility = isCreateCamera ? Visibility.Visible : Visibility.Collapsed;
            if (!isCreateCamera)
            {
                if (CameraTypeFilterCombo is not null)
                    CameraTypeFilterCombo.SelectedIndex = 0;
                if (CameraMovementFilterCombo is not null)
                    CameraMovementFilterCombo.SelectedIndex = 0;
            }
        }

        if (TargetFilterPanel is not null)
        {
            TargetFilterPanel.Visibility = isCreateTarget ? Visibility.Visible : Visibility.Collapsed;
            if (!isCreateTarget && TargetTypeFilterCombo is not null)
            {
                TargetTypeFilterCombo.SelectedIndex = 0;
            }
        }

        if (ActivatePrimFilterPanel is not null)
        {
            ActivatePrimFilterPanel.Visibility = isActivatePrim ? Visibility.Visible : Visibility.Collapsed;
            if (!isActivatePrim && ActivatePrimTypeFilterCombo is not null)
            {
                ActivatePrimTypeFilterCombo.SelectedIndex = 0;
            }
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

        bool hiddenInPrimOnly = HiddenInPrimOnlyCheckBox.IsChecked == true
                                && ItemFilterPanel.Visibility == Visibility.Visible;
        int? itemTypeFilter = ItemFilterPanel.Visibility == Visibility.Visible
                              && ItemTypeFilterCombo.SelectedItem is FilterItem { Value: int itemType }
                              && itemType >= 0
            ? itemType
            : null;
        int? cameraTypeFilter = CameraFilterPanel.Visibility == Visibility.Visible
                                && CameraTypeFilterCombo.SelectedItem is FilterItem { Value: int cameraType }
                                && cameraType >= 0
            ? cameraType
            : null;
        int? cameraMovementFilter = CameraFilterPanel.Visibility == Visibility.Visible
                                    && CameraMovementFilterCombo.SelectedItem is FilterItem { Value: int movementType }
                                    && movementType >= 0
            ? movementType
            : null;
        int? targetTypeFilter = TargetFilterPanel.Visibility == Visibility.Visible
                                && TargetTypeFilterCombo.SelectedItem is FilterItem { Value: int targetType }
                                && targetType >= 0
            ? targetType
            : null;
        int? activatePrimTypeFilter = ActivatePrimFilterPanel.Visibility == Visibility.Visible
                                      && ActivatePrimTypeFilterCombo.SelectedItem is FilterItem { Value: int activatePrimType }
                                      && activatePrimType >= 0
            ? activatePrimType
            : null;
        int? vehicleTypeFilter = VehicleFilterPanel.Visibility == Visibility.Visible
                                 && VehicleTypeFilterCombo.SelectedItem is FilterItem { Value: int vehicleType }
                                 && vehicleType >= 0
            ? vehicleType
            : null;
        int? vehicleBehaviorFilter = VehicleFilterPanel.Visibility == Visibility.Visible
                                     && VehicleBehaviorFilterCombo.SelectedItem is FilterItem { Value: int vehicleBehavior }
                                     && vehicleBehavior >= 0
            ? vehicleBehavior
            : null;
        int? vehicleKeyFilter = VehicleFilterPanel.Visibility == Visibility.Visible
                                && VehicleKeyFilterCombo.SelectedItem is FilterItem { Value: int vehicleKey }
                                && vehicleKey >= 0
            ? vehicleKey
            : null;

        try
        {
            var results = await Task.Run(
                () => FindMatches(directory, selectedFilter.Value, hiddenInPrimOnly, itemTypeFilter, cameraTypeFilter, cameraMovementFilter, targetTypeFilter, activatePrimTypeFilter, vehicleTypeFilter, vehicleBehaviorFilter, vehicleKeyFilter, _searchCancellation.Token),
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

    private List<EventPointUsageResult> FindMatches(
        string directory,
        object filterValue,
        bool hiddenInPrimOnly,
        int? itemTypeFilter,
        int? cameraTypeFilter,
        int? cameraMovementFilter,
        int? targetTypeFilter,
        int? activatePrimTypeFilter,
        int? vehicleTypeFilter,
        int? vehicleBehaviorFilter,
        int? vehicleKeyFilter,
        CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(directory, "*.ucm", SearchOption.AllDirectories).ToList();
        var matches = new System.Collections.Concurrent.ConcurrentBag<EventPointUsageResult>();

        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
        };

        try
        {
            Parallel.ForEach(files, options, filePath =>
            {
                try
                {
                    var (missionName, eps) = _ucmFileService.ReadEventPointsForScan(filePath);
                    var matchedEps = eps
                        .Where(ep => ep.Used && MatchesFilter(ep, filterValue)
                                     && (!hiddenInPrimOnly || IsItemHiddenInPrim(ep)))
                        .Where(ep => !itemTypeFilter.HasValue || IsCreateItemType(ep, itemTypeFilter.Value))
                        .Where(ep => !cameraTypeFilter.HasValue || IsCreateCameraType(ep, cameraTypeFilter.Value))
                        .Where(ep => !cameraMovementFilter.HasValue || IsCreateCameraMovement(ep, cameraMovementFilter.Value))
                        .Where(ep => !targetTypeFilter.HasValue || IsCreateTargetType(ep, targetTypeFilter.Value))
                        .Where(ep => !activatePrimTypeFilter.HasValue || IsActivatePrimType(ep, activatePrimTypeFilter.Value))
                        .Where(ep => !vehicleTypeFilter.HasValue || IsCreateVehicleType(ep, vehicleTypeFilter.Value))
                        .Where(ep => !vehicleBehaviorFilter.HasValue || IsCreateVehicleBehavior(ep, vehicleBehaviorFilter.Value))
                        .Where(ep => !vehicleKeyFilter.HasValue || IsCreateVehicleKey(ep, vehicleKeyFilter.Value))
                        .OrderBy(ep => ep.Index)
                        .ToList();

                    if (matchedEps.Count > 0)
                    {
                        string fileName = Path.GetFileName(filePath);
                        string displayMissionName = string.IsNullOrWhiteSpace(missionName) ? "(unnamed)" : missionName;

                        if (filterValue is WaypointType.Increment)
                        {
                            foreach (var eventPoint in matchedEps.Where(ep => ep.WaypointType == WaypointType.Increment))
                            {
                                matches.Add(new EventPointUsageResult
                                {
                                    FileName = fileName,
                                    FullPath = filePath,
                                    MissionName = displayMissionName,
                                    MatchCount = 1,
                                    EpIndices = eventPoint.Index.ToString(),
                                    CounterIndex = GetIncrementCounterIndex(eventPoint).ToString(),
                                    IncrementNumber = GetIncrementBy(eventPoint).ToString()
                                });
                            }
                        }
                        else
                        {
                            var matchedIndices = matchedEps
                                .Select(ep => ep.Index)
                                .ToList();

                            matches.Add(new EventPointUsageResult
                            {
                                FileName = fileName,
                                FullPath = filePath,
                                MissionName = displayMissionName,
                                MatchCount = matchedEps.Count,
                                EpIndices = string.Join(", ", matchedIndices)
                            });
                        }
                    }
                }
                catch
                {
                    // Skip files that are not readable UCM missions.
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Propagate so caller can update UI state
            throw;
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

    private static bool IsItemHiddenInPrim(EventPoint eventPoint)
    {
        // Create Item flag bit 1 in Data[2] (matches EventPointViewModel.ItemFlagHiddenInPrim)
        return eventPoint.WaypointType == WaypointType.CreateItem
               && (eventPoint.Data[2] & (1 << 1)) != 0;
    }

    private static bool IsCreateItemType(EventPoint eventPoint, int itemType)
    {
        return eventPoint.WaypointType == WaypointType.CreateItem
               && eventPoint.Data.Length > 0
               && eventPoint.Data[0] == itemType;
    }

    private static bool IsCreateCameraType(EventPoint eventPoint, int cameraType)
    {
        return eventPoint.WaypointType == WaypointType.CreateCamera
               && eventPoint.Data.Length > 0
               && eventPoint.Data[0] == cameraType;
    }

    private static bool IsCreateCameraMovement(EventPoint eventPoint, int movementType)
    {
        return eventPoint.WaypointType == WaypointType.CreateCamera
               && eventPoint.Data.Length > 1
               && eventPoint.Data[1] == movementType;
    }

    private static bool IsCreateTargetType(EventPoint eventPoint, int targetType)
    {
        return eventPoint.WaypointType == WaypointType.CreateTarget
               && eventPoint.Data.Length > 1
               && eventPoint.Data[1] == targetType;
    }

    private static bool IsActivatePrimType(EventPoint eventPoint, int primType)
    {
        return eventPoint.WaypointType == WaypointType.ActivatePrim
               && eventPoint.Data.Length > 0
               && eventPoint.Data[0] == primType;
    }

    private static bool IsCreateVehicleType(EventPoint eventPoint, int vehicleType)
    {
        return eventPoint.WaypointType == WaypointType.CreateVehicle
               && eventPoint.Data.Length > 0
               && eventPoint.Data[0] == vehicleType;
    }

    private static bool IsCreateVehicleBehavior(EventPoint eventPoint, int behavior)
    {
        return eventPoint.WaypointType == WaypointType.CreateVehicle
               && eventPoint.Data.Length > 1
               && eventPoint.Data[1] == behavior;
    }

    private static bool IsCreateVehicleKey(EventPoint eventPoint, int key)
    {
        return eventPoint.WaypointType == WaypointType.CreateVehicle
               && eventPoint.Data.Length > 3
               && eventPoint.Data[3] == key;
    }

    private static int GetIncrementBy(EventPoint eventPoint)
    {
        return eventPoint.Data.Length > 0 ? Math.Clamp(eventPoint.Data[0], 0, 256) : 0;
    }

    private static int GetIncrementCounterIndex(EventPoint eventPoint)
    {
        return eventPoint.Data.Length > 1 && eventPoint.Data[1] >= 1 ? eventPoint.Data[1] : 1;
    }

    private static List<FilterItem> BuildItemTypeFilterItems()
    {
        var items = new List<FilterItem> { new("All Item Types", -1) };
        for (int i = 0; i < EditorStrings.ItemTypeNames.Length; i++)
        {
            items.Add(new FilterItem(EditorStrings.ItemTypeNames[i], i + 1));
        }

        return items;
    }

    private static List<FilterItem> BuildVehicleTypeFilterItems()
    {
        var items = new List<FilterItem> { new("All Vehicle Types", -1) };
        for (int i = 0; i < EditorStrings.VehicleTypeNames.Length; i++)
        {
            items.Add(new FilterItem(EditorStrings.VehicleTypeNames[i], i + 1));
        }

        return items;
    }

    private static List<FilterItem> BuildVehicleBehaviorFilterItems()
    {
        var items = new List<FilterItem> { new("All Behaviours", -1) };
        for (int i = 0; i < EditorStrings.VehicleBehaviorNames.Length; i++)
        {
            items.Add(new FilterItem(EditorStrings.VehicleBehaviorNames[i], i));
        }

        return items;
    }

    private static List<FilterItem> BuildVehicleKeyFilterItems()
    {
        var items = new List<FilterItem> { new("All Key Requirements", -1) };
        for (int i = 0; i < EditorStrings.VehicleKeyNames.Length; i++)
        {
            items.Add(new FilterItem(EditorStrings.VehicleKeyNames[i], i));
        }

        return items;
    }

    private static List<FilterItem> BuildCameraTypeFilterItems()
    {
        var items = new List<FilterItem> { new("All Camera Types", -1) };
        for (int i = 0; i < EditorStrings.CameraTypeNames.Length; i++)
        {
            items.Add(new FilterItem(EditorStrings.CameraTypeNames[i], i));
        }

        return items;
    }

    private static List<FilterItem> BuildCameraMovementFilterItems()
    {
        var items = new List<FilterItem> { new("All Movement Types", -1) };
        for (int i = 0; i < EditorStrings.CameraMoveNames.Length; i++)
        {
            items.Add(new FilterItem(EditorStrings.CameraMoveNames[i], i));
        }

        return items;
    }

    private static List<FilterItem> BuildTargetTypeFilterItems()
    {
        var items = new List<FilterItem> { new("All Target Types", -1) };
        for (int i = 0; i < EditorStrings.TargetTypeNames.Length; i++)
        {
            items.Add(new FilterItem(EditorStrings.TargetTypeNames[i], i + 1));
        }

        return items;
    }

    private static List<FilterItem> BuildActivatePrimTypeFilterItems()
    {
        var items = new List<FilterItem> { new("All Prim Types", -1) };
        for (int i = 0; i < EditorStrings.ActivatePrimNames.Length; i++)
        {
            items.Add(new FilterItem(EditorStrings.ActivatePrimNames[i], i));
        }

        return items;
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
        public string CounterIndex { get; init; } = string.Empty;
        public string IncrementNumber { get; init; } = string.Empty;
        public string MissionName { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
    }
}
