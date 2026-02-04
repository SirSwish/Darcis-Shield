using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.Infrastructure;
using UrbanChaosMissionEditor.Models;
using UrbanChaosMissionEditor.Services;

namespace UrbanChaosMissionEditor.ViewModels;

/// <summary>
/// Main ViewModel for the UCM Editor application
/// </summary>
public class MainViewModel : BaseViewModel
{
    private readonly UcmFileService _fileService;
    private readonly IDialogService _dialogService;

    private Mission? _currentMission;
    private string? _currentFilePath;
    private EventPointViewModel? _selectedEventPoint;
    private string _statusMessage = "Ready";
    private double _mapZoom = 1.0;
    private double _mapOffsetX = 0;
    private double _mapOffsetY = 0;

    public MainViewModel() : this(new UcmFileService(), new WpfDialogService())
    {
    }

    public MainViewModel(UcmFileService fileService, IDialogService dialogService)
    {
        _fileService = fileService;
        _dialogService = dialogService;

        // Initialize commands
        OpenFileCommand = new RelayCommand(ExecuteOpenFile);
        CloseFileCommand = new RelayCommand(ExecuteCloseFile, () => HasMission);
        ExitCommand = new RelayCommand(ExecuteExit);

        ZoomInCommand = new RelayCommand(ExecuteZoomIn);
        ZoomOutCommand = new RelayCommand(ExecuteZoomOut);
        ResetZoomCommand = new RelayCommand(ExecuteResetZoom);
        CenterOnSelectedCommand = new RelayCommand(ExecuteCenterOnSelected, () => SelectedEventPoint != null);

        ToggleCategoryCommand = new RelayCommand<CategoryFilterViewModel>(ExecuteToggleCategory);
        ShowAllCategoriesCommand = new RelayCommand(ExecuteShowAllCategories);
        HideAllCategoriesCommand = new RelayCommand(ExecuteHideAllCategories);

        // Initialize category filters
        InitializeCategoryFilters();
    }

    // Collections

    public ObservableCollection<EventPointViewModel> EventPoints { get; } = [];
    public ObservableCollection<CategoryFilterViewModel> CategoryFilters { get; } = [];

    // Commands

    public ICommand OpenFileCommand { get; }
    public ICommand CloseFileCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetZoomCommand { get; }
    public ICommand CenterOnSelectedCommand { get; }
    public ICommand ToggleCategoryCommand { get; }
    public ICommand ShowAllCategoriesCommand { get; }
    public ICommand HideAllCategoriesCommand { get; }

    // Properties

