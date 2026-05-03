using System.Collections.ObjectModel;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.Infrastructure;
using UrbanChaosMissionEditor.Models;

namespace UrbanChaosMissionEditor.ViewModels;

/// <summary>
/// ViewModel for the New EventPoint type selection dialog
/// </summary>
public class NewEventPointDialogViewModel : BaseViewModel
{
    private static readonly HashSet<WaypointType> LegacyWaypointTypes = new()
    {
        WaypointType.BurnPrim,
        WaypointType.Autosave,
        WaypointType.CreateMapExit,
        WaypointType.TargetWaypoint,
        WaypointType.CutScene,
        WaypointType.Teleport,
        WaypointType.TeleportTarget,
        WaypointType.Shout,
        WaypointType.CreateBomb,
        WaypointType.Interesting,
        WaypointType.DynamicLight,
        WaypointType.GothereDothis,
    };

    private string _searchText = string.Empty;
    private WaypointTypeItem? _selectedType;
    private ObservableCollection<WaypointTypeItem> _filteredTypes = new();
    private bool _hideLegacyEventPoints = true;

    public NewEventPointDialogViewModel()
    {
        // Build list of all waypoint types
        AllWaypointTypes = new ObservableCollection<WaypointTypeItem>();

        foreach (WaypointType wt in Enum.GetValues<WaypointType>())
        {
            // Skip None/Unknown types
            if (wt == WaypointType.None) continue;

            string name = EditorStrings.GetWaypointTypeName(wt);
            var category = EventPoint.GetCategory(wt);

            AllWaypointTypes.Add(new WaypointTypeItem(wt, name, category));
        }

        // Sort by category then name
        var sorted = AllWaypointTypes
            .OrderBy(t => t.Category)
            .ThenBy(t => t.DisplayName)
            .ToList();

        AllWaypointTypes.Clear();
        foreach (var item in sorted)
            AllWaypointTypes.Add(item);

        // Initially show all
        ApplyFilter();
    }

    public ObservableCollection<WaypointTypeItem> AllWaypointTypes { get; }

    public ObservableCollection<WaypointTypeItem> FilteredWaypointTypes
    {
        get => _filteredTypes;
        private set => SetProperty(ref _filteredTypes, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public WaypointTypeItem? SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetProperty(ref _selectedType, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => SelectedType != null;

    public bool HideLegacyEventPoints
    {
        get => _hideLegacyEventPoints;
        set
        {
            if (SetProperty(ref _hideLegacyEventPoints, value))
            {
                ApplyFilter();
            }
        }
    }

    /// <summary>
    /// Get the selected WaypointType value
    /// </summary>
    public WaypointType? SelectedWaypointType => SelectedType?.Type;

    private void ApplyFilter()
    {
        IEnumerable<WaypointTypeItem> source = AllWaypointTypes;

        if (_hideLegacyEventPoints)
        {
            source = source.Where(t => !LegacyWaypointTypes.Contains(t.Type));
        }

        var filtered = string.IsNullOrWhiteSpace(_searchText)
            ? source.ToList()
            : source
                .Where(t => t.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           t.Category.ToString().Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

        FilteredWaypointTypes = new ObservableCollection<WaypointTypeItem>(filtered);

        // Auto-select first item if current selection is no longer visible
        if (SelectedType != null && !filtered.Contains(SelectedType))
        {
            SelectedType = filtered.FirstOrDefault();
        }
        else if (SelectedType == null && filtered.Count > 0)
        {
            SelectedType = filtered.First();
        }
    }
}

/// <summary>
/// Represents a waypoint type option in the list
/// </summary>
public class WaypointTypeItem
{
    public WaypointType Type { get; }
    public string DisplayName { get; }
    public WaypointCategory Category { get; }

    public WaypointTypeItem(WaypointType type, string displayName, WaypointCategory category)
    {
        Type = type;
        DisplayName = displayName;
        Category = category;
    }

    public override string ToString() => DisplayName;
}