    public Mission? CurrentMission
    {
        get => _currentMission;
        private set
        {
            if (SetProperty(ref _currentMission, value))
            {
                OnPropertyChanged(nameof(HasMission));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(MissionName));
                OnPropertyChanged(nameof(MapName));
                OnPropertyChanged(nameof(LightMapName));
                OnPropertyChanged(nameof(EventPointCount));
                OnPropertyChanged(nameof(CrimeRate));
                OnPropertyChanged(nameof(CivsRate));
                OnPropertyChanged(nameof(BoredomRate));
                OnPropertyChanged(nameof(CarsRate));
                OnPropertyChanged(nameof(MusicWorld));
                OnPropertyChanged(nameof(Version));
            }
        }
    }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set
        {
            if (SetProperty(ref _currentFilePath, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    public EventPointViewModel? SelectedEventPoint
    {
        get => _selectedEventPoint;
        set
        {
            var oldSelection = _selectedEventPoint;
            if (SetProperty(ref _selectedEventPoint, value))
            {
                if (oldSelection != null)
                    oldSelection.IsSelected = false;
                if (value != null)
                    value.IsSelected = true;

                OnPropertyChanged(nameof(HasSelection));
                ((RelayCommand)CenterOnSelectedCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public double MapZoom
    {
        get => _mapZoom;
        set => SetProperty(ref _mapZoom, Math.Clamp(value, 0.1, 10.0));
    }

    public double MapOffsetX
    {
        get => _mapOffsetX;
        set => SetProperty(ref _mapOffsetX, value);
    }

    public double MapOffsetY
    {
        get => _mapOffsetY;
        set => SetProperty(ref _mapOffsetY, value);
    }

    // Computed properties

    public bool HasMission => _currentMission != null;
    public bool HasSelection => _selectedEventPoint != null;

    public string WindowTitle => _currentFilePath != null
        ? $"Urban Chaos Mission Editor - {Path.GetFileName(_currentFilePath)}"
        : "Urban Chaos Mission Editor";

    public string FileName => _currentFilePath != null
        ? Path.GetFileName(_currentFilePath)
        : string.Empty;

    public string MissionName => _currentMission?.MissionName ?? string.Empty;
    public string MapName => _currentMission?.MapName ?? string.Empty;
    public string LightMapName => _currentMission?.LightMapName ?? string.Empty;
    public int EventPointCount => _currentMission?.UsedEventPointCount ?? 0;
    public byte CrimeRate => _currentMission?.CrimeRate ?? 0;
    public byte CivsRate => _currentMission?.CivsRate ?? 0;
    public byte BoredomRate => _currentMission?.BoredomRate ?? 0;
    public byte CarsRate => _currentMission?.CarsRate ?? 0;
    public byte MusicWorld => _currentMission?.MusicWorld ?? 0;
    public uint Version => _currentMission?.Version ?? 0;

    // Visible EventPoints (filtered)
    public IEnumerable<EventPointViewModel> VisibleEventPoints =>
        EventPoints.Where(ep => ep.IsVisible);

    // Category filter initialization

    private void InitializeCategoryFilters()
    {
        CategoryFilters.Clear();
        foreach (WaypointCategory category in Enum.GetValues<WaypointCategory>())
        {
            var filter = new CategoryFilterViewModel(category);
            filter.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CategoryFilterViewModel.IsEnabled))
                {
                    ApplyFilters();
                }
            };
            CategoryFilters.Add(filter);
        }
    }

    // Command implementations

    private void ExecuteOpenFile()
    {
        var filePath = _dialogService.ShowOpenFileDialog(
            "Open UCM Mission File",
            "UCM Files (*.ucm)|*.ucm|All Files (*.*)|*.*",
            @"C:\Fallen\Levels"
        );

        if (filePath != null)
        {
            LoadMission(filePath);
        }
    }

    private void ExecuteCloseFile()
    {
        CurrentMission = null;
        CurrentFilePath = null;
        EventPoints.Clear();
        SelectedEventPoint = null;
        UpdateCategoryCounts();
        StatusMessage = "File closed";
    }

    private void ExecuteExit()
    {
        System.Windows.Application.Current.Shutdown();
    }

    private void ExecuteZoomIn()
    {
        MapZoom *= 1.25;
    }

    private void ExecuteZoomOut()
    {
        MapZoom /= 1.25;
    }

    private void ExecuteResetZoom()
    {
        MapZoom = 1.0;
        MapOffsetX = 0;
        MapOffsetY = 0;
    }

    private void ExecuteCenterOnSelected()
    {
        if (SelectedEventPoint == null) return;

        // This will be implemented in the view to scroll to the selected point
        // The view listens for SelectedEventPoint changes
    }

    private void ExecuteToggleCategory(CategoryFilterViewModel? filter)
    {
        filter?.Toggle();
    }

    private void ExecuteShowAllCategories()
    {
        foreach (var filter in CategoryFilters)
            filter.IsEnabled = true;
    }

    private void ExecuteHideAllCategories()
    {
        foreach (var filter in CategoryFilters)
            filter.IsEnabled = false;
    }

    // Loading

    public void LoadMission(string filePath)
    {
        try
        {
            StatusMessage = "Loading mission...";

            var mission = _fileService.ReadMission(filePath);

            CurrentMission = mission;
            CurrentFilePath = filePath;

            LoadEventPoints();
            UpdateCategoryCounts();
            ApplyFilters();

            StatusMessage = $"Loaded {EventPointCount} event points from {FileName}";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Failed to load mission file:\n\n{ex.Message}");
            StatusMessage = "Failed to load file";
        }
    }

    private void LoadEventPoints()
    {
        EventPoints.Clear();
        SelectedEventPoint = null;

        if (_currentMission == null) return;

        foreach (var ep in _currentMission.UsedEventPoints.OrderBy(e => e.Index))
        {
            var vm = new EventPointViewModel(ep);
            EventPoints.Add(vm);
        }
    }

    private void UpdateCategoryCounts()
    {
        var counts = _currentMission?.GetCategoryCounts() ?? [];

        foreach (var filter in CategoryFilters)
        {
            filter.Count = counts.TryGetValue(filter.Category, out var count) ? count : 0;
        }
    }

    private void ApplyFilters()
    {
        var enabledCategories = CategoryFilters
            .Where(f => f.IsEnabled)
            .Select(f => f.Category)
            .ToHashSet();

        foreach (var ep in EventPoints)
        {
            ep.IsVisible = enabledCategories.Contains(ep.Category);
        }

        OnPropertyChanged(nameof(VisibleEventPoints));
    }

    /// <summary>
    /// Select an EventPoint by its index
    /// </summary>
    public void SelectEventPointByIndex(int index)
    {
        SelectedEventPoint = EventPoints.FirstOrDefault(ep => ep.Index == index);
    }

    /// <summary>
    /// Find EventPoint at pixel coordinates
    /// </summary>
    public EventPointViewModel? FindEventPointAtPosition(double pixelX, double pixelY, double tolerance = 10)
    {
        return VisibleEventPoints
            .Where(ep =>
            {
                double dx = ep.PixelX - pixelX;
                double dy = ep.PixelZ - pixelY;
                return Math.Sqrt(dx * dx + dy * dy) <= tolerance;
            })
            .OrderBy(ep =>
            {
                double dx = ep.PixelX - pixelX;
                double dy = ep.PixelZ - pixelY;
                return dx * dx + dy * dy;
            })
            .FirstOrDefault();
    }
